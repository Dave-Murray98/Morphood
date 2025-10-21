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
        // Check for interactable objects
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null && interactable.IsAvailable && interactable.CanInteract(this))
        {
            if (!interactablesInRange.Contains(interactable))
            {
                interactablesInRange.Add(interactable);
                SortInteractablesByPriority();
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
                DebugLog($"Station entered range: {other.name}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Remove interactable objects
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            interactablesInRange.Remove(interactable);
            DebugLog($"Interactable left range: {other.name}");
        }

        // Remove stations
        BaseStation station = other.GetComponent<BaseStation>();
        if (station != null)
        {
            stationsInRange.Remove(station);
            DebugLog($"Station left range: {other.name}");
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

    #endregion

    #region Input Handling

    /// <summary>
    /// Called when the player presses their interact button
    /// </summary>
    public void OnInteractPressed()
    {
        if (isInteracting) return;

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
    /// Try to drop or place the carried item
    /// </summary>
    private void TryDropOrPlaceItem()
    {
        if (!IsCarryingItems) return;

        GameObject itemToDrop = heldObjects[heldObjects.Count - 1]; // Get the last picked up item

        // First, try to place on a station
        BaseStation bestStation = GetBestStation();
        if (bestStation != null && bestStation.CanAcceptItem(itemToDrop, this))
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