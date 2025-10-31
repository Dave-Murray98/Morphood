using UnityEngine;

/// <summary>
/// Interactable component for serving stations.
/// Handles player interaction to serve food to customers.
/// </summary>
[RequireComponent(typeof(ServingStation))]
public class ServingStationInteractable : BaseInteractable
{
    private ServingStation servingStation;

    protected override void Awake()
    {
        base.Awake();
        servingStation = GetComponent<ServingStation>();

        if (servingStation == null)
        {
            Debug.LogError($"[{name}] ServingStationInteractable requires a ServingStation component!");
        }

        // Set as universal interaction
        interactionType = InteractionType.Universal;
        interactionPrompt = "Serve Food";
    }

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        // Can only interact if:
        // 1. Station has a customer
        // 2. Player is carrying a food item
        if (!servingStation.HasCustomer)
        {
            return false;
        }

        if (!playerEnd.IsCarryingItems)
        {
            return false;
        }

        // Check if player is carrying a food item
        GameObject carriedItem = playerEnd.HeldObjects[playerEnd.HeldObjects.Count - 1];
        FoodItem foodItem = carriedItem.GetComponent<FoodItem>();

        return foodItem != null && foodItem.HasValidFoodData;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        // Get the food item the player is carrying
        if (!playerEnd.IsCarryingItems)
        {
            DebugLog("Player is not carrying anything");
            return false;
        }

        GameObject carriedItem = playerEnd.HeldObjects[playerEnd.HeldObjects.Count - 1];
        FoodItem foodItem = carriedItem.GetComponent<FoodItem>();

        if (foodItem == null)
        {
            DebugLog("Player is not carrying a food item");
            return false;
        }

        // Try to serve the customer
        bool serveSuccessful = servingStation.TryServeCustomer(foodItem, playerEnd);

        if (serveSuccessful)
        {
            // Remove the item from player's inventory
            playerEnd.DropObject(carriedItem, servingStation.GetPlacementPosition());
            DebugLog($"Successfully served customer");
        }
        else
        {
            DebugLog("Failed to serve customer");
        }

        return serveSuccessful;
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!servingStation.HasCustomer)
        {
            return "No customer";
        }

        if (!playerEnd.IsCarryingItems)
        {
            return "Need food";
        }

        GameObject carriedItem = playerEnd.HeldObjects[playerEnd.HeldObjects.Count - 1];
        FoodItem foodItem = carriedItem.GetComponent<FoodItem>();

        if (foodItem == null || !foodItem.HasValidFoodData)
        {
            return "Not food";
        }

        // Show if it's the correct food
        if (foodItem.FoodData == servingStation.RequestedFood)
        {
            return "Serve Food";
        }
        else
        {
            return "Wrong food";
        }
    }
}
