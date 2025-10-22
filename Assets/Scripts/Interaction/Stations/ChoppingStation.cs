using UnityEngine;
using System.Collections;

/// <summary>
/// Chopping station that allows Player 2 to chop ingredients.
/// Extends BaseStation to provide chopping-specific functionality.
/// </summary>
public class ChoppingStation : BaseStation
{
    [Header("Chopping Station Settings")]
    [SerializeField] private float choppingTime = 2f;
    [Tooltip("Time in seconds to chop an ingredient")]

    [SerializeField] private int chopsRequired = 3;
    [Tooltip("Number of chop actions required to complete chopping")]

    [SerializeField] private float choppingSoundDelay = 0.5f;
    [Tooltip("Delay between chopping sound effects")]

    [Header("Visual Feedback")]
    [SerializeField] private Transform choppingIndicator;
    [Tooltip("Visual indicator that shows when chopping is in progress")]

    [SerializeField] private ParticleSystem choppingParticles;
    [Tooltip("Particle effect for chopping action")]

    [SerializeField] private Animator stationAnimator;
    [Tooltip("Animator for chopping animations")]

    [Header("Audio")]
    [SerializeField] private AudioClip choppingSound;
    [SerializeField] private AudioClip choppingCompleteSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Debug")]
    [SerializeField] private bool enableChoppingDebugLogs = false;

    // Internal chopping state
    private bool isChopping = false;
    private float currentChoppingProgress = 0f;
    private int currentChops = 0;
    private Ingredient currentIngredient;
    private PlayerEnd currentChoppingPlayer;
    private Coroutine choppingCoroutine;

    // Public properties
    public bool IsChopping => isChopping;
    public float ChoppingProgress => currentChoppingProgress;
    public bool CanStartChopping => IsOccupied && !isChopping && CanChopCurrentItem();

    protected override void Initialize()
    {
        // Set up as chopping station
        stationType = StationType.Chopping;
        stationName = string.IsNullOrEmpty(stationName) || stationName == "Station" ? "Chopping Board" : stationName;

        // Only Player 2 can use chopping stations
        allowPlayer1Interaction = false;
        allowPlayer2Interaction = true;

        // Only accept ingredients
        acceptAllItemTypes = false;
        acceptedItemTypes = new ItemType[] { ItemType.Ingredient };

        base.Initialize();

        // Set up audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Hide chopping indicator initially
        if (choppingIndicator != null)
        {
            choppingIndicator.gameObject.SetActive(false);
        }

        ChoppingDebugLog("Chopping station initialized for Player 2 only");
    }

    #region Item Acceptance Override

    protected override bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        // Must be Player 2
        if (playerEnd.PlayerNumber != 2)
        {
            ChoppingDebugLog($"Player {playerEnd.PlayerNumber} cannot use chopping station - only Player 2 allowed");
            return false;
        }

        // Must be an ingredient
        Ingredient ingredient = item.GetComponent<Ingredient>();
        if (ingredient == null)
        {
            ChoppingDebugLog($"Item {item.name} is not an ingredient");
            return false;
        }

        // Ingredient must be choppable
        if (!ingredient.CanBeChopped)
        {
            ChoppingDebugLog($"Ingredient {ingredient.IngredientName} cannot be chopped");
            return false;
        }

        return true;
    }

    protected override void OnItemPlacedInternal(GameObject item, PlayerEnd playerEnd)
    {
        currentIngredient = item.GetComponent<Ingredient>();
        ChoppingDebugLog($"Ingredient {currentIngredient.IngredientName} placed on chopping station");
    }

    protected override void OnItemRemovedInternal(GameObject item, PlayerEnd playerEnd)
    {
        if (isChopping)
        {
            StopChopping();
        }

        currentIngredient = null;
        ChoppingDebugLog($"Ingredient {item.name} removed from chopping station");
    }

    #endregion

    #region Chopping Logic

    /// <summary>
    /// Check if the current item can be chopped
    /// </summary>
    private bool CanChopCurrentItem()
    {
        if (currentIngredient == null) return false;
        return currentIngredient.CanBeChopped;
    }

    /// <summary>
    /// Start chopping the current ingredient
    /// </summary>
    public bool StartChopping(PlayerEnd playerEnd)
    {
        if (!CanStartChopping)
        {
            ChoppingDebugLog($"Cannot start chopping - CanStartChopping: {CanStartChopping}");
            return false;
        }

        if (playerEnd.PlayerNumber != 2)
        {
            ChoppingDebugLog($"Player {playerEnd.PlayerNumber} cannot chop - only Player 2 allowed");
            return false;
        }

        isChopping = true;
        currentChoppingProgress = 0f;
        currentChops = 0;
        currentChoppingPlayer = playerEnd;

        // Start chopping coroutine
        choppingCoroutine = StartCoroutine(ChoppingProcess());

        // Show visual feedback
        if (choppingIndicator != null)
        {
            choppingIndicator.gameObject.SetActive(true);
        }

        // Start particles
        if (choppingParticles != null)
        {
            choppingParticles.Play();
        }

        // Trigger animation
        if (stationAnimator != null)
        {
            stationAnimator.SetBool("IsChopping", true);
        }

        ChoppingDebugLog($"Player {playerEnd.PlayerNumber} started chopping {currentIngredient.ItemName}");
        return true;
    }

    /// <summary>
    /// Stop chopping the current ingredient
    /// </summary>
    public void StopChopping()
    {
        if (!isChopping) return;

        isChopping = false;

        // Stop chopping coroutine
        if (choppingCoroutine != null)
        {
            StopCoroutine(choppingCoroutine);
            choppingCoroutine = null;
        }

        // Hide visual feedback
        if (choppingIndicator != null)
        {
            choppingIndicator.gameObject.SetActive(false);
        }

        // Stop particles
        if (choppingParticles != null)
        {
            choppingParticles.Stop();
        }

        // Stop animation
        if (stationAnimator != null)
        {
            stationAnimator.SetBool("IsChopping", false);
        }

        ChoppingDebugLog($"Stopped chopping {(currentIngredient != null ? currentIngredient.ItemName : "ingredient")}");
    }

    /// <summary>
    /// Coroutine that handles the chopping process
    /// </summary>
    private IEnumerator ChoppingProcess()
    {
        float chopsInterval = choppingTime / chopsRequired;

        while (currentChops < chopsRequired && isChopping && currentIngredient != null)
        {
            // Wait for next chop interval
            yield return new WaitForSeconds(chopsInterval);

            if (!isChopping) break;

            // Perform a chop
            PerformChop();
            currentChops++;
            currentChoppingProgress = (float)currentChops / chopsRequired;

            ChoppingDebugLog($"Chop {currentChops}/{chopsRequired} completed. Progress: {currentChoppingProgress:P0}");
        }

        // Chopping completed
        if (currentChops >= chopsRequired && currentIngredient != null)
        {
            CompleteChopping();
        }
    }

    /// <summary>
    /// Perform a single chop action (visual and audio feedback)
    /// </summary>
    private void PerformChop()
    {
        // Play chopping sound
        if (choppingSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(choppingSound);
        }

        // Trigger chop animation/effect
        if (stationAnimator != null)
        {
            stationAnimator.SetTrigger("Chop");
        }

        // Create additional particle burst
        if (choppingParticles != null)
        {
            choppingParticles.Emit(10);
        }

        // Screen shake or other feedback could go here
    }

    /// <summary>
    /// Complete the chopping process
    /// </summary>
    private void CompleteChopping()
    {
        if (currentIngredient == null) return;

        // Start the chopping transformation process
        bool choppedSuccessfully = currentIngredient.StartProcessing(TransformationType.Chopping);

        if (choppedSuccessfully)
        {
            // Play completion sound
            if (choppingCompleteSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(choppingCompleteSound);
            }

            ChoppingDebugLog($"Successfully started chopping transformation for {currentIngredient.IngredientName}");
        }
        else
        {
            ChoppingDebugLog($"Failed to start chopping transformation for {currentIngredient.IngredientName}");
        }

        // Stop chopping process
        StopChopping();
    }

    #endregion

    #region Public Interface for Interaction System

    /// <summary>
    /// Handle interaction when Player 2 presses interact button
    /// </summary>
    public bool HandleInteraction(PlayerEnd playerEnd)
    {
        if (playerEnd.PlayerNumber != 2)
        {
            ChoppingDebugLog($"Player {playerEnd.PlayerNumber} cannot interact with chopping station");
            return false;
        }

        // If not chopping and can chop, start chopping
        if (!isChopping && CanStartChopping)
        {
            return StartChopping(playerEnd);
        }

        return false;
    }

    /// <summary>
    /// Handle when Player 2 stops holding interact button
    /// </summary>
    public void HandleInteractionStop(PlayerEnd playerEnd)
    {
        if (playerEnd.PlayerNumber != 2) return;

        if (isChopping && currentChoppingPlayer == playerEnd)
        {
            StopChopping();
        }
    }

    #endregion

    #region Debug and Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw chopping progress indicator
        if (isChopping && Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Vector3 progressPos = transform.position + Vector3.up * 4f;
            float progressWidth = currentChoppingProgress * 2f;
            Gizmos.DrawCube(progressPos, new Vector3(progressWidth, 0.1f, 0.1f));

            // Draw chop count
            Gizmos.color = Color.blue;
            for (int i = 0; i < currentChops; i++)
            {
                Vector3 chopPos = progressPos + Vector3.right * (i * 0.3f - 0.45f) + Vector3.up * 0.3f;
                Gizmos.DrawCube(chopPos, Vector3.one * 0.1f);
            }
        }

        // Draw player restriction indicator
        Gizmos.color = Color.blue;
        Vector3 restrictionPos = transform.position + Vector3.up * 2.5f;
        Gizmos.DrawCube(restrictionPos, Vector3.one * 0.15f);

#if UNITY_EDITOR
        // Show chopping info in scene view
        if (Application.isPlaying)
        {
            string info = $"{stationName}\nPlayer 2 Only";
            if (isChopping)
                info += $"\nChopping: {currentChops}/{chopsRequired}";
            if (currentIngredient != null)
                info += $"\n{currentIngredient.IngredientName}";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4.5f, info);
        }
#endif
    }

    protected override Color GetStationColor()
    {
        return Color.blue; // Blue for Player 2 stations
    }

    private void ChoppingDebugLog(string message)
    {
        if (enableChoppingDebugLogs)
            Debug.Log($"[ChoppingStation] {message}");
    }

    #endregion

    #region Validation

    protected override void OnValidate()
    {
        base.OnValidate();

        // Ensure reasonable chopping values
        choppingTime = Mathf.Max(0.5f, choppingTime);
        chopsRequired = Mathf.Max(1, chopsRequired);
        choppingSoundDelay = Mathf.Max(0.1f, choppingSoundDelay);

        // Ensure Player 2 only
        allowPlayer1Interaction = false;
        allowPlayer2Interaction = true;

        // Ensure chopping station type
        stationType = StationType.Chopping;
    }

    #endregion
}