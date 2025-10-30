using UnityEngine;

/// <summary>
/// Interactable wrapper for CookingStation to allow direct player interaction.
/// Uses the scalable BaseProcessingStationInteractable system for hold-to-cook vs press-to-pickup logic.
/// Only Player 1 can interact with cooking stations.
/// </summary>
[RequireComponent(typeof(CookingStation))]
public class CookingStationInteractable : BaseProcessingStationInteractable
{
    // Internal references
    private CookingStation cookingStation;

    // BaseProcessingStationInteractable implementation
    protected override BaseStation ProcessingStation => cookingStation;
    protected override InteractionType RequiredInteractionType => InteractionType.Cooking;
    protected override FoodProcessType ProcessType => FoodProcessType.Cooking;
    protected override string ProcessVerb => "cook";
    protected override string ProcessingVerb => "cooking";

    protected override void Awake()
    {
        base.Awake();

        // Get the cooking station component
        cookingStation = GetComponent<CookingStation>();
        if (cookingStation == null)
        {
            Debug.LogError($"[CookingStationInteractable] {name} requires a CookingStation component!");
        }

        // Set up interaction type for cooking (only Player 1)
        interactionType = InteractionType.Cooking;
        interactionPriority = 3; // Higher priority than regular items and plain stations
    }

    #region BaseProcessingStationInteractable Implementation

    protected override bool IsCurrentlyProcessing()
    {
        return cookingStation != null && cookingStation.IsCooking;
    }

    protected override float GetProcessingProgress()
    {
        return cookingStation?.GetCookingProgress() ?? 0f;
    }

    protected override bool CanItemBeProcessed(FoodItem foodItem)
    {
        if (foodItem == null || !foodItem.HasValidFoodData) return false;
        return foodItem.CanBeProcessed(FoodProcessType.Cooking);
    }

    protected override bool StartProcessing(PlayerEnd playerEnd)
    {
        if (cookingStation == null) return false;
        return cookingStation.StartCooking(playerEnd);
    }

    protected override void StopProcessing()
    {
        cookingStation?.StopCooking();
    }

    protected override PlayerEnd GetCurrentProcessingPlayer()
    {
        return cookingStation?.GetCurrentCookingPlayer();
    }

    #endregion

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw cooking-specific gizmos
        if (Application.isPlaying && cookingStation != null)
        {
            // Draw cooking station type indicator
            Gizmos.color = Color.red;
            Vector3 indicatorPos = transform.position + Vector3.up * 2.5f;
            Gizmos.DrawCube(indicatorPos, Vector3.one * 0.3f);

#if UNITY_EDITOR
            // Show cooking station label
            if (cookingStation.IsCooking)
            {
                string progressText = $"Cooking: {cookingStation.GetCookingProgress() * 100f:F0}%";
                UnityEditor.Handles.Label(indicatorPos + Vector3.up * 0.5f, progressText);
            }
#endif
        }
    }

    #endregion
}