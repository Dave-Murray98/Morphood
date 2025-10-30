using UnityEngine;

/// <summary>
/// Interactable wrapper for IngredientStation to allow direct player interaction.
/// When a player interacts, they receive the ingredient directly in their hands.
/// </summary>
public class IngredientStationInteractable : BaseInteractable
{
    [SerializeField] private IngredientStation ingredientStation;

    protected override void Awake()
    {
        base.Awake();

        // Get the IngredientStation component if not assigned
        if (ingredientStation == null)
            ingredientStation = GetComponent<IngredientStation>();

        // Set up as universal interaction (both players can use)
        interactionType = InteractionType.Universal;
        interactionPriority = 2; // Slightly higher than regular items
    }

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        if (ingredientStation == null)
        {
            DebugLog("No IngredientStation component found");
            return false;
        }

        // Can only interact if:
        // 1. Station has a valid ingredient assigned
        // 2. Player has free hands to receive the ingredient
        bool hasValidIngredient = ingredientStation.HasValidIngredient;
        bool playerHasFreeHands = playerEnd.HasFreeHands;

        if (!hasValidIngredient)
        {
            DebugLog("Station has no valid ingredient assigned");
        }

        if (!playerHasFreeHands)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} has no free hands");
        }

        return hasValidIngredient && playerHasFreeHands;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (ingredientStation == null)
        {
            DebugLog("Cannot interact - no IngredientStation component");
            return false;
        }

        if (!ingredientStation.HasValidIngredient)
        {
            DebugLog("Cannot interact - no valid ingredient assigned");
            return false;
        }

        if (!playerEnd.HasFreeHands)
        {
            DebugLog($"Cannot interact - Player {playerEnd.PlayerNumber} has no free hands");
            return false;
        }

        // Dispense the ingredient from the station
        GameObject dispensedIngredient = ingredientStation.DispenseIngredient(playerEnd);

        if (dispensedIngredient == null)
        {
            DebugLog("Failed to dispense ingredient");
            return false;
        }

        // Immediately give it to the player
        bool pickupSuccessful = playerEnd.PickUpObject(dispensedIngredient);

        if (!pickupSuccessful)
        {
            DebugLog($"Failed to give ingredient to Player {playerEnd.PlayerNumber}");

            // If pickup failed, destroy the spawned ingredient to avoid clutter
            FoodItem foodItem = dispensedIngredient.GetComponent<FoodItem>();
            if (foodItem != null && FoodManager.Instance != null)
            {
                FoodManager.Instance.DestroyFoodItem(foodItem);
            }
            else
            {
                Destroy(dispensedIngredient);
            }

            return false;
        }

        DebugLog($"Successfully gave {ingredientStation.IngredientData.DisplayName} to Player {playerEnd.PlayerNumber}");
        return true;
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        // Show what ingredient they'll receive
        if (ingredientStation != null && ingredientStation.HasValidIngredient)
        {
            return $"Pick up {ingredientStation.IngredientData.DisplayName}";
        }

        return "Pick up ingredient";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (ingredientStation == null)
            return "Station not available";

        if (!ingredientStation.HasValidIngredient)
            return "No ingredient assigned";

        if (!playerEnd.HasFreeHands)
            return "Hands are full";

        return base.GetUnavailablePrompt(playerEnd);
    }

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw a line from the station to where the ingredient will spawn
        if (ingredientStation != null && Application.isPlaying)
        {
            Gizmos.color = ingredientStation.HasValidIngredient ? Color.green : Color.red;
            Vector3 spawnPos = ingredientStation.GetPlacementPosition();
            Gizmos.DrawLine(transform.position, spawnPos);
        }
    }

    #endregion
}
