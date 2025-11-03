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

    [Header("References")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI quotaText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button startRoundButton;

    // Round state
    private bool isRoundActive = false;
    private bool isWarmUpActive = false;
    private float currentRevenue = 0;
    private float currentRoundTimeRemaining = 0f;
    private float currentWarmUpTimeRemaining = 0f;
    private Coroutine roundCoroutine;

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

        // Start customer spawning
        CustomerManager.Instance.StartSpawning();

        while (currentRoundTimeRemaining > 0f)
        {
            currentRoundTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();
            yield return null;
        }

        // === ROUND END ===
        EndRound();
    }

    private void EndRound()
    {
        isRoundActive = false;

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
            timerText.text = $"Time: {minutes:00}:{seconds:00}";
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
