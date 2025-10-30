using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Manages a pool of food item GameObjects to avoid frequent instantiation and destruction.
/// This improves performance by reusing existing objects and just updating their data.
/// </summary>
public class FoodItemPool : MonoBehaviour
{
    [Header("Pool Configuration")]
    [SerializeField] private GameObject foodItemPrefab;
    [Tooltip("The prefab used to create food items (should have FoodItem + FoodItemInteractable + required components)")]

    [SerializeField] private int initialPoolSize = 20;
    [Tooltip("Number of food items to pre-create in the pool")]

    [SerializeField] private int maxPoolSize = 50;
    [Tooltip("Maximum number of items the pool can hold (prevents unlimited growth)")]

    [SerializeField] private bool allowGrowth = true;
    [Tooltip("Whether the pool can create new items if all are in use")]

    [Header("Pool Organization")]
    [SerializeField] private Transform poolParent;
    [Tooltip("Parent transform for inactive pooled items (for organization)")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showPoolStats = false;

    // Pool management
    private Queue<FoodItem> availableItems = new Queue<FoodItem>();
    [ShowInInspector] private HashSet<FoodItem> allPooledItems = new HashSet<FoodItem>();
    private int nextItemId = 1;

    // Pool statistics
    private int totalCreated = 0;
    private int totalRecycled = 0;
    private int peakActiveItems = 0;

    // Public properties
    public int AvailableCount => availableItems.Count;
    public int TotalPooledItems => allPooledItems.Count;
    public int ActiveItems => TotalPooledItems - AvailableCount;
    public bool HasAvailableItems => availableItems.Count > 0;

    #region Initialization

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Validate setup
        if (foodItemPrefab == null)
        {
            Debug.LogError("[FoodItemPool] No food item prefab assigned! Pool cannot function.");
            return;
        }

        ValidatePrefab();
        SetupPoolParent();
        PreCreatePoolItems();

        DebugLog($"Food item pool initialized with {initialPoolSize} items");
    }

    private void ValidatePrefab()
    {
        // Ensure the prefab has required components
        FoodItem foodItem = foodItemPrefab.GetComponent<FoodItem>();
        FoodItemInteractable interactable = foodItemPrefab.GetComponent<FoodItemInteractable>();

        if (foodItem == null)
        {
            Debug.LogError("[FoodItemPool] Prefab is missing FoodItem component!");
        }

        if (interactable == null)
        {
            Debug.LogError("[FoodItemPool] Prefab is missing FoodItemInteractable component!");
        }
    }

    private void SetupPoolParent()
    {
        if (poolParent == null)
        {
            GameObject poolParentObj = new GameObject("Food Item Pool");
            poolParentObj.transform.SetParent(transform);
            poolParent = poolParentObj.transform;
        }
    }

    private void PreCreatePoolItems()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            FoodItem item = CreateNewPoolItem();
            if (item != null)
            {
                ReturnToPool(item);
            }
        }

        DebugLog($"Pre-created {initialPoolSize} pool items");
    }

    #endregion

    #region Pool Management

    /// <summary>
    /// Get a food item from the pool, creating a new one if necessary and allowed
    /// </summary>
    /// <returns>An available FoodItem, or null if none available and growth not allowed</returns>
    public FoodItem GetFromPool()
    {
        FoodItem item = null;

        // Try to get from available pool first
        if (availableItems.Count > 0)
        {
            item = availableItems.Dequeue();
            DebugLog($"Retrieved item from pool. Available: {availableItems.Count}");
        }
        // Create new item if growth is allowed and we haven't hit the limit
        else if (allowGrowth && allPooledItems.Count < maxPoolSize)
        {
            item = CreateNewPoolItem();
            DebugLog($"Created new pool item. Total pooled: {allPooledItems.Count}");
        }
        else
        {
            Debug.LogWarning("[FoodItemPool] No items available in pool and cannot create more!");
            return null;
        }

        if (item != null)
        {
            // Activate the item
            item.gameObject.SetActive(true);

            // Ensure the item is properly set up for use
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }

            Collider col = item.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }

            // Ensure it's marked as available for interaction
            BaseInteractable interactable = item.GetComponent<BaseInteractable>();
            if (interactable != null)
            {
                interactable.SetAvailable(true);
            }

            // Update statistics
            int currentActive = ActiveItems;
            if (currentActive > peakActiveItems)
            {
                peakActiveItems = currentActive;
            }
        }

        return item;
    }

    /// <summary>
    /// Return a food item to the pool for reuse
    /// </summary>
    /// <param name="item">The food item to return to the pool</param>
    public void ReturnToPool(FoodItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("[FoodItemPool] Attempted to return null item to pool");
            return;
        }

        if (!allPooledItems.Contains(item))
        {
            Debug.LogWarning($"[FoodItemPool] Attempted to return item that doesn't belong to this pool: {item.name}");
            return;
        }

        if (availableItems.Contains(item))
        {
            Debug.LogWarning($"[FoodItemPool] Item is already in the available pool: {item.name}");
            return;
        }

        // Reset the item state
        ResetPooledItem(item);

        // Add back to available pool
        availableItems.Enqueue(item);
        totalRecycled++;

        DebugLog($"Returned item to pool: {item.name}. Available: {availableItems.Count}");
    }

    /// <summary>
    /// Check if a food item belongs to this pool
    /// </summary>
    /// <param name="item">The food item to check</param>
    /// <returns>True if the item belongs to this pool</returns>
    public bool IsPooledItem(FoodItem item)
    {
        return item != null && allPooledItems.Contains(item);
    }

    #endregion

    #region Item Creation and Reset

    /// <summary>
    /// Create a new food item for the pool
    /// </summary>
    /// <returns>The created FoodItem component</returns>
    private FoodItem CreateNewPoolItem()
    {
        if (foodItemPrefab == null) return null;

        // Instantiate the prefab
        GameObject newObj = Instantiate(foodItemPrefab, poolParent);
        newObj.name = $"PooledFoodItem_{nextItemId++}";

        // Get the FoodItem component
        FoodItem foodItem = newObj.GetComponent<FoodItem>();
        if (foodItem == null)
        {
            Debug.LogError("[FoodItemPool] Created item doesn't have FoodItem component!");
            Destroy(newObj);
            return null;
        }

        // Mark as pooled item
        foodItem.SetPooledStatus(true);

        // Add to our tracking
        allPooledItems.Add(foodItem);
        totalCreated++;

        // Start inactive
        newObj.SetActive(false);

        return foodItem;
    }

    /// <summary>
    /// Reset a pooled item to a clean state for reuse
    /// </summary>
    /// <param name="item">The food item to reset</param>
    private void ResetPooledItem(FoodItem item)
    {
        if (item == null) return;

        GameObject obj = item.gameObject;

        // Deactivate the object
        obj.SetActive(false);

        // Reset transform
        obj.transform.SetParent(poolParent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        // Reset physics - handle kinematic state properly
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Only reset velocities if the rigidbody is not kinematic
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Ensure rigidbody is not kinematic for normal physics
            rb.isKinematic = false;
        }

        // Reset collider
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        // Reset pickupable state
        if (item.IsPickedUp)
        {
            // If the item is still being held, we need to force drop it
            // This should be handled by the calling code, but let's be safe
            Debug.LogWarning($"[FoodItemPool] Returning item that is still picked up: {item.name}");
        }

        // Clear any highlighting
        BaseInteractable interactable = obj.GetComponent<BaseInteractable>();
        if (interactable != null && interactable.IsHighlighted)
        {
            interactable.StopHighlighting();
        }

        // Reset availability
        interactable?.SetAvailable(true);

        // Reset the FoodItem's pooling state
        item.ResetForPooling();

        DebugLog($"Reset pooled item: {item.name}");
    }

    #endregion

    #region Pool Statistics and Utilities

    /// <summary>
    /// Clear all items from the pool (useful for level resets)
    /// </summary>
    public void ClearPool()
    {
        // Destroy all pooled items
        foreach (FoodItem item in allPooledItems)
        {
            if (item != null && item.gameObject != null)
            {
                Destroy(item.gameObject);
            }
        }

        // Reset collections and stats
        availableItems.Clear();
        allPooledItems.Clear();
        totalCreated = 0;
        totalRecycled = 0;
        peakActiveItems = 0;
        nextItemId = 1;

        DebugLog("Cleared all items from pool");
    }

    /// <summary>
    /// Get detailed statistics about the pool
    /// </summary>
    /// <returns>String containing pool statistics</returns>
    public string GetPoolStats()
    {
        return $"Food Item Pool Stats:\n" +
               $"- Total Items Created: {totalCreated}\n" +
               $"- Total Items Recycled: {totalRecycled}\n" +
               $"- Current Pool Size: {TotalPooledItems}\n" +
               $"- Available for Use: {AvailableCount}\n" +
               $"- Currently Active: {ActiveItems}\n" +
               $"- Peak Active Items: {peakActiveItems}\n" +
               $"- Max Pool Size: {maxPoolSize}";
    }

    /// <summary>
    /// Force the pool to pre-create additional items up to the maximum
    /// </summary>
    public void WarmUpPool()
    {
        int itemsToCreate = maxPoolSize - TotalPooledItems;

        for (int i = 0; i < itemsToCreate; i++)
        {
            FoodItem item = CreateNewPoolItem();
            if (item != null)
            {
                ReturnToPool(item);
            }
        }

        DebugLog($"Warmed up pool - added {itemsToCreate} items");
    }

    #endregion

    #region Debug and Validation

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[FoodItemPool] {message}");
    }

    private void OnValidate()
    {
        // Ensure reasonable values
        initialPoolSize = Mathf.Max(1, initialPoolSize);
        maxPoolSize = Mathf.Max(initialPoolSize, maxPoolSize);
    }

    /// <summary>
    /// Log current pool statistics (useful for debugging)
    /// </summary>
    [ContextMenu("Log Pool Stats")]
    public void LogPoolStats()
    {
        Debug.Log($"[FoodItemPool] {GetPoolStats()}");
    }

    private void OnGUI()
    {
        if (showPoolStats && Application.isPlaying)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label("Food Item Pool Stats:", GUI.skin.box);
            GUILayout.Label($"Available: {AvailableCount}");
            GUILayout.Label($"Active: {ActiveItems}");
            GUILayout.Label($"Total: {TotalPooledItems}");
            GUILayout.Label($"Peak Active: {peakActiveItems}");
            GUILayout.EndArea();
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        ClearPool();
    }

    #endregion
}