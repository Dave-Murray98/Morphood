using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// ScriptableObject database that stores all possible food combinations and recipes.
/// This keeps combination logic separate from individual food items, making the system
/// more scalable and easier to manage.
/// </summary>
[CreateAssetMenu(fileName = "Food Combination Database", menuName = "Food System/Combination Database")]
public class FoodCombinationDatabase : ScriptableObject
{
    [Header("Food Combinations")]
    [SerializeField] private List<FoodCombination> combinations = new List<FoodCombination>();
    [Tooltip("List of all possible food combinations in the game")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Cache for faster lookups
    private Dictionary<CombinationKey, FoodItemData> combinationCache;
    private bool isCacheInitialized = false;

    // Public property for readonly access to combinations
    public IReadOnlyList<FoodCombination> Combinations => combinations.AsReadOnly();

    /// <summary>
    /// Initialize the combination cache for fast lookups
    /// </summary>
    private void InitializeCache()
    {
        if (isCacheInitialized) return;

        combinationCache = new Dictionary<CombinationKey, FoodItemData>();

        foreach (var combination in combinations)
        {
            if (combination.IsValid())
            {
                var key = new CombinationKey(combination.GetSortedIngredients());

                if (!combinationCache.ContainsKey(key))
                {
                    combinationCache[key] = combination.result;
                    DebugLog($"Added combination: {string.Join(" + ", combination.ingredients.Select(i => i.name))} = {combination.result.name}");
                }
                else
                {
                    Debug.LogWarning($"[FoodCombinationDatabase] Duplicate combination found: {string.Join(" + ", combination.ingredients.Select(i => i.name))}");
                }
            }
            else
            {
                Debug.LogWarning($"[FoodCombinationDatabase] Invalid combination found: {combination}");
            }
        }

        isCacheInitialized = true;
        DebugLog($"Initialized combination cache with {combinationCache.Count} combinations");
    }

    /// <summary>
    /// Try to find a combination result for the given list of food items
    /// </summary>
    /// <param name="ingredients">List of food item data to combine</param>
    /// <returns>The resulting food item data, or null if no combination exists</returns>
    public FoodItemData FindCombination(List<FoodItemData> ingredients)
    {
        if (ingredients == null || ingredients.Count == 0)
            return null;

        // Remove null entries and duplicates, then sort for consistent lookup
        var cleanIngredients = ingredients
            .Where(item => item != null)
            .Distinct()
            .OrderBy(item => item.name)
            .ToList();

        if (cleanIngredients.Count == 0)
            return null;

        InitializeCache();

        var key = new CombinationKey(cleanIngredients);

        if (combinationCache.TryGetValue(key, out FoodItemData result))
        {
            DebugLog($"Found combination: {string.Join(" + ", cleanIngredients.Select(i => i.name))} = {result.name}");
            return result;
        }

        DebugLog($"No combination found for: {string.Join(" + ", cleanIngredients.Select(i => i.name))}");
        return null;
    }

    /// <summary>
    /// Try to find a combination result for exactly two food items
    /// </summary>
    /// <param name="item1">First food item</param>
    /// <param name="item2">Second food item</param>
    /// <returns>The resulting food item data, or null if no combination exists</returns>
    public FoodItemData FindCombination(FoodItemData item1, FoodItemData item2)
    {
        return FindCombination(new List<FoodItemData> { item1, item2 });
    }

    /// <summary>
    /// Check if a combination exists for the given ingredients
    /// </summary>
    /// <param name="ingredients">List of food item data to check</param>
    /// <returns>True if a combination exists</returns>
    public bool HasCombination(List<FoodItemData> ingredients)
    {
        return FindCombination(ingredients) != null;
    }

    /// <summary>
    /// Check if a combination exists for exactly two food items
    /// </summary>
    /// <param name="item1">First food item</param>
    /// <param name="item2">Second food item</param>
    /// <returns>True if a combination exists</returns>
    public bool HasCombination(FoodItemData item1, FoodItemData item2)
    {
        return FindCombination(item1, item2) != null;
    }

    /// <summary>
    /// Get all combinations that include a specific food item
    /// </summary>
    /// <param name="foodItem">The food item to search for</param>
    /// <returns>List of combinations that include this food item</returns>
    public List<FoodCombination> GetCombinationsContaining(FoodItemData foodItem)
    {
        if (foodItem == null) return new List<FoodCombination>();

        return combinations.Where(combo => combo.ingredients.Contains(foodItem)).ToList();
    }

    /// <summary>
    /// Get all possible results that can be created with a specific food item
    /// </summary>
    /// <param name="foodItem">The food item to search with</param>
    /// <returns>List of possible result food items</returns>
    public List<FoodItemData> GetPossibleResults(FoodItemData foodItem)
    {
        return GetCombinationsContaining(foodItem)
            .Select(combo => combo.result)
            .Where(result => result != null)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Add a new combination to the database at runtime
    /// </summary>
    /// <param name="ingredients">List of ingredient food items</param>
    /// <param name="result">The resulting food item</param>
    /// <returns>True if the combination was added successfully</returns>
    public bool AddCombination(List<FoodItemData> ingredients, FoodItemData result)
    {
        if (ingredients == null || ingredients.Count == 0 || result == null)
        {
            Debug.LogWarning("[FoodCombinationDatabase] Cannot add combination - invalid ingredients or result");
            return false;
        }

        // Check if combination already exists
        if (HasCombination(ingredients))
        {
            Debug.LogWarning($"[FoodCombinationDatabase] Combination already exists: {string.Join(" + ", ingredients.Select(i => i.name))}");
            return false;
        }

        // Create new combination
        var newCombination = new FoodCombination
        {
            ingredients = new List<FoodItemData>(ingredients),
            result = result
        };

        combinations.Add(newCombination);

        // Clear cache to force rebuild
        isCacheInitialized = false;

        DebugLog($"Added new combination: {string.Join(" + ", ingredients.Select(i => i.name))} = {result.name}");
        return true;
    }

    /// <summary>
    /// Remove a combination from the database
    /// </summary>
    /// <param name="ingredients">The ingredients of the combination to remove</param>
    /// <returns>True if a combination was removed</returns>
    public bool RemoveCombination(List<FoodItemData> ingredients)
    {
        if (ingredients == null || ingredients.Count == 0) return false;

        var sortedIngredients = ingredients.OrderBy(i => i.name).ToList();

        for (int i = combinations.Count - 1; i >= 0; i--)
        {
            var combo = combinations[i];
            var comboSorted = combo.GetSortedIngredients();

            if (comboSorted.SequenceEqual(sortedIngredients))
            {
                combinations.RemoveAt(i);
                isCacheInitialized = false; // Clear cache
                DebugLog($"Removed combination: {string.Join(" + ", ingredients.Select(item => item.name))}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clear all combinations and reset the database
    /// </summary>
    public void ClearAllCombinations()
    {
        combinations.Clear();
        isCacheInitialized = false;
        DebugLog("Cleared all combinations");
    }

    /// <summary>
    /// Get statistics about the combination database
    /// </summary>
    /// <returns>String containing database statistics</returns>
    public string GetDatabaseStats()
    {
        int validCombinations = combinations.Count(combo => combo.IsValid());
        int invalidCombinations = combinations.Count - validCombinations;

        var uniqueIngredients = combinations
            .SelectMany(combo => combo.ingredients)
            .Where(ingredient => ingredient != null)
            .Distinct()
            .Count();

        var uniqueResults = combinations
            .Select(combo => combo.result)
            .Where(result => result != null)
            .Distinct()
            .Count();

        return $"Database Stats:\n" +
               $"- Total Combinations: {combinations.Count}\n" +
               $"- Valid Combinations: {validCombinations}\n" +
               $"- Invalid Combinations: {invalidCombinations}\n" +
               $"- Unique Ingredients: {uniqueIngredients}\n" +
               $"- Unique Results: {uniqueResults}";
    }

    #region Validation and Debug

    private void OnValidate()
    {
        // Clear cache when combinations are modified in editor
        isCacheInitialized = false;

        // Validate combinations
        for (int i = 0; i < combinations.Count; i++)
        {
            if (!combinations[i].IsValid())
            {
                Debug.LogWarning($"[FoodCombinationDatabase] Invalid combination at index {i}");
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[FoodCombinationDatabase] {message}");
    }

    /// <summary>
    /// Log all combinations in the database (useful for debugging)
    /// </summary>
    [ContextMenu("Log All Combinations")]
    public void LogAllCombinations()
    {
        Debug.Log($"[FoodCombinationDatabase] Logging all {combinations.Count} combinations:");

        for (int i = 0; i < combinations.Count; i++)
        {
            var combo = combinations[i];
            if (combo.IsValid())
            {
                string ingredientNames = string.Join(" + ", combo.ingredients.Select(ingredient => ingredient.name));
                Debug.Log($"  {i + 1}: {ingredientNames} = {combo.result.name}");
            }
            else
            {
                Debug.LogWarning($"  {i + 1}: INVALID COMBINATION");
            }
        }
    }

    #endregion
}

/// <summary>
/// Represents a single food combination (ingredients -> result)
/// </summary>
[System.Serializable]
public class FoodCombination
{
    [Tooltip("The food items that need to be combined")]
    public List<FoodItemData> ingredients = new List<FoodItemData>();

    [Tooltip("The food item that results from combining the ingredients")]
    public FoodItemData result;

    /// <summary>
    /// Check if this combination is valid (has ingredients and a result)
    /// </summary>
    public bool IsValid()
    {
        return ingredients != null &&
               ingredients.Count > 0 &&
               ingredients.All(ingredient => ingredient != null) &&
               result != null;
    }

    /// <summary>
    /// Get the ingredients sorted by name for consistent comparison
    /// </summary>
    public List<FoodItemData> GetSortedIngredients()
    {
        return ingredients?.OrderBy(ingredient => ingredient.name).ToList() ?? new List<FoodItemData>();
    }
}

/// <summary>
/// Key class for fast combination lookups
/// </summary>
public class CombinationKey
{
    private readonly List<string> ingredientNames;
    private readonly int hashCode;

    public CombinationKey(List<FoodItemData> ingredients)
    {
        ingredientNames = ingredients.Select(ingredient => ingredient.name).OrderBy(name => name).ToList();
        hashCode = string.Join("|", ingredientNames).GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is CombinationKey other)
        {
            return ingredientNames.SequenceEqual(other.ingredientNames);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return hashCode;
    }
}