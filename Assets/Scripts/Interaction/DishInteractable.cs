using UnityEngine;

/// <summary>
/// Interactable wrapper for Dish to allow players to add/remove ingredients.
/// Handles the connection between the interaction system and dish functionality.
/// </summary>
[RequireComponent(typeof(Dish))]
public class DishInteractable : BaseInteractable
{
    [Header("Dish Interaction")]
    [SerializeField] private Dish dish;

    [Header("Interaction Settings")]
    [SerializeField] private bool allowIngredientRemoval = true;
    [Tooltip("If true, players can remove ingredients from the dish by interacting when carrying nothing")]

    [SerializeField] private bool allowDirectPickup = true;
    [Tooltip("If true, players can pick up the entire dish. If false, only ingredient management")]

    protected override void Awake()
    {
        base.Awake();

        if (dish == null)
            dish = GetComponent<Dish>();

        // Set up as universal interaction (both players can use dishes)
        interactionType = InteractionType.Universal;
        interactionPriority = 2; // Higher than basic items, lower than stations
    }

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        if (dish == null) return false;

        // If player wants to pick up the dish directly
        if (allowDirectPickup && ShouldPickupDish(playerEnd))
        {
            return playerEnd.HasFreeHands;
        }

        // If player wants to add an ingredient to the dish
        if (playerEnd.IsCarryingItems && CanAddCurrentItem(playerEnd))
        {
            return true;
        }

        // If player wants to remove an ingredient from the dish
        if (allowIngredientRemoval && playerEnd.HasFreeHands && dish.IngredientCount > 0)
        {
            return true;
        }

        return false;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (dish == null) return false;

        // Handle dish pickup
        if (allowDirectPickup && ShouldPickupDish(playerEnd))
        {
            return TryPickupDish(playerEnd);
        }

        // Handle adding ingredients
        if (playerEnd.IsCarryingItems)
        {
            return TryAddIngredientToDish(playerEnd);
        }

        // Handle removing ingredients
        if (allowIngredientRemoval && playerEnd.HasFreeHands && dish.IngredientCount > 0)
        {
            return TryRemoveIngredientFromDish(playerEnd);
        }

        return false;
    }

    #region Dish Management

    /// <summary>
    /// Determine if the player should pick up the dish vs manage ingredients
    /// </summary>
    private bool ShouldPickupDish(PlayerEnd playerEnd)
    {
        // Pick up dish if:
        // 1. Player has free hands AND
        // 2. Either dish is empty OR completed meal OR player indicates they want the whole dish

        if (!playerEnd.HasFreeHands) return false;

        // For now, simple logic: pick up if empty or completed
        return dish.IsEmpty || dish.IsCompletedMeal;
    }

    /// <summary>
    /// Try to pick up the entire dish
    /// </summary>
    private bool TryPickupDish(PlayerEnd playerEnd)
    {
        if (!playerEnd.HasFreeHands)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} hands are full, cannot pick up dish");
            return false;
        }

        bool pickupSuccessful = playerEnd.PickUpObject(gameObject);

        if (pickupSuccessful)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} picked up dish: {dish.ItemName}");
            return true;
        }

        DebugLog($"Failed to pick up dish");
        return false;
    }

    /// <summary>
    /// Try to add the player's carried item to the dish
    /// </summary>
    private bool TryAddIngredientToDish(PlayerEnd playerEnd)
    {
        if (!playerEnd.IsCarryingItems)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} is not carrying anything");
            return false;
        }

        GameObject carriedItem = playerEnd.HeldObjects[playerEnd.HeldObjects.Count - 1];
        CookingIngredient ingredient = carriedItem.GetComponent<CookingIngredient>();

        if (ingredient == null)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} is not carrying an ingredient");
            return false;
        }

        if (!dish.CanAddIngredient(ingredient))
        {
            DebugLog($"Cannot add {ingredient.ItemName} to dish");
            return false;
        }

        // Remove from player's inventory
        bool removedSuccessfully = playerEnd.DropObject(carriedItem);
        if (!removedSuccessfully)
        {
            DebugLog($"Failed to remove ingredient from player inventory");
            return false;
        }

        // Add to dish
        bool addedToDish = dish.TryAddIngredient(ingredient);
        if (!addedToDish)
        {
            // Failed to add to dish, give it back to player
            playerEnd.PickUpObject(carriedItem);
            DebugLog($"Failed to add ingredient to dish, returned to player");
            return false;
        }

        DebugLog($"Player {playerEnd.PlayerNumber} added {ingredient.ItemName} to dish");
        return true;
    }

    /// <summary>
    /// Try to remove an ingredient from the dish
    /// </summary>
    private bool TryRemoveIngredientFromDish(PlayerEnd playerEnd)
    {
        if (!playerEnd.HasFreeHands)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} hands are full, cannot remove ingredient");
            return false;
        }

        if (dish.IsEmpty)
        {
            DebugLog($"Dish is empty, nothing to remove");
            return false;
        }

        // Remove the last added ingredient
        CookingIngredient removedIngredient = dish.RemoveLastIngredient();
        if (removedIngredient == null)
        {
            DebugLog($"Failed to remove ingredient from dish");
            return false;
        }

        // Give to player
        bool pickedUpSuccessfully = playerEnd.PickUpObject(removedIngredient.gameObject);
        if (!pickedUpSuccessfully)
        {
            // Failed to give to player, put it back in dish
            dish.TryAddIngredient(removedIngredient);
            DebugLog($"Failed to give ingredient to player, returned to dish");
            return false;
        }

        DebugLog($"Player {playerEnd.PlayerNumber} removed {removedIngredient.ItemName} from dish");
        return true;
    }

    /// <summary>
    /// Check if the player's current item can be added to the dish
    /// </summary>
    private bool CanAddCurrentItem(PlayerEnd playerEnd)
    {
        if (!playerEnd.IsCarryingItems) return false;

        GameObject carriedItem = playerEnd.HeldObjects[playerEnd.HeldObjects.Count - 1];
        CookingIngredient ingredient = carriedItem.GetComponent<CookingIngredient>();

        if (ingredient == null) return false;

        return dish.CanAddIngredient(ingredient);
    }

    #endregion

    #region Interaction Prompts

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        // Dish pickup prompts
        if (allowDirectPickup && ShouldPickupDish(playerEnd))
        {
            if (dish.IsCompletedMeal)
                return $"Pick up {dish.DetectedMeal.Name}";
            else
                return $"Pick up {dish.ItemName}";
        }

        // Ingredient management prompts
        if (playerEnd.IsCarryingItems && CanAddCurrentItem(playerEnd))
        {
            GameObject carriedItem = playerEnd.HeldObjects[playerEnd.HeldObjects.Count - 1];
            CookingIngredient ingredient = carriedItem.GetComponent<CookingIngredient>();
            return $"Add {ingredient.ItemName} to dish";
        }

        if (allowIngredientRemoval && playerEnd.HasFreeHands && dish.IngredientCount > 0)
        {
            return "Remove ingredient";
        }

        return "Interact with dish";
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (dish == null) return "Dish not available";

        if (allowDirectPickup && ShouldPickupDish(playerEnd) && !playerEnd.HasFreeHands)
            return "Hands full";

        if (playerEnd.IsCarryingItems && !CanAddCurrentItem(playerEnd))
        {
            if (!dish.HasSpace)
                return "Dish is full";
            else
                return "Cannot add this item";
        }

        if (allowIngredientRemoval && playerEnd.HasFreeHands && dish.IsEmpty)
            return "Dish is empty";

        return base.GetUnavailablePrompt(playerEnd);
    }

    #endregion

    #region State Monitoring

    /// <summary>
    /// Update availability based on dish state
    /// </summary>
    private void Update()
    {
        if (dish == null) return;

        // Dish should be available if it can be interacted with in any way
        bool shouldBeAvailable = true; // Dishes are generally always interactable

        if (isCurrentlyAvailable != shouldBeAvailable)
        {
            SetAvailable(shouldBeAvailable);
        }

        // Update interaction prompt dynamically if someone is nearby
        // (The interaction system will handle checking CanInteract)
    }

    #endregion

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw dish-specific indicators
        if (dish != null)
        {
            Color dishColor = GetDishStateColor();
            Gizmos.color = dishColor;
            Vector3 dishIndicator = transform.position + Vector3.up * 3.2f;
            Gizmos.DrawCube(dishIndicator, Vector3.one * 0.15f);

            // Draw ingredient count indicator
            if (dish.IngredientCount > 0)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < dish.IngredientCount; i++)
                {
                    Vector3 ingredientIndicator = dishIndicator + Vector3.right * (i * 0.2f - 0.3f) + Vector3.up * 0.3f;
                    Gizmos.DrawCube(ingredientIndicator, Vector3.one * 0.08f);
                }
            }

            // Draw completed meal indicator
            if (dish.IsCompletedMeal)
            {
                Gizmos.color = Color.green;
                Vector3 mealIndicator = dishIndicator + Vector3.up * 0.5f;
                Gizmos.DrawWireSphere(mealIndicator, 0.2f);
            }
        }

#if UNITY_EDITOR
        // Show dish info
        if (Application.isPlaying && dish != null)
        {
            string info = $"{dish.ItemName}\n{dish.State}\n{dish.IngredientCount} ingredients";
            if (dish.IsCompletedMeal)
                info += $"\n{dish.DetectedMeal.Name} (Score: {dish.DetectedMeal.Score})";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4f, info);
        }
#endif
    }

    private Color GetDishStateColor()
    {
        if (dish == null) return Color.gray;

        switch (dish.State)
        {
            case DishState.Empty: return Color.white;
            case DishState.HasIngredients: return Color.yellow;
            case DishState.CompletedMeal: return Color.green;
            default: return Color.gray;
        }
    }

    #endregion

    #region Validation

    protected override void OnValidate()
    {
        base.OnValidate();

        // Ensure correct interaction type
        interactionType = InteractionType.Universal;

        // Auto-find dish component
        if (dish == null)
        {
            dish = GetComponent<Dish>();
        }

        // Ensure we have the required component
        if (dish == null)
        {
            Debug.LogWarning($"[{name}] DishInteractable requires a Dish component!");
        }
    }

    #endregion
}