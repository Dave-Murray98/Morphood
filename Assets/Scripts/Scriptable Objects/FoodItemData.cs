using UnityEngine;

/// <summary>
/// ScriptableObject that defines the properties and behavior of a food item.
/// This is the data container that drives how food items look and what they can become.
/// </summary>
[CreateAssetMenu(fileName = "New Food Item", menuName = "Food System/Food Item Data")]
public class FoodItemData : ScriptableObject
{
    [Header("Visual Representation")]
    [Tooltip("The prefab that contains the visual representation (mesh, materials, etc.) - will be spawned as a child of the food item")]
    [SerializeField] private GameObject visualPrefab;

    [Header("Basic Properties")]
    [Tooltip("The name shown to players when they interact with this food item")]
    [SerializeField] private string displayName = "Food Item";
    [Tooltip("The icon shown to players when they interact with this food item")]
    [SerializeField] private Sprite icon;

    [Header("Food Properties")]
    [Tooltip("How much money this food item is worth?")]
    public float foodValue = 10f;

    [Header("Processing Results")]
    [Tooltip("What this food item becomes when chopped (only used if canBeChopped is true)")]
    [SerializeField] private FoodItemData choppedResult;

    [Tooltip("What this food item becomes when cooked (only used if canBeCooked is true)")]
    [SerializeField] private FoodItemData cookedResult;


    // Public properties for easy access
    public GameObject VisualPrefab => visualPrefab;
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

        // Validate visual prefab setup
        if (visualPrefab != null)
        {
            MeshFilter meshFilter = visualPrefab.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = visualPrefab.GetComponent<MeshRenderer>();

            if (meshFilter == null)
            {
                Debug.LogWarning($"[{name}] Visual prefab should have a MeshFilter component!");
            }

            if (meshRenderer == null)
            {
                Debug.LogWarning($"[{name}] Visual prefab should have a MeshRenderer component!");
            }

            if (meshFilter != null && meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"[{name}] Visual prefab's MeshFilter has no mesh assigned!");
            }
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