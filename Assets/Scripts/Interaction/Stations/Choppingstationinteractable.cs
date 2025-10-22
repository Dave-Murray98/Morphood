using UnityEngine;

/// <summary>
/// Interactable wrapper for ChoppingStation to allow Player 2 to interact with it.
/// Handles the connection between the interaction system and chopping functionality.
/// </summary>
[RequireComponent(typeof(ChoppingStation))]
public class ChoppingStationInteractable : BaseInteractable
{
    [Header("Chopping Station Interaction")]
    [SerializeField] private ChoppingStation choppingStation;

    [Header("Interaction Settings")]
    [SerializeField] private bool requireHoldToChop = true;
    [Tooltip("If true, player must hold interact button to chop. If false, press once to start")]

    protected override void Awake()
    {
        base.Awake();

        if (choppingStation == null)
            choppingStation = GetComponent<ChoppingStation>();

        // Set up as chopping interaction (Player 2 only)
        interactionType = InteractionType.Chopping;
        interactionPriority = 3; // Higher than regular items
    }

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return false;

        // Must be Player 2
        if (playerEnd.PlayerNumber != 2)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} cannot use chopping station - Player 2 only");
            return false;
        }

        // Must have an item that can be chopped on the station
        if (!choppingStation.CanStartChopping)
        {
            DebugLog($"No choppable ingredients on station");
            return false;
        }

        return true;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return false;

        // Start chopping
        bool choppingStarted = choppingStation.StartChopping(playerEnd);

        if (choppingStarted)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} started chopping interaction");

            // If we don't require holding, the chopping will complete automatically
            if (!requireHoldToChop)
            {
                // Let the chopping station handle the automatic completion
                return true;
            }
        }

        return choppingStarted;
    }

    protected override void OnInteractionStopped(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return;

        if (requireHoldToChop)
        {
            // Stop chopping when player releases interact button
            choppingStation.StopChopping();
            DebugLog($"Player {playerEnd.PlayerNumber} stopped chopping interaction");
        }
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        if (choppingStation.IsChopping)
        {
            return requireHoldToChop ? "Hold to chop" : "Chopping...";
        }

        return requireHoldToChop ? "Hold to chop" : "Chop ingredient";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return "Station not available";

        if (playerEnd.PlayerNumber != 2)
            return "Player 2 only";

        if (!choppingStation.IsOccupied)
            return "Place ingredient first";

        if (!choppingStation.CanStartChopping)
            return "Cannot chop this item";

        return base.GetUnavailablePrompt(playerEnd);
    }

    #region Station State Monitoring

    /// <summary>
    /// Update availability based on station state
    /// </summary>
    private void Update()
    {
        if (choppingStation == null) return;

        // Update availability based on whether station can be used
        bool shouldBeAvailable = choppingStation.IsOccupied || choppingStation.CanStartChopping;

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

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw chopping-specific indicators
        if (choppingStation != null && choppingStation.IsChopping)
        {
            Gizmos.color = Color.cyan;
            Vector3 choppingIndicator = transform.position + Vector3.up * 3.5f;
            Gizmos.DrawWireSphere(choppingIndicator, 0.3f);
        }

        // Draw Player 2 restriction
        Gizmos.color = Color.blue;
        Vector3 restrictionPos = transform.position + Vector3.up * 2.8f;
        Gizmos.DrawCube(restrictionPos, Vector3.one * 0.12f);

#if UNITY_EDITOR
        // Show interaction info
        if (Application.isPlaying && choppingStation != null)
        {
            string info = "Chopping Station\nPlayer 2 Only";
            if (choppingStation.IsChopping)
                info += $"\nProgress: {choppingStation.ChoppingProgress:P0}";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4f, info);
        }
#endif
    }

    #endregion

    #region Validation

    protected override void OnValidate()
    {
        base.OnValidate();

        // Ensure correct interaction type
        interactionType = InteractionType.Chopping;

        // Auto-find chopping station component
        if (choppingStation == null)
        {
            choppingStation = GetComponent<ChoppingStation>();
        }

        // Ensure we have the required component
        if (choppingStation == null)
        {
            Debug.LogWarning($"[{name}] ChoppingStationInteractable requires a ChoppingStation component!");
        }
    }

    #endregion
}