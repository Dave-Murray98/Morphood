using UnityEngine;

/// <summary>
/// Base class that provides common functionality for all interactable objects.
/// Inherit from this to create specific interactable types like ingredients, dishes, stations, etc.
/// </summary>
public abstract class BaseInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] protected InteractionType interactionType = InteractionType.Universal;
    [Tooltip("What type of interaction this is (Universal, Cooking, Chopping)")]

    [SerializeField] protected int interactionPriority = 1;
    [Tooltip("Priority when multiple objects are in range (higher = more priority)")]

    [SerializeField] protected string interactionPrompt = "Interact";
    [Tooltip("Text shown to player when they can interact with this object")]

    [Header("State")]
    [SerializeField] protected bool isCurrentlyAvailable = true;
    [Tooltip("Whether this object can be interacted with right now")]

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Internal state
    protected PlayerEnd currentInteractingPlayer;
    protected bool isBeingInteractedWith = false;

    #region IInteractable Implementation

    public virtual bool CanInteract(PlayerEnd playerEnd)
    {
        // Check if the object is available
        if (!IsAvailable) return false;

        // Check if the player end can perform this type of interaction
        if (!playerEnd.CanPerformInteraction(interactionType)) return false;

        // Allow derived classes to add additional conditions
        return CanInteractCustom(playerEnd);
    }

    public virtual bool Interact(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
        {
            DebugLog($"Interaction denied for player {playerEnd.PlayerNumber}");
            return false;
        }

        // Set up interaction state
        currentInteractingPlayer = playerEnd;
        isBeingInteractedWith = true;

        DebugLog($"Player {playerEnd.PlayerNumber} started interacting");

        // Call the specific interaction logic
        bool result = PerformInteraction(playerEnd);

        // If interaction failed, clean up state
        if (!result)
        {
            currentInteractingPlayer = null;
            isBeingInteractedWith = false;
        }

        return result;
    }

    public virtual void StopInteracting(PlayerEnd playerEnd)
    {
        if (currentInteractingPlayer != playerEnd)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} tried to stop interaction, but is not the current interactor");
            return;
        }

        DebugLog($"Player {playerEnd.PlayerNumber} stopped interacting");

        // Call the specific stop logic
        OnInteractionStopped(playerEnd);

        // Clean up state
        currentInteractingPlayer = null;
        isBeingInteractedWith = false;
    }

    public virtual int GetInteractionPriority()
    {
        return interactionPriority;
    }

    public virtual string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
        {
            return GetUnavailablePrompt(playerEnd);
        }

        return interactionPrompt;
    }

    public Transform Transform => transform;

    public virtual bool IsAvailable => isCurrentlyAvailable && !isBeingInteractedWith;

    #endregion

    #region Abstract and Virtual Methods for Derived Classes

    /// <summary>
    /// Override this to add custom interaction conditions beyond the basic type checking
    /// </summary>
    protected virtual bool CanInteractCustom(PlayerEnd playerEnd)
    {
        return true;
    }

    /// <summary>
    /// Override this to implement the specific interaction behavior
    /// </summary>
    protected abstract bool PerformInteraction(PlayerEnd playerEnd);

    /// <summary>
    /// Override this to handle what happens when interaction stops
    /// </summary>
    protected virtual void OnInteractionStopped(PlayerEnd playerEnd)
    {
        // Default: do nothing
    }

    /// <summary>
    /// Override this to customize the prompt shown when interaction is not available
    /// </summary>
    protected virtual string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (!playerEnd.CanPerformInteraction(interactionType))
        {
            switch (interactionType)
            {
                case InteractionType.Cooking:
                    return "Only Player 1 can cook";
                case InteractionType.Chopping:
                    return "Only Player 2 can chop";
                default:
                    return "Cannot interact";
            }
        }

        return "Not available";
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Set whether this object is available for interaction
    /// </summary>
    public virtual void SetAvailable(bool available)
    {
        isCurrentlyAvailable = available;
        DebugLog($"Availability set to: {available}");
    }

    /// <summary>
    /// Check if a specific player is currently interacting with this object
    /// </summary>
    public bool IsBeingInteractedWithBy(PlayerEnd playerEnd)
    {
        return isBeingInteractedWith && currentInteractingPlayer == playerEnd;
    }

    /// <summary>
    /// Get the player currently interacting with this object (null if none)
    /// </summary>
    public PlayerEnd GetCurrentInteractingPlayer()
    {
        return currentInteractingPlayer;
    }

    #endregion

    #region Debug

    protected virtual void OnDrawGizmos()
    {
        // Draw interaction indicator
        if (isCurrentlyAvailable)
        {
            Gizmos.color = isBeingInteractedWith ? Color.red : Color.green;
        }
        else
        {
            Gizmos.color = Color.gray;
        }

        // Draw a small sphere above the object to indicate it's interactable
        Vector3 indicatorPosition = transform.position + Vector3.up * 2f;
        Gizmos.DrawWireSphere(indicatorPosition, 0.2f);

        // Draw interaction type indicator
        switch (interactionType)
        {
            case InteractionType.Cooking:
                Gizmos.color = Color.red;
                break;
            case InteractionType.Chopping:
                Gizmos.color = Color.blue;
                break;
            case InteractionType.Universal:
                Gizmos.color = Color.yellow;
                break;
        }

        Gizmos.DrawCube(indicatorPosition, Vector3.one * 0.1f);
    }

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[{GetType().Name}] {message}");
    }

    #endregion

    #region Unity Events

    protected virtual void Start()
    {
        // Ensure the object is on the correct layer for interaction detection
        if (gameObject.layer == 0) // Default layer
        {
            Debug.LogWarning($"[{name}] Interactable object is on Default layer. Consider putting it on a dedicated Interactable layer for better performance.");
        }
    }

    protected virtual void OnValidate()
    {
        // Clamp priority to reasonable values
        interactionPriority = Mathf.Max(0, interactionPriority);

        // Ensure we have a collider for interaction detection
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"[{name}] Interactable object has no Collider. Add one for interaction detection to work.");
        }
    }

    #endregion
}