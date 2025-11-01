using System.Collections;
using UnityEngine;
using TMPro;

public class RoundManager : MonoBehaviour
{
    [Header("Round Settings")]
    [SerializeField] private float roundTimer = 120f; // 2 minutes
    [SerializeField] private float warmUpTime = 5f; // 5 seconds
    [SerializeField] private int customerServeQuota = 2; // Minimum customers to serve

    [Header("References")]
    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI quotaText;
    [SerializeField] private TextMeshProUGUI resultText;

    // Round state
    private bool isRoundActive = false;
    private bool isWarmUpActive = false;
    private int customersServedThisRound = 0;
    private float currentRoundTimeRemaining = 0f;
    private float currentWarmUpTimeRemaining = 0f;
    private Coroutine roundCoroutine;

    private void OnEnable()
    {
        if (customerManager != null)
        {
            customerManager.OnCustomerServedSuccessfully += OnCustomerServed;
        }
    }

    private void OnDisable()
    {
        if (customerManager != null)
        {
            customerManager.OnCustomerServedSuccessfully -= OnCustomerServed;
        }
    }

    private void Start()
    {
        // Hide result text at start
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
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

        if (customerManager == null)
        {
            Debug.LogError("CustomerManager reference is missing!");
            return;
        }

        // Reset round state
        customersServedThisRound = 0;
        currentRoundTimeRemaining = roundTimer;
        currentWarmUpTimeRemaining = warmUpTime;

        // Hide result text
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
        }

        // Update UI
        UpdateQuotaUI();

        // Start the round coroutine
        if (roundCoroutine != null)
        {
            StopCoroutine(roundCoroutine);
        }
        roundCoroutine = StartCoroutine(RoundCoroutine());
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
        customerManager.StartSpawning();

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
        customerManager.StopSpawning();

        // Despawn all active customers
        customerManager.DespawnAllCustomers();

        // Determine pass or fail
        bool passed = customersServedThisRound >= customerServeQuota;

        // Show result
        if (resultText != null)
        {
            resultText.gameObject.SetActive(true);
            resultText.text = passed ? "ROUND PASSED!" : "ROUND FAILED!";
            resultText.color = passed ? Color.green : Color.red;
        }

        Debug.Log($"Round ended! Customers served: {customersServedThisRound}/{customerServeQuota} - {(passed ? "PASSED" : "FAILED")}");
    }

    private void OnCustomerServed()
    {
        if (isRoundActive)
        {
            customersServedThisRound++;
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

        quotaText.text = $"Served: {customersServedThisRound}/{customerServeQuota}";
    }
}
