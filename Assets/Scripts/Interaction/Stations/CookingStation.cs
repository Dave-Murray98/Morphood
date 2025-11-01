using UnityEngine;

/// <summary>
/// A specialized station for cooking food items.
/// Both players can place items here, but only Player 1 can perform cooking.
/// Integrates with the pooling system for efficient item transformation.
/// </summary>
public class CookingStation : BaseStation
{
    [Header("Cooking Station Settings")]
    [Tooltip("Time required to complete cooking process")]

    [SerializeField] private bool autoStartCooking = false;
    [Tooltip("Whether cooking starts automatically when a valid item is placed")]

    [Header("Processing State")]
    [SerializeField] private bool isCurrentlyCooking = false;
    [Tooltip("Whether the station is currently processing an item")]

    [SerializeField] private float cookingProgress = 0f;
    [Tooltip("Current progress of the cooking process (0-1)")]

    [Header("Audio & Effects")]
    [SerializeField] private AudioSource cookingAudioSource;
    [Tooltip("Audio source for cooking sounds")]

    [SerializeField] private ParticleSystem cookingEffects;
    [Tooltip("Particle effects for cooking process")]

    // Internal state
    private PlayerEnd currentCookingPlayer;
    private float cookingStartTime;
    private bool wasCookingLastFrame = false;

    // Events
    public System.Action<FoodItem> OnCookingStarted;
    public System.Action<FoodItem> OnCookingCompleted;
    public System.Action<FoodItem> OnCookingStopped;

    protected override void Initialize()
    {
        // Set up as a cooking station
        stationType = StationType.Cooking;
        stationName = string.IsNullOrEmpty(stationName) || stationName == "Station" ? "Cooking Station" : stationName;

        // Both players can place items, but only Player 1 can perform cooking
        allowPlayer1Interaction = true;
        allowPlayer2Interaction = true;

        base.Initialize();

        // Validate components
        ValidateComponents();

        DebugLog("Cooking station ready for food processing");
    }

    private void ValidateComponents()
    {
        // Validate audio source
        if (cookingAudioSource == null)
        {
            cookingAudioSource = GetComponent<AudioSource>();
            if (cookingAudioSource == null)
            {
                DebugLog("No audio source found - cooking will be silent");
            }
        }

        // Validate particle system
        if (cookingEffects == null)
        {
            cookingEffects = GetComponentInChildren<ParticleSystem>();
            if (cookingEffects == null)
            {
                DebugLog("No particle system found - cooking will have no visual effects");
            }
        }
    }

    #region Update Loop

    private void Update()
    {
        if (isCurrentlyCooking)
        {
            UpdateCookingProcess();
        }

        // Handle audio and effects
        UpdateAudioAndEffects();
    }

    private void UpdateCookingProcess()
    {
        if (currentCookingPlayer == null || currentItem == null)
        {
            StopCooking();
            return;
        }

        // Update progress
        float elapsedTime = Time.time - cookingStartTime;
        cookingProgress = Mathf.Clamp01(elapsedTime / FoodManager.Instance.cookingSettings.cookingHoldTime);

        // Check if cooking is complete
        if (cookingProgress >= 1f)
        {
            CompleteCooking();
        }
    }

    private void UpdateAudioAndEffects()
    {
        // Start/stop audio
        if (cookingAudioSource != null)
        {
            if (isCurrentlyCooking && !cookingAudioSource.isPlaying)
            {
                cookingAudioSource.Play();
            }
            else if (!isCurrentlyCooking && cookingAudioSource.isPlaying)
            {
                cookingAudioSource.Stop();
            }
        }

        // Start/stop particle effects
        if (cookingEffects != null)
        {
            if (isCurrentlyCooking && !cookingEffects.isPlaying)
            {
                cookingEffects.Play();
            }
            else if (!isCurrentlyCooking && cookingEffects.isPlaying)
            {
                cookingEffects.Stop();
            }
        }

        wasCookingLastFrame = isCurrentlyCooking;
    }

    #endregion

    #region Station Overrides

    protected override bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        // Check if it's a food item that can be cooked
        FoodItem foodItem = item.GetComponent<FoodItem>();
        if (foodItem == null || !foodItem.HasValidFoodData)
        {
            DebugLog("Item is not a valid food item");
            return false;
        }

        if (!foodItem.CanBeProcessed(FoodProcessType.Cooking))
        {
            DebugLog($"{foodItem.FoodData.DisplayName} cannot be cooked");
            return false;
        }

        // Don't accept items if currently cooking
        if (isCurrentlyCooking)
        {
            DebugLog("Station is currently cooking - cannot accept new items");
            return false;
        }

        return true;
    }

    protected override void OnItemPlacedInternal(GameObject item, PlayerEnd playerEnd)
    {
        DebugLog($"Food item {item.name} placed on cooking station by Player {playerEnd.PlayerNumber}");

        // Refresh PlayerEnd detection after placing item
        PlayerEndDetectionRefresher.RefreshNearStation(transform, stationName);

        // Start cooking automatically if enabled
        if (autoStartCooking)
        {
            StartCooking(playerEnd);
        }
    }

    protected override void OnItemRemovedInternal(GameObject item, PlayerEnd playerEnd)
    {
        // Stop cooking if an item was removed
        if (isCurrentlyCooking)
        {
            StopCooking();
        }

        // Refresh PlayerEnd detection after removing item
        PlayerEndDetectionRefresher.RefreshNearStation(transform, stationName);

        DebugLog($"Food item {item.name} removed from cooking station");
    }

    #endregion

    #region Cooking Process

    /// <summary>
    /// Start the cooking process
    /// </summary>
    /// <param name="playerEnd">The player starting the cooking</param>
    /// <returns>True if cooking started successfully</returns>
    public bool StartCooking(PlayerEnd playerEnd)
    {
        if (isCurrentlyCooking)
        {
            DebugLog("Station is already cooking");
            return false;
        }

        if (!isOccupied || currentItem == null)
        {
            DebugLog("No item to cook");
            return false;
        }

        // Validate the player can cook
        if (!playerEnd.CanPerformInteraction(InteractionType.Cooking))
        {
            DebugLog($"Player {playerEnd.PlayerNumber} cannot perform cooking");
            return false;
        }

        // Validate the item can be cooked
        FoodItem foodItem = currentItem.GetComponent<FoodItem>();
        if (foodItem == null || !foodItem.CanBeProcessed(FoodProcessType.Cooking))
        {
            DebugLog("Current item cannot be cooked");
            return false;
        }

        // Start cooking
        isCurrentlyCooking = true;
        currentCookingPlayer = playerEnd;
        cookingStartTime = Time.time;
        cookingProgress = 0f;

        DebugLog($"Started cooking {foodItem.FoodData.DisplayName} by Player {playerEnd.PlayerNumber}");

        // Fire event
        OnCookingStarted?.Invoke(foodItem);

        return true;
    }

    /// <summary>
    /// Stop the cooking process
    /// </summary>
    public void StopCooking()
    {
        if (!isCurrentlyCooking) return;

        FoodItem foodItem = currentItem?.GetComponent<FoodItem>();

        isCurrentlyCooking = false;
        currentCookingPlayer = null;
        cookingProgress = 0f;

        DebugLog("Stopped cooking process");

        // Fire event
        if (foodItem != null)
        {
            OnCookingStopped?.Invoke(foodItem);
        }
    }

    /// <summary>
    /// Complete the cooking process and transform the food item
    /// </summary>
    private void CompleteCooking()
    {
        if (!isCurrentlyCooking || currentItem == null) return;

        FoodItem foodItem = currentItem.GetComponent<FoodItem>();
        if (foodItem == null)
        {
            DebugLog("No valid food item to complete cooking");
            StopCooking();
            return;
        }

        DebugLog($"Completing cooking of {foodItem.FoodData.DisplayName}");

        // Store the current player and position
        PlayerEnd completingPlayer = currentCookingPlayer;
        Vector3 itemPosition = currentItem.transform.position;

        // Remove the item from the station first
        GameObject removedItem = RemoveItem(completingPlayer);
        if (removedItem == null)
        {
            DebugLog("Failed to remove item for cooking completion");
            StopCooking();
            return;
        }

        // Transform the food item using the FoodManager for pooling efficiency
        FoodItem transformedItem = null;
        if (FoodManager.Instance != null)
        {
            transformedItem = FoodManager.Instance.TransformFoodItem(foodItem, FoodProcessType.Cooking, itemPosition);
        }
        else
        {
            // Fallback: transform in place
            bool success = foodItem.TransformFood(FoodProcessType.Cooking);
            if (success)
            {
                transformedItem = foodItem;
            }
        }

        // Stop the cooking process
        isCurrentlyCooking = false;
        currentCookingPlayer = null;
        cookingProgress = 0f;

        if (transformedItem != null)
        {
            // Place the transformed item back on the station
            bool placedSuccessfully = PlaceItem(transformedItem.gameObject, completingPlayer);

            if (placedSuccessfully)
            {
                DebugLog($"Successfully cooked and placed {transformedItem.FoodData.DisplayName} on station");

                // CRITICAL FIX: Force refresh PlayerEnd detection after transformation
                PlayerEndDetectionRefresher.RefreshNearStation(transform, stationName);
                DebugLog("Refreshed PlayerEnd detection after food transformation");
            }
            else
            {
                DebugLog("Cooking succeeded but failed to place result back on station");
            }

            // Fire completion event
            OnCookingCompleted?.Invoke(transformedItem);
        }
        else
        {
            DebugLog("Failed to transform food item during cooking");
        }
    }

    /// <summary>
    /// Check if the station is currently processing an item
    /// </summary>
    public bool IsCooking => isCurrentlyCooking;

    /// <summary>
    /// Get the current cooking progress (0-1)
    /// </summary>
    public float GetCookingProgress() => cookingProgress;

    /// <summary>
    /// Get the player currently cooking on this station
    /// </summary>
    public PlayerEnd GetCurrentCookingPlayer() => currentCookingPlayer;

    #endregion

    #region Public Configuration

    /// <summary>
    /// Set whether cooking should start automatically when items are placed
    /// </summary>
    /// <param name="auto">Whether to auto-start cooking</param>
    public void SetAutoStartCooking(bool auto)
    {
        autoStartCooking = auto;
        DebugLog($"Auto-start cooking: {auto}");
    }

    #endregion

    #region Debug and Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw cooking progress indicator
        if (Application.isPlaying && isCurrentlyCooking)
        {
            Gizmos.color = Color.red;
            Vector3 progressPos = transform.position + Vector3.up * 3f;

            // Draw progress bar
            Vector3 barStart = progressPos - Vector3.right * 0.5f;
            Vector3 barEnd = progressPos + Vector3.right * 0.5f;
            Vector3 progressPoint = Vector3.Lerp(barStart, barEnd, cookingProgress);

            Gizmos.DrawLine(barStart, progressPoint);

            Gizmos.color = Color.gray;
            Gizmos.DrawLine(progressPoint, barEnd);

            // Draw percentage text
#if UNITY_EDITOR
            string progressText = $"Cooking: {cookingProgress * 100f:F0}%";
            UnityEditor.Handles.Label(progressPos + Vector3.up * 0.5f, progressText);
#endif
        }

        // Draw cooking station indicator
        Gizmos.color = Color.red;
        Vector3 indicatorPos = transform.position + Vector3.up * 2.5f;
        Gizmos.DrawCube(indicatorPos, Vector3.one * 0.3f);
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        // Ensure reasonable values
        cookingProgress = Mathf.Clamp01(cookingProgress);

        // Ensure this is configured as a cooking station
        if (Application.isPlaying)
        {
            stationType = StationType.Cooking;
            allowPlayer1Interaction = true;
            allowPlayer2Interaction = true;
        }
    }

    #endregion
}