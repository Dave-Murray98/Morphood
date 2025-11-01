using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple interactable for displaying recipe information.
/// When a player interacts, toggles visibility of the recipe image.
/// Automatically hides the image when the player moves away.
/// </summary>
public class RecipeStationInteractable : BaseInteractable
{
    [Header("Recipe Display")]
    [SerializeField] private Image recipeImage;
    [Tooltip("The Image component that displays the recipe (should be in a world space canvas)")]

    [SerializeField] private bool recipeIsVisible = false;
    [Tooltip("Current visibility state of the recipe")]

    protected override void Awake()
    {
        base.Awake();

        // Set up as universal interaction (both players can use)
        interactionType = InteractionType.Universal;
        interactionPriority = 1; // Standard priority
        interactionPrompt = "View Recipe";

        // Find the recipe image if not assigned
        if (recipeImage == null)
        {
            // Try to find the Image component in children
            recipeImage = GetComponentInChildren<Image>(true);

            if (recipeImage == null)
            {
                Debug.LogError($"[{name}] No Image component found! Please assign a recipe image in the inspector.");
            }
        }

        // Initialize recipe as hidden
        if (recipeImage != null)
        {
            SetRecipeVisibility(false);
        }
    }

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        // Anyone can interact at any time
        return recipeImage != null;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (recipeImage == null)
        {
            DebugLog("Cannot interact - no recipe image assigned");
            return false;
        }

        // Toggle recipe visibility
        ToggleRecipeVisibility();

        DebugLog($"Player {playerEnd.PlayerNumber} toggled recipe visibility to {recipeIsVisible}");

        // Return true to indicate successful interaction
        // This allows the player to walk away and trigger StopInteracting
        return true;
    }

    protected override void OnInteractionStopped(PlayerEnd playerEnd)
    {
        // When player walks away (leaves range), hide the recipe
        if (recipeIsVisible)
        {
            SetRecipeVisibility(false);
            DebugLog($"Player {playerEnd.PlayerNumber} walked away - hiding recipe");
        }
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        // Show appropriate prompt based on current state
        return recipeIsVisible ? "Hide Recipe" : "View Recipe";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (recipeImage == null)
            return "No recipe configured";

        return base.GetUnavailablePrompt(playerEnd);
    }

    /// <summary>
    /// Toggle the visibility of the recipe image
    /// </summary>
    private void ToggleRecipeVisibility()
    {
        SetRecipeVisibility(!recipeIsVisible);
    }

    /// <summary>
    /// Set the visibility of the recipe image
    /// </summary>
    private void SetRecipeVisibility(bool visible)
    {
        if (recipeImage == null) return;

        recipeIsVisible = visible;
        recipeImage.enabled = visible;

        DebugLog($"Recipe visibility set to {visible}");
    }

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw a visual indicator for the recipe display
        if (recipeImage != null)
        {
            Gizmos.color = recipeIsVisible ? Color.cyan : Color.gray;
            Vector3 canvasPosition = recipeImage.transform.position;
            Gizmos.DrawWireCube(canvasPosition, Vector3.one * 0.5f);
        }
    }

    #endregion
}
