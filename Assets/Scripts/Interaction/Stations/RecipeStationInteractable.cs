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

    [SerializeField] private float hideDistance = 3f;
    [Tooltip("Distance at which the recipe automatically hides when player moves away")]

    [SerializeField] private bool recipeIsVisible = false;
    [Tooltip("Current visibility state of the recipe")]

    // Track which player is currently viewing the recipe
    private PlayerEnd viewingPlayer;

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

    private void Update()
    {
        // Check if the viewing player has moved too far away
        if (recipeIsVisible && viewingPlayer != null)
        {
            float distance = Vector3.Distance(transform.position, viewingPlayer.transform.position);
            if (distance > hideDistance)
            {
                SetRecipeVisibility(false);
                viewingPlayer = null;
                DebugLog($"Player moved too far away (distance: {distance:F2}) - hiding recipe");
            }
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
        if (recipeIsVisible)
        {
            // Hide the recipe
            SetRecipeVisibility(false);
            viewingPlayer = null;
            DebugLog($"Player {playerEnd.PlayerNumber} hid the recipe");
        }
        else
        {
            // Show the recipe and track this player
            SetRecipeVisibility(true);
            viewingPlayer = playerEnd;
            DebugLog($"Player {playerEnd.PlayerNumber} is now viewing the recipe");
        }

        // Return false so this doesn't register as an ongoing interaction
        // This prevents OnInteractionStopped from being called when button is released
        return false;
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

        // Draw the hide distance range
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hideDistance);

        // Draw line to viewing player if active
        if (recipeIsVisible && viewingPlayer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, viewingPlayer.transform.position);
        }
    }

    #endregion
}
