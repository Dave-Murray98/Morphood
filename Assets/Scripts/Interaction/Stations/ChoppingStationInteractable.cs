using UnityEngine;

/// <summary>
/// Interactable wrapper for ChoppingStation to allow direct player interaction.
/// Handles placing items and starting/stopping the chopping process.
/// Only Player 2 can interact with chopping stations.
/// </summary>
[RequireComponent(typeof(ChoppingStation))]
public class ChoppingStationInteractable : BaseInteractable
{
    [Header("Chopping Interaction Settings")]
    [SerializeField] private bool showProgressInPrompt = true;
    [Tooltip("Whether to show chopping progress percentage in the interaction prompt")]

    [SerializeField] private bool requireHoldToChop = true;
    [Tooltip("Whether player must hold the interact button to chop")]

    // Internal references
    private ChoppingStation choppingStation;

    protected override void Awake()
    {
        base.Awake();

        // Get the chopping station component
        choppingStation = GetComponent<ChoppingStation>();
        if (choppingStation == null)
        {
            Debug.LogError($"[ChoppingStationInteractable] {name} requires a ChoppingStation component!");
        }

        // Set up interaction type for chopping (only Player 2)
        interactionType = InteractionType.Chopping;
        interactionPriority = 3; // Higher priority than regular items and plain stations
    }

    #region BaseInteractable Implementation

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return false;

        // Check if player can perform chopping interactions
        if (!playerEnd.CanPerformInteraction(InteractionType.Chopping))
        {
            DebugLog($"Player {playerEnd.PlayerNumber} cannot perform chopping");
            return false;
        }

        // Can interact if:
        // 1. Player has free hands and station has a processed item (to retrieve), OR
        // 2. Player is carrying a choppable item and station has space (to place), OR
        // 3. Station has an item that can be chopped and chopping can be started/continued

        bool canRetrieve = playerEnd.HasFreeHands && choppingStation.IsOccupied && !choppingStation.IsChopping;
        bool canPlace = playerEnd.IsCarryingItems && choppingStation.HasSpace && CanPlaceChoppableItem(playerEnd);
        bool canChop = choppingStation.IsOccupied && CanStartOrContinueChopping(playerEnd);

        return canRetrieve || canPlace || canChop;
    }

    /// <summary>
    /// Check if the player is carrying an item that can be placed for chopping
    /// </summary>
    private bool CanPlaceChoppableItem(PlayerEnd playerEnd)
    {
        if (!playerEnd.IsCarryingItems) return false;

        foreach (GameObject carriedObj in playerEnd.HeldObjects)
        {
            if (choppingStation.CanAcceptItem(carriedObj, playerEnd))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if chopping can be started or continued
    /// </summary>
    private bool CanStartOrContinueChopping(PlayerEnd playerEnd)
    {
        if (!choppingStation.IsOccupied) return false;

        // If already chopping, only the same player can continue
        if (choppingStation.IsChopping)
        {
            return choppingStation.GetCurrentChoppingPlayer() == playerEnd;
        }

        // Check if the current item can be chopped
        FoodItem foodItem = choppingStation.CurrentItem?.GetComponent<FoodItem>();
        return foodItem != null && foodItem.CanBeProcessed(FoodProcessType.Chopping);
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return false;

        // Priority 1: If player has free hands and station has finished item - retrieve it
        if (playerEnd.HasFreeHands && choppingStation.IsOccupied && !choppingStation.IsChopping)
        {
            return TryRetrieveItem(playerEnd);
        }

        // Priority 2: If station has an item that can be chopped - start/continue chopping
        if (choppingStation.IsOccupied && CanStartOrContinueChopping(playerEnd))
        {
            return TryStartChopping(playerEnd);
        }

        // Priority 3: If player is carrying choppable item and station has space - place it
        // This will be handled by PlayerEnd's drop logic, so we return false to let that handle it
        return false;
    }

    protected override void OnInteractionStopped(PlayerEnd playerEnd)
    {
        // If we were chopping with hold-to-chop mode, stop the chopping
        if (requireHoldToChop && choppingStation != null && choppingStation.IsChopping)
        {
            PlayerEnd currentChopper = choppingStation.GetCurrentChoppingPlayer();
            if (currentChopper == playerEnd)
            {
                choppingStation.StopChopping();
                DebugLog($"Player {playerEnd.PlayerNumber} stopped chopping by releasing interact");
            }
        }
    }

    #endregion

    #region Interaction Logic

    /// <summary>
    /// Try to retrieve the processed item from the station
    /// </summary>
    private bool TryRetrieveItem(PlayerEnd playerEnd)
    {
        if (!playerEnd.HasFreeHands || !choppingStation.IsOccupied)
        {
            return false;
        }

        // Use the base station's removal logic
        GameObject retrievedItem = choppingStation.RemoveItem(playerEnd);
        if (retrievedItem == null) return false;

        // Give it to the player
        bool pickupSuccessful = playerEnd.PickUpObject(retrievedItem);

        if (!pickupSuccessful)
        {
            // If pickup failed, put the item back on the station
            choppingStation.PlaceItem(retrievedItem, playerEnd);
            DebugLog("Failed to retrieve item - returned to station");
            return false;
        }

        DebugLog($"Player {playerEnd.PlayerNumber} retrieved item from chopping station");
        return true;
    }

    /// <summary>
    /// Try to start the chopping process
    /// </summary>
    private bool TryStartChopping(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return false;

        // If already chopping, this is a continue action (for hold-to-chop mode)
        if (choppingStation.IsChopping)
        {
            return choppingStation.GetCurrentChoppingPlayer() == playerEnd;
        }

        // Start new chopping process
        bool choppingStarted = choppingStation.StartChopping(playerEnd);

        if (choppingStarted)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} started chopping");
            return true;
        }

        return false;
    }

    #endregion

    #region Prompt Generation

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        // If player has free hands and station has finished item - show retrieval prompt
        if (playerEnd.HasFreeHands && choppingStation.IsOccupied && !choppingStation.IsChopping)
        {
            return $"Pick up {choppingStation.CurrentItem.name}";
        }

        // If currently chopping - show progress
        if (choppingStation.IsChopping)
        {
            if (requireHoldToChop)
            {
                string progressText = showProgressInPrompt ?
                    $" ({choppingStation.GetChoppingProgress() * 100f:F0}%)" : "";
                return $"Hold to chop{progressText}";
            }
            else
            {
                string progressText = showProgressInPrompt ?
                    $" ({choppingStation.GetChoppingProgress() * 100f:F0}%)" : "";
                return $"Chopping{progressText}";
            }
        }

        // If station has an item that can be chopped - show chop prompt
        if (choppingStation.IsOccupied && CanStartOrContinueChopping(playerEnd))
        {
            FoodItem foodItem = choppingStation.CurrentItem?.GetComponent<FoodItem>();
            if (foodItem != null)
            {
                if (requireHoldToChop)
                {
                    return $"Hold to chop {foodItem.FoodData.DisplayName}";
                }
                else
                {
                    return $"Chop {foodItem.FoodData.DisplayName}";
                }
            }
            return "Start chopping";
        }

        // If player is carrying choppable item and station has space - show placement prompt
        if (playerEnd.IsCarryingItems && choppingStation.HasSpace && CanPlaceChoppableItem(playerEnd))
        {
            return "Place item for chopping";
        }

        return "Interact";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (choppingStation == null) return "Station not available";

        // Check player permissions first
        if (!playerEnd.CanPerformInteraction(InteractionType.Chopping))
        {
            return "Only Player 2 can chop";
        }

        // Check specific unavailability reasons
        if (playerEnd.HasFreeHands && !choppingStation.IsOccupied)
            return "Nothing to pick up";

        if (playerEnd.IsCarryingItems && !choppingStation.HasSpace)
            return "Station is busy";

        if (playerEnd.IsCarryingItems && choppingStation.HasSpace && !CanPlaceChoppableItem(playerEnd))
            return "Cannot chop this item";

        if (choppingStation.IsChopping && choppingStation.GetCurrentChoppingPlayer() != playerEnd)
            return "Being used by other player";

        return "Cannot interact";
    }

    #endregion

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw chopping-specific gizmos
        if (Application.isPlaying && choppingStation != null)
        {
            // Draw interaction availability indicator
            Gizmos.color = choppingStation.IsChopping ? Color.red : Color.blue;
            Vector3 indicatorPos = transform.position + Vector3.up * 3.5f;
            Gizmos.DrawWireCube(indicatorPos, Vector3.one * 0.2f);

            // Draw chopping range indicator
            if (choppingStation.IsOccupied)
            {
                FoodItem foodItem = choppingStation.CurrentItem?.GetComponent<FoodItem>();
                if (foodItem != null && foodItem.CanBeProcessed(FoodProcessType.Chopping))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(indicatorPos + Vector3.up * 0.5f, 0.3f);
                }
            }
        }
    }

    #endregion
}