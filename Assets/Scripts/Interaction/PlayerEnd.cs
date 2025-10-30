using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents one end of the player character (Player 1 or Player 2).
/// Handles interaction detection via trigger collisions, input processing, and object management for that specific player.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerEnd : MonoBehaviour
{
    [Header("Player Configuration")]
    [SerializeField] private int playerNumber = 1;
    [Tooltip("Which player this end represents (1 or 2)")]

    [SerializeField] private PlayerEndType endType = PlayerEndType.Player1;
    [Tooltip("Defines what special abilities this end has")]

    [Header("Inventory")]
    [SerializeField] private Transform holdPoint;
    [Tooltip("Where held objects will be positioned")]

    [SerializeField] private int maxCarryCapacity = 1;
    [Tooltip("Maximum number of objects this end can carry")]

    [Header("Drop Settings")]
    [SerializeField] private float dropDistance = 1.5f;
    [Tooltip("How far in front of the player to drop items when not placing on a station")]

    [SerializeField] private LayerMask groundLayerMask = 1;
    [Tooltip("Layer mask for ground detection when dropping items")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showInteractionGizmos = true;

    // Internal state
    private List<IInteractable> interactablesInRange = new List<IInteractable>();
    private List<BaseStation> stationsInRange = new List<BaseStation>();
    private List<GameObject> heldObjects = new List<GameObject>();
    private IInteractable currentInteractionTarget;
    private IInteractable currentHighlightedInteractable; // Track what we're currently highlighting
    private bool isInteracting = false;
    private bool isHoldingInteraction = false;

    // Collision detection
    private Collider triggerCollider;

    // Public properties
    public int PlayerNumber => playerNumber;
    public PlayerEndType EndType => endType;
    public bool HasFreeHands => heldObjects.Count < maxCarryCapacity;
    public bool IsCarryingItems => heldObjects.Count > 0;
    public int CarriedItemCount => heldObjects.Count;
    public IReadOnlyList<GameObject> HeldObjects => heldObjects.AsReadOnly();

    // Events for other systems to react to
    public System.Action<GameObject> OnItemPickedUp;
    public System.Action<GameObject> OnItemDropped;
    public System.Action<IInteractable> OnInteractionStarted;
    public System.Action<IInteractable> OnInteractionStopped;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        SetupTriggerCollider();
        SetupHoldPoint();

        DebugLog($"Player {playerNumber} end initialized with type: {endType}");
    }

    private void SetupTriggerCollider()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
        {
            // Create a box collider if none exists
            triggerCollider = gameObject.AddComponent<BoxCollider>();
            DebugLog("Created BoxCollider for interaction detection");
        }

        // Ensure it's set up as a trigger
        triggerCollider.isTrigger = true;

        // Set reasonable default size if it's a box collider
        if (triggerCollider is BoxCollider boxCollider)
        {
            if (boxCollider.size == Vector3.one) // Default size, probably needs adjustment
            {
                boxCollider.size = new Vector3(2f, 2f, 2f); // Reasonable interaction area
                DebugLog("Set default BoxCollider size for interaction detection");
            }
        }
    }

    private void SetupHoldPoint()
    {
        // Set up hold point if not assigned
        if (holdPoint == null)
        {
            GameObject holdPointObj = new GameObject($"HoldPoint_Player{playerNumber}");
            holdPointObj.transform.SetParent(transform);
            holdPointObj.transform.localPosition = Vector3.up * 1.5f; // Slightly above the player
            holdPoint = holdPointObj.transform;
        }
    }

    #region Collision Detection

    private void OnTriggerEnter(Collider other)
    {
        bool needsHighlightUpdate = false;

        // Check for interactable objects
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null && interactable.IsAvailable && interactable.CanInteract(this))
        {
            if (!interactablesInRange.Contains(interactable))
            {
                interactablesInRange.Add(interactable);
                SortInteractablesByPriority();
                needsHighlightUpdate = true;
                DebugLog($"Interactable entered range: {other.name}");
            }
        }

        // Check for stations
        BaseStation station = other.GetComponent<BaseStation>();
        if (station != null && station.CanPlayerInteract(this))
        {
            if (!stationsInRange.Contains(station))
            {
                stationsInRange.Add(station);
                SortStationsByDistance();
                needsHighlightUpdate = true;
                DebugLog($"Station entered range: {other.name}");
            }
        }

        // Update highlighting if needed (but not during interaction)
        if (needsHighlightUpdate && !isInteracting)
        {
            // Force immediate highlight update
            IInteractable newTarget = GetInteractablePlayerWouldUse();
            if (newTarget != currentHighlightedInteractable)
            {
                // Update highlighting immediately rather than waiting for Update()
                if (currentHighlightedInteractable != null && currentHighlightedInteractable is BaseInteractable prevHighlighted)
                {
                    prevHighlighted.StopHighlighting();
                }

                if (newTarget != null && newTarget is BaseInteractable newHighlighted)
                {
                    newHighlighted.StartHighlighting();
                    DebugLog($"Immediately started highlighting: {newHighlighted.name}");
                }

                currentHighlightedInteractable = newTarget;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        bool needsHighlightUpdate = false;

        // Remove interactable objects
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            if (interactablesInRange.Remove(interactable))
            {
                needsHighlightUpdate = true;
                DebugLog($"Interactable left range: {other.name}");

                // If this was the highlighted interactable, stop highlighting it immediately
                if (currentHighlightedInteractable == interactable && interactable is BaseInteractable highlightedInteractable)
                {
                    highlightedInteractable.StopHighlighting();
                    currentHighlightedInteractable = null;
                    DebugLog($"Stopped highlighting departing interactable: {other.name}");
                }
            }
        }

        // Remove stations
        BaseStation station = other.GetComponent<BaseStation>();
        if (station != null)
        {
            if (stationsInRange.Remove(station))
            {
                needsHighlightUpdate = true;
                DebugLog($"Station left range: {other.name}");

                // If this station's interactable was highlighted, stop highlighting it
                PlainStationInteractable stationInteractable = station.GetComponent<PlainStationInteractable>();
                if (ReferenceEquals(currentHighlightedInteractable, stationInteractable) && stationInteractable is BaseInteractable highlightedStation)
                {
                    highlightedStation.StopHighlighting();
                    currentHighlightedInteractable = null;
                    DebugLog($"Stopped highlighting departing station: {other.name}");
                }
            }
        }

        // Update highlighting if needed (but not during interaction)
        if (needsHighlightUpdate && !isInteracting)
        {
            // Force immediate highlight update
            IInteractable newTarget = GetInteractablePlayerWouldUse();
            if (newTarget != currentHighlightedInteractable)
            {
                if (newTarget != null && newTarget is BaseInteractable newHighlighted)
                {
                    newHighlighted.StartHighlighting();
                    DebugLog($"Started highlighting new target: {newHighlighted.name}");
                }

                currentHighlightedInteractable = newTarget;
            }
        }
    }

    private void SortInteractablesByPriority()
    {
        // Sort by priority (highest first) and then by distance (closest first)
        interactablesInRange.Sort((a, b) =>
        {
            int priorityComparison = b.GetInteractionPriority().CompareTo(a.GetInteractionPriority());
            if (priorityComparison != 0) return priorityComparison;

            float distanceA = Vector3.Distance(transform.position, a.Transform.position);
            float distanceB = Vector3.Distance(transform.position, b.Transform.position);
            return distanceA.CompareTo(distanceB);
        });
    }

    private void SortStationsByDistance()
    {
        // Sort stations by distance (closest first)
        stationsInRange.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(transform.position, a.transform.position);
            float distanceB = Vector3.Distance(transform.position, b.transform.position);
            return distanceA.CompareTo(distanceB);
        });
    }

    /// <summary>
    /// Get the best interactable object currently in range
    /// </summary>
    public IInteractable GetBestInteractable()
    {
        // Clean up any null or unavailable interactables
        interactablesInRange.RemoveAll(i => i == null || !i.IsAvailable || !i.CanInteract(this));

        return interactablesInRange.Count > 0 ? interactablesInRange[0] : null;
    }

    /// <summary>
    /// Get the best station for placing items
    /// </summary>
    public BaseStation GetBestStation()
    {
        // Clean up any null stations
        stationsInRange.RemoveAll(s => s == null || !s.CanPlayerInteract(this));

        return stationsInRange.Count > 0 ? stationsInRange[0] : null;
    }

    /// <summary>
    /// Get the interactable that the player would actually interact with if they pressed the interact button right now
    /// ENHANCED VERSION with food combination support
    /// This method should replace the existing GetInteractablePlayerWouldUse method in PlayerEnd.cs
    /// </summary>
    public IInteractable GetInteractablePlayerWouldUse()
    {
        if (IsCarryingItems)
        {
            // Player is carrying something - they would interact with the best station for placement or combination
            BaseStation bestStation = GetBestStation();
            if (bestStation != null)
            {
                // Check if this is a food item that can be combined with a PlainStation
                GameObject carriedItem = heldObjects[heldObjects.Count - 1];
                FoodItem carriedFoodItem = carriedItem.GetComponent<FoodItem>();
                PlainStation plainStation = bestStation as PlainStation;

                // If it's a food item and PlainStation with combination potential
                if (carriedFoodItem != null && plainStation != null && plainStation.IsOccupied)
                {
                    if (plainStation.CanAcceptForCombination(carriedFoodItem))
                    {
                        // Player would interact with the station for combination
                        PlainStationInteractable stationInteractable = bestStation.GetComponent<PlainStationInteractable>();
                        if (stationInteractable != null && stationInteractable.CanInteract(this))
                        {
                            return stationInteractable;
                        }
                    }
                }

                // Otherwise, check normal placement
                if (bestStation.CanAcceptItem(carriedItem, this))
                {
                    // Check if the station has a PlainStationInteractable component
                    PlainStationInteractable stationInteractable = bestStation.GetComponent<PlainStationInteractable>();
                    if (stationInteractable != null && stationInteractable.CanInteract(this))
                    {
                        return stationInteractable;
                    }
                }
            }
            // If no suitable station, player would drop on ground (no highlighting)
            return null;
        }
        else
        {
            // Player has free hands - they would interact with the best regular interactable
            return GetBestInteractable();
        }
    }

    /// <summary>
    /// Update highlighting immediately after carrying state changes (pickup/drop)
    /// </summary>
    private void UpdateHighlightingAfterCarryStateChange()
    {
        if (isInteracting) return;

        IInteractable newTarget = GetInteractablePlayerWouldUse();

        // Stop current highlighting
        if (currentHighlightedInteractable != null && currentHighlightedInteractable is BaseInteractable prevHighlighted)
        {
            prevHighlighted.StopHighlighting();
            DebugLog($"Stopped highlighting after carry state change: {prevHighlighted.name}");
        }

        // Start new highlighting
        if (newTarget != null && newTarget is BaseInteractable newHighlighted)
        {
            newHighlighted.StartHighlighting();
            DebugLog($"Started highlighting after carry state change: {newHighlighted.name}");
        }

        currentHighlightedInteractable = newTarget;
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Called when the player presses their interact button
    /// </summary>
    public void OnInteractPressed()
    {
        if (isInteracting) return;

        // Stop any current highlighting since we're about to interact
        if (currentHighlightedInteractable != null && currentHighlightedInteractable is BaseInteractable highlighted)
        {
            highlighted.StopHighlighting();
            currentHighlightedInteractable = null;
        }

        // If player is carrying something, try to drop/place it
        if (IsCarryingItems)
        {
            TryDropOrPlaceItem();
            return;
        }

        // Otherwise, try to pick up or interact with something
        IInteractable targetInteractable = GetBestInteractable();
        if (targetInteractable == null) return;

        DebugLog($"Player {playerNumber} attempting to interact with: {targetInteractable.GetType().Name}");

        // Attempt the interaction
        bool interactionSuccessful = targetInteractable.Interact(this);

        if (interactionSuccessful)
        {
            currentInteractionTarget = targetInteractable;
            isInteracting = true;
            OnInteractionStarted?.Invoke(targetInteractable);

            DebugLog($"Player {playerNumber} started interacting with: {targetInteractable.GetType().Name}");
        }
    }

    /// <summary>
    /// Called when the player releases their interact button
    /// </summary>
    public void OnInteractReleased()
    {
        if (!isInteracting || currentInteractionTarget == null) return;

        DebugLog($"Player {playerNumber} stopped interacting with: {currentInteractionTarget.GetType().Name}");

        currentInteractionTarget.StopInteracting(this);
        OnInteractionStopped?.Invoke(currentInteractionTarget);

        currentInteractionTarget = null;
        isInteracting = false;
        isHoldingInteraction = false;

        // Resume highlighting after interaction ends
        // The next Update() call will handle finding the new best interactable
    }

    /// <summary>
    /// Called when the player holds their interact button (for cooking/chopping)
    /// </summary>
    public void OnInteractHeld()
    {
        if (isInteracting && currentInteractionTarget != null)
        {
            isHoldingInteraction = true;
            // The interaction target handles the hold logic internally
        }
    }

    #endregion

    #region Object Management

    /// <summary>
    /// Try to drop or place the carried item - ENHANCED VERSION with food combination support
    /// This method should replace the existing TryDropOrPlaceItem method in PlayerEnd.cs
    /// </summary>
    private void TryDropOrPlaceItem()
    {
        if (!IsCarryingItems) return;

        GameObject itemToDrop = heldObjects[heldObjects.Count - 1]; // Get the last picked up item

        // First, try to place on a station or combine with station contents
        BaseStation bestStation = GetBestStation();
        if (bestStation != null)
        {
            // Check if this is a PlainStation and we can combine
            PlainStation plainStation = bestStation as PlainStation;
            FoodItem carriedFoodItem = itemToDrop.GetComponent<FoodItem>();

            if (plainStation != null && carriedFoodItem != null && plainStation.IsOccupied)
            {
                // Check if we can combine with what's on the station
                if (plainStation.CanAcceptForCombination(carriedFoodItem))
                {
                    DebugLog($"Attempting combination with station item");

                    // Attempt combination
                    bool combinationSuccessful = plainStation.TryCombineWithStationItem(carriedFoodItem, this);
                    if (combinationSuccessful)
                    {
                        // Remove from our inventory since it was consumed in the combination
                        heldObjects.Remove(itemToDrop);
                        OnItemDropped?.Invoke(itemToDrop);
                        DebugLog($"Successfully combined {itemToDrop.name} with station item");
                        return;
                    }
                    else
                    {
                        DebugLog("Combination failed, trying normal placement");
                    }
                }
            }

            // If combination didn't work or wasn't possible, try normal placement
            if (bestStation.CanAcceptItem(itemToDrop, this))
            {
                Vector3 placePosition = bestStation.GetPlacementPosition();
                bool placedSuccessfully = bestStation.PlaceItem(itemToDrop, this);

                if (placedSuccessfully)
                {
                    // Remove from our inventory (station will handle the object positioning)
                    heldObjects.Remove(itemToDrop);
                    OnItemDropped?.Invoke(itemToDrop);

                    DebugLog($"Player {playerNumber} placed {itemToDrop.name} on station: {bestStation.name}");
                    return;
                }
            }
        }

        // If no station available or placement failed, drop on ground
        Vector3 dropPosition = CalculateGroundDropPosition();
        DropObject(itemToDrop, dropPosition);

        DebugLog($"Player {playerNumber} dropped {itemToDrop.name} on ground");
    }

    /// <summary>
    /// Calculate where to drop an item on the ground in front of the player
    /// </summary>
    private Vector3 CalculateGroundDropPosition()
    {
        Vector3 dropDirection = transform.forward;
        Vector3 targetPosition = transform.position + dropDirection * dropDistance;

        // Try to drop at ground level
        if (Physics.Raycast(targetPosition + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, groundLayerMask))
        {
            return hit.point + Vector3.up * 0.1f; // Slightly above ground
        }

        // Fallback to player's Y level
        return new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
    }

    /// <summary>
    /// Add an object to this player's inventory
    /// </summary>
    public bool PickUpObject(GameObject obj)
    {
        if (!HasFreeHands)
        {
            DebugLog($"Player {playerNumber} hands are full, cannot pick up {obj.name}");
            return false;
        }

        // Move object to hold point
        obj.transform.SetParent(holdPoint);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        // Disable physics while held
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Disable collider to prevent interference
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        // Add to inventory
        heldObjects.Add(obj);

        OnItemPickedUp?.Invoke(obj);
        DebugLog($"Player {playerNumber} picked up: {obj.name}");

        // Update highlighting since carrying state changed
        // Player now might interact with stations instead of items
        UpdateHighlightingAfterCarryStateChange();

        return true;
    }

    /// <summary>
    /// Remove an object from this player's inventory
    /// </summary>
    public bool DropObject(GameObject obj, Vector3? dropPosition = null)
    {
        if (!heldObjects.Contains(obj))
        {
            DebugLog($"Player {playerNumber} is not holding {obj.name}");
            return false;
        }

        // Remove from inventory
        heldObjects.Remove(obj);

        // Position the object
        Vector3 finalDropPosition = dropPosition ?? CalculateGroundDropPosition();
        obj.transform.SetParent(null);
        obj.transform.position = finalDropPosition;

        // Re-enable physics
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        // Re-enable collider
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        OnItemDropped?.Invoke(obj);
        DebugLog($"Player {playerNumber} dropped: {obj.name}");

        // Update highlighting since carrying state changed
        // Player now might interact with items instead of stations
        UpdateHighlightingAfterCarryStateChange();

        return true;
    }

    /// <summary>
    /// Check if this player end can perform a specific interaction type
    /// </summary>
    public bool CanPerformInteraction(InteractionType interactionType)
    {
        switch (interactionType)
        {
            case InteractionType.Universal:
                return true;
            case InteractionType.Cooking:
                return endType == PlayerEndType.Player1;
            case InteractionType.Chopping:
                return endType == PlayerEndType.Player2;
            default:
                return false;
        }
    }

    #endregion

    #region Debug and Gizmos

    private void OnDrawGizmos()
    {
        if (!showInteractionGizmos) return;

        // Draw trigger collider bounds
        if (triggerCollider != null)
        {
            Gizmos.color = Color.yellow;
            if (triggerCollider is BoxCollider boxCollider)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        // Draw line to best interactable
        if (Application.isPlaying)
        {
            IInteractable best = GetBestInteractable();
            if (best != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, best.Transform.position);
            }

            // Draw line to best station
            BaseStation bestStation = GetBestStation();
            if (bestStation != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, bestStation.transform.position);
            }
        }

        // Draw hold point
        if (holdPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(holdPoint.position, Vector3.one * 0.3f);
        }

        // Draw drop position
        if (Application.isPlaying && IsCarryingItems)
        {
            Gizmos.color = Color.red;
            Vector3 dropPos = CalculateGroundDropPosition();
            Gizmos.DrawWireSphere(dropPos, 0.2f);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerEnd{playerNumber}] {message}");
    }

    #endregion
}

/// <summary>
/// Defines the type of player end and what special abilities it has
/// </summary>
public enum PlayerEndType
{
    Player1, // Can cook
    Player2  // Can chop
}

/// <summary>
/// Defines the type of interaction for permission checking
/// </summary>
public enum InteractionType
{
    Universal, // Both players can do this
    Cooking,   // Only Player 1
    Chopping   // Only Player 2
}