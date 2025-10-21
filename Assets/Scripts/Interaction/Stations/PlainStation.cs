using UnityEngine;

/// <summary>
/// A simple storage station that acts like a worktop in Overcooked.
/// Players can place any item on it for temporary storage.
/// Both players can interact with it.
/// </summary>
public class PlainStation : BaseStation
{
    [Header("Plain Station Settings")]
    [SerializeField] private bool allowItemRetrieval = true;
    [Tooltip("Whether players can pick up items that are placed on this station")]

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

        DebugLog("Plain station ready for item storage");
    }

    protected override bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        // Plain stations accept any item by default
        // Could add specific logic here if needed (e.g., size restrictions)
        return true;
    }

    protected override void OnItemPlacedInternal(GameObject item, PlayerEnd playerEnd)
    {
        // When an item is placed, make it available for pickup by any player
        PickupableItem pickupableItem = item.GetComponent<PickupableItem>();
        if (pickupableItem != null)
        {
            // The item is now on the station and available for pickup
            DebugLog($"Item {item.name} is now available for pickup from this station");
        }
    }

    protected override void OnItemRemovedInternal(GameObject item, PlayerEnd playerEnd)
    {
        // When an item is removed, no special cleanup needed for plain stations
        DebugLog($"Item {item.name} removed from storage");
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

    #region IInteractable Implementation

    // Make PlainStation interactable so players can also retrieve items by interacting directly
    // This allows for both placing (when carrying) and retrieving (when not carrying)

    #endregion
}

