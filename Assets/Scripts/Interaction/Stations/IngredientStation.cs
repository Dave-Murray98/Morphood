using UnityEngine;

/// <summary>
/// A simple ingredient dispenser station that provides unlimited quantities of a specific food item.
/// When a player interacts with it, they receive the specified ingredient in their hands.
/// Both players can interact with this station.
/// </summary>
public class IngredientStation : BaseStation
{
    [Header("Ingredient Station Settings")]
    [SerializeField] private FoodItemData ingredientToDispense;
    [Tooltip("The food item that this station will provide when players interact with it")]

    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [Tooltip("Offset from the placement point where the ingredient will spawn")]

    /// <summary>
    /// Get the food item data this station dispenses
    /// </summary>
    public FoodItemData IngredientData => ingredientToDispense;

    /// <summary>
    /// Check if this station has a valid ingredient assigned
    /// </summary>
    public bool HasValidIngredient => ingredientToDispense != null;

    protected override void Initialize()
    {
        // Set up as an ingredient station
        stationType = StationType.Plain; // Use Plain type since Ingredient isn't in the enum
        stationName = string.IsNullOrEmpty(stationName) || stationName == "Station"
            ? "Ingredient Station"
            : stationName;

        // Both players can interact with ingredient stations
        allowPlayer1Interaction = true;
        allowPlayer2Interaction = true;

        // Ingredient stations don't store items, so set capacity to 0
        maxItemCapacity = 0;

        base.Initialize();

        // Validate that we have an ingredient assigned
        if (ingredientToDispense == null)
        {
            Debug.LogWarning($"[{stationName}] No ingredient assigned! This station won't work until you assign a FoodItemData.");
        }
        else
        {
            DebugLog($"Ingredient station ready to dispense: {ingredientToDispense.DisplayName}");
        }
    }

    /// <summary>
    /// Spawn and return the ingredient for a player to pick up
    /// </summary>
    /// <param name="playerEnd">The player requesting the ingredient</param>
    /// <returns>The spawned food item GameObject, or null if spawning failed</returns>
    public GameObject DispenseIngredient(PlayerEnd playerEnd)
    {
        if (!HasValidIngredient)
        {
            DebugLog("Cannot dispense - no ingredient assigned");
            return null;
        }

        if (FoodManager.Instance == null)
        {
            Debug.LogError($"[{stationName}] FoodManager not found! Cannot spawn ingredient.");
            return null;
        }

        // Calculate spawn position
        Vector3 spawnPosition = GetPlacementPosition() + spawnOffset;

        // Spawn the ingredient using FoodManager
        FoodItem spawnedFood = FoodManager.Instance.SpawnFoodItem(ingredientToDispense, spawnPosition);

        if (spawnedFood == null)
        {
            Debug.LogError($"[{stationName}] Failed to spawn {ingredientToDispense.DisplayName}");
            return null;
        }

        DebugLog($"Dispensed {ingredientToDispense.DisplayName} for Player {playerEnd.PlayerNumber}");
        return spawnedFood.gameObject;
    }

    /// <summary>
    /// Ingredient stations don't accept items for placement
    /// </summary>
    protected override bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        return false; // Ingredient stations are dispensers only, not storage
    }

    /// <summary>
    /// Override PlaceItem to prevent items from being placed on this station
    /// </summary>
    public override bool PlaceItem(GameObject item, PlayerEnd playerEnd)
    {
        DebugLog("Ingredient stations cannot store items - they only dispense ingredients");
        return false;
    }

    /// <summary>
    /// Override RemoveItem since there's never an item on this station
    /// </summary>
    public override GameObject RemoveItem(PlayerEnd playerEnd)
    {
        DebugLog("Ingredient stations don't store items to remove");
        return null;
    }

    #region Debug and Validation

    protected override Color GetStationColor()
    {
        // Use a distinct color for ingredient stations
        return Color.green;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        // Update station name based on assigned ingredient
        if (ingredientToDispense != null && Application.isPlaying)
        {
            stationName = $"{ingredientToDispense.DisplayName} Station";
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw spawn position indicator
        if (itemPlacementPoint != null)
        {
            Gizmos.color = HasValidIngredient ? Color.green : Color.red;
            Vector3 spawnPos = GetPlacementPosition() + spawnOffset;
            Gizmos.DrawWireSphere(spawnPos, 0.15f);

#if UNITY_EDITOR
            // Show ingredient name in scene view
            if (ingredientToDispense != null)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2.5f,
                    ingredientToDispense.DisplayName
                );
            }
            else
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2.5f,
                    "NO INGREDIENT!"
                );
            }
#endif
        }
    }

    #endregion
}
