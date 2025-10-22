using UnityEngine;

/// <summary>
/// ScriptableObject that defines the properties and behavior of a food item.
/// This is the data container that drives how food items look and what they can become.
/// </summary>
[CreateAssetMenu(fileName = "New Food Item", menuName = "Food System/Food Item Data")]
public class FoodItemData : ScriptableObject
{
    [Header("Visual Representation")]
    [SerializeField] private GameObject meshPrefab;
    [Tooltip("The 3D model/prefab used to represent this food item in the game world")]

    [SerializeField] private Material itemMaterial;
    [Tooltip("The material applied to the food item (optional - mesh prefab may have its own materials)")]

    [Header("Basic Properties")]
    [SerializeField] private string displayName = "Food Item";
    [Tooltip("The name shown to players when they interact with this food item")]

    [Header("Processing Capabilities")]
    [SerializeField] private bool canBeChopped = false;
    [Tooltip("Whether this food item can be chopped by Player 2")]

    [SerializeField] private bool canBeCooked = false;
    [Tooltip("Whether this food item can be cooked by Player 1")]

    [Header("Processing Results")]
    [SerializeField] private FoodItemData choppedResult;
    [Tooltip("What this food item becomes when chopped (only used if canBeChopped is true)")]

    [SerializeField] private FoodItemData cookedResult;
    [Tooltip("What this food item becomes when cooked (only used if canBeCooked is true)")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Public properties for easy access
    public GameObject MeshPrefab => meshPrefab;
    public Material ItemMaterial => itemMaterial;
    public string DisplayName => displayName;
    public bool CanBeChopped => canBeChopped;
    public bool CanBeCooked => canBeCooked;
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

        // Validate chopping setup
        if (canBeChopped && choppedResult == null)
        {
            Debug.LogWarning($"[{name}] Food item can be chopped but no chopped result is assigned!");
        }

        if (!canBeChopped && choppedResult != null)
        {
            Debug.LogWarning($"[{name}] Food item cannot be chopped but has a chopped result assigned. Consider setting canBeChopped to true or removing the chopped result.");
        }

        // Validate cooking setup
        if (canBeCooked && cookedResult == null)
        {
            Debug.LogWarning($"[{name}] Food item can be cooked but no cooked result is assigned!");
        }

        if (!canBeCooked && cookedResult != null)
        {
            Debug.LogWarning($"[{name}] Food item cannot be cooked but has a cooked result assigned. Consider setting canBeCooked to true or removing the cooked result.");
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
        return canBeChopped || canBeCooked;
    }

    /// <summary>
    /// Get a description of what processing options are available for this food item
    /// </summary>
    public string GetProcessingOptions()
    {
        if (!CanBeProcessed())
            return "Cannot be processed";

        string options = "";
        if (canBeChopped)
            options += "Can be chopped";

        if (canBeCooked)
        {
            if (!string.IsNullOrEmpty(options))
                options += " and ";
            options += "Can be cooked";
        }

        return options;
    }

    /// <summary>
    /// Debug method to log information about this food item
    /// </summary>
    public void LogFoodItemInfo()
    {
        if (!enableDebugLogs) return;

        Debug.Log($"[FoodItemData] {displayName}:");
        Debug.Log($"  - Processing: {GetProcessingOptions()}");

        if (canBeChopped && choppedResult != null)
            Debug.Log($"  - Chopped Result: {choppedResult.displayName}");

        if (canBeCooked && cookedResult != null)
            Debug.Log($"  - Cooked Result: {cookedResult.displayName}");
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
                return canBeChopped ? choppedResult : null;
            case FoodProcessType.Cooking:
                return canBeCooked ? cookedResult : null;
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
                return canBeChopped && choppedResult != null;
            case FoodProcessType.Cooking:
                return canBeCooked && cookedResult != null;
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