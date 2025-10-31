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
        // Can interact if station has a customer waiting
        // This allows the station to highlight and alert the player that work needs to be done
        if (!servingStation.HasCustomer)
        {
            return false;
        }

        // FIXED: Allow interaction even if player has empty hands
        // This makes the station highlight when a customer is waiting, alerting the player
        // The station will show different prompts based on what the player is carrying

        // If player has empty hands, still return true to show there's a customer waiting
        if (!playerEnd.IsCarryingItems)
        {
            return true;
        }

        // If player is carrying something, check if it's food
        GameObject carriedItem = playerEnd.HeldObjects[playerEnd.HeldObjects.Count - 1];
        FoodItem foodItem = carriedItem.GetComponent<FoodItem>();

        // Allow interaction with any food item or no item
        // GetInteractionPrompt will show if it's the correct food
        return foodItem != null && foodItem.HasValidFoodData;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        // If player has empty hands, they can't actually serve - just highlight to show customer waiting
        if (!playerEnd.IsCarryingItems)
        {
            DebugLog("Player has empty hands - station highlighting to show customer is waiting");
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
            DebugLog($"Successfully served customer with {foodItem.FoodData.DisplayName}");

            // FIXED: Refresh player detection after serving (similar to processing stations)
            // This ensures the player's interaction state updates properly
            PlayerEndDetectionRefresher.RefreshNearStation(transform, name);
        }
        else
        {
            DebugLog("Failed to serve customer - wrong food or other issue");
        }

        return serveSuccessful;
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!servingStation.HasCustomer)
        {
            return "No customer";
        }

        // IMPROVED: Show what food the customer wants when player has empty hands
        if (!playerEnd.IsCarryingItems)
        {
            if (servingStation.RequestedFood != null)
            {
                return $"Customer wants: {servingStation.RequestedFood.DisplayName}";
            }
            return "Customer waiting";
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
            return $"Serve {foodItem.FoodData.DisplayName}";
        }
        else
        {
            return $"Wrong food (wants {servingStation.RequestedFood.DisplayName})";
        }
    }
}
