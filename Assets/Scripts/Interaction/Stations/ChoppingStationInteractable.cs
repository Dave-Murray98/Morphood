using UnityEngine;

/// <summary>
/// Interactable wrapper for ChoppingStation to allow direct player interaction.
/// Uses the scalable BaseProcessingStationInteractable system for hold-to-chop vs press-to-pickup logic.
/// Both players can place and pick up items, but only Player 2 can perform chopping.
/// </summary>
[RequireComponent(typeof(ChoppingStation))]
public class ChoppingStationInteractable : BaseProcessingStationInteractable
{
    // Internal references
    private ChoppingStation choppingStation;

    // BaseProcessingStationInteractable implementation
    protected override BaseStation ProcessingStation => choppingStation;
    protected override InteractionType RequiredInteractionType => InteractionType.Chopping;
    protected override FoodProcessType ProcessType => FoodProcessType.Chopping;
    protected override string ProcessVerb => "chop";
    protected override string ProcessingVerb => "chopping";

    protected override void Awake()
    {
        base.Awake();

        // Get the chopping station component
        choppingStation = GetComponent<ChoppingStation>();
        if (choppingStation == null)
        {
            Debug.LogError($"[ChoppingStationInteractable] {name} requires a ChoppingStation component!");
        }

        // Set up interaction type for chopping
        // Both players can place/pick up items, but only Player 2 can chop (enforced in BaseProcessingStationInteractable)
        interactionType = InteractionType.Chopping;
        interactionPriority = 3; // Higher priority than regular items and plain stations
    }

    #region BaseProcessingStationInteractable Implementation

    protected override bool IsCurrentlyProcessing()
    {
        return choppingStation != null && choppingStation.IsChopping;
    }

    protected override float GetProcessingProgress()
    {
        return choppingStation?.GetChoppingProgress() ?? 0f;
    }

    protected override bool CanItemBeProcessed(FoodItem foodItem)
    {
        if (foodItem == null || !foodItem.HasValidFoodData) return false;
        return foodItem.CanBeProcessed(FoodProcessType.Chopping);
    }

    protected override bool StartProcessing(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return false;
        return choppingStation.StartChopping(playerEnd);
    }

    protected override void StopProcessing()
    {
        choppingStation?.StopChopping();
    }

    protected override PlayerEnd GetCurrentProcessingPlayer()
    {
        return choppingStation?.GetCurrentChoppingPlayer();
    }

    #endregion

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw chopping-specific gizmos
        if (Application.isPlaying && choppingStation != null)
        {
            // Draw chopping station type indicator
            Gizmos.color = Color.blue;
            Vector3 indicatorPos = transform.position + Vector3.up * 2.5f;
            Gizmos.DrawCube(indicatorPos, Vector3.one * 0.3f);

#if UNITY_EDITOR
            // Show chopping station label
            if (choppingStation.IsChopping)
            {
                string progressText = $"Chopping: {choppingStation.GetChoppingProgress() * 100f:F0}%";
                UnityEditor.Handles.Label(indicatorPos + Vector3.up * 0.5f, progressText);
            }
#endif
        }
    }

    #endregion
}