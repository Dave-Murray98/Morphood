using UnityEngine;

/// <summary>
/// Handles food-specific interactions for food items.
/// This component works alongside FoodItem to provide food processing capabilities
/// while maintaining compatibility with the existing interaction system.
/// </summary>
[RequireComponent(typeof(FoodItem))]
public class FoodItemInteractable : BaseInteractable
{
    [Header("Food Interaction Settings")]
    [SerializeField] private bool allowDirectProcessing = false;
    [Tooltip("Whether players can process this food item by interacting with it directly (without a station)")]

    [SerializeField] private float directProcessingTime = 2f;
    [Tooltip("Time required to process this food item when interacting directly")]

    [Header("Processing Feedback")]
    [SerializeField] private bool showProcessingPrompts = true;
    [Tooltip("Whether to show specific prompts for chopping/cooking when near processing stations")]

    // Internal references
    private FoodItem foodItem;
    private float processingStartTime = 0f;
    private bool isProcessing = false;
    private FoodProcessType currentProcessType;

    protected override void Awake()
    {
        base.Awake();

        // Get the FoodItem component
        foodItem = GetComponent<FoodItem>();
        if (foodItem == null)
        {
            Debug.LogError($"[FoodItemInteractable] {name} requires a FoodItem component!");
        }

        // Set up as universal interaction by default
        interactionType = InteractionType.Universal;
        interactionPriority = 1; // Standard priority for food items
    }

    #region BaseInteractable Implementation

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        if (foodItem == null || !foodItem.HasValidFoodData)
        {
            DebugLog("Cannot interact - no valid food data");
            return false;
        }

        // If item is already being processed, only the same player can continue
        if (isProcessing && currentInteractingPlayer != playerEnd)
        {
            DebugLog($"Food is being processed by another player");
            return false;
        }

        // Check if direct processing is allowed
        if (allowDirectProcessing)
        {
            // Check if the player can perform any processing on this food
            bool canChop = playerEnd.CanPerformInteraction(InteractionType.Chopping) &&
                          foodItem.CanBeProcessed(FoodProcessType.Chopping);
            bool canCook = playerEnd.CanPerformInteraction(InteractionType.Cooking) &&
                          foodItem.CanBeProcessed(FoodProcessType.Cooking);

            if (canChop || canCook)
            {
                DebugLog($"Player {playerEnd.PlayerNumber} can process this food directly");
                return true;
            }
        }

        // If direct processing isn't allowed or available, fall back to standard pickup behavior
        // This is handled by the base FoodItem (PickupableItem) logic
        return false;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (foodItem == null) return false;

        // If direct processing is enabled and player can process this food
        if (allowDirectProcessing)
        {
            return StartDirectProcessing(playerEnd);
        }

        // Otherwise, let the PickupableItem handle the interaction
        // This maintains the existing pickup behavior
        return false;
    }

    protected override void OnInteractionStopped(PlayerEnd playerEnd)
    {
        if (isProcessing)
        {
            StopProcessing();
        }
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        if (foodItem == null || !foodItem.HasValidFoodData)
            return "Food item not ready";

        // If currently processing
        if (isProcessing)
        {
            float progress = (Time.time - processingStartTime) / directProcessingTime;
            return $"Processing... ({progress * 100f:F0}%)";
        }

        // If direct processing is available
        if (allowDirectProcessing)
        {
            // Check what processing the player can do
            bool canChop = playerEnd.CanPerformInteraction(InteractionType.Chopping) &&
                          foodItem.CanBeProcessed(FoodProcessType.Chopping);
            bool canCook = playerEnd.CanPerformInteraction(InteractionType.Cooking) &&
                          foodItem.CanBeProcessed(FoodProcessType.Cooking);

            if (canChop && canCook)
            {
                return $"Hold to chop or cook {foodItem.FoodData.DisplayName}";
            }
            else if (canChop)
            {
                return $"Hold to chop {foodItem.FoodData.DisplayName}";
            }
            else if (canCook)
            {
                return $"Hold to cook {foodItem.FoodData.DisplayName}";
            }
        }

        // Default to pickup prompt (handled by PickupableItem)
        return $"Pick up {foodItem.FoodData.DisplayName}";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (foodItem == null || !foodItem.HasValidFoodData)
            return "Food item not available";

        if (isProcessing && currentInteractingPlayer != playerEnd)
            return "Being processed by other player";

        // Check specific processing limitations
        if (allowDirectProcessing)
        {
            bool canChop = playerEnd.CanPerformInteraction(InteractionType.Chopping);
            bool canCook = playerEnd.CanPerformInteraction(InteractionType.Cooking);
            bool foodCanBeChopped = foodItem.CanBeProcessed(FoodProcessType.Chopping);
            bool foodCanBeCooked = foodItem.CanBeProcessed(FoodProcessType.Cooking);

            if (foodCanBeChopped && !canChop)
                return "Only Player 2 can chop";
            if (foodCanBeCooked && !canCook)
                return "Only Player 1 can cook";
            if (!foodCanBeChopped && !foodCanBeCooked)
                return "Cannot be processed";
        }

        return base.GetUnavailablePrompt(playerEnd);
    }

    #endregion

    #region Direct Processing System

    private bool StartDirectProcessing(PlayerEnd playerEnd)
    {
        // Determine what type of processing to do
        FoodProcessType processType = DetermineProcessType(playerEnd);
        if (processType == FoodProcessType.Chopping && !foodItem.CanBeProcessed(FoodProcessType.Chopping))
            return false;
        if (processType == FoodProcessType.Cooking && !foodItem.CanBeProcessed(FoodProcessType.Cooking))
            return false;

        // Start processing
        isProcessing = true;
        currentProcessType = processType;
        processingStartTime = Time.time;

        DebugLog($"Started {processType} processing by Player {playerEnd.PlayerNumber}");
        return true;
    }

    private void StopProcessing()
    {
        isProcessing = false;
        processingStartTime = 0f;
        DebugLog("Stopped food processing");
    }

    private FoodProcessType DetermineProcessType(PlayerEnd playerEnd)
    {
        // Prioritize the processing type that the player can do
        if (playerEnd.CanPerformInteraction(InteractionType.Chopping) &&
            foodItem.CanBeProcessed(FoodProcessType.Chopping))
        {
            return FoodProcessType.Chopping;
        }

        if (playerEnd.CanPerformInteraction(InteractionType.Cooking) &&
            foodItem.CanBeProcessed(FoodProcessType.Cooking))
        {
            return FoodProcessType.Cooking;
        }

        // Default to chopping if both are available (shouldn't happen with proper setup)
        return FoodProcessType.Chopping;
    }

    #endregion

    #region Update Loop for Processing

    private void Update()
    {
        if (isProcessing)
        {
            UpdateProcessing();
        }
    }

    private void UpdateProcessing()
    {
        float elapsedTime = Time.time - processingStartTime;

        if (elapsedTime >= directProcessingTime)
        {
            // Processing complete - transform the food
            CompleteProcessing();
        }
    }

    private void CompleteProcessing()
    {
        if (foodItem != null)
        {
            bool success = foodItem.TransformFood(currentProcessType);

            if (success)
            {
                DebugLog($"Successfully processed {foodItem.FoodData.DisplayName} using {currentProcessType}");

                // Update interaction prompt since the food has changed
                interactionPrompt = $"Pick up {foodItem.FoodData.DisplayName}";
            }
            else
            {
                DebugLog($"Failed to process food using {currentProcessType}");
            }
        }

        StopProcessing();
    }

    #endregion

    #region Station Integration Methods

    /// <summary>
    /// Process this food item using a station (called by cooking/chopping stations)
    /// </summary>
    /// <param name="processType">The type of processing to apply</param>
    /// <param name="playerEnd">The player performing the processing</param>
    /// <returns>True if processing was successful</returns>
    public bool ProcessWithStation(FoodProcessType processType, PlayerEnd playerEnd)
    {
        if (foodItem == null)
        {
            DebugLog("Cannot process - no FoodItem component");
            return false;
        }

        // Check if the player can perform this type of processing
        InteractionType requiredInteraction = processType == FoodProcessType.Chopping ?
            InteractionType.Chopping : InteractionType.Cooking;

        if (!playerEnd.CanPerformInteraction(requiredInteraction))
        {
            DebugLog($"Player {playerEnd.PlayerNumber} cannot perform {processType}");
            return false;
        }

        // Perform the transformation
        bool success = foodItem.TransformFood(processType);

        if (success)
        {
            DebugLog($"Successfully processed {foodItem.FoodData.DisplayName} using station ({processType})");
        }
        else
        {
            DebugLog($"Failed to process food using station ({processType})");
        }

        return success;
    }

    /// <summary>
    /// Check if this food item can be processed with a specific process type
    /// </summary>
    /// <param name="processType">The type of processing to check</param>
    /// <returns>True if the food can be processed this way</returns>
    public bool CanBeProcessedWithStation(FoodProcessType processType)
    {
        if (foodItem == null) return false;
        return foodItem.CanBeProcessed(processType);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Enable or disable direct processing capability
    /// </summary>
    /// <param name="enabled">Whether direct processing should be enabled</param>
    public void SetDirectProcessingEnabled(bool enabled)
    {
        allowDirectProcessing = enabled;
        DebugLog($"Direct processing {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Check if this food item is currently being processed
    /// </summary>
    public bool IsBeingProcessed => isProcessing;

    /// <summary>
    /// Get the current processing progress (0-1)
    /// </summary>
    public float GetProcessingProgress()
    {
        if (!isProcessing) return 0f;

        float elapsedTime = Time.time - processingStartTime;
        return Mathf.Clamp01(elapsedTime / directProcessingTime);
    }

    #endregion

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw processing progress indicator when processing
        if (isProcessing)
        {
            Gizmos.color = Color.yellow;
            Vector3 progressPos = transform.position + Vector3.up * 2.5f;
            float progress = GetProcessingProgress();

            // Draw a progress bar
            Vector3 barStart = progressPos - Vector3.right * 0.5f;
            Vector3 barEnd = progressPos + Vector3.right * 0.5f;
            Vector3 progressPoint = Vector3.Lerp(barStart, barEnd, progress);

            Gizmos.DrawLine(barStart, progressPoint);

            Gizmos.color = Color.gray;
            Gizmos.DrawLine(progressPoint, barEnd);
        }
    }

    #endregion
}