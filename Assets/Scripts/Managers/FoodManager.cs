using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Centralized manager for food system operations including combination processing,
/// food item spawning, and database management. All food items in the scene are
/// managed as children of this object for organization.
/// </summary>
public class FoodManager : MonoBehaviour
{
    public static FoodManager Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private FoodCombinationDatabase combinationDatabase;
    [Tooltip("The database containing all possible food combinations")]

    [Header("Food Item Prefab")]
    [SerializeField] private GameObject foodItemPrefab;
    [Tooltip("The prefab used to create new food items (should have FoodItem + FoodItemInteractable + required components)")]

    [Header("Spawning Settings")]
    [SerializeField] private bool organizeFoodItemsAsChildren = true;
    [Tooltip("Whether to make spawned food items children of this manager for organization")]

    [SerializeField] private float spawnHeight = 0.1f;
    [Tooltip("Height offset when spawning combined items")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal tracking
    private List<FoodItem> activeFoodItems = new List<FoodItem>();
    private int nextFoodItemId = 1;

    #region Singleton Setup

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[FoodManager] Multiple FoodManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Initialize();
    }

    private void Initialize()
    {
        // Validate setup
        if (combinationDatabase == null)
        {
            Debug.LogError("[FoodManager] No combination database assigned! Food combinations will not work.");
        }

        if (foodItemPrefab == null)
        {
            Debug.LogError("[FoodManager] No food item prefab assigned! Cannot spawn food items.");
        }
        else
        {
            // Validate the prefab has required components
            ValidateFoodItemPrefab();
        }

        // Find any existing food items in the scene and register them
        RegisterExistingFoodItems();

        DebugLog("FoodManager initialized");
    }

    private void ValidateFoodItemPrefab()
    {
        FoodItem foodItem = foodItemPrefab.GetComponent<FoodItem>();
        FoodItemInteractable interactable = foodItemPrefab.GetComponent<FoodItemInteractable>();
        MeshRenderer meshRenderer = foodItemPrefab.GetComponent<MeshRenderer>();
        MeshCollider meshCollider = foodItemPrefab.GetComponent<MeshCollider>();
        InteractableOutline outline = foodItemPrefab.GetComponent<InteractableOutline>();

        List<string> missingComponents = new List<string>();

        if (foodItem == null) missingComponents.Add("FoodItem");
        if (interactable == null) missingComponents.Add("FoodItemInteractable");
        if (meshRenderer == null) missingComponents.Add("MeshRenderer");
        if (meshCollider == null) missingComponents.Add("MeshCollider");
        if (outline == null) missingComponents.Add("InteractableOutline");

        if (missingComponents.Count > 0)
        {
            Debug.LogError($"[FoodManager] Food item prefab is missing required components: {string.Join(", ", missingComponents)}");
        }
        else
        {
            DebugLog("Food item prefab validation passed");
        }
    }

    #endregion

    #region Food Item Registration

    private void RegisterExistingFoodItems()
    {
        // Find all existing food items in the scene
        FoodItem[] existingFoodItems = FindObjectsByType<FoodItem>(FindObjectsSortMode.None);

        foreach (FoodItem foodItem in existingFoodItems)
        {
            RegisterFoodItem(foodItem);
        }

        DebugLog($"Registered {existingFoodItems.Length} existing food items");
    }

    /// <summary>
    /// Register a food item with the manager
    /// </summary>
    /// <param name="foodItem">The food item to register</param>
    public void RegisterFoodItem(FoodItem foodItem)
    {
        if (foodItem == null) return;

        if (!activeFoodItems.Contains(foodItem))
        {
            activeFoodItems.Add(foodItem);

            // Make it a child if organization is enabled
            if (organizeFoodItemsAsChildren && foodItem.transform.parent != transform)
            {
                foodItem.transform.SetParent(transform);
            }

            DebugLog($"Registered food item: {foodItem.ItemName}");
        }
    }

    /// <summary>
    /// Unregister a food item from the manager
    /// </summary>
    /// <param name="foodItem">The food item to unregister</param>
    public void UnregisterFoodItem(FoodItem foodItem)
    {
        if (foodItem == null) return;

        if (activeFoodItems.Remove(foodItem))
        {
            DebugLog($"Unregistered food item: {foodItem.ItemName}");
        }
    }

    #endregion

    #region Food Combination System

    /// <summary>
    /// Try to combine two food items and create the result
    /// </summary>
    /// <param name="item1">First food item</param>
    /// <param name="item2">Second food item</param>
    /// <param name="spawnPosition">Where to spawn the combined result</param>
    /// <returns>The newly created combined food item, or null if combination failed</returns>
    public FoodItem TryCombineFoodItems(FoodItem item1, FoodItem item2, Vector3 spawnPosition)
    {
        if (item1 == null || item2 == null)
        {
            DebugLog("Cannot combine - one or both food items are null");
            return null;
        }

        if (!item1.HasValidFoodData || !item2.HasValidFoodData)
        {
            DebugLog("Cannot combine - one or both food items have invalid food data");
            return null;
        }

        if (combinationDatabase == null)
        {
            DebugLog("Cannot combine - no combination database available");
            return null;
        }

        // Try to find a combination
        FoodItemData resultData = combinationDatabase.FindCombination(item1.FoodData, item2.FoodData);

        if (resultData == null)
        {
            DebugLog($"No combination found for {item1.FoodData.DisplayName} + {item2.FoodData.DisplayName}");
            return null;
        }

        // Create the combined result
        FoodItem combinedItem = SpawnFoodItem(resultData, spawnPosition);

        if (combinedItem != null)
        {
            DebugLog($"Successfully combined {item1.FoodData.DisplayName} + {item2.FoodData.DisplayName} = {resultData.DisplayName}");

            // Remove the original items
            DestroyFoodItem(item1);
            DestroyFoodItem(item2);
        }

        return combinedItem;
    }

    /// <summary>
    /// Check if two food items can be combined
    /// </summary>
    /// <param name="item1">First food item</param>
    /// <param name="item2">Second food item</param>
    /// <returns>True if the items can be combined</returns>
    public bool CanCombineFoodItems(FoodItem item1, FoodItem item2)
    {
        if (item1 == null || item2 == null) return false;
        if (!item1.HasValidFoodData || !item2.HasValidFoodData) return false;
        if (combinationDatabase == null) return false;

        return combinationDatabase.HasCombination(item1.FoodData, item2.FoodData);
    }

    /// <summary>
    /// Check if a food item can be combined with any food items currently on a station
    /// </summary>
    /// <param name="newItem">The food item to check</param>
    /// <param name="existingItems">List of food items already on the station</param>
    /// <returns>The first compatible item found, or null if no combinations possible</returns>
    public FoodItem FindCompatibleItem(FoodItem newItem, List<FoodItem> existingItems)
    {
        if (newItem == null || !newItem.HasValidFoodData) return null;
        if (existingItems == null || existingItems.Count == 0) return null;

        foreach (FoodItem existingItem in existingItems)
        {
            if (CanCombineFoodItems(newItem, existingItem))
            {
                return existingItem;
            }
        }

        return null;
    }

    #endregion

    #region Food Item Spawning

    /// <summary>
    /// Spawn a new food item with the specified food data
    /// </summary>
    /// <param name="foodData">The food data for the new item</param>
    /// <param name="position">World position to spawn at</param>
    /// <param name="rotation">Optional rotation (defaults to identity)</param>
    /// <returns>The spawned FoodItem component, or null if spawning failed</returns>
    public FoodItem SpawnFoodItem(FoodItemData foodData, Vector3 position, Quaternion? rotation = null)
    {
        if (foodData == null)
        {
            Debug.LogError("[FoodManager] Cannot spawn food item - food data is null");
            return null;
        }

        if (foodItemPrefab == null)
        {
            Debug.LogError("[FoodManager] Cannot spawn food item - no prefab assigned");
            return null;
        }

        // Adjust spawn position with height offset
        Vector3 spawnPosition = position + Vector3.up * spawnHeight;
        Quaternion spawnRotation = rotation ?? Quaternion.identity;

        // Instantiate the prefab
        GameObject newObj = Instantiate(foodItemPrefab, spawnPosition, spawnRotation);
        newObj.name = $"FoodItem_{foodData.DisplayName}_{nextFoodItemId++}";

        // Get the FoodItem component and set its data
        FoodItem foodItem = newObj.GetComponent<FoodItem>();
        if (foodItem == null)
        {
            Debug.LogError("[FoodManager] Spawned prefab doesn't have FoodItem component!");
            Destroy(newObj);
            return null;
        }

        // Set the food data (this will update all visual components)
        foodItem.SetFoodData(foodData);

        // Update components with food data references
        UpdateFoodItemComponents(foodItem, foodData);

        // Register the new food item
        RegisterFoodItem(foodItem);

        DebugLog($"Spawned food item: {foodData.DisplayName} at {spawnPosition}");
        return foodItem;
    }

    /// <summary>
    /// Update all components on a food item with data from FoodItemData
    /// </summary>
    /// <param name="foodItem">The food item to update</param>
    /// <param name="foodData">The food data to apply</param>
    private void UpdateFoodItemComponents(FoodItem foodItem, FoodItemData foodData)
    {
        GameObject obj = foodItem.gameObject;
    }

    /// <summary>
    /// Spawn a food item at a random position within a radius
    /// </summary>
    /// <param name="foodData">The food data for the new item</param>
    /// <param name="center">Center point for random spawning</param>
    /// <param name="radius">Radius for random spawning</param>
    /// <returns>The spawned FoodItem component</returns>
    public FoodItem SpawnFoodItemRandomly(FoodItemData foodData, Vector3 center, float radius = 2f)
    {
        Vector2 randomCircle = Random.insideUnitCircle * radius;
        Vector3 spawnPosition = center + new Vector3(randomCircle.x, 0, randomCircle.y);

        return SpawnFoodItem(foodData, spawnPosition);
    }

    #endregion

    #region Food Item Destruction

    /// <summary>
    /// Safely destroy a food item and clean up references
    /// </summary>
    /// <param name="foodItem">The food item to destroy</param>
    public void DestroyFoodItem(FoodItem foodItem)
    {
        if (foodItem == null) return;

        // Unregister from manager
        UnregisterFoodItem(foodItem);

        // If the item is being carried by a player, drop it first
        if (foodItem.IsPickedUp)
        {
            // Find which player is carrying it and force drop
            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                for (int i = 1; i <= 2; i++)
                {
                    PlayerEnd playerEnd = playerController.GetPlayerEnd(i);
                    if (playerEnd != null && playerEnd.HeldObjects.Contains(foodItem.gameObject))
                    {
                        playerEnd.DropObject(foodItem.gameObject);
                        break;
                    }
                }
            }
        }

        // Destroy the GameObject
        string itemName = foodItem.FoodData?.DisplayName ?? "Unknown";
        Destroy(foodItem.gameObject);

        DebugLog($"Destroyed food item: {itemName}");
    }

    #endregion

    #region Public Utility Methods

    /// <summary>
    /// Get all currently active food items in the scene
    /// </summary>
    /// <returns>Readonly list of active food items</returns>
    public IReadOnlyList<FoodItem> GetActiveFoodItems()
    {
        // Clean up any null references
        activeFoodItems.RemoveAll(item => item == null);
        return activeFoodItems.AsReadOnly();
    }

    /// <summary>
    /// Find food items by their food data
    /// </summary>
    /// <param name="foodData">The food data to search for</param>
    /// <returns>List of food items with matching data</returns>
    public List<FoodItem> FindFoodItemsByData(FoodItemData foodData)
    {
        if (foodData == null) return new List<FoodItem>();

        return activeFoodItems.Where(item => item != null && item.FoodData == foodData).ToList();
    }

    /// <summary>
    /// Get the total count of food items in the scene
    /// </summary>
    /// <returns>Number of active food items</returns>
    public int GetFoodItemCount()
    {
        activeFoodItems.RemoveAll(item => item == null);
        return activeFoodItems.Count;
    }

    /// <summary>
    /// Clear all food items from the scene (useful for level resets)
    /// </summary>
    public void ClearAllFoodItems()
    {
        var itemsToDestroy = new List<FoodItem>(activeFoodItems);

        foreach (FoodItem item in itemsToDestroy)
        {
            if (item != null)
            {
                DestroyFoodItem(item);
            }
        }

        activeFoodItems.Clear();
        nextFoodItemId = 1;

        DebugLog("Cleared all food items from scene");
    }

    #endregion

    #region Debug and Validation

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[FoodManager] {message}");
    }

    /// <summary>
    /// Log current status of the food manager
    /// </summary>
    [ContextMenu("Log Food Manager Status")]
    public void LogStatus()
    {
        Debug.Log($"[FoodManager] Status Report:");
        Debug.Log($"  - Active Food Items: {GetFoodItemCount()}");
        Debug.Log($"  - Database Available: {combinationDatabase != null}");
        Debug.Log($"  - Prefab Available: {foodItemPrefab != null}");

        if (combinationDatabase != null)
        {
            Debug.Log($"  - Database Stats: {combinationDatabase.GetDatabaseStats()}");
        }
    }

    private void OnValidate()
    {
        if (foodItemPrefab != null && Application.isPlaying)
        {
            ValidateFoodItemPrefab();
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion
}