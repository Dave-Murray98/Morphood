using UnityEngine;

/// <summary>
/// Represents a food item in the game world. Inherits from PickupableItem to maintain 
/// all the existing pickup/drop functionality while adding food-specific behavior.
/// </summary>
[RequireComponent(typeof(FoodItemInteractable))]
public class FoodItem : PickupableItem
{
    [Header("Food Item Configuration")]
    [SerializeField] private FoodItemData foodData;
    [Tooltip("The ScriptableObject that defines this food item's properties and behavior")]

    [Header("Visual Components")]
    [SerializeField] private Transform meshContainer;
    [Tooltip("Container for the food item's visual representation. If not assigned, will use this transform.")]

    [SerializeField] private bool updateMaterialOnStart = true;
    [Tooltip("Whether to apply the food data's material to the mesh renderer on start")]

    // Internal components
    private FoodItemInteractable foodInteractable;
    private GameObject currentMeshInstance;
    private MeshRenderer meshRenderer;

    // Public properties
    public FoodItemData FoodData => foodData;
    public bool HasValidFoodData => foodData != null;

    protected override void Start()
    {
        // Initialize food-specific setup first
        InitializeFoodItem();

        // Then call the base PickupableItem start
        base.Start();
    }

    private void InitializeFoodItem()
    {
        // Get the food interactable component
        foodInteractable = GetComponent<FoodItemInteractable>();
        if (foodInteractable == null)
        {
            Debug.LogError($"[FoodItem] {name} requires a FoodItemInteractable component!");
            return;
        }

        // Set up mesh container
        if (meshContainer == null)
        {
            meshContainer = transform;
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
    }

    /// <summary>
    /// Apply the visual representation and properties from the food data
    /// </summary>
    private void ApplyFoodData()
    {
        if (!HasValidFoodData) return;

        // Update the item name and type from food data
        itemName = foodData.DisplayName;
        itemType = ItemType.Ingredient; // Food items are always ingredients in your system

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

        // Remove existing mesh instance if any
        if (currentMeshInstance != null)
        {
            if (Application.isPlaying)
                Destroy(currentMeshInstance);
            else
                DestroyImmediate(currentMeshInstance);
        }

        // Instantiate new mesh if available
        if (foodData.MeshPrefab != null)
        {
            currentMeshInstance = Instantiate(foodData.MeshPrefab, meshContainer);
            currentMeshInstance.transform.localPosition = Vector3.zero;
            currentMeshInstance.transform.localRotation = Quaternion.identity;

            // Get mesh renderer from the instantiated mesh
            meshRenderer = currentMeshInstance.GetComponentInChildren<MeshRenderer>();
        }
        else
        {
            // Try to get mesh renderer from existing geometry
            meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        // Apply material if specified and we have a renderer
        if (updateMaterialOnStart && foodData.ItemMaterial != null && meshRenderer != null)
        {
            meshRenderer.material = foodData.ItemMaterial;
        }

        DebugLog($"Updated visual representation for {foodData.DisplayName}");
    }

    /// <summary>
    /// Transform this food item into another food item based on processing
    /// </summary>
    /// <param name="processType">The type of processing applied</param>
    /// <returns>True if transformation was successful</returns>
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

        // Apply the new food data
        foodData = resultData;
        ApplyFoodData();

        DebugLog($"Transformed food to {foodData.DisplayName} using {processType}");
        return true;
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
    /// Set new food data for this item (useful for creating food items at runtime)
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

        DebugLog($"Set food data to {foodData.DisplayName}");
    }

    /// <summary>
    /// Create a new food item GameObject with the specified food data
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

        // Create a new GameObject for the food item
        GameObject foodItemObj = new GameObject($"FoodItem_{foodItemData.DisplayName}");
        foodItemObj.transform.position = position;
        if (parent != null)
            foodItemObj.transform.SetParent(parent);

        // Add required components
        FoodItem foodItem = foodItemObj.AddComponent<FoodItem>();
        foodItemObj.AddComponent<FoodItemInteractable>();

        // Add physics components
        Rigidbody rb = foodItemObj.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 2f; // Some drag for realistic feel

        // Add a default collider (can be overridden by the mesh prefab)
        BoxCollider collider = foodItemObj.AddComponent<BoxCollider>();
        collider.size = Vector3.one * 0.5f; // Reasonable default size

        // Set the food data
        foodItem.SetFoodData(foodItemData);

        return foodItem;
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

            // Ensure item type is set correctly
            if (itemType != ItemType.Ingredient)
            {
                itemType = ItemType.Ingredient;
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
            if (foodData.CanBeChopped)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(gizmoPos + Vector3.left * 0.3f, Vector3.one * 0.1f);
            }

            // Cooking indicator  
            if (foodData.CanBeCooked)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(gizmoPos + Vector3.right * 0.3f, Vector3.one * 0.1f);
            }

#if UNITY_EDITOR
            // Show food name and processing options in scene view
            if (HasValidFoodData)
            {
                string label = foodData.DisplayName;
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
}