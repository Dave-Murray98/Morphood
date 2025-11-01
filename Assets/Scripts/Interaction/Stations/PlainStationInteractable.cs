using UnityEngine;

/// <summary>
/// Interactable wrapper for PlainStation to allow direct player interaction.
/// Now handles automatic food combination when compatible food items are placed together.
/// </summary>
[System.Serializable]
public class PlainStationInteractable : BaseInteractable
{
    [SerializeField] private PlainStation plainStation;

    [Header("Combination Feedback")]
    [SerializeField] private bool showCombinationPrompts = true;
    [Tooltip("Whether to show special prompts when food items can be combined")]

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
        // 2. Player is carrying something and station has space (to place), OR
        // 3. Player is carrying a food item that can combine with what's on the station

        bool canRetrieve = playerEnd.HasFreeHands && plainStation.IsOccupied;
        bool canPlace = playerEnd.IsCarryingItems && plainStation.HasSpace;
        bool canCombine = CanCombineWithStation(playerEnd);

        return canRetrieve || canPlace || canCombine;
    }

    /// <summary>
    /// Check if the player is carrying food that can combine with what's on the station
    /// </summary>
    /// <param name="playerEnd">The player to check</param>
    /// <returns>True if combination is possible</returns>
    private bool CanCombineWithStation(PlayerEnd playerEnd)
    {
        if (!playerEnd.IsCarryingItems || !plainStation.IsOccupied)
            return false;

        // Check if any carried item can combine with the station item
        foreach (GameObject carriedObj in playerEnd.HeldObjects)
        {
            FoodItem carriedFoodItem = carriedObj.GetComponent<FoodItem>();
            if (carriedFoodItem != null && plainStation.CanAcceptForCombination(carriedFoodItem))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get the food item the player is carrying that can combine with the station item
    /// </summary>
    /// <param name="playerEnd">The player to check</param>
    /// <returns>The combinable food item, or null if none found</returns>
    private FoodItem GetCombinableFoodItem(PlayerEnd playerEnd)
    {
        if (!playerEnd.IsCarryingItems || !plainStation.IsOccupied)
            return null;

        foreach (GameObject carriedObj in playerEnd.HeldObjects)
        {
            FoodItem carriedFoodItem = carriedObj.GetComponent<FoodItem>();
            if (carriedFoodItem != null && plainStation.CanAcceptForCombination(carriedFoodItem))
            {
                return carriedFoodItem;
            }
        }

        return null;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        if (plainStation == null) return false;

        // Priority 1: If player has free hands and station has item - retrieve it
        if (playerEnd.HasFreeHands && plainStation.IsOccupied)
        {
            return plainStation.TryRetrieveItem(playerEnd);
        }

        // Priority 2: If player is carrying food that can combine - attempt combination
        if (CanCombineWithStation(playerEnd))
        {
            FoodItem combinableFoodItem = GetCombinableFoodItem(playerEnd);
            if (combinableFoodItem != null)
            {
                DebugLog($"Attempting to combine {combinableFoodItem.FoodData.DisplayName} with station item");

                // Attempt combination directly without dropping first
                bool combinationSuccessful = plainStation.TryCombineWithStationItem(combinableFoodItem, playerEnd);
                if (combinationSuccessful)
                {
                    // Remove the item from player's hands after successful combination
                    playerEnd.DropObject(combinableFoodItem.gameObject);
                    //DebugLog($"Successfully combined {combinableFoodItem.FoodData.DisplayName} with station item");
                    return true;
                }
                else
                {
                    DebugLog("Combination failed");
                    return false;
                }
            }
        }

        // Priority 3: If player is carrying something and station has space - this will be handled by PlayerEnd's drop logic
        // This interaction just confirms that the station can accept items
        return false; // Let PlayerEnd handle the placement
    }

    public override string GetInteractionPrompt(PlayerEnd playerEnd)
    {
        if (!CanInteract(playerEnd))
            return GetUnavailablePrompt(playerEnd);

        // If player has free hands and station has item - show retrieval prompt
        if (playerEnd.HasFreeHands && plainStation.IsOccupied)
        {
            return $"Pick up {plainStation.CurrentItem.name}";
        }

        // If player can combine items - show combination prompt
        if (showCombinationPrompts && CanCombineWithStation(playerEnd))
        {
            FoodItem combinableFoodItem = GetCombinableFoodItem(playerEnd);
            if (combinableFoodItem != null)
            {
                FoodItem stationFoodItem = plainStation.CurrentItem?.GetComponent<FoodItem>();
                if (stationFoodItem != null)
                {
                    return $"Combine {combinableFoodItem.FoodData.DisplayName} + {stationFoodItem.FoodData.DisplayName}";
                }
                else
                {
                    return $"Combine {combinableFoodItem.FoodData.DisplayName}";
                }
            }
        }

        // If player is carrying something and station has space - show placement prompt
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

        if (playerEnd.IsCarryingItems && !plainStation.HasSpace && !CanCombineWithStation(playerEnd))
            return "Station is full";

        if (playerEnd.HasFreeHands && !plainStation.IsOccupied)
            return "Nothing to pick up";

        return base.GetUnavailablePrompt(playerEnd);
    }

    #region Debug

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw combination indicator if combination is possible
        if (Application.isPlaying && plainStation != null && plainStation.IsOccupied)
        {
            // Check if any players are carrying items that could combine
            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                bool canCombineWithSomeone = false;

                for (int i = 1; i <= 2; i++)
                {
                    PlayerEnd playerEnd = playerController.GetPlayerEnd(i);
                    if (playerEnd != null && CanCombineWithStation(playerEnd))
                    {
                        canCombineWithSomeone = true;
                        break;
                    }
                }

                if (canCombineWithSomeone)
                {
                    // Draw a pulsing indicator for potential combination
                    Gizmos.color = Color.green;
                    Vector3 indicatorPos = transform.position + Vector3.up * 3f;
                    float pulseSize = 0.2f + Mathf.Sin(Time.time * 3f) * 0.1f;
                    Gizmos.DrawWireSphere(indicatorPos, pulseSize);
                }
            }
        }
    }

    #endregion
}