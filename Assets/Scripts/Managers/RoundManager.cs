using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class RoundManager : MonoBehaviour
{

    public static RoundManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private float roundTimer = 120f; // 2 minutes
    [SerializeField] private float warmUpTime = 5f; // 5 seconds
    [Tooltip("Minimum cash player needs to have earned to pass")]
    [SerializeField] private float revenueNeeded = 20;

    [Header("Timer Extension Settings")]
    [Tooltip("Enable the timer extension feature")]
    [SerializeField] private bool enableTimerExtension = true;
    [Tooltip("When timer reaches this many seconds, start slowing down time")]
    [SerializeField] private float extensionTriggerTime = 10f;
    [Tooltip("How much to slow down time (0.5 = half speed, 0.3 = very slow)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float timeSlowMultiplier = 0.5f;

    [Header("References")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI quotaText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button startRoundButton;

    [Header("UI Feedback")]
    [SerializeField] private UIFeedbackManager uiFeedbackManager;

    // Round state
    private bool isRoundActive = false;
    private bool isWarmUpActive = false;
    private float currentRevenue = 0;
    private float currentRoundTimeRemaining = 0f;
    private float currentWarmUpTimeRemaining = 0f;
    private Coroutine roundCoroutine;

    // Timer extension state
    private bool isTimerExtensionActive = false;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[RoundManager] Multiple RoundManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        else
            Instance = this;
    }

    private void Start()
    {
        // Hide result text at start
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        // Update initial UI
        UpdateQuotaUI();
        UpdateTimerUI();

        // Set initial music to low intensity (not in round)
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetLowIntensity();
        }
    }

    /// <summary>
    /// Starts a new round. Call this from a UI button.
    /// </summary>
    public void StartRound()
    {
        if (isRoundActive || isWarmUpActive)
        {
            Debug.LogWarning("Round is already active!");
            return;
        }

        if (CustomerManager.Instance == null)
        {
            Debug.LogError("CustomerManager.Instance reference is missing!");
            return;
        }

        // Reset round state
        currentRevenue = 0;
        currentRoundTimeRemaining = roundTimer;
        currentWarmUpTimeRemaining = warmUpTime;
        isTimerExtensionActive = false;

        // Hide result text
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        // Update UI
        UpdateQuotaUI();

        // Start the round coroutine
        if (roundCoroutine != null)
        {
            StopCoroutine(roundCoroutine);
        }
        roundCoroutine = StartCoroutine(RoundCoroutine());

        // Clear all stations
        StationManager.Instance.ClearAllStations();

        // Reset the food
        FoodManager.Instance.ResetAllFood();

        // Hide start round button
        DisplayStartRoundButton(false);

        uiFeedbackManager.PlayButtonFeedback();
    }

    private IEnumerator RoundCoroutine()
    {
        // === WARM UP PHASE ===
        isWarmUpActive = true;

        while (currentWarmUpTimeRemaining > 0f)
        {
            currentWarmUpTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();
            yield return null;
        }

        isWarmUpActive = false;

        // === ROUND ACTIVE PHASE ===
        isRoundActive = true;

        // Switch to high intensity music when round starts
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetHighIntensity();
        }

        // Start customer spawning
        CustomerManager.Instance.StartSpawning();

        while (currentRoundTimeRemaining > 0f)
        {
            // Calculate how much time to subtract this frame
            float timeToSubtract = Time.deltaTime;

            // Check if we should activate timer extension
            if (enableTimerExtension &&
                currentRoundTimeRemaining <= extensionTriggerTime &&
                !isTimerExtensionActive)
            {
                isTimerExtensionActive = true;
                Debug.Log("[RoundManager] Timer extension activated!");

                // Start pitch escalation when timer extension begins
                if (MusicManager.Instance != null)
                {
                    MusicManager.Instance.StartPitchEscalation();
                }
            }

            // Update pitch escalation progress during timer extension
            if (isTimerExtensionActive && MusicManager.Instance != null)
            {
                // Calculate how far through the extension period we are
                float extensionProgress = 1f - (currentRoundTimeRemaining / extensionTriggerTime);
                MusicManager.Instance.UpdatePitchEscalation(extensionProgress);
            }

            // Apply time slow multiplier if extension is active
            if (isTimerExtensionActive)
            {
                timeToSubtract *= timeSlowMultiplier;
            }

            currentRoundTimeRemaining -= timeToSubtract;
            UpdateTimerUI();
            yield return null;
        }

        // === ROUND END ===
        EndRound();
    }

    private void EndRound()
    {
        isRoundActive = false;
        isTimerExtensionActive = false;

        // Switch back to low intensity music when round ends
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetLowIntensity();
            MusicManager.Instance.StopPitchEscalation(); // Reset pitch to normal
        }

        // Stop customer spawning
        CustomerManager.Instance.StopSpawning();

        // Despawn all active customers
        CustomerManager.Instance.DespawnAllCustomers();

        // Clear all stations
        StationManager.Instance.ClearAllStations();

        // Reset the food
        FoodManager.Instance.ResetAllFood();

        // Determine pass or fail
        bool passed = currentRevenue >= revenueNeeded;

        // Show result
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            resultText.text = passed ? "ROUND PASSED!" : "ROUND FAILED!";
            resultText.color = passed ? Color.green : Color.red;
        }

        UpdateUI();

        Debug.Log($"Round ended! cash earned vs breakeven: {currentRevenue}/{revenueNeeded} - {(passed ? "PASSED" : "FAILED")}");

        // Show start round button again
        DisplayStartRoundButton(true);
    }

    private void UpdateUI()
    {
        UpdateTimerUI();
        UpdateQuotaUI();
    }

    // Called as a listener to the CustomerManager.OnCustomerServedSuccessfully event
    public void OnCustomerServed(float foodValue)
    {
        if (isRoundActive)
        {
            currentRevenue += foodValue;
            UpdateQuotaUI();
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        if (isWarmUpActive)
        {
            // Show warm up countdown
            int seconds = Mathf.CeilToInt(currentWarmUpTimeRemaining);
            timerText.text = $"Get Ready: {seconds}";
        }
        else if (isRoundActive)
        {
            // Show round timer
            int minutes = Mathf.FloorToInt(currentRoundTimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(currentRoundTimeRemaining % 60f);

            // Add visual indicator when timer extension is active
            string timerDisplay = $"Time: {minutes:00}:{seconds:00}";

            timerText.text = timerDisplay;
        }
        else
        {
            timerText.text = "Press Start";
        }
    }

    private void UpdateQuotaUI()
    {
        if (quotaText == null) return;

        quotaText.text = $"Cash Earned: ${currentRevenue}/${revenueNeeded}";
    }

    private void DisplayStartRoundButton(bool show)
    {
        if (startRoundButton != null)
        {
            startRoundButton.gameObject.SetActive(show);
        }
    }
}