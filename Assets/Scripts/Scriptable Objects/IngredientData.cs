using UnityEngine;

/// <summary>
/// ScriptableObject that defines all the properties and behavior of an ingredient.
/// Each ingredient state (raw tomato, chopped tomato, cooked tomato) gets its own IngredientData asset.
/// </summary>
[CreateAssetMenu(fileName = "New Ingredient", menuName = "Cooking System/Ingredient Data")]
public class IngredientData : ScriptableObject
{
    [Header("Basic Information")]
    [SerializeField] private string ingredientName = "Ingredient";
    [Tooltip("Display name for this ingredient")]

    [SerializeField] private string description = "";
    [Tooltip("Description of this ingredient for UI/tooltips")]

    [SerializeField] private IngredientCategory category = IngredientCategory.Vegetable;
    [Tooltip("What category this ingredient belongs to")]

    [Header("Ingredient State")]
    [SerializeField] private IngredientPreparationState preparationState = IngredientPreparationState.Raw;
    [Tooltip("What preparation state this ingredient is in")]

    [SerializeField] private IngredientTemperatureState temperatureState = IngredientTemperatureState.Room;
    [Tooltip("What temperature state this ingredient is in")]

    [Header("Visual and Audio")]
    [SerializeField] private GameObject ingredientPrefab;
    [Tooltip("3D model prefab for this ingredient")]

    [SerializeField] private Material ingredientMaterial;
    [Tooltip("Material to use for this ingredient")]

    [SerializeField] private Sprite ingredientIcon;
    [Tooltip("2D icon for UI displays")]

    [SerializeField] private AudioClip ingredientSound;
    [Tooltip("Sound that plays when this ingredient is created/transformed")]

    [Header("Transformation Rules")]
    [SerializeField] private bool canBeChopped = false;
    [Tooltip("Whether this ingredient can be chopped")]

    [SerializeField] private IngredientData choppedResult = null;
    [Tooltip("What ingredient this becomes when chopped")]

    [SerializeField] private float choppingTime = 2f;
    [Tooltip("Time required to chop this ingredient")]

    [SerializeField] private bool canBeCooked = false;
    [Tooltip("Whether this ingredient can be cooked")]

    [SerializeField] private IngredientData cookedResult = null;
    [Tooltip("What ingredient this becomes when cooked")]

    [SerializeField] private float cookingTime = 5f;
    [Tooltip("Time required to cook this ingredient")]

    [SerializeField] private float burningTime = 3f;
    [Tooltip("Additional time before this ingredient burns")]

    [SerializeField] private IngredientData burntResult = null;
    [Tooltip("What ingredient this becomes when burnt (optional)")]

    [Header("Recipe Properties")]
    [SerializeField] private bool isEdible = false;
    [Tooltip("Whether this ingredient can be used in completed meals")]

    [Header("Compatibility")]
    [SerializeField] private IngredientCategory[] compatibleCategories;
    [Tooltip("What ingredient categories this works well with in recipes")]

    [SerializeField] private string[] incompatibleTags;
    [Tooltip("Tags that this ingredient cannot be combined with")]

    [Header("Advanced Properties")]
    [SerializeField] private bool requiresSpecialHandling = false;
    [Tooltip("Whether this ingredient needs special processing")]

    [SerializeField] private string[] specialTags;
    [Tooltip("Special tags for recipe matching and effects")]

    // Public properties for easy access
    public string IngredientName => ingredientName;
    public string Description => description;
    public IngredientCategory Category => category;
    public IngredientPreparationState PreparationState => preparationState;
    public IngredientTemperatureState TemperatureState => temperatureState;

    public GameObject IngredientPrefab => ingredientPrefab;
    public Material IngredientMaterial => ingredientMaterial;
    public Sprite IngredientIcon => ingredientIcon;
    public AudioClip IngredientSound => ingredientSound;

    public bool CanBeChopped => canBeChopped;
    public IngredientData ChoppedResult => choppedResult;
    public float ChoppingTime => choppingTime;

    public bool CanBeCooked => canBeCooked;
    public IngredientData CookedResult => cookedResult;
    public float CookingTime => cookingTime;
    public float BurningTime => burningTime;
    public IngredientData BurntResult => burntResult;

    public bool IsEdible => isEdible;
    public bool RequiresSpecialHandling => requiresSpecialHandling;
    public string[] SpecialTags => specialTags;

    #region Validation and Helper Methods

    /// <summary>
    /// Check if this ingredient is compatible with another ingredient
    /// </summary>
    public bool IsCompatibleWith(IngredientData other)
    {
        if (other == null) return false;

        // Check category compatibility
        if (compatibleCategories != null && compatibleCategories.Length > 0)
        {
            bool categoryMatch = false;
            foreach (IngredientCategory compatCategory in compatibleCategories)
            {
                if (other.Category == compatCategory)
                {
                    categoryMatch = true;
                    break;
                }
            }
            if (!categoryMatch) return false;
        }

        // Check for incompatible tags
        if (incompatibleTags != null && other.specialTags != null)
        {
            foreach (string incompatibleTag in incompatibleTags)
            {
                foreach (string otherTag in other.specialTags)
                {
                    if (incompatibleTag.Equals(otherTag, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Check if this ingredient has a specific tag
    /// </summary>
    public bool HasTag(string tag)
    {
        if (specialTags == null) return false;

        foreach (string specialTag in specialTags)
        {
            if (specialTag.Equals(tag, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get the final result after a transformation (accounts for chaining)
    /// </summary>
    public IngredientData GetTransformationResult(TransformationType transformationType)
    {
        switch (transformationType)
        {
            case TransformationType.Chopping:
                return canBeChopped ? choppedResult : null;
            case TransformationType.Cooking:
                return canBeCooked ? cookedResult : null;
            case TransformationType.Burning:
                return burntResult;
            default:
                return null;
        }
    }

    /// <summary>
    /// Get the time required for a transformation
    /// </summary>
    public float GetTransformationTime(TransformationType transformationType)
    {
        switch (transformationType)
        {
            case TransformationType.Chopping:
                return choppingTime;
            case TransformationType.Cooking:
                return cookingTime;
            case TransformationType.Burning:
                return burningTime;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Check if this ingredient is in its final form (cannot be processed further)
    /// </summary>
    public bool IsFinalForm()
    {
        return !canBeChopped && !canBeCooked;
    }

    /// <summary>
    /// Check if this ingredient is spoiled/unusable
    /// </summary>
    public bool IsSpoiled()
    {
        return HasTag("spoiled") || HasTag("burnt") || preparationState == IngredientPreparationState.Burnt;
    }

    #endregion

    #region Editor Validation

    private void OnValidate()
    {
        // Ensure name is not empty
        if (string.IsNullOrEmpty(ingredientName))
        {
            ingredientName = name.Replace("Data", "").Replace("Ingredient", "");
        }

        // Validate transformation chains
        if (canBeChopped && choppedResult == this)
        {
            Debug.LogWarning($"[{name}] Ingredient cannot transform into itself when chopped!");
            choppedResult = null;
        }

        if (canBeCooked && cookedResult == this)
        {
            Debug.LogWarning($"[{name}] Ingredient cannot transform into itself when cooked!");
            cookedResult = null;
        }

        // Ensure positive times
        choppingTime = Mathf.Max(0.1f, choppingTime);
        cookingTime = Mathf.Max(0.1f, cookingTime);
        burningTime = Mathf.Max(0.1f, burningTime);
    }

    #endregion

    #region Development Helpers

#if UNITY_EDITOR
    [Header("Development Tools")]
    [SerializeField] private bool debugTransformations = false;

    /// <summary>
    /// Debug method to trace transformation chains
    /// </summary>
    [ContextMenu("Debug Transformation Chain")]
    private void DebugTransformationChain()
    {
        Debug.Log($"=== Transformation Chain for {ingredientName} ===");

        if (canBeChopped && choppedResult != null)
            Debug.Log($"Chopping: {ingredientName} → {choppedResult.ingredientName} (in {choppingTime}s)");

        if (canBeCooked && cookedResult != null)
            Debug.Log($"Cooking: {ingredientName} → {cookedResult.ingredientName} (in {cookingTime}s)");

        if (burntResult != null)
            Debug.Log($"Burning: {ingredientName} → {burntResult.ingredientName} (after {burningTime}s extra)");

        Debug.Log($"Final form: {IsFinalForm()}, Edible: {isEdible}, Spoiled: {IsSpoiled()}");
    }
#endif

    #endregion
}

/// <summary>
/// Categories for organizing ingredients
/// </summary>
public enum IngredientCategory
{
    Vegetable,      // Tomatoes, onions, lettuce
    Meat,           // Beef, chicken, fish
    Dairy,          // Cheese, milk, butter
    Grain,          // Bread, rice, pasta
    Seasoning,      // Salt, pepper, herbs
    Liquid,         // Water, oil, sauce
    Processed       // Pre-made items
}

/// <summary>
/// Different preparation states an ingredient can be in
/// </summary>
public enum IngredientPreparationState
{
    Raw,            // Unprocessed
    Chopped,        // Cut up
    Cooked,         // Properly cooked
    Burnt,          // Overcooked/ruined
    Mixed           // Combined with other ingredients
}

/// <summary>
/// Temperature states for ingredients
/// </summary>
public enum IngredientTemperatureState
{
    Room,           // Room temperature
    Warm,           // Slightly heated
    Hot,            // Fully heated
    Burning         // Dangerously hot
}

/// <summary>
/// Types of transformations ingredients can undergo
/// </summary>
public enum TransformationType
{
    Chopping,
    Cooking,
    Burning,
    Mixing
}