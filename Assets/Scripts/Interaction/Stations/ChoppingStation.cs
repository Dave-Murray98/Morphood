using UnityEngine;

/// <summary>
/// A specialized station for chopping food items.
/// Both players can place items here, but only Player 2 can perform chopping.
/// Integrates with the pooling system for efficient item transformation.
/// FIXED: Now properly refreshes PlayerEnd detection after food transformation.
/// </summary>
public class ChoppingStation : BaseStation
{
    [Header("Chopping Station Settings")]
    [SerializeField] private float choppingTime = 3f;
    [Tooltip("Time required to complete chopping process")]

    [SerializeField] private bool autoStartChopping = false;
    [Tooltip("Whether chopping starts automatically when a valid item is placed")]

    [Header("Processing State")]
    [SerializeField] private bool isCurrentlyChopping = false;
    [Tooltip("Whether the station is currently processing an item")]

    [SerializeField] private float choppingProgress = 0f;
    [Tooltip("Current progress of the chopping process (0-1)")]

    [Header("Audio & Effects")]
    [SerializeField] private AudioSource choppingAudioSource;
    [Tooltip("Audio source for chopping sounds")]

    [SerializeField] private ParticleSystem choppingEffects;
    [Tooltip("Particle effects for chopping process")]

    // Internal state
    private PlayerEnd currentChoppingPlayer;
    private float choppingStartTime;
    private bool wasChoppingLastFrame = false;

    // Events
    public System.Action<FoodItem> OnChoppingStarted;
    public System.Action<FoodItem> OnChoppingCompleted;
    public System.Action<FoodItem> OnChoppingStopped;

    protected override void Initialize()
    {
        // Set up as a chopping station
        stationType = StationType.Chopping;
        stationName = string.IsNullOrEmpty(stationName) || stationName == "Station" ? "Chopping Station" : stationName;

        // Both players can place items, but only Player 2 can perform chopping
        allowPlayer1Interaction = true;
        allowPlayer2Interaction = true;

        base.Initialize();

        // Validate components
        ValidateComponents();

        DebugLog("Chopping station ready for food processing");
    }

    private void ValidateComponents()
    {
        // Validate audio source
        if (choppingAudioSource == null)
        {
            choppingAudioSource = GetComponent<AudioSource>();
            if (choppingAudioSource == null)
            {
                DebugLog("No audio source found - chopping will be silent");
            }
        }

        // Validate particle system
        if (choppingEffects == null)
        {
            choppingEffects = GetComponentInChildren<ParticleSystem>();
            if (choppingEffects == null)
            {
                DebugLog("No particle system found - chopping will have no visual effects");
            }
        }
    }

    #region Update Loop

    private void Update()
    {
        if (isCurrentlyChopping)
        {
            UpdateChoppingProcess();
        }

        // Handle audio and effects
        UpdateAudioAndEffects();
    }

    private void UpdateChoppingProcess()
    {
        if (currentChoppingPlayer == null || currentItem == null)
        {
            StopChopping();
            return;
        }

        // Update progress
        float elapsedTime = Time.time - choppingStartTime;
        choppingProgress = Mathf.Clamp01(elapsedTime / choppingTime);

        // Check if chopping is complete
        if (choppingProgress >= 1f)
        {
            CompleteChopping();
        }
    }

    private void UpdateAudioAndEffects()
    {
        // Start/stop audio
        if (choppingAudioSource != null)
        {
            if (isCurrentlyChopping && !choppingAudioSource.isPlaying)
            {
                choppingAudioSource.Play();
            }
            else if (!isCurrentlyChopping && choppingAudioSource.isPlaying)
            {
                choppingAudioSource.Stop();
            }
        }

        // Start/stop particle effects
        if (choppingEffects != null)
        {
            if (isCurrentlyChopping && !choppingEffects.isPlaying)
            {
                choppingEffects.Play();
            }
            else if (!isCurrentlyChopping && choppingEffects.isPlaying)
            {
                choppingEffects.Stop();
            }
        }

        wasChoppingLastFrame = isCurrentlyChopping;
    }

    #endregion

    #region Station Overrides

    protected override bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        // Check if it's a food item that can be chopped
        FoodItem foodItem = item.GetComponent<FoodItem>();
        if (foodItem == null || !foodItem.HasValidFoodData)
        {
            DebugLog("Item is not a valid food item");
            return false;
        }

        if (!foodItem.CanBeProcessed(FoodProcessType.Chopping))
        {
            DebugLog($"{foodItem.FoodData.DisplayName} cannot be chopped");
            return false;
        }

        // Don't accept items if currently chopping
        if (isCurrentlyChopping)
        {
            DebugLog("Station is currently chopping - cannot accept new items");
            return false;
        }

        return true;
    }

    protected override void OnItemPlacedInternal(GameObject item, PlayerEnd playerEnd)
    {
        base.OnItemPlacedInternal(item, playerEnd);
        DebugLog($"Food item {item.name} placed on chopping station by Player {playerEnd.PlayerNumber}");

        // NOTE: Do NOT call PlayerEndDetectionRefresher here during placement!
        // The BaseProcessingStationInteractable handles refresh via OnItemPlaced event
        // after placement is fully complete and player's inventory is updated.

        // Start chopping automatically if enabled
        if (autoStartChopping)
        {
            StartChopping(playerEnd);
        }
    }

    protected override void OnItemRemovedInternal(GameObject item, PlayerEnd playerEnd)
    {
        // Stop chopping if an item was removed
        if (isCurrentlyChopping)
        {
            StopChopping();
        }

        // Refresh PlayerEnd detection after removing item
        PlayerEndDetectionRefresher.RefreshNearStation(transform, stationName);

        DebugLog($"Food item {item.name} removed from chopping station");
    }

    #endregion

    #region Chopping Process

    /// <summary>
    /// Start the chopping process
    /// </summary>
    /// <param name="playerEnd">The player starting the chopping</param>
    /// <returns>True if chopping started successfully</returns>
    public bool StartChopping(PlayerEnd playerEnd)
    {
        if (isCurrentlyChopping)
        {
            DebugLog("Station is already chopping");
            return false;
        }

        if (!isOccupied || currentItem == null)
        {
            DebugLog("No item to chop");
            return false;
        }

        // Validate the player can chop
        if (!playerEnd.CanPerformInteraction(InteractionType.Chopping))
        {
            DebugLog($"Player {playerEnd.PlayerNumber} cannot perform chopping");
            return false;
        }

        // Validate the item can be chopped
        FoodItem foodItem = currentItem.GetComponent<FoodItem>();
        if (foodItem == null || !foodItem.CanBeProcessed(FoodProcessType.Chopping))
        {
            DebugLog("Current item cannot be chopped");
            return false;
        }

        // Start chopping
        isCurrentlyChopping = true;
        currentChoppingPlayer = playerEnd;
        currentChoppingPlayer.isChopping = true;
        choppingStartTime = Time.time;
        choppingProgress = 0f;

        DebugLog($"Started chopping {foodItem.FoodData.DisplayName} by Player {playerEnd.PlayerNumber}");

        // Fire event
        OnChoppingStarted?.Invoke(foodItem);

        foodItem.OnProcessingStarted(FoodProcessType.Chopping);

        return true;
    }

    /// <summary>
    /// Stop the chopping process
    /// </summary>
    public void StopChopping()
    {
        if (!isCurrentlyChopping) return;

        FoodItem foodItem = currentItem?.GetComponent<FoodItem>();
        foodItem.OnProcessingStopped(FoodProcessType.Chopping);

        isCurrentlyChopping = false;
        currentChoppingPlayer.isChopping = false;
        currentChoppingPlayer = null;
        choppingProgress = 0f;

        DebugLog("Stopped chopping process");

        // Fire event
        if (foodItem != null)
        {
            OnChoppingStopped?.Invoke(foodItem);
        }
    }

    /// <summary>
    /// Complete the chopping process and transform the food item
    /// FIXED: Now properly refreshes PlayerEnd detection after transformation
    /// </summary>
    private void CompleteChopping()
    {
        if (!isCurrentlyChopping || currentItem == null) return;

        FoodItem foodItem = currentItem.GetComponent<FoodItem>();
        if (foodItem == null)
        {
            DebugLog("No valid food item to complete chopping");
            StopChopping();
            return;
        }

        DebugLog($"Completing chopping of {foodItem.FoodData.DisplayName}");

        // Store the current player and position
        PlayerEnd completingPlayer = currentChoppingPlayer;
        Vector3 itemPosition = currentItem.transform.position;

        // Remove the item from the station first
        GameObject removedItem = RemoveItem(completingPlayer);
        if (removedItem == null)
        {
            DebugLog("Failed to remove item for chopping completion");
            StopChopping();
            return;
        }

        // Transform the food item using the FoodManager for pooling efficiency
        FoodItem transformedItem = null;
        if (FoodManager.Instance != null)
        {
            transformedItem = FoodManager.Instance.TransformFoodItem(foodItem, FoodProcessType.Chopping, itemPosition);
        }
        else
        {
            // Fallback: transform in place
            bool success = foodItem.TransformFood(FoodProcessType.Chopping);
            if (success)
            {
                transformedItem = foodItem;
            }
        }

        // Stop the chopping process
        isCurrentlyChopping = false;
        currentChoppingPlayer = null;
        choppingProgress = 0f;

        if (transformedItem != null)
        {
            // Place the transformed item back on the station
            bool placedSuccessfully = PlaceItem(transformedItem.gameObject, completingPlayer);

            if (placedSuccessfully)
            {
                DebugLog($"Successfully chopped and placed {transformedItem.FoodData.DisplayName} on station");

                // CRITICAL FIX: Force refresh PlayerEnd detection after transformation
                PlayerEndDetectionRefresher.RefreshNearStation(transform, stationName);
                DebugLog("Refreshed PlayerEnd detection after food transformation");
            }
            else
            {
                DebugLog("Chopping succeeded but failed to place result back on station");
            }

            // Fire completion event
            OnChoppingCompleted?.Invoke(transformedItem);
        }
        else
        {
            DebugLog("Failed to transform food item during chopping");
        }
    }

    /// <summary>
    /// Check if the station is currently processing an item
    /// </summary>
    public bool IsChopping => isCurrentlyChopping;

    /// <summary>
    /// Get the current chopping progress (0-1)
    /// </summary>
    public float GetChoppingProgress() => choppingProgress;

    /// <summary>
    /// Get the player currently chopping on this station
    /// </summary>
    public PlayerEnd GetCurrentChoppingPlayer() => currentChoppingPlayer;

    #endregion

    #region Public Configuration

    /// <summary>
    /// Set the time required for chopping
    /// </summary>
    /// <param name="time">Chopping time in seconds</param>
    public void SetChoppingTime(float time)
    {
        choppingTime = Mathf.Max(0.1f, time);
        DebugLog($"Chopping time set to {choppingTime} seconds");
    }

    /// <summary>
    /// Set whether chopping should start automatically when items are placed
    /// </summary>
    /// <param name="auto">Whether to auto-start chopping</param>
    public void SetAutoStartChopping(bool auto)
    {
        autoStartChopping = auto;
        DebugLog($"Auto-start chopping: {auto}");
    }

    #endregion

    #region Debug and Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw chopping progress indicator
        if (Application.isPlaying && isCurrentlyChopping)
        {
            Gizmos.color = Color.blue;
            Vector3 progressPos = transform.position + Vector3.up * 3f;

            // Draw progress bar
            Vector3 barStart = progressPos - Vector3.right * 0.5f;
            Vector3 barEnd = progressPos + Vector3.right * 0.5f;
            Vector3 progressPoint = Vector3.Lerp(barStart, barEnd, choppingProgress);

            Gizmos.DrawLine(barStart, progressPoint);

            Gizmos.color = Color.gray;
            Gizmos.DrawLine(progressPoint, barEnd);

            // Draw percentage text
#if UNITY_EDITOR
            string progressText = $"Chopping: {choppingProgress * 100f:F0}%";
            UnityEditor.Handles.Label(progressPos + Vector3.up * 0.5f, progressText);
#endif
        }

        // Draw chopping station indicator
        Gizmos.color = Color.blue;
        Vector3 indicatorPos = transform.position + Vector3.up * 2.5f;
        Gizmos.DrawCube(indicatorPos, Vector3.one * 0.3f);
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        // Ensure reasonable values
        choppingTime = Mathf.Max(0.1f, choppingTime);
        choppingProgress = Mathf.Clamp01(choppingProgress);

        // Ensure this is configured as a chopping station
        if (Application.isPlaying)
        {
            stationType = StationType.Chopping;
            allowPlayer1Interaction = true;
            allowPlayer2Interaction = true;
        }
    }

    #endregion
}