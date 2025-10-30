using UnityEngine;
using System.Collections;

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

    [Header("Outline Highlighting")]
    [SerializeField] protected bool enableOutlineHighlighting = true;
    [Tooltip("Whether to show outline when this object can be interacted with")]

    [SerializeField] protected float minOutlineWidth = 0f;
    [Tooltip("Starting outline width for the highlight animation")]

    [SerializeField] protected float maxOutlineWidth = 5f;
    [Tooltip("Maximum outline width for the highlight animation")]

    [SerializeField] protected float outlineAnimationDuration = 1f;
    [Tooltip("Duration of one full outline pulse cycle (min->max->min)")]

    [SerializeField] protected AnimationCurve outlineAnimationCurve;
    [Tooltip("Animation curve for the outline width animation. Should go 0->1->0 for pulsing effect")]

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Internal state
    protected PlayerEnd currentInteractingPlayer;
    protected bool isBeingInteractedWith = false;

    // Outline system
    protected InteractableOutline outlineComponent;
    protected Coroutine outlineAnimationCoroutine;
    protected bool isHighlighted = false;

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        // Get the outline component
        outlineComponent = GetComponent<InteractableOutline>();

        if (enableOutlineHighlighting && outlineComponent == null)
        {
            Debug.LogWarning($"[{name}] Outline highlighting is enabled but no Outline component found! Add an Outline component for highlighting to work.");
        }

        // Initially disable the outline
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false;
        }

        // Set up default animation curve if not configured
        if (outlineAnimationCurve == null || outlineAnimationCurve.keys.Length == 0)
        {
            // Create a pulse curve: 0 -> 1 -> 0
            outlineAnimationCurve = new AnimationCurve();
            outlineAnimationCurve.AddKey(0f, 0f);      // Start at min
            outlineAnimationCurve.AddKey(0.5f, 1f);    // Peak at middle
            outlineAnimationCurve.AddKey(1f, 0f);      // Back to min at end

            // Make it smooth
            for (int i = 0; i < outlineAnimationCurve.keys.Length; i++)
            {
                outlineAnimationCurve.SmoothTangents(i, 0f);
            }
        }
    }

    #endregion

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

        // Stop highlighting when interaction starts
        StopHighlighting();

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

    #region Outline Highlighting System

    /// <summary>
    /// Start highlighting this object with an animated outline
    /// </summary>
    public virtual void StartHighlighting()
    {
        // Safety check - don't try to highlight inactive objects
        if (!gameObject.activeInHierarchy)
        {
            DebugLog("Cannot start highlighting - object is inactive");
            return;
        }

        if (!enableOutlineHighlighting || outlineComponent == null || isHighlighted) return;

        isHighlighted = true;
        outlineComponent.enabled = true;

        // Start the outline animation coroutine
        if (outlineAnimationCoroutine != null)
        {
            StopCoroutine(outlineAnimationCoroutine);
        }

        outlineAnimationCoroutine = StartCoroutine(AnimateOutline());

        DebugLog("Started outline highlighting");
    }

    /// <summary>
    /// Stop highlighting this object and disable the outline
    /// </summary>
    public virtual void StopHighlighting()
    {
        if (!isHighlighted) return;

        isHighlighted = false;

        // Stop the animation coroutine
        if (outlineAnimationCoroutine != null)
        {
            StopCoroutine(outlineAnimationCoroutine);
            outlineAnimationCoroutine = null;
        }

        // Disable the outline
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false;
        }

        DebugLog("Stopped outline highlighting");
    }

    /// <summary>
    /// Coroutine that animates the outline width in a continuous loop
    /// </summary>
    protected virtual IEnumerator AnimateOutline()
    {
        if (outlineComponent == null) yield break;

        while (isHighlighted && gameObject.activeInHierarchy)
        {
            float elapsedTime = 0f;

            // Animate from min to max and back to min in one cycle
            while (elapsedTime < outlineAnimationDuration && isHighlighted && gameObject.activeInHierarchy)
            {
                float normalizedTime = elapsedTime / outlineAnimationDuration;
                float curveValue = outlineAnimationCurve.Evaluate(normalizedTime);
                float currentWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, curveValue);

                outlineComponent.OutlineWidth = currentWidth;

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Exit if object became inactive
            if (!gameObject.activeInHierarchy)
            {
                break;
            }

            // Seamlessly restart the cycle - no reset needed since we're using a curve
        }

        // Clean up when highlighting stops or object becomes inactive
        isHighlighted = false;
        outlineAnimationCoroutine = null;
    }

    /// <summary>
    /// Check if this object is currently being highlighted
    /// </summary>
    public bool IsHighlighted => isHighlighted;

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

        // Stop highlighting if becoming unavailable
        if (!available && isHighlighted)
        {
            StopHighlighting();
        }

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

        // Draw highlighting indicator
        if (isHighlighted)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(indicatorPosition + Vector3.up * 0.5f, Vector3.one * 0.3f);
        }
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

        // Ensure outline width values are reasonable
        minOutlineWidth = Mathf.Max(0f, minOutlineWidth);
        maxOutlineWidth = Mathf.Max(minOutlineWidth, maxOutlineWidth);
        outlineAnimationDuration = Mathf.Max(0.1f, outlineAnimationDuration);

        // Ensure we have a collider for interaction detection
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"[{name}] Interactable object has no Collider. Add one for interaction detection to work.");
        }
    }

    protected virtual void OnDestroy()
    {
        // Clean up coroutine when object is destroyed
        if (outlineAnimationCoroutine != null)
        {
            StopCoroutine(outlineAnimationCoroutine);
        }
    }

    #endregion
}