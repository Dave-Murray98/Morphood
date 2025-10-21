using UnityEngine;

/// <summary>
/// Interactable wrapper for PlainStation to allow direct player interaction
/// </summary>
[System.Serializable]
public class PlainStationInteractable : BaseInteractable
{
    [SerializeField] private PlainStation plainStation;

    protected override void Awake()
    {
        base.Awake();
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