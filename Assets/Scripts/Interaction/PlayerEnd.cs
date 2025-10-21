using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents one end of the player character (Player 1 or Player 2).
/// Handles interaction detection, input processing, and object management for that specific player.
/// </summary>
public class PlayerEnd : MonoBehaviour
{
    [Header("Player Configuration")]
    [SerializeField] private int playerNumber = 1;
    [Tooltip("Which player this end represents (1 or 2)")]

    [SerializeField] private PlayerEndType endType = PlayerEndType.Player1;
    [Tooltip("Defines what special abilities this end has")]

    [Header("Interaction Detection")]
    [SerializeField] private float interactionRange = 2f;
    [Tooltip("How close objects need to be to interact with them")]

    [SerializeField] private LayerMask interactableLayerMask = -1;
    [Tooltip("Which layers contain interactable objects")]

    [Header("Inventory")]
    [SerializeField] private Transform holdPoint;
    [Tooltip("Where held objects will be positioned")]

    [SerializeField] private int maxCarryCapacity = 1;
    [Tooltip("Maximum number of objects this end can carry")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showInteractionGizmos = true;

    // Internal state
    private List<IInteractable> interactablesInRange = new List<IInteractable>();
    private List<GameObject> heldObjects = new List<GameObject>();
    private IInteractable currentInteractionTarget;
    private bool isInteracting = false;
    private bool isHoldingInteraction = false;

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
        // Set up hold point if not assigned
        if (holdPoint == null)
        {
            GameObject holdPointObj = new GameObject($"HoldPoint_Player{playerNumber}");
            holdPointObj.transform.SetParent(transform);
            holdPointObj.transform.localPosition = Vector3.up * 1.5f; // Slightly above the player
            holdPoint = holdPointObj.transform;
        }

        // Register with the interaction system
        RegisterWithInputManager();

        DebugLog($"Player {playerNumber} end initialized with type: {endType}");
    }

    private void RegisterWithInputManager()
    {
        // We'll extend InputManager to handle interaction input
        if (InputManager.Instance != null)
        {
            // This method will be added to InputManager
            // InputManager.Instance.RegisterPlayerEnd(this);
        }
        else
        {
            // Try again next frame if InputManager isn't ready
            Invoke(nameof(RegisterWithInputManager), 0.1f);
        }
    }

    private void Update()
    {
        UpdateInteractableDetection();
    }

    #region Interaction Detection

    private void UpdateInteractableDetection()
    {
        // Find all interactable objects in range
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, interactionRange, interactableLayerMask);

        // Clear the current list
        interactablesInRange.Clear();

        // Check each collider for interactable components
        foreach (Collider col in nearbyColliders)
        {
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable != null && interactable.IsAvailable && interactable.CanInteract(this))
            {
                interactablesInRange.Add(interactable);
            }
        }

        // Sort by priority (highest first) and then by distance (closest first)
        interactablesInRange.Sort((a, b) =>
        {
            int priorityComparison = b.GetInteractionPriority().CompareTo(a.GetInteractionPriority());
            if (priorityComparison != 0) return priorityComparison;

            float distanceA = Vector3.Distance(transform.position, a.Transform.position);
            float distanceB = Vector3.Distance(transform.position, b.Transform.position);
            return distanceA.CompareTo(distanceB);
        });

        // Debug logging for nearby interactables
        if (enableDebugLogs && interactablesInRange.Count > 0)
        {
            DebugLog($"Found {interactablesInRange.Count} interactables in range");
        }
    }

    /// <summary>
    /// Get the best interactable object currently in range
    /// </summary>
    public IInteractable GetBestInteractable()
    {
        return interactablesInRange.Count > 0 ? interactablesInRange[0] : null;
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Called when the player presses their interact button
    /// </summary>
    public void OnInteractPressed()
    {
        if (isInteracting) return;

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
        Vector3 finalDropPosition = dropPosition ?? (transform.position + transform.forward * 1f);
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
    /// Drop the most recently picked up object
    /// </summary>
    public bool DropLastObject(Vector3? dropPosition = null)
    {
        if (heldObjects.Count == 0) return false;

        GameObject lastObject = heldObjects[heldObjects.Count - 1];
        return DropObject(lastObject, dropPosition);
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

        // Draw interaction range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Draw line to best interactable
        if (Application.isPlaying)
        {
            IInteractable best = GetBestInteractable();
            if (best != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, best.Transform.position);
            }
        }

        // Draw hold point
        if (holdPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(holdPoint.position, Vector3.one * 0.3f);
        }
    }

    private void OnDestroy()
    {
        // Clean up when destroyed
        if (InputManager.Instance != null)
        {
            // InputManager.Instance.UnregisterPlayerEnd(this);
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