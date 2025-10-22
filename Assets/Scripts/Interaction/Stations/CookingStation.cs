using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Cooking station (stove) that allows Player 1 to cook ingredients in pans or pots.
/// Extends BaseStation to provide cooking-specific functionality.
/// </summary>
public class CookingStation : BaseStation
{
    [Header("Cooking Station Settings")]
    [SerializeField] private CookingMethod cookingMethod = CookingMethod.Frying;
    [Tooltip("What type of cooking this station does")]

    [SerializeField] private float cookingUpdateInterval = 0.1f;
    [Tooltip("How often to update cooking progress (seconds)")]

    [Header("Visual Feedback")]
    [SerializeField] private Transform cookingIndicator;
    [Tooltip("Visual indicator for when cooking is active")]

    [SerializeField] private ParticleSystem cookingParticles;
    [Tooltip("Steam/smoke particles for cooking")]

    [SerializeField] private ParticleSystem burningParticles;
    [Tooltip("Burning particles for overcooked food")]

    [SerializeField] private Light cookingLight;
    [Tooltip("Light that indicates cooking heat")]

    [SerializeField] private Animator stationAnimator;
    [Tooltip("Animator for cooking animations")]

    [Header("Audio")]
    [SerializeField] private AudioClip cookingStartSound;
    [SerializeField] private AudioClip cookingLoopSound;
    [SerializeField] private AudioClip cookingCompleteSound;
    [SerializeField] private AudioClip burningSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Cooking Vessel")]
    [SerializeField] private GameObject cookingVessel;
    [Tooltip("The pan or pot that sits on this stove")]

    [Header("Debug")]
    [SerializeField] private bool enableCookingDebugLogs = false;

    // Internal cooking state
    private bool isCooking = false;
    private List<CookingIngredient> ingredientsBeingCooked = new List<CookingIngredient>();
    private PlayerEnd currentCookingPlayer;
    private Coroutine cookingCoroutine;
    private bool wasPlayingCookingLoop = false;

    // Public properties
    public bool IsCooking => isCooking;
    public CookingMethod Method => cookingMethod;
    public bool CanStartCooking => HasCookableIngredients() && !isCooking;
    public int CookableIngredientCount => CountCookableIngredients();

    protected override void Initialize()
    {
        // Set up as cooking station
        stationType = StationType.Cooking;
        stationName = string.IsNullOrEmpty(stationName) || stationName == "Station" ? GetCookingStationName() : stationName;

        // Only Player 1 can use cooking stations
        allowPlayer1Interaction = true;
        allowPlayer2Interaction = false;

        // Accept ingredients and dishes with ingredients
        acceptAllItemTypes = false;
        acceptedItemTypes = new ItemType[] { ItemType.Ingredient, ItemType.Dish };

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

        // Hide cooking indicator initially
        if (cookingIndicator != null)
        {
            cookingIndicator.gameObject.SetActive(false);
        }

        // Set up cooking light
        if (cookingLight != null)
        {
            cookingLight.enabled = false;
        }

        CookingDebugLog($"Cooking station ({cookingMethod}) initialized for Player 1 only");
    }

    #region Item Acceptance Override

    protected override bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        // Must be Player 1
        if (playerEnd.PlayerNumber != 1)
        {
            CookingDebugLog($"Player {playerEnd.PlayerNumber} cannot use cooking station - only Player 1 allowed");
            return false;
        }

        // Check if it's a cooking ingredient
        CookingIngredient ingredient = item.GetComponent<CookingIngredient>();
        if (ingredient != null)
        {
            if (!ingredient.CanBeCooked)
            {
                CookingDebugLog($"Ingredient {ingredient.ItemName} cannot be cooked - current state: {ingredient.State}");
                return false;
            }
            return true;
        }

        // Check if it's a dish with cookable ingredients
        Dish dish = item.GetComponent<Dish>();
        if (dish != null)
        {
            if (dish.IsEmpty)
            {
                CookingDebugLog($"Dish is empty, cannot cook");
                return false;
            }

            // Check if any ingredients in the dish can be cooked
            bool hasCookableIngredients = false;
            foreach (CookingIngredient dishIngredient in dish.Ingredients)
            {
                if (dishIngredient.CanBeCooked)
                {
                    hasCookableIngredients = true;
                    break;
                }
            }

            if (!hasCookableIngredients)
            {
                CookingDebugLog($"Dish has no cookable ingredients");
                return false;
            }

            return true;
        }

        CookingDebugLog($"Item {item.name} is not cookable");
        return false;
    }

    protected override void OnItemPlacedInternal(GameObject item, PlayerEnd playerEnd)
    {
        // Collect all cookable ingredients from the placed item
        CollectCookableIngredients(item);
        CookingDebugLog($"Item {item.name} placed on cooking station. Cookable ingredients: {ingredientsBeingCooked.Count}");
    }

    protected override void OnItemRemovedInternal(GameObject item, PlayerEnd playerEnd)
    {
        if (isCooking)
        {
            StopCooking();
        }

        ingredientsBeingCooked.Clear();
        CookingDebugLog($"Item {item.name} removed from cooking station");
    }

    #endregion

    #region Cooking Logic

    /// <summary>
    /// Collect all cookable ingredients from the placed item
    /// </summary>
    private void CollectCookableIngredients(GameObject item)
    {
        ingredientsBeingCooked.Clear();

        // If it's a single ingredient
        CookingIngredient singleIngredient = item.GetComponent<CookingIngredient>();
        if (singleIngredient != null && singleIngredient.CanBeCooked)
        {
            ingredientsBeingCooked.Add(singleIngredient);
            return;
        }

        // If it's a dish with ingredients
        Dish dish = item.GetComponent<Dish>();
        if (dish != null)
        {
            foreach (CookingIngredient ingredient in dish.Ingredients)
            {
                if (ingredient.CanBeCooked)
                {
                    ingredientsBeingCooked.Add(ingredient);
                }
            }
        }
    }

    /// <summary>
    /// Check if there are any cookable ingredients
    /// </summary>
    private bool HasCookableIngredients()
    {
        return ingredientsBeingCooked.Count > 0;
    }

    /// <summary>
    /// Count cookable ingredients that aren't already cooked
    /// </summary>
    private int CountCookableIngredients()
    {
        int count = 0;
        foreach (CookingIngredient ingredient in ingredientsBeingCooked)
        {
            if (ingredient.CanBeCooked)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Start cooking the ingredients
    /// </summary>
    public bool StartCooking(PlayerEnd playerEnd)
    {
        if (!CanStartCooking)
        {
            CookingDebugLog($"Cannot start cooking - CanStartCooking: {CanStartCooking}");
            return false;
        }

        if (playerEnd.PlayerNumber != 1)
        {
            CookingDebugLog($"Player {playerEnd.PlayerNumber} cannot cook - only Player 1 allowed");
            return false;
        }

        isCooking = true;
        currentCookingPlayer = playerEnd;

        // Start cooking for all ingredients
        foreach (CookingIngredient ingredient in ingredientsBeingCooked)
        {
            if (ingredient.CanBeCooked)
            {
                ingredient.StartCooking();
            }
        }

        // Start cooking update coroutine
        cookingCoroutine = StartCoroutine(CookingUpdateProcess());

        // Visual and audio feedback
        StartCookingEffects();

        CookingDebugLog($"Player {playerEnd.PlayerNumber} started cooking {ingredientsBeingCooked.Count} ingredients");
        return true;
    }

    /// <summary>
    /// Stop cooking the ingredients
    /// </summary>
    public void StopCooking()
    {
        if (!isCooking) return;

        isCooking = false;

        // Stop cooking for all ingredients
        foreach (CookingIngredient ingredient in ingredientsBeingCooked)
        {
            ingredient.StopCooking();
        }

        // Stop cooking coroutine
        if (cookingCoroutine != null)
        {
            StopCoroutine(cookingCoroutine);
            cookingCoroutine = null;
        }

        // Stop visual and audio effects
        StopCookingEffects();

        CookingDebugLog($"Stopped cooking");
    }

    /// <summary>
    /// Coroutine that updates cooking progress for all ingredients
    /// </summary>
    private IEnumerator CookingUpdateProcess()
    {
        while (isCooking)
        {
            yield return new WaitForSeconds(cookingUpdateInterval);

            if (!isCooking) break;

            bool anyStillCooking = false;
            bool anyBurning = false;

            // Update each ingredient's cooking progress
            foreach (CookingIngredient ingredient in ingredientsBeingCooked)
            {
                if (ingredient.IsBeingCooked)
                {
                    ingredient.UpdateCooking(cookingUpdateInterval);
                    anyStillCooking = true;

                    // Check if ingredient is burning
                    if (ingredient.IsSpoiled)
                    {
                        anyBurning = true;
                    }
                }
            }

            // Update visual effects based on cooking state
            UpdateCookingEffects(anyStillCooking, anyBurning);

            // If no ingredients are still cooking, stop
            if (!anyStillCooking)
            {
                CookingDebugLog("All ingredients finished cooking");
                StopCooking();
            }
        }
    }

    #endregion

    #region Visual and Audio Effects

    /// <summary>
    /// Start cooking visual and audio effects
    /// </summary>
    private void StartCookingEffects()
    {
        // Show cooking indicator
        if (cookingIndicator != null)
        {
            cookingIndicator.gameObject.SetActive(true);
        }

        // Start cooking particles
        if (cookingParticles != null)
        {
            cookingParticles.Play();
        }

        // Enable cooking light
        if (cookingLight != null)
        {
            cookingLight.enabled = true;
        }

        // Start cooking animation
        if (stationAnimator != null)
        {
            stationAnimator.SetBool("IsCooking", true);
        }

        // Play start sound
        if (cookingStartSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(cookingStartSound);
        }

        // Start cooking loop sound
        if (cookingLoopSound != null && audioSource != null)
        {
            audioSource.clip = cookingLoopSound;
            audioSource.loop = true;
            audioSource.Play();
            wasPlayingCookingLoop = true;
        }
    }

    /// <summary>
    /// Update cooking effects based on current state
    /// </summary>
    private void UpdateCookingEffects(bool stillCooking, bool burning)
    {
        // Handle burning effects
        if (burning)
        {
            if (burningParticles != null && !burningParticles.isPlaying)
            {
                burningParticles.Play();
            }

            // Play burning sound
            if (burningSound != null && audioSource != null && !audioSource.isPlaying)
            {
                audioSource.Stop();
                audioSource.clip = burningSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            // Change cooking light to red/orange
            if (cookingLight != null)
            {
                cookingLight.color = Color.red;
            }
        }
    }

    /// <summary>
    /// Stop cooking visual and audio effects
    /// </summary>
    private void StopCookingEffects()
    {
        // Hide cooking indicator
        if (cookingIndicator != null)
        {
            cookingIndicator.gameObject.SetActive(false);
        }

        // Stop particles
        if (cookingParticles != null)
        {
            cookingParticles.Stop();
        }

        if (burningParticles != null)
        {
            burningParticles.Stop();
        }

        // Disable cooking light
        if (cookingLight != null)
        {
            cookingLight.enabled = false;
            cookingLight.color = Color.white; // Reset color
        }

        // Stop cooking animation
        if (stationAnimator != null)
        {
            stationAnimator.SetBool("IsCooking", false);
        }

        // Stop cooking loop sound
        if (wasPlayingCookingLoop && audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            wasPlayingCookingLoop = false;
        }

        // Play cooking complete sound
        if (cookingCompleteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(cookingCompleteSound);
        }
    }

    #endregion

    #region Public Interface for Interaction System

    /// <summary>
    /// Handle interaction when Player 1 presses interact button
    /// </summary>
    public bool HandleInteraction(PlayerEnd playerEnd)
    {
        if (playerEnd.PlayerNumber != 1)
        {
            CookingDebugLog($"Player {playerEnd.PlayerNumber} cannot interact with cooking station");
            return false;
        }

        // If not cooking and can cook, start cooking
        if (!isCooking && CanStartCooking)
        {
            return StartCooking(playerEnd);
        }

        return false;
    }

    /// <summary>
    /// Handle when Player 1 stops holding interact button
    /// </summary>
    public void HandleInteractionStop(PlayerEnd playerEnd)
    {
        if (playerEnd.PlayerNumber != 1) return;

        if (isCooking && currentCookingPlayer == playerEnd)
        {
            StopCooking();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get the appropriate name for this cooking station type
    /// </summary>
    private string GetCookingStationName()
    {
        switch (cookingMethod)
        {
            case CookingMethod.Frying: return "Frying Pan";
            case CookingMethod.Boiling: return "Cooking Pot";
            default: return "Stove";
        }
    }

    /// <summary>
    /// Get cooking progress for the most advanced ingredient
    /// </summary>
    public float GetMaxCookingProgress()
    {
        float maxProgress = 0f;
        foreach (CookingIngredient ingredient in ingredientsBeingCooked)
        {
            if (ingredient.IsBeingCooked)
            {
                maxProgress = Mathf.Max(maxProgress, ingredient.CookingProgress);
            }
        }
        return maxProgress;
    }

    #endregion

    #region Debug and Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw cooking method indicator
        Gizmos.color = GetCookingMethodColor();
        Vector3 methodPos = transform.position + Vector3.up * 2.5f;
        Gizmos.DrawCube(methodPos, Vector3.one * 0.15f);

        // Draw cooking progress
        if (isCooking && Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Vector3 progressPos = transform.position + Vector3.up * 4f;
            float maxProgress = GetMaxCookingProgress();
            float progressWidth = maxProgress * 2f;
            Gizmos.DrawCube(progressPos, new Vector3(progressWidth, 0.1f, 0.1f));
        }

        // Draw player restriction indicator
        Gizmos.color = Color.red;
        Vector3 restrictionPos = transform.position + Vector3.up * 3f;
        Gizmos.DrawCube(restrictionPos, Vector3.one * 0.15f);

#if UNITY_EDITOR
        // Show cooking info in scene view
        if (Application.isPlaying)
        {
            string info = $"{stationName}\nPlayer 1 Only\n{cookingMethod}";
            if (isCooking)
                info += $"\nCooking: {GetMaxCookingProgress():P0}";
            if (ingredientsBeingCooked.Count > 0)
                info += $"\n{ingredientsBeingCooked.Count} ingredients";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4.5f, info);
        }
#endif
    }

    protected override Color GetStationColor()
    {
        return Color.red; // Red for Player 1 stations
    }

    private Color GetCookingMethodColor()
    {
        switch (cookingMethod)
        {
            case CookingMethod.Frying: return Color.yellow;
            case CookingMethod.Boiling: return Color.blue;
            default: return new Color(1f, 0.5f, 0f); //orange
        }
    }

    private void CookingDebugLog(string message)
    {
        if (enableCookingDebugLogs)
            Debug.Log($"[CookingStation] {message}");
    }

    #endregion

    #region Validation

    protected override void OnValidate()
    {
        base.OnValidate();

        // Ensure reasonable cooking values
        cookingUpdateInterval = Mathf.Max(0.05f, cookingUpdateInterval);

        // Ensure Player 1 only
        allowPlayer1Interaction = true;
        allowPlayer2Interaction = false;

        // Ensure cooking station type
        stationType = StationType.Cooking;
    }

    #endregion
}

/// <summary>
/// Different cooking methods available
/// </summary>
public enum CookingMethod
{
    Frying,     // Using a frying pan
    Boiling     // Using a pot with water
}