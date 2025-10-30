using UnityEngine;

/// <summary>
/// Represents a food item in the game world. Inherits from PickupableItem to maintain 
/// all the existing pickup/drop functionality while adding food-specific behavior.
/// Now supports object pooling for better performance.
/// </summary>
[RequireComponent(typeof(FoodItemInteractable))]
public class FoodItem : PickupableItem
{
    [Header("Food Item Configuration")]
    [SerializeField] private FoodItemData foodData;
    [Tooltip("The ScriptableObject that defines this food item's properties and behavior")]

    [Header("Visual Components")]
    [SerializeField] private MeshFilter meshFilter;

    [SerializeField] private bool updateMaterialOnStart = true;
    [Tooltip("Whether to apply the food data's material to the mesh renderer on start")]

    [Header("Pooling Support")]
    [SerializeField] private bool isPooledItem = false;
    [Tooltip("Whether this item is managed by the pooling system (set automatically)")]

    // Internal components
    private FoodItemInteractable foodInteractable;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    // Pooling state
    private bool hasBeenInitialized = false;

    // Public properties
    public FoodItemData FoodData => foodData;
    public bool HasValidFoodData => foodData != null;
    public bool IsPooledItem => isPooledItem;

    protected override void Awake()
    {
        base.Awake();

        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
    }

    protected override void Start()
    {
        // Always call base start for PickupableItem functionality
        base.Start();

        // Initialize if not already done (for non-pooled items or scene-placed items)
        if (!hasBeenInitialized)
        {
            InitializeFoodItem();
        }
    }

    /// <summary>
    /// Initialize the food item (called on Start for non-pooled items, or when retrieved from pool)
    /// </summary>
    private void InitializeFoodItem()
    {
        if (hasBeenInitialized && isPooledItem)
        {
            // For pooled items, only initialize once on first use
            // Subsequent uses just update the food data
            return;
        }

        // Get the food interactable component
        foodInteractable = GetComponent<FoodItemInteractable>();
        if (foodInteractable == null)
        {
            Debug.LogError($"[FoodItem] {name} requires a FoodItemInteractable component!");
            return;
        }

        // Apply food data if available
        if (HasValidFoodData)
        {
            ApplyFoodData();
        }
        else
        {
            Debug.LogWarning($"[FoodItem] {name} has no food data assigned!");
        }

        // Register with FoodManager if available
        if (FoodManager.Instance != null)
        {
            FoodManager.Instance.RegisterFoodItem(this);
        }

        hasBeenInitialized = true;
    }

    /// <summary>
    /// Apply the visual representation and properties from the food data
    /// </summary>
    private void ApplyFoodData()
    {
        if (!HasValidFoodData) return;

        // Update the item name and type from food data
        itemName = foodData.DisplayName;

        // Update visual representation
        UpdateVisualRepresentation();

        DebugLog($"Applied food data: {foodData.DisplayName}");
    }

    /// <summary>
    /// Update the visual representation of the food item based on its data
    /// </summary>
    private void UpdateVisualRepresentation()
    {
        if (!HasValidFoodData) return;

        // Update mesh
        if (meshFilter != null && foodData.VisualMesh != null)
        {
            meshFilter.mesh = foodData.VisualMesh;
        }

        // Update material
        if (meshRenderer != null && foodData.ItemMaterial != null && updateMaterialOnStart)
        {
            meshRenderer.material = foodData.ItemMaterial;
        }

        // Update collider mesh
        if (meshCollider != null && foodData.ColliderMesh != null)
        {
            meshCollider.sharedMesh = foodData.ColliderMesh;
        }
        else if (meshCollider != null && foodData.VisualMesh != null)
        {
            // Fall back to visual mesh if no specific collider mesh
            meshCollider.sharedMesh = foodData.VisualMesh;
        }

        DebugLog($"Updated visual representation for {foodData.DisplayName}");
    }

    /// <summary>
    /// Transform this food item into another food item based on processing
    /// Returns true if the FoodManager should handle the transformation with pooling
    /// </summary>
    /// <param name="processType">The type of processing applied</param>
    /// <returns>True if transformation was initiated (handled by FoodManager)</returns>
    public bool TransformFood(FoodProcessType processType)
    {
        if (!HasValidFoodData)
        {
            DebugLog("Cannot transform food - no food data available");
            return false;
        }

        FoodItemData resultData = foodData.GetProcessingResult(processType);
        if (resultData == null)
        {
            DebugLog($"Cannot transform {foodData.DisplayName} using {processType} - no result data available");
            return false;
        }

        // Let the FoodManager handle the transformation for pooling efficiency
        if (FoodManager.Instance != null)
        {
            Vector3 currentPosition = transform.position;
            FoodItem transformedItem = FoodManager.Instance.TransformFoodItem(this, processType, currentPosition);

            if (transformedItem != null)
            {
                DebugLog($"Transformed food to {transformedItem.FoodData.DisplayName} using {processType}");
                return true;
            }
        }
        else
        {
            // Fallback: direct transformation without pooling
            foodData = resultData;
            ApplyFoodData();
            DebugLog($"Transformed food to {foodData.DisplayName} using {processType} (no pooling)");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if this food item can be processed with the specified process type
    /// </summary>
    /// <param name="processType">The type of processing to check</param>
    /// <returns>True if the food can be processed this way</returns>
    public bool CanBeProcessed(FoodProcessType processType)
    {
        if (!HasValidFoodData) return false;
        return foodData.CanBeProcessed(processType);
    }

    /// <summary>
    /// Get a description of available processing options for this food item
    /// </summary>
    /// <returns>String describing what can be done with this food</returns>
    public string GetProcessingOptions()
    {
        if (!HasValidFoodData) return "No processing options";
        return foodData.GetProcessingOptions();
    }

    /// <summary>
    /// Set new food data for this item (used by pooling system and runtime creation)
    /// </summary>
    /// <param name="newFoodData">The new food data to apply</param>
    public void SetFoodData(FoodItemData newFoodData)
    {
        if (newFoodData == null)
        {
            Debug.LogWarning($"[FoodItem] Attempted to set null food data on {name}");
            return;
        }

        foodData = newFoodData;
        ApplyFoodData();

        // Initialize if this is the first time setting data (for pooled items)
        if (!hasBeenInitialized)
        {
            InitializeFoodItem();
        }

        DebugLog($"Set food data to {foodData.DisplayName}");
    }

    /// <summary>
    /// Mark this item as pooled (called by FoodItemPool)
    /// </summary>
    /// <param name="pooled">Whether this item is pooled</param>
    public void SetPooledStatus(bool pooled)
    {
        isPooledItem = pooled;

        if (pooled)
        {
            DebugLog($"Item marked as pooled: {name}");
        }
    }

    /// <summary>
    /// Reset this food item to a clean state (used by pooling system)
    /// </summary>
    public void ResetForPooling()
    {
        // Clear food data reference
        foodData = null;

        // Reset visual state
        if (meshFilter != null)
        {
            meshFilter.mesh = null;
        }

        if (meshRenderer != null)
        {
            meshRenderer.material = null;
        }

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
        }

        // Reset item properties
        itemName = "Food Item";

        // Reset initialization flag so the item can be properly reinitialized
        hasBeenInitialized = false;

        DebugLog($"Reset food item for pooling: {name}");
    }

    /// <summary>
    /// Create a new food item GameObject with the specified food data using the pooling system
    /// </summary>
    /// <param name="foodItemData">The food data for the new item</param>
    /// <param name="position">World position for the new item</param>
    /// <param name="parent">Optional parent transform</param>
    /// <returns>The created FoodItem component, or null if creation failed</returns>
    public static FoodItem CreateFoodItem(FoodItemData foodItemData, Vector3 position, Transform parent = null)
    {
        if (foodItemData == null)
        {
            Debug.LogError("[FoodItem] Cannot create food item with null food data");
            return null;
        }

        // Use FoodManager for pooled creation
        if (FoodManager.Instance != null)
        {
            FoodItem pooledItem = FoodManager.Instance.SpawnFoodItem(foodItemData, position);

            if (pooledItem != null && parent != null)
            {
                // Only reparent if it's not a pooled item (pooled items stay under the pool)
                if (!pooledItem.IsPooledItem)
                {
                    pooledItem.transform.SetParent(parent);
                }
            }

            return pooledItem;
        }

        Debug.LogError("[FoodItem] Cannot create food item - no FoodManager available");
        return null;
    }

    #region Debug and Validation

    protected override void OnValidate()
    {
        base.OnValidate();

        // Validate food data setup
        if (foodData != null)
        {
            // Update item name to match food data
            if (itemName != foodData.DisplayName)
            {
                itemName = foodData.DisplayName;
            }
        }

        // Ensure we have a FoodItemInteractable component
        if (GetComponent<FoodItemInteractable>() == null)
        {
            Debug.LogWarning($"[FoodItem] {name} should have a FoodItemInteractable component. Add one for proper food interaction behavior.");
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw additional gizmos for food items
        if (HasValidFoodData)
        {
            // Draw processing indicators
            Vector3 gizmoPos = transform.position + Vector3.up * 3f;

            // Chopping indicator
            if (foodData.ChoppedResult != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(gizmoPos + Vector3.left * 0.3f, Vector3.one * 0.1f);
            }

            // Cooking indicator  
            if (foodData.CookedResult != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(gizmoPos + Vector3.right * 0.3f, Vector3.one * 0.1f);
            }

            // Pooling indicator
            if (isPooledItem)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(gizmoPos + Vector3.up * 0.5f, 0.15f);
            }

#if UNITY_EDITOR
            // Show food name and processing options in scene view
            if (HasValidFoodData)
            {
                string label = foodData.DisplayName;
                if (isPooledItem) label += " (Pooled)";
                if (foodData.CanBeProcessed())
                {
                    label += $"\n({GetProcessingOptions()})";
                }
                UnityEditor.Handles.Label(gizmoPos + Vector3.up * 0.5f, label);
            }
#endif
        }
    }

    #endregion

    #region Cleanup

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // Unregister from FoodManager if available and not pooled
        // Pooled items are handled by the FoodManager/Pool system
        if (FoodManager.Instance != null && !isPooledItem)
        {
            FoodManager.Instance.UnregisterFoodItem(this);
        }
    }

    #endregion
}