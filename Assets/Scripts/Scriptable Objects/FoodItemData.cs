using UnityEngine;

/// <summary>
/// ScriptableObject that defines the properties and behavior of a food item.
/// This is the data container that drives how food items look and what they can become.
/// </summary>
[CreateAssetMenu(fileName = "New Food Item", menuName = "Food System/Food Item Data")]
public class FoodItemData : ScriptableObject
{
    [Header("Visual Representation")]
    [SerializeField] private Mesh visualMesh;
    [Tooltip("The 3D model/prefab used to represent this food item in the game world")]

    [SerializeField] private Material itemMaterial;
    [Tooltip("The material applied to the food item (optional - mesh prefab may have its own materials)")]

    [SerializeField] private Mesh colliderMesh;
    [Tooltip("The mesh used for the collider (optional - will use meshPrefab's mesh if not specified)")]

    [Header("Basic Properties")]
    [SerializeField] private string displayName = "Food Item";
    [Tooltip("The name shown to players when they interact with this food item")]
    [SerializeField] private Sprite icon;
    [Tooltip("The icon shown to players when they interact with this food item")]

    [Header("Processing Results")]
    [SerializeField] private FoodItemData choppedResult;
    [Tooltip("What this food item becomes when chopped (only used if canBeChopped is true)")]

    [SerializeField] private FoodItemData cookedResult;
    [Tooltip("What this food item becomes when cooked (only used if canBeCooked is true)")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Public properties for easy access
    public Mesh VisualMesh => visualMesh;
    public Material ItemMaterial => itemMaterial;
    public Mesh ColliderMesh => colliderMesh;
    public string DisplayName => displayName;
    public Sprite Icon => icon;

    public FoodItemData ChoppedResult => choppedResult;
    public FoodItemData CookedResult => cookedResult;

    /// <summary>
    /// Validate that the food item data is set up correctly
    /// </summary>
    private void OnValidate()
    {
        // Ensure display name is not empty
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = name; // Use the asset name as fallback
        }



        // Check for circular references
        if (choppedResult == this)
        {
            Debug.LogError($"[{name}] Food item cannot be its own chopped result! This would create a circular reference.");
            choppedResult = null;
        }

        if (cookedResult == this)
        {
            Debug.LogError($"[{name}] Food item cannot be its own cooked result! This would create a circular reference.");
            cookedResult = null;
        }
    }

    /// <summary>
    /// Check if this food item can be processed in any way
    /// </summary>
    public bool CanBeProcessed()
    {
        return choppedResult != null || cookedResult != null;
    }

    /// <summary>
    /// Get a description of what processing options are available for this food item
    /// </summary>
    public string GetProcessingOptions()
    {
        if (!CanBeProcessed())
            return "Cannot be processed";

        string options = "";
        if (choppedResult != null)
            options += "Can be chopped";

        if (cookedResult != null)
        {
            if (!string.IsNullOrEmpty(options))
                options += " and ";
            options += "Can be cooked";
        }

        return options;
    }

    /// <summary>
    /// Get the result of processing this food item
    /// </summary>
    /// <param name="processType">The type of processing (chopping or cooking)</param>
    /// <returns>The resulting food item data, or null if processing is not possible</returns>
    public FoodItemData GetProcessingResult(FoodProcessType processType)
    {
        switch (processType)
        {
            case FoodProcessType.Chopping:
                return choppedResult;
            case FoodProcessType.Cooking:
                return cookedResult;
            default:
                return null;
        }
    }

    /// <summary>
    /// Check if this food item can undergo a specific type of processing
    /// </summary>
    /// <param name="processType">The type of processing to check</param>
    /// <returns>True if the food item can be processed this way</returns>
    public bool CanBeProcessed(FoodProcessType processType)
    {
        switch (processType)
        {
            case FoodProcessType.Chopping:
                return choppedResult != null;
            case FoodProcessType.Cooking:
                return cookedResult != null;
            default:
                return false;
        }
    }
}

/// <summary>
/// Enum defining the types of food processing available
/// </summary>
public enum FoodProcessType
{
    Chopping,
    Cooking
}