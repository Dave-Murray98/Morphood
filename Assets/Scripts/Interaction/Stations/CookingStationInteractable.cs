using UnityEngine;

/// <summary>
/// Interactable wrapper for CookingStation to allow Player 1 to interact with it.
/// Handles the connection between the interaction system and cooking functionality.
/// </summary>
[RequireComponent(typeof(CookingStation))]
public class CookingStationInteractable : BaseInteractable
{
    [Header("Cooking Station Interaction")]
    [SerializeField] private CookingStation cookingStation;

    [Header("Interaction Settings")]
    [SerializeField] private bool requireHoldToCook = false;
    [Tooltip("If true, player must hold interact button to cook. If false, press once to start")]

    [SerializeField] private bool allowCookingToggle = true;
    [Tooltip("If true, player can press interact again to stop cooking manually")]

    protected override void Awake()
    {
        base.Awake();

        if (cookingStation == null)
            cookingStation = GetComponent<CookingStation>();

        // Set up as cooking interaction (Player 1 only)
        interactionType = InteractionType.Cooking;
        interactionPriority = 3; // Higher than regular items
    }

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        if (cookingStation == null) return false;

        // Must be Player 1
        if (playerEnd.PlayerNumber != 1)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} cannot use cooking station - Player 1 only");
            return false;
        }

        // Can interact if can start cooking OR can stop cooking (toggle mode)
        bool canStart = cookingStation.CanStartCooking;
        bool canStop = allowCookingToggle && cookingStation.IsCooking;

        if (!canStart && !canStop)
        {
            DebugLog($"No cookable ingredients on station and not currently cooking");
            return false;
        }

        return true;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (cookingStation == null) return false;

        // If currently cooking and toggle is allowed, stop cooking
        if (cookingStation.IsCooking && allowCookingToggle)
        {
            cookingStation.StopCooking();
            DebugLog($"Player {playerEnd.PlayerNumber} stopped cooking (toggle)");
            return true;
        }

        // Otherwise, try to start cooking
        if (cookingStation.CanStartCooking)
        {
            bool cookingStarted = cookingStation.StartCooking(playerEnd);

            if (cookingStarted)
            {
                DebugLog($"Player {playerEnd.PlayerNumber} started cooking interaction");

                // If we don't require holding, the cooking will continue automatically
                if (!requireHoldToCook)
                {
                    // Let the cooking station handle the automatic cooking
                    return true;
                }
            }

            return cookingStarted;
        }

        return false;
    }

    protected override void OnInteractionStopped(PlayerEnd playerEnd)
    {
        if (cookingStation == null) return;

        if (requireHoldToCook && cookingStation.IsCooking)
        {
            // Stop cooking when player releases interact button (only in hold mode)
            cookingStation.StopCooking();
            DebugLog($"Player {playerEnd.PlayerNumber} stopped cooking interaction (released)");
        }
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        if (cookingStation.IsCooking)
        {
            if (allowCookingToggle)
                return "Stop cooking";
            else if (requireHoldToCook)
                return "Hold to cook";
            else
                return "Cooking...";
        }

        return requireHoldToCook ? "Hold to cook" : "Start cooking";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (cookingStation == null) return "Station not available";

        if (playerEnd.PlayerNumber != 1)
            return "Player 1 only";

        if (!cookingStation.IsOccupied)
            return "Place ingredients first";

        if (!cookingStation.CanStartCooking && !cookingStation.IsCooking)
            return "No cookable ingredients";

        return base.GetUnavailablePrompt(playerEnd);
    }

    #region Station State Monitoring

    /// <summary>
    /// Update availability based on station state
    /// </summary>
    private void Update()
    {
        if (cookingStation == null) return;

        // Update availability based on whether station can be used
        bool shouldBeAvailable = cookingStation.IsOccupied || cookingStation.CanStartCooking ||
                               (allowCookingToggle && cookingStation.IsCooking);

        if (isCurrentlyAvailable != shouldBeAvailable)
        {
            SetAvailable(shouldBeAvailable);
        }

        // Update interaction prompt dynamically
        if (currentInteractingPlayer != null)
        {
            // Update prompt based on current state
            interactionPrompt = GetInteractionPrompt(currentInteractingPlayer);
        }
    }

    #endregion

    #region Cooking Progress Information

    /// <summary>
    /// Get the cooking progress for UI or other systems
    /// </summary>
    public float GetCookingProgress()
    {
        return cookingStation?.GetMaxCookingProgress() ?? 0f;
    }

    /// <summary>
    /// Get the number of ingredients being cooked
    /// </summary>
    public int GetCookingIngredientCount()
    {
        return cookingStation?.CookableIngredientCount ?? 0;
    }

    /// <summary>
    /// Check if any ingredients are burning
    /// </summary>
    public bool HasBurningIngredients()
    {
        if (cookingStation?.CurrentItem == null) return false;

        // Check single ingredient
        Ingredient singleIngredient = cookingStation.CurrentItem.GetComponent<Ingredient>();
        if (singleIngredient != null)
        {
            return singleIngredient.IsSpoiled;
        }

        // Check dish ingredients
        Dish dish = cookingStation.CurrentItem.GetComponent<Dish>();
        if (dish != null)
        {
            foreach (Ingredient ingredient in dish.Ingredients)
            {
                if (ingredient.IsSpoiled)
                    return true;
            }
        }

        return false;
    }

    #endregion

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw cooking-specific indicators
        if (cookingStation != null && cookingStation.IsCooking)
        {
            Gizmos.color = Color.red;
            Vector3 cookingIndicator = transform.position + Vector3.up * 3.5f;
            Gizmos.DrawWireSphere(cookingIndicator, 0.3f);

            // Draw progress bar
            float progress = GetCookingProgress();
            Gizmos.color = new Color(1f, 0.5f, 0f);  //orange
            Vector3 progressStart = cookingIndicator + Vector3.left;
            Vector3 progressEnd = Vector3.Lerp(progressStart, cookingIndicator + Vector3.right, progress);
            Gizmos.DrawLine(progressStart, progressEnd);
        }

        // Draw Player 1 restriction
        Gizmos.color = Color.red;
        Vector3 restrictionPos = transform.position + Vector3.up * 2.8f;
        Gizmos.DrawCube(restrictionPos, Vector3.one * 0.12f);

        // Draw burning warning
        if (HasBurningIngredients())
        {
            Gizmos.color = Color.red;
            Vector3 warningPos = transform.position + Vector3.up * 4.2f;
            Gizmos.DrawWireCube(warningPos, Vector3.one * 0.2f);
        }

#if UNITY_EDITOR
        // Show interaction info
        if (Application.isPlaying && cookingStation != null)
        {
            string info = $"Cooking Station\nPlayer 1 Only\n{cookingStation.Method}";
            if (cookingStation.IsCooking)
                info += $"\nProgress: {GetCookingProgress():P0}";
            if (HasBurningIngredients())
                info += "\nBURNING!";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4.5f, info);
        }
#endif
    }

    #endregion

    #region Validation

    protected override void OnValidate()
    {
        base.OnValidate();

        // Ensure correct interaction type
        interactionType = InteractionType.Cooking;

        // Auto-find cooking station component
        if (cookingStation == null)
        {
            cookingStation = GetComponent<CookingStation>();
        }

        // Ensure we have the required component
        if (cookingStation == null)
        {
            Debug.LogWarning($"[{name}] CookingStationInteractable requires a CookingStation component!");
        }

        // Validate interaction settings
        if (requireHoldToCook && allowCookingToggle)
        {
            Debug.LogWarning($"[{name}] Having both 'requireHoldToCook' and 'allowCookingToggle' enabled may create confusing interactions.");
        }
    }

    #endregion
}