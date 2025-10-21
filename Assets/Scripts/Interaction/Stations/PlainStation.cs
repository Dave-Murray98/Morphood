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

        // Remove item from station
        GameObject itemToPickup = RemoveItem(playerEnd);
        if (itemToPickup == null) return false;

        // Give it to the player
        bool pickupSuccessful = playerEnd.PickUpObject(itemToPickup);

        if (!pickupSuccessful)
        {
            // If pickup failed, put the item back on the station
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

/// <summary>
/// Interactable wrapper for PlainStation to allow direct player interaction
/// </summary>
[System.Serializable]
public class PlainStationInteractable : BaseInteractable
{
    [SerializeField] private PlainStation plainStation;

    private void Awake()
    {
        if (plainStation == null)
            plainStation = GetComponent<PlainStation>();

        // Set up as universal interaction
        interactionType = InteractionType.Universal;
        interactionPriority = 2; // Slightly higher than regular items
    }

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        if (plainStation == null) return false;

        // Can interact if:
        // 1. Player has free hands and station has an item (to retrieve), OR
        // 2. Player is carrying something and station has space (to place)

        bool canRetrieve = playerEnd.HasFreeHands && plainStation.IsOccupied;
        bool canPlace = playerEnd.IsCarryingItems && plainStation.HasSpace;

        return canRetrieve || canPlace;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (plainStation == null) return false;

        // If player has free hands and station has item - retrieve it
        if (playerEnd.HasFreeHands && plainStation.IsOccupied)
        {
            return plainStation.TryRetrieveItem(playerEnd);
        }

        // If player is carrying something and station has space - this will be handled by PlayerEnd's drop logic
        // This interaction just confirms that the station can accept items
        return false; // Let PlayerEnd handle the placement
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        if (playerEnd.HasFreeHands && plainStation.IsOccupied)
        {
            return $"Pick up {plainStation.CurrentItem.name}";
        }

        if (playerEnd.IsCarryingItems && plainStation.HasSpace)
        {
            return "Place item";
        }

        return "Interact";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (plainStation == null) return "Station not available";

        if (!playerEnd.HasFreeHands && !playerEnd.IsCarryingItems)
            return "Nothing to do";

        if (playerEnd.IsCarryingItems && !plainStation.HasSpace)
            return "Station is full";

        if (playerEnd.HasFreeHands && !plainStation.IsOccupied)
            return "Nothing to pick up";

        return base.GetUnavailablePrompt(playerEnd);
    }
}