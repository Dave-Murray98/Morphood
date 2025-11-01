using UnityEngine;

/// <summary>
/// Base class for processing station interactables (ChoppingStation, CookingStation, etc.)
///
/// INTERACTION DESIGN:
/// This class implements a "hold-to-process, tap-to-pickup" interaction pattern that allows
/// players to clearly distinguish between two different actions at a processing station:
/// 1. QUICK TAP: Pick up the item on the station (to move it elsewhere)
/// 2. HOLD: Start processing the item (chop, cook, etc.)
///
/// WHY THIS DESIGN?
/// Without hold detection, players would accidentally start processing when they just wanted
/// to pick up an item. This pattern gives clear, intentional control over both actions.
///
/// HOW IT WORKS:
/// 1. Player presses interact → Start hold detection timer
/// 2. Player holds for < threshold (0.3s) → Quick tap detected → Pick up item
/// 3. Player holds for >= threshold → Long hold detected → Start processing
/// 4. During processing, holding continues the process, releasing stops it
///
/// IMPLEMENTATION:
/// - PerformInteraction: Handles the initial button press and routes to appropriate action
/// - Update: Monitors hold duration and triggers processing when threshold is reached
/// - OnInteractionStopped: Handles button release and determines final action
/// - State flags prevent accidental double-actions and track current interaction phase
/// </summary>
public abstract class BaseProcessingStationInteractable : BaseInteractable
{
    [Header("Processing Interaction Settings")]
    [SerializeField] protected float holdThresholdTime = 0.3f;
    [Tooltip("How long player must hold before processing starts (prevents accidental processing)")]

    [SerializeField] protected bool requireHoldToProcess = true;
    [Tooltip("Whether player must hold the interact button to process items")]

    [SerializeField] protected bool showProgressInPrompt = true;
    [Tooltip("Whether to show processing progress percentage in interaction prompts")]

    [Header("Debug")]
    [SerializeField] protected bool enableProcessingDebug = false;

    // Hold detection state - tracks the current phase of interaction
    protected float interactionStartTime = 0f;                              // When the player started holding interact
    protected bool isWaitingForHoldDecision = false;                        // True while monitoring if this is a tap or hold
    protected bool hasCommittedToAction = false;                            // True once we've decided on tap/hold (prevents double-action)
    protected ProcessingActionType pendingActionType = ProcessingActionType.None;  // What action we're committing to

    // Abstract properties to be implemented by derived classes
    protected abstract BaseStation ProcessingStation { get; }
    protected abstract InteractionType RequiredInteractionType { get; }
    protected abstract FoodProcessType ProcessType { get; }
    protected abstract string ProcessVerb { get; } // "chop", "cook", etc.
    protected abstract string ProcessingVerb { get; } // "chopping", "cooking", etc.

    // Abstract methods for derived classes
    protected abstract bool IsCurrentlyProcessing();
    protected abstract float GetProcessingProgress();
    protected abstract bool CanItemBeProcessed(FoodItem foodItem);
    protected abstract bool StartProcessing(PlayerEnd playerEnd);
    protected abstract void StopProcessing();
    protected abstract PlayerEnd GetCurrentProcessingPlayer();

    #region Unity Lifecycle

    protected virtual void Start()
    {
        // Subscribe to station events to refresh player interaction state
        if (ProcessingStation != null)
        {
            ProcessingStation.OnItemPlaced += OnStationItemPlaced;
            ProcessingStation.OnItemRemoved += OnStationItemRemoved;
        }
    }

    protected virtual void OnDestroy()
    {
        // Unsubscribe from station events
        if (ProcessingStation != null)
        {
            ProcessingStation.OnItemPlaced -= OnStationItemPlaced;
            ProcessingStation.OnItemRemoved -= OnStationItemRemoved;
        }
    }

    /// <summary>
    /// Called when an item is placed on this station
    /// Refreshes nearby player interaction state so they can immediately pick up the item
    /// </summary>
    private void OnStationItemPlaced(GameObject item, PlayerEnd playerEnd)
    {
        ProcessingDebugLog($"Item {item.name} placed by Player {playerEnd.PlayerNumber} - refreshing interaction state");

        // Use a coroutine to refresh after a brief delay
        // This ensures the placement is fully complete before refreshing
        StartCoroutine(RefreshPlayerInteractionAfterPlacement(playerEnd));
    }

    /// <summary>
    /// Called when an item is removed from this station
    /// </summary>
    private void OnStationItemRemoved(GameObject item, PlayerEnd playerEnd)
    {
        ProcessingDebugLog($"Item {item.name} removed by Player {playerEnd.PlayerNumber}");
    }

    /// <summary>
    /// Coroutine to refresh player interaction state after item placement
    /// </summary>
    private System.Collections.IEnumerator RefreshPlayerInteractionAfterPlacement(PlayerEnd playerEnd)
    {
        // Wait a frame to ensure placement is fully complete
        yield return null;

        // Reset our own interaction state
        ForceResetInteractionState();

        // Wait another frame
        yield return null;

        // Refresh the player's interaction state if they're still nearby
        if (playerEnd != null)
        {
            playerEnd.RefreshInteractionState();
            ProcessingDebugLog($"Refreshed Player {playerEnd.PlayerNumber} interaction state after placement (via event)");
        }
    }

    /// <summary>
    /// Coroutine to refresh player interaction state after placement completes
    /// This is called when PerformInteraction returns false for a player carrying items
    /// </summary>
    private System.Collections.IEnumerator RefreshAfterPlacement(PlayerEnd playerEnd)
    {
        // Wait for the placement to fully complete
        // The PlayerEnd will call TryDropOrPlaceItem after this method returns
        yield return null; // Wait for the current frame to complete
        yield return null; // Wait another frame to ensure placement is done
        yield return null; // Wait one more frame to be absolutely sure

        // Reset our own interaction state
        ForceResetInteractionState();

        // Refresh the player's interaction state if they're still nearby
        if (playerEnd != null)
        {
            playerEnd.RefreshInteractionState();
            ProcessingDebugLog($"Refreshed Player {playerEnd.PlayerNumber} interaction state after placement (via PerformInteraction)");
        }
    }

    #endregion

    /// <summary>
    /// Called when processing completes successfully - override to add custom behavior
    /// </summary>
    protected virtual void OnProcessingCompleted(PlayerEnd playerEnd)
    {
        // Default implementation: notify that interaction context has changed
        NotifyInteractionContextChanged(playerEnd);
    }

    #region BaseInteractable Implementation

    /// <summary>
    /// Override CanInteract to allow both players to place/pick up items,
    /// while restricting processing actions to the correct player
    /// </summary>
    public override bool CanInteract(PlayerEnd playerEnd)
    {
        // Check if the object is available
        if (!IsAvailable) return false;

        if (ProcessingStation == null) return false;

        // Both players can interact for placement and pickup
        bool canRetrieve = playerEnd.HasFreeHands && ProcessingStation.IsOccupied && !IsCurrentlyProcessing();
        bool canPlace = playerEnd.IsCarryingItems && ProcessingStation.HasSpace && CanPlaceProcessableItem(playerEnd);

        // Only the correct player can interact for processing
        bool canProcess = playerEnd.CanPerformInteraction(RequiredInteractionType) &&
                         ProcessingStation.IsOccupied &&
                         CanStartOrContinueProcessing(playerEnd);

        bool result = canRetrieve || canPlace || canProcess;

        if (!result)
        {
            ProcessingDebugLog($"Player {playerEnd.PlayerNumber} cannot interact: " +
                             $"canRetrieve={canRetrieve}, canPlace={canPlace}, canProcess={canProcess}");
        }

        return result;
    }

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        // This is now handled in CanInteract override above
        // Kept for compatibility but always returns true if we get here
        return true;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (ProcessingStation == null) return false;

        // If we're waiting for hold decision, don't start new interactions
        if (isWaitingForHoldDecision && !hasCommittedToAction)
        {
            ProcessingDebugLog("Still waiting for hold decision");
            return true; // Keep the interaction active
        }

        // Priority 1: If already processing, continue processing (for hold-to-process mode)
        if (IsCurrentlyProcessing() && CanStartOrContinueProcessing(playerEnd))
        {
            ProcessingDebugLog($"Continuing {ProcessingVerb}");
            return true;
        }

        // Priority 2: If station has processable item and player has free hands - start hold detection
        if (playerEnd.HasFreeHands && ProcessingStation.IsOccupied && CanStartOrContinueProcessing(playerEnd))
        {
            return HandleOccupiedStationInteraction(playerEnd);
        }

        // Priority 3: If player has free hands and station has non-processable item - immediate pickup
        if (playerEnd.HasFreeHands && ProcessingStation.IsOccupied && !CanStartOrContinueProcessing(playerEnd))
        {
            return TryRetrieveItem(playerEnd);
        }

        // Priority 4: Placement will be handled by PlayerEnd's drop logic
        // If player is carrying items, schedule a refresh after placement completes
        if (playerEnd.IsCarryingItems)
        {
            ProcessingDebugLog("Player carrying items - will refresh after placement");

            // CRITICAL: Reset interaction state immediately before returning false
            // This ensures IsAvailable returns true when the player's highlighting refresh logic runs
            ForceResetInteractionState();

            // Also schedule a delayed refresh to ensure state is clean after placement completes
            StartCoroutine(RefreshAfterPlacement(playerEnd));
        }

        return false;
    }

    protected override void OnInteractionStopped(PlayerEnd playerEnd)
    {
        ProcessingDebugLog($"Interaction stopped by Player {playerEnd.PlayerNumber}");

        // If we were in hold detection phase
        if (isWaitingForHoldDecision && !hasCommittedToAction)
        {
            float holdDuration = Time.time - interactionStartTime;
            ProcessingDebugLog($"Hold released after {holdDuration:F2}s (threshold: {holdThresholdTime:F2}s)");

            if (holdDuration < holdThresholdTime)
            {
                // Quick press - pickup item
                ProcessingDebugLog("Quick press detected - picking up item");
                TryRetrieveItem(playerEnd);
            }
            else
            {
                // This shouldn't happen as we should have started processing already
                ProcessingDebugLog("Long hold detected but processing not started - fallback pickup");
                TryRetrieveItem(playerEnd);
            }

            ResetHoldDetectionState();
        }

        // If we were processing with hold-to-process mode, stop processing
        if (requireHoldToProcess && IsCurrentlyProcessing())
        {
            PlayerEnd currentProcessor = GetCurrentProcessingPlayer();
            if (currentProcessor == playerEnd)
            {
                StopProcessing();
                ProcessingDebugLog($"Player {playerEnd.PlayerNumber} stopped {ProcessingVerb} by releasing interact");
            }
        }

        ResetHoldDetectionState();

        // CRITICAL FIX: Ensure interaction state is fully cleared
        // This prevents stale interaction states that can block future interactions
        ForceResetInteractionState();
    }

    /// <summary>
    /// Force reset all interaction state to ensure clean state for future interactions
    /// </summary>
    protected virtual void ForceResetInteractionState()
    {
        // Clear any lingering interaction state
        currentInteractingPlayer = null;
        isBeingInteractedWith = false;

        // Ensure we're available for new interactions
        SetAvailable(true);

        ProcessingDebugLog("Forced interaction state reset - ready for new interactions");
    }

    #endregion

    #region Hold Detection System

    /// <summary>
    /// Handle interaction when station is occupied and player might want to process or pickup
    /// </summary>
    protected virtual bool HandleOccupiedStationInteraction(PlayerEnd playerEnd)
    {
        FoodItem stationFoodItem = ProcessingStation.CurrentItem?.GetComponent<FoodItem>();
        if (stationFoodItem == null) return false;

        // If item cannot be processed, immediate pickup
        if (!CanItemBeProcessed(stationFoodItem))
        {
            ProcessingDebugLog($"Item {stationFoodItem.FoodData.DisplayName} cannot be processed - immediate pickup");
            return TryRetrieveItem(playerEnd);
        }

        // If hold-to-process is disabled, start processing immediately
        if (!requireHoldToProcess)
        {
            ProcessingDebugLog("Hold-to-process disabled - starting processing immediately");
            return StartProcessing(playerEnd);
        }

        // Start hold detection
        ProcessingDebugLog($"Starting hold detection for {ProcessingVerb}");
        interactionStartTime = Time.time;
        isWaitingForHoldDecision = true;
        hasCommittedToAction = false;
        pendingActionType = ProcessingActionType.Process;

        return true; // Keep interaction active for hold detection
    }

    /// <summary>
    /// Reset the hold detection state
    /// </summary>
    protected virtual void ResetHoldDetectionState()
    {
        isWaitingForHoldDecision = false;
        hasCommittedToAction = false;
        pendingActionType = ProcessingActionType.None;
        interactionStartTime = 0f;
    }

    #endregion

    #region Update Loop for Hold Detection

    /// <summary>
    /// Frame-by-frame monitoring of hold duration
    /// NOTE: This Update loop is necessary because Unity's input system doesn't provide
    /// a "hold for X seconds" event. We must manually track the duration between
    /// OnInteractPressed and OnInteractReleased to determine tap vs hold.
    /// </summary>
    protected virtual void Update()
    {
        // Only monitor during the decision phase (after press, before threshold reached or release)
        if (isWaitingForHoldDecision && !hasCommittedToAction)
        {
            UpdateHoldDetection();
        }
    }

    /// <summary>
    /// Check if hold threshold has been reached and start processing if so
    /// </summary>
    protected virtual void UpdateHoldDetection()
    {
        float holdDuration = Time.time - interactionStartTime;

        // If hold threshold reached, commit to processing
        if (holdDuration >= holdThresholdTime)
        {
            ProcessingDebugLog($"Hold threshold reached ({holdDuration:F2}s) - starting {ProcessingVerb}");

            // Find the current interacting player
            PlayerEnd currentPlayer = currentInteractingPlayer;
            if (currentPlayer != null && CanStartOrContinueProcessing(currentPlayer))
            {
                bool success = StartProcessing(currentPlayer);
                if (success)
                {
                    hasCommittedToAction = true;
                    pendingActionType = ProcessingActionType.Process;
                    ProcessingDebugLog($"Successfully started {ProcessingVerb}");
                }
                else
                {
                    ProcessingDebugLog($"Failed to start {ProcessingVerb} - resetting");
                    ResetHoldDetectionState();
                }
            }
            else
            {
                ProcessingDebugLog("Cannot start processing - player invalid or cannot process");
                ResetHoldDetectionState();
            }
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Check if the player is carrying an item that can be placed for processing
    /// </summary>
    protected virtual bool CanPlaceProcessableItem(PlayerEnd playerEnd)
    {
        if (!playerEnd.IsCarryingItems) return false;

        foreach (GameObject carriedObj in playerEnd.HeldObjects)
        {
            if (ProcessingStation.CanAcceptItem(carriedObj, playerEnd))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if processing can be started or continued
    /// </summary>
    protected virtual bool CanStartOrContinueProcessing(PlayerEnd playerEnd)
    {
        if (!ProcessingStation.IsOccupied) return false;

        // Check if player has permission to perform this type of processing
        if (!playerEnd.CanPerformInteraction(RequiredInteractionType))
        {
            return false;
        }

        // If already processing, only the same player can continue
        if (IsCurrentlyProcessing())
        {
            return GetCurrentProcessingPlayer() == playerEnd;
        }

        // Check if the current item can be processed
        FoodItem foodItem = ProcessingStation.CurrentItem?.GetComponent<FoodItem>();
        return foodItem != null && CanItemBeProcessed(foodItem);
    }

    /// <summary>
    /// Try to retrieve the processed item from the station
    /// </summary>
    protected virtual bool TryRetrieveItem(PlayerEnd playerEnd)
    {
        if (!playerEnd.HasFreeHands || !ProcessingStation.IsOccupied)
        {
            return false;
        }

        // Use the base station's removal logic
        GameObject retrievedItem = ProcessingStation.RemoveItem(playerEnd);
        if (retrievedItem == null) return false;

        // Give it to the player
        bool pickupSuccessful = playerEnd.PickUpObject(retrievedItem);

        if (!pickupSuccessful)
        {
            // If pickup failed, put the item back on the station
            ProcessingStation.PlaceItem(retrievedItem, playerEnd);
            ProcessingDebugLog("Failed to retrieve item - returned to station");
            return false;
        }

        // Refresh detection after item removal
        PlayerEndDetectionRefresher.RefreshNearStation(transform, name);

        ProcessingDebugLog($"Player {playerEnd.PlayerNumber} retrieved item from {ProcessingVerb} station");
        return true;
    }

    #endregion

    #region Prompt Generation

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        // If waiting for hold decision, show hold prompt
        if (isWaitingForHoldDecision && !hasCommittedToAction)
        {
            float holdDuration = Time.time - interactionStartTime;
            float progress = Mathf.Clamp01(holdDuration / holdThresholdTime);
            return $"Hold to {ProcessVerb} ({progress * 100f:F0}%)";
        }

        // If currently processing - show progress
        if (IsCurrentlyProcessing())
        {
            if (requireHoldToProcess)
            {
                string progressText = showProgressInPrompt ?
                    $" ({GetProcessingProgress() * 100f:F0}%)" : "";
                return $"Hold to {ProcessVerb}{progressText}";
            }
            else
            {
                string progressText = showProgressInPrompt ?
                    $" ({GetProcessingProgress() * 100f:F0}%)" : "";
                return $"{ProcessingVerb.Substring(0, 1).ToUpper() + ProcessingVerb.Substring(1)}{progressText}";
            }
        }

        // If station has processable item - show appropriate prompt
        if (ProcessingStation.IsOccupied && CanStartOrContinueProcessing(playerEnd))
        {
            FoodItem foodItem = ProcessingStation.CurrentItem?.GetComponent<FoodItem>();
            if (foodItem != null && CanItemBeProcessed(foodItem))
            {
                if (requireHoldToProcess)
                {
                    return $"Hold to {ProcessVerb} / Tap to pick up {foodItem.FoodData.DisplayName}";
                }
                else
                {
                    return $"{ProcessVerb.Substring(0, 1).ToUpper() + ProcessVerb.Substring(1)} {foodItem.FoodData.DisplayName}";
                }
            }
        }

        // If player has free hands and station has item - show pickup prompt
        if (playerEnd.HasFreeHands && ProcessingStation.IsOccupied && !IsCurrentlyProcessing())
        {
            return $"Pick up {ProcessingStation.CurrentItem.name}";
        }

        // If player is carrying processable item and station has space - show placement prompt
        if (playerEnd.IsCarryingItems && ProcessingStation.HasSpace && CanPlaceProcessableItem(playerEnd))
        {
            return $"Place item for {ProcessingVerb}";
        }

        return "Interact";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (ProcessingStation == null) return "Station not available";

        // Check player permissions first
        if (!playerEnd.CanPerformInteraction(RequiredInteractionType))
        {
            switch (RequiredInteractionType)
            {
                case InteractionType.Chopping:
                    return "Only Player 2 can chop";
                case InteractionType.Cooking:
                    return "Only Player 1 can cook";
                default:
                    return $"Cannot perform {RequiredInteractionType}";
            }
        }

        // Check specific unavailability reasons
        if (playerEnd.HasFreeHands && !ProcessingStation.IsOccupied)
            return "Nothing to pick up";

        if (playerEnd.IsCarryingItems && !ProcessingStation.HasSpace)
            return "Station is busy";

        if (playerEnd.IsCarryingItems && ProcessingStation.HasSpace && !CanPlaceProcessableItem(playerEnd))
            return $"Cannot {ProcessVerb} this item";

        if (IsCurrentlyProcessing() && GetCurrentProcessingPlayer() != playerEnd)
            return "Being used by other player";

        return "Cannot interact";
    }

    /// <summary>
    /// Notify the PlayerEnd that the interaction context has changed
    /// This should be called when processing completes and the item transforms
    /// </summary>
    protected virtual void NotifyInteractionContextChanged(PlayerEnd playerEnd)
    {
        if (playerEnd == null) return;

        ProcessingDebugLog($"Notifying Player {playerEnd.PlayerNumber} of interaction context change");

        // Force reset our own interaction state first
        ForceResetInteractionState();

        // Use a coroutine to refresh after a frame delay to ensure all state changes have propagated
        StartCoroutine(RefreshPlayerInteractionStateDelayed(playerEnd));
    }

    /// <summary>
    /// Coroutine to refresh player interaction state after a brief delay
    /// </summary>
    protected virtual System.Collections.IEnumerator RefreshPlayerInteractionStateDelayed(PlayerEnd playerEnd)
    {
        // Wait a frame to ensure all state changes have propagated
        yield return null;

        // Refresh the player's interaction state
        if (playerEnd != null)
        {
            playerEnd.RefreshInteractionState();
            ProcessingDebugLog($"Player {playerEnd.PlayerNumber} interaction state refreshed");
        }
    }

    protected void ProcessingDebugLog(string message)
    {
        if (enableProcessingDebug)
            Debug.Log($"[{GetType().Name}] {message}");
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw processing-specific gizmos
        if (Application.isPlaying && ProcessingStation != null)
        {
            // Draw interaction state indicator
            Gizmos.color = IsCurrentlyProcessing() ? Color.red :
                          (ProcessingStation.IsOccupied ? Color.yellow : Color.green);
            Vector3 indicatorPos = transform.position + Vector3.up * 3.5f;
            Gizmos.DrawWireCube(indicatorPos, Vector3.one * 0.2f);

            // Draw hold detection indicator
            if (isWaitingForHoldDecision)
            {
                Gizmos.color = Color.cyan;
                float progress = (Time.time - interactionStartTime) / holdThresholdTime;
                Gizmos.DrawWireSphere(indicatorPos + Vector3.up * 0.5f, 0.1f + progress * 0.2f);
            }

            // Draw processing capability indicator
            if (ProcessingStation.IsOccupied)
            {
                FoodItem foodItem = ProcessingStation.CurrentItem?.GetComponent<FoodItem>();
                if (foodItem != null && CanItemBeProcessed(foodItem))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(indicatorPos + Vector3.up * 0.8f, 0.3f);
                }
            }
        }
    }

    #endregion
}

/// <summary>
/// Enum to track what type of action is pending during hold detection
/// </summary>
public enum ProcessingActionType
{
    None,
    Pickup,
    Process
}