using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// World space UI progress bar for processing stations (Chopping and Cooking).
/// Automatically shows/hides and updates based on station processing events.
/// Attach this to a station GameObject and assign the UI elements.
/// </summary>
public class ProcessingStationProgressBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject progressBarContainer;
    [Tooltip("The parent GameObject containing the progress bar UI (will be shown/hidden)")]

    [SerializeField] private Image fillImage;
    [Tooltip("The Image component that will be filled to show progress (0-1)")]

    [SerializeField] private TextMeshProUGUI percentageText;
    [Tooltip("Optional: Text component to display percentage (e.g., '50%')")]

    [Header("Display Settings")]
    [SerializeField] private bool showPercentage = true;
    [Tooltip("Whether to display the percentage text")]

    [SerializeField] private bool hideOnComplete = true;
    [Tooltip("Whether to hide the progress bar when processing completes")]

    [SerializeField] private float hideDelay = 0.5f;
    [Tooltip("Delay before hiding the bar after completion (gives time to see 100%)")]

    [Header("Visual Settings")]
    [SerializeField] private Color choppingColor = Color.blue;
    [Tooltip("Color of the progress bar for chopping stations")]

    [SerializeField] private Color cookingColor = Color.red;
    [Tooltip("Color of the progress bar for cooking stations")]

    [SerializeField] private bool updateColorBasedOnStation = true;
    [Tooltip("Whether to automatically set the fill color based on station type")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Station references (automatically detected)
    private ChoppingStation choppingStation;
    private CookingStation cookingStation;
    private bool isChoppingStation;

    // Internal state
    private bool isProcessing = false;
    private float hideTimer = 0f;

    #region Unity Lifecycle

    private void Awake()
    {
        // Detect which type of station this is
        choppingStation = GetComponentInParent<ChoppingStation>();
        cookingStation = GetComponentInParent<CookingStation>();

        isChoppingStation = choppingStation != null;

        // Validate setup
        if (choppingStation == null && cookingStation == null)
        {
            Debug.LogError($"[ProcessingStationProgressBar] No ChoppingStation or CookingStation found on {gameObject.name}! This component requires one of these station types.");
            enabled = false;
            return;
        }

        if (progressBarContainer == null)
        {
            Debug.LogError($"[ProcessingStationProgressBar] No progress bar container assigned on {gameObject.name}!");
            enabled = false;
            return;
        }

        if (fillImage == null)
        {
            Debug.LogError($"[ProcessingStationProgressBar] No fill image assigned on {gameObject.name}!");
            enabled = false;
            return;
        }

        // Initially hide the progress bar
        HideProgressBar();

        DebugLog($"Initialized for {(isChoppingStation ? "Chopping" : "Cooking")} station");
    }

    private void Start()
    {
        // Subscribe to station events
        SubscribeToEvents();

        // Set the color based on station type
        if (updateColorBasedOnStation && fillImage != null)
        {
            fillImage.color = isChoppingStation ? choppingColor : cookingColor;
        }
    }

    private void Update()
    {
        if (isProcessing)
        {
            UpdateProgressBar();
        }
        else if (hideTimer > 0f)
        {
            // Count down hide delay
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f)
            {
                HideProgressBar();
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        UnsubscribeFromEvents();
    }

    #endregion

    #region Event Subscription

    private void SubscribeToEvents()
    {
        if (isChoppingStation && choppingStation != null)
        {
            choppingStation.OnChoppingStarted += OnProcessingStarted;
            choppingStation.OnChoppingCompleted += OnProcessingCompleted;
            choppingStation.OnChoppingStopped += OnProcessingStopped;
            DebugLog("Subscribed to chopping station events");
        }
        else if (!isChoppingStation && cookingStation != null)
        {
            cookingStation.OnCookingStarted += OnProcessingStarted;
            cookingStation.OnCookingCompleted += OnProcessingCompleted;
            cookingStation.OnCookingStopped += OnProcessingStopped;
            DebugLog("Subscribed to cooking station events");
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (isChoppingStation && choppingStation != null)
        {
            choppingStation.OnChoppingStarted -= OnProcessingStarted;
            choppingStation.OnChoppingCompleted -= OnProcessingCompleted;
            choppingStation.OnChoppingStopped -= OnProcessingStopped;
        }
        else if (!isChoppingStation && cookingStation != null)
        {
            cookingStation.OnCookingStarted -= OnProcessingStarted;
            cookingStation.OnCookingCompleted -= OnProcessingCompleted;
            cookingStation.OnCookingStopped -= OnProcessingStopped;
        }
    }

    #endregion

    #region Event Handlers

    private void OnProcessingStarted(FoodItem foodItem)
    {
        DebugLog($"Processing started for {foodItem.FoodData.DisplayName}");
        isProcessing = true;
        hideTimer = 0f;
        ShowProgressBar();
    }

    private void OnProcessingCompleted(FoodItem foodItem)
    {
        DebugLog($"Processing completed for {foodItem.FoodData.DisplayName}");
        isProcessing = false;

        // Update to 100% one last time
        UpdateProgressDisplay(1f);

        // Start hide delay timer if enabled
        if (hideOnComplete)
        {
            hideTimer = hideDelay;
        }
    }

    private void OnProcessingStopped(FoodItem foodItem)
    {
        DebugLog($"Processing stopped for {foodItem.FoodData.DisplayName}");
        isProcessing = false;

        // Hide immediately when stopped (cancelled)
        HideProgressBar();
    }

    #endregion

    #region Progress Bar Control

    private void ShowProgressBar()
    {
        if (progressBarContainer != null)
        {
            progressBarContainer.SetActive(true);
            DebugLog("Progress bar shown");
        }
    }

    private void HideProgressBar()
    {
        if (progressBarContainer != null)
        {
            progressBarContainer.SetActive(false);
            DebugLog("Progress bar hidden");
        }
    }

    private void UpdateProgressBar()
    {
        // Get current progress from the station
        float progress = GetCurrentProgress();

        // Update the display
        UpdateProgressDisplay(progress);
    }

    private void UpdateProgressDisplay(float progress)
    {
        // Clamp progress to 0-1
        progress = Mathf.Clamp01(progress);

        // Update fill image
        if (fillImage != null)
        {
            fillImage.fillAmount = progress;
        }

        // Update percentage text if enabled
        if (showPercentage && percentageText != null)
        {
            int percentage = Mathf.RoundToInt(progress * 100f);
            percentageText.text = $"{percentage}%";
        }
    }

    private float GetCurrentProgress()
    {
        if (isChoppingStation && choppingStation != null)
        {
            return choppingStation.GetChoppingProgress();
        }
        else if (!isChoppingStation && cookingStation != null)
        {
            return cookingStation.GetCookingProgress();
        }

        return 0f;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Manually show the progress bar (useful for testing)
    /// </summary>
    public void Show()
    {
        ShowProgressBar();
    }

    /// <summary>
    /// Manually hide the progress bar (useful for testing)
    /// </summary>
    public void Hide()
    {
        HideProgressBar();
    }

    /// <summary>
    /// Set the progress bar color (overrides automatic coloring)
    /// </summary>
    public void SetColor(Color color)
    {
        if (fillImage != null)
        {
            fillImage.color = color;
        }
    }

    /// <summary>
    /// Check if the progress bar is currently visible
    /// </summary>
    public bool IsVisible()
    {
        return progressBarContainer != null && progressBarContainer.activeSelf;
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ProcessingStationProgressBar - {gameObject.name}] {message}");
        }
    }

    private void OnValidate()
    {
        // Ensure reasonable values
        hideDelay = Mathf.Max(0f, hideDelay);

        // Validate that we have required components
        if (progressBarContainer == null)
        {
            Debug.LogWarning($"[{gameObject.name}] ProcessingStationProgressBar: No progress bar container assigned!");
        }

        if (fillImage == null)
        {
            Debug.LogWarning($"[{gameObject.name}] ProcessingStationProgressBar: No fill image assigned!");
        }
    }

    #endregion
}
