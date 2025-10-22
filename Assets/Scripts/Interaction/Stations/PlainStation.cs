using UnityEngine;

/// <summary>
/// A simple storage station that acts like a worktop in Overcooked.
/// Players can place any item on it for temporary storage.
/// Now supports automatic food combination when compatible food items are placed together.
/// Both players can interact with it.
/// </summary>
public class PlainStation : BaseStation
{
    [Header("Plain Station Settings")]
    [SerializeField] private bool allowItemRetrieval = true;
    [Tooltip("Whether players can pick up items that are placed on this station")]

    [SerializeField] private bool enableFoodCombination = true;
    [Tooltip("Whether this station should automatically combine compatible food items")]

    [SerializeField] private float combinationDelay = 0.5f;
    [Tooltip("Delay before checking for combinations (allows time for placement animation)")]

    // Internal state for combination checking
    private bool hasPendingCombinationCheck = false;

    protected override void Initialize()
    {
        // Set up as a plain station
        stationType = StationType.Plain;
        stationName = string.IsNullOrEmpty(stationName) || stationName == "Station" ? "Worktop" : stationName;

        // Plain stations accept all item types and both players can use them
        acceptAllItemTypes = true;
        allowPlayer1Interaction = true;
        allowPlayer2Interaction = true;

        base.Initialize();

        DebugLog("Plain station ready for item storage and food combination");
    }

    protected override bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        // Plain stations accept any item by default
        // But also check if this item can be combined with what's already on the station
        if (enableFoodCombination && isOccupied && currentItem != null)
        {
            FoodItem newFoodItem = item.GetComponent<FoodItem>();
            if (newFoodItem != null && CanAcceptForCombination(newFoodItem))
            {
                DebugLog($"Station is full but {newFoodItem.FoodData.DisplayName} can be combined with station item");
                return true; // Allow "placement" for combination purposes
            }
        }

        // For non-food items or non-combinable items, use normal logic
        return true;
    }

    protected override void OnItemPlacedInternal(GameObject item, PlayerEnd playerEnd)
    {
        // When an item is placed, check for food combinations if enabled
        FoodItem placedFoodItem = item.GetComponent<FoodItem>();

        if (placedFoodItem != null && enableFoodCombination && FoodManager.Instance != null)
        {
            DebugLog($"Food item {placedFoodItem.FoodData.DisplayName} placed - checking for combinations");

            // Schedule a combination check after a brief delay
            // This allows for proper item placement and any animations to complete
            if (!hasPendingCombinationCheck)
            {
                Invoke(nameof(CheckForFoodCombinations), combinationDelay);
                hasPendingCombinationCheck = true;
            }
        }
        else
        {
            DebugLog($"Item {item.name} is now available for pickup from this station");
        }
    }

    protected override void OnItemRemovedInternal(GameObject item, PlayerEnd playerEnd)
    {
        // When an item is removed, no special cleanup needed for plain stations
        DebugLog($"Item {item.name} removed from storage");

        // Cancel any pending combination checks since the station state changed
        if (hasPendingCombinationCheck)
        {
            CancelInvoke(nameof(CheckForFoodCombinations));
            hasPendingCombinationCheck = false;
        }
    }

    /// <summary>
    /// Place an item on this station - overridden to handle food combinations
    /// </summary>
    public override bool PlaceItem(GameObject item, PlayerEnd playerEnd)
    {
        // Check if this is a combination scenario (station is occupied but we can combine)
        if (enableFoodCombination && isOccupied && currentItem != null)
        {
            FoodItem newFoodItem = item.GetComponent<FoodItem>();
            if (newFoodItem != null && CanAcceptForCombination(newFoodItem))
            {
                DebugLog($"Attempting combination placement for {newFoodItem.FoodData.DisplayName}");
                return TryCombineWithStationItem(newFoodItem, playerEnd);
            }
        }

        // Otherwise, use the normal base placement logic
        return base.PlaceItem(item, playerEnd);
    }

    /// <summary>
    /// Check if food items on this station can be combined
    /// </summary>
    private void CheckForFoodCombinations()
    {
        hasPendingCombinationCheck = false;

        if (!enableFoodCombination || FoodManager.Instance == null || !isOccupied)
        {
            return;
        }

        // For this implementation, we'll handle the simple case where we have one item on the station
        // and a player just placed another item. Since BaseStation currently supports only one item,
        // we need to modify our approach.

        // Get the current food item on the station
        FoodItem stationFoodItem = currentItem?.GetComponent<FoodItem>();

        if (stationFoodItem == null || !stationFoodItem.HasValidFoodData)
        {
            DebugLog("No valid food item on station for combination");
            return;
        }

        // In a real scenario, we'd check if there are multiple items that could be combined.
        // For now, let's check if any players are carrying items that could combine with the station item.
        CheckForPlayerCombinations(stationFoodItem);
    }

    /// <summary>
    /// Check if any players are carrying items that could combine with the item on this station
    /// </summary>
    /// <param name="stationFoodItem">The food item currently on the station</param>
    private void CheckForPlayerCombinations(FoodItem stationFoodItem)
    {
        // Find the player controller to check what players are carrying
        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null) return;

        // Check both players
        for (int playerNum = 1; playerNum <= 2; playerNum++)
        {
            PlayerEnd playerEnd = playerController.GetPlayerEnd(playerNum);
            if (playerEnd == null || !playerEnd.IsCarryingItems) continue;

            // Check each item the player is carrying
            foreach (GameObject carriedObj in playerEnd.HeldObjects)
            {
                FoodItem carriedFoodItem = carriedObj.GetComponent<FoodItem>();
                if (carriedFoodItem == null || !carriedFoodItem.HasValidFoodData) continue;

                // Check if these items can be combined
                if (FoodManager.Instance.CanCombineFoodItems(stationFoodItem, carriedFoodItem))
                {
                    DebugLog($"Found potential combination: {stationFoodItem.FoodData.DisplayName} + {carriedFoodItem.FoodData.DisplayName}");

                    // For now, just log this. In a full implementation, you might:
                    // 1. Show a visual indicator
                    // 2. Auto-combine when player places the item
                    // 3. Show a prompt to the player
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Attempt to combine a new food item with the item already on this station
    /// Called by PlainStationInteractable when a food item is being combined
    /// </summary>
    /// <param name="newFoodItem">The food item being combined (can still be held by player)</param>
    /// <param name="playerEnd">The player performing the combination</param>
    /// <returns>True if a combination occurred</returns>
    public bool TryCombineWithStationItem(FoodItem newFoodItem, PlayerEnd playerEnd)
    {
        if (!enableFoodCombination || FoodManager.Instance == null)
        {
            DebugLog("Food combination disabled or no FoodManager available");
            return false;
        }

        if (!isOccupied || currentItem == null)
        {
            DebugLog("No item on station to combine with");
            return false; // No item on station to combine with
        }

        FoodItem stationFoodItem = currentItem.GetComponent<FoodItem>();
        if (stationFoodItem == null || !stationFoodItem.HasValidFoodData)
        {
            DebugLog("Station item is not a valid food item");
            return false;
        }

        if (newFoodItem == null || !newFoodItem.HasValidFoodData)
        {
            DebugLog("New item is not a valid food item");
            return false;
        }

        // Check if these items can be combined
        if (!FoodManager.Instance.CanCombineFoodItems(stationFoodItem, newFoodItem))
        {
            DebugLog($"Items cannot be combined: {stationFoodItem.FoodData.DisplayName} + {newFoodItem.FoodData.DisplayName}");
            return false;
        }

        DebugLog($"Combining {stationFoodItem.FoodData.DisplayName} + {newFoodItem.FoodData.DisplayName}");

        // Remove the current item from the station (but don't destroy it yet)
        GameObject removedItem = RemoveItem(playerEnd);
        if (removedItem == null)
        {
            DebugLog("Failed to remove item from station for combination");
            return false;
        }

        // Perform the combination at the station's placement position
        Vector3 combinationPosition = GetPlacementPosition();
        FoodItem combinedItem = FoodManager.Instance.TryCombineFoodItems(stationFoodItem, newFoodItem, combinationPosition);

        if (combinedItem != null)
        {
            // Place the combined item on this station
            bool placementSuccessful = PlaceItem(combinedItem.gameObject, playerEnd);

            if (placementSuccessful)
            {
                DebugLog($"Successfully combined and placed {combinedItem.FoodData.DisplayName} on station");
                return true;
            }
            else
            {
                DebugLog("Failed to place combined item back on station");
                // The FoodManager already destroyed the original items, so we just have the combined item floating
                // At least the combination worked, even if placement failed
                return true;
            }
        }
        else
        {
            // Combination failed, put the original item back on the station
            PlaceItem(removedItem, playerEnd);
            DebugLog("Combination failed, restored original item to station");
            return false;
        }
    }

    /// <summary>
    /// Try to retrieve the item from this station for a player to pick up
    /// </summary>
    public bool TryRetrieveItem(PlayerEnd playerEnd)
    {
        if (!allowItemRetrieval)
        {
            DebugLog("Item retrieval not allowed on this station");
            return false;
        }

        if (!isOccupied)
        {
            DebugLog("No item to retrieve");
            return false;
        }

        if (!playerEnd.HasFreeHands)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} hands are full");
            return false;
        }

        // Use the base class RemoveItem method to properly update station state
        GameObject itemToPickup = RemoveItem(playerEnd);
        if (itemToPickup == null) return false;

        // Give it to the player
        bool pickupSuccessful = playerEnd.PickUpObject(itemToPickup);

        if (!pickupSuccessful)
        {
            // If pickup failed, put the item back on the station using PlaceItem
            // This will properly restore the station state
            PlaceItem(itemToPickup, playerEnd);
            DebugLog("Failed to retrieve item - returned to station");
            return false;
        }

        DebugLog($"Player {playerEnd.PlayerNumber} retrieved {itemToPickup.name} from station");
        return true;
    }

    #region Public Methods

    /// <summary>
    /// Enable or disable food combination functionality
    /// </summary>
    /// <param name="enabled">Whether food combination should be enabled</param>
    public void SetFoodCombinationEnabled(bool enabled)
    {
        enableFoodCombination = enabled;
        DebugLog($"Food combination {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Check if this station can currently accept a food item for combination
    /// </summary>
    /// <param name="newFoodItem">The food item to check</param>
    /// <returns>True if the item can be combined with what's on the station</returns>
    public bool CanAcceptForCombination(FoodItem newFoodItem)
    {
        if (!enableFoodCombination || !isOccupied || currentItem == null)
            return false;

        FoodItem stationFoodItem = currentItem.GetComponent<FoodItem>();
        if (stationFoodItem == null) return false;

        return FoodManager.Instance?.CanCombineFoodItems(stationFoodItem, newFoodItem) ?? false;
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Clean up any pending invokes
        CancelInvoke();
    }

    private void OnDisable()
    {
        // Clean up any pending invokes
        CancelInvoke();
        hasPendingCombinationCheck = false;
    }

    #endregion
}