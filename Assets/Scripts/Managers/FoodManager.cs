using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// Centralized manager for food system operations including combination processing,
/// food item spawning, and database management. Now supports object pooling for better performance.
/// All food items in the scene are managed as children of this object for organization.
/// </summary>
public class FoodManager : MonoBehaviour
{
    public static FoodManager Instance { get; private set; }

    [Header("Cooking Settings")]
    public CookingSettings cookingSettings;

    [Header("Database")]
    [SerializeField] private FoodCombinationDatabase combinationDatabase;
    [Tooltip("The database containing all possible food combinations")]

    [Header("Pooling System")]
    [SerializeField] private FoodItemPool itemPool;
    [Tooltip("The pool manager for food items")]

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

    // Statistics
    private int pooledItemsCreated = 0;

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

        // Validate pooling setup
        if (itemPool == null)
        {
            // Try to find a pool component on this GameObject
            itemPool = GetComponent<FoodItemPool>();

            if (itemPool == null)
            {
                Debug.LogError("[FoodManager] No FoodItemPool found! Add a FoodItemPool component for the food system to work.");
                return;
            }
        }

        // Find any existing food items in the scene and register them
        RegisterExistingFoodItems();

        DebugLog("FoodManager initialized with pooling system");
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

            // Make it a child if organization is enabled and it's not from the pool
            if (organizeFoodItemsAsChildren && foodItem.transform.parent != transform)
            {
                // Don't reparent pooled items - they should stay under the pool
                if (itemPool == null || !itemPool.IsPooledItem(foodItem))
                {
                    foodItem.transform.SetParent(transform);
                }
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
    /// Spawn a new food item with the specified food data using the pooling system
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

        if (itemPool == null)
        {
            Debug.LogError("[FoodManager] Cannot spawn food item - no pool available");
            return null;
        }

        FoodItem foodItem = SpawnFromPool(foodData, position, rotation);

        if (foodItem != null)
        {
            // Register the new food item
            RegisterFoodItem(foodItem);
            DebugLog($"Spawned food item: {foodData.DisplayName} at {position}");
            pooledItemsCreated++;
        }

        return foodItem;
    }

    /// <summary>
    /// Spawn a food item using the pooling system
    /// </summary>
    private FoodItem SpawnFromPool(FoodItemData foodData, Vector3 position, Quaternion? rotation)
    {
        FoodItem foodItem = itemPool.GetFromPool();
        if (foodItem == null)
        {
            DebugLog("Failed to get item from pool");
            return null;
        }

        // Adjust spawn position with height offset
        Vector3 spawnPosition = position + Vector3.up * spawnHeight;
        Quaternion spawnRotation = rotation ?? Quaternion.identity;

        // Position the item
        foodItem.transform.position = spawnPosition;
        foodItem.transform.rotation = spawnRotation;


        // Ensure the item is properly set up for interaction
        Rigidbody rb = foodItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Ensure it can be affected by physics
            rb.linearVelocity = Vector3.zero;
        }

        Collider col = foodItem.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true; // Ensure collider is enabled for interaction
        }

        // Update the food data (this will update all visual components and initialize the item)
        foodItem.SetFoodData(foodData);

        // Update the GameObject name for organization
        foodItem.gameObject.name = $"PooledFoodItem_{foodData.DisplayName}_{nextFoodItemId++}";

        // Ensure the item is marked as pooled
        foodItem.SetPooledStatus(true);

        DebugLog($"Spawned pooled food item: {foodData.DisplayName}");
        return foodItem;
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

    [Button]
    public void SpawnTestItemNearPlayer(FoodItemData foodData)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector3 spawnPosition = player.transform.position + new Vector3(Random.Range(-1f, 1f), 3, Random.Range(-1f, 1f));
            SpawnFromPool(foodData, spawnPosition, Quaternion.identity);
        }
    }

    #endregion

    #region Food Item Destruction

    /// <summary>
    /// Safely destroy a food item and clean up references using the pooling system
    /// </summary>
    /// <param name="foodItem">The food item to destroy</param>
    public void DestroyFoodItem(FoodItem foodItem)
    {
        if (foodItem == null) return;

        string itemName = foodItem.FoodData?.DisplayName ?? "Unknown";

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

        // Return to pool
        if (itemPool != null && itemPool.IsPooledItem(foodItem))
        {
            itemPool.ReturnToPool(foodItem);
            DebugLog($"Returned food item to pool: {itemName}");
        }
        else
        {
            Debug.LogWarning($"[FoodManager] Attempting to destroy non-pooled food item: {itemName}. This shouldn't happen in pool-only mode.");
            Destroy(foodItem.gameObject);
        }
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

        // Clear the pool as well
        if (itemPool != null)
        {
            itemPool.ClearPool();
        }

        DebugLog("Cleared all food items from scene");
    }

    /// <summary>
    /// Transform a food item using processing (chopping/cooking)
    /// Uses pooling for efficient transformation
    /// </summary>
    /// <param name="originalItem">The item to transform</param>
    /// <param name="processType">The type of processing</param>
    /// <param name="position">Where to place the transformed item</param>
    /// <returns>The transformed food item, or null if transformation failed</returns>
    public FoodItem TransformFoodItem(FoodItem originalItem, FoodProcessType processType, Vector3 position)
    {
        if (originalItem == null || !originalItem.HasValidFoodData)
        {
            DebugLog("Cannot transform - invalid original item");
            return null;
        }

        // Get the result data
        FoodItemData resultData = originalItem.FoodData.GetProcessingResult(processType);
        if (resultData == null)
        {
            DebugLog($"Cannot transform {originalItem.FoodData.DisplayName} using {processType} - no result data");
            return null;
        }

        // Create the transformed item
        FoodItem transformedItem = SpawnFoodItem(resultData, position);

        if (transformedItem != null)
        {
            DebugLog($"Successfully transformed {originalItem.FoodData.DisplayName} using {processType} = {resultData.DisplayName}");

            // Remove the original item
            DestroyFoodItem(originalItem);
        }

        return transformedItem;
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
        Debug.Log($"  - Items Created: {pooledItemsCreated}");
        Debug.Log($"  - Database Available: {combinationDatabase != null}");
        Debug.Log($"  - Pool Available: {itemPool != null}");

        if (itemPool != null)
        {
            Debug.Log($"  - Pool Stats: {itemPool.GetPoolStats()}");
        }

        if (combinationDatabase != null)
        {
            Debug.Log($"  - Database Stats: {combinationDatabase.GetDatabaseStats()}");
        }
    }

    private void OnValidate()
    {
        // Validate that we have a pool assigned in editor
        if (itemPool == null && GetComponent<FoodItemPool>() == null)
        {
            Debug.LogWarning("[FoodManager] No FoodItemPool assigned or found. Add a FoodItemPool component.");
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