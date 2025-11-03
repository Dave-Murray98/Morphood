using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// ScriptableObject database that stores all possible food combinations organized by final result.
/// Each entry represents a final dish and contains all possible ingredient combinations that create it.
/// This makes the system more scalable and easier to manage for complex recipes.
/// </summary>
[CreateAssetMenu(fileName = "Food Combination Database", menuName = "Food System/Combination Database")]
public class FoodCombinationDatabase : ScriptableObject
{
    [Header("Food Combination Recipes")]
    [SerializeField] private List<FoodRecipe> recipes = new List<FoodRecipe>();
    [Tooltip("List of all food recipes, each containing multiple ways to create the same result")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Cache for faster lookups
    private Dictionary<CombinationKey, FoodItemData> combinationCache;
    private bool isCacheInitialized = false;

    // Public property for readonly access to recipes
    public IReadOnlyList<FoodRecipe> Recipes => recipes.AsReadOnly();

    /// <summary>
    /// Initialize the combination cache for fast lookups
    /// </summary>
    private void InitializeCache()
    {
        if (isCacheInitialized) return;

        combinationCache = new Dictionary<CombinationKey, FoodItemData>();

        foreach (var recipe in recipes)
        {
            if (recipe.IsValid())
            {
                foreach (var combination in recipe.combinations)
                {
                    if (combination.IsValid())
                    {
                        var key = new CombinationKey(combination.GetSortedIngredients());

                        if (!combinationCache.ContainsKey(key))
                        {
                            combinationCache[key] = recipe.result;
                            DebugLog($"Added combination: {string.Join(" + ", combination.ingredients.Select(i => i.name))} = {recipe.result.name}");
                        }
                        else
                        {
                            Debug.LogWarning($"[FoodCombinationDatabase] Duplicate combination found: {string.Join(" + ", combination.ingredients.Select(i => i.name))}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[FoodCombinationDatabase] Invalid recipe found: {recipe.result?.name ?? "Unknown"}");
            }
        }

        isCacheInitialized = true;
        DebugLog($"Initialized combination cache with {combinationCache.Count} combinations across {recipes.Count} recipes");
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
    /// Get the recipe for a specific result item
    /// </summary>
    /// <param name="result">The food item result to search for</param>
    /// <returns>The recipe that creates this result, or null if not found</returns>
    public FoodRecipe GetRecipeFor(FoodItemData result)
    {
        return recipes.FirstOrDefault(recipe => recipe.result == result);
    }

    /// <summary>
    /// Get all combinations that include a specific food item as an ingredient
    /// </summary>
    /// <param name="foodItem">The food item to search for</param>
    /// <returns>List of recipes that use this food item as an ingredient</returns>
    public List<FoodRecipe> GetRecipesContaining(FoodItemData foodItem)
    {
        if (foodItem == null) return new List<FoodRecipe>();

        return recipes.Where(recipe =>
            recipe.combinations.Any(combo =>
                combo.ingredients.Contains(foodItem))).ToList();
    }

    /// <summary>
    /// Get all possible results that can be created with a specific food item
    /// </summary>
    /// <param name="foodItem">The food item to search with</param>
    /// <returns>List of possible result food items</returns>
    public List<FoodItemData> GetPossibleResults(FoodItemData foodItem)
    {
        return GetRecipesContaining(foodItem)
            .Select(recipe => recipe.result)
            .Where(result => result != null)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Add a new combination to an existing recipe or create a new recipe
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

        // Find existing recipe for this result
        var existingRecipe = GetRecipeFor(result);

        if (existingRecipe != null)
        {
            // Add to existing recipe
            var newCombination = new IngredientCombination
            {
                ingredients = new List<FoodItemData>(ingredients)
            };
            existingRecipe.combinations.Add(newCombination);
            DebugLog($"Added combination to existing recipe for {result.name}: {string.Join(" + ", ingredients.Select(i => i.name))}");
        }
        else
        {
            // Create new recipe
            var newRecipe = new FoodRecipe
            {
                result = result,
                combinations = new List<IngredientCombination>
                {
                    new IngredientCombination
                    {
                        ingredients = new List<FoodItemData>(ingredients)
                    }
                }
            };
            recipes.Add(newRecipe);
            DebugLog($"Created new recipe for {result.name}: {string.Join(" + ", ingredients.Select(i => i.name))}");
        }

        // Clear cache to force rebuild
        isCacheInitialized = false;

        return true;
    }

    /// <summary>
    /// Remove a specific combination from the database
    /// </summary>
    /// <param name="ingredients">The ingredients of the combination to remove</param>
    /// <returns>True if a combination was removed</returns>
    public bool RemoveCombination(List<FoodItemData> ingredients)
    {
        if (ingredients == null || ingredients.Count == 0) return false;

        var sortedIngredients = ingredients.OrderBy(i => i.name).ToList();

        foreach (var recipe in recipes)
        {
            for (int i = recipe.combinations.Count - 1; i >= 0; i--)
            {
                var combo = recipe.combinations[i];
                var comboSorted = combo.GetSortedIngredients();

                if (comboSorted.SequenceEqual(sortedIngredients))
                {
                    recipe.combinations.RemoveAt(i);
                    isCacheInitialized = false; // Clear cache
                    DebugLog($"Removed combination: {string.Join(" + ", ingredients.Select(item => item.name))} from {recipe.result.name}");

                    // If recipe has no combinations left, remove the recipe
                    if (recipe.combinations.Count == 0)
                    {
                        recipes.Remove(recipe);
                        DebugLog($"Removed empty recipe for {recipe.result.name}");
                    }

                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Remove all combinations for a specific result
    /// </summary>
    /// <param name="result">The result food item to remove all combinations for</param>
    /// <returns>True if a recipe was removed</returns>
    public bool RemoveRecipe(FoodItemData result)
    {
        if (result == null) return false;

        for (int i = recipes.Count - 1; i >= 0; i--)
        {
            if (recipes[i].result == result)
            {
                recipes.RemoveAt(i);
                isCacheInitialized = false;
                DebugLog($"Removed recipe for {result.name}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clear all recipes and reset the database
    /// </summary>
    public void ClearAllRecipes()
    {
        recipes.Clear();
        isCacheInitialized = false;
        DebugLog("Cleared all recipes");
    }

    /// <summary>
    /// Get statistics about the combination database
    /// </summary>
    /// <returns>String containing database statistics</returns>
    public string GetDatabaseStats()
    {
        int validRecipes = recipes.Count(recipe => recipe.IsValid());
        int invalidRecipes = recipes.Count - validRecipes;
        int totalCombinations = recipes.Sum(recipe => recipe.combinations.Count);
        int validCombinations = recipes.Sum(recipe => recipe.combinations.Count(combo => combo.IsValid()));

        var uniqueIngredients = recipes
            .SelectMany(recipe => recipe.combinations)
            .SelectMany(combo => combo.ingredients)
            .Where(ingredient => ingredient != null)
            .Distinct()
            .Count();

        var uniqueResults = recipes
            .Select(recipe => recipe.result)
            .Where(result => result != null)
            .Distinct()
            .Count();

        return $"Database Stats:\n" +
               $"- Total Recipes: {recipes.Count}\n" +
               $"- Valid Recipes: {validRecipes}\n" +
               $"- Invalid Recipes: {invalidRecipes}\n" +
               $"- Total Combinations: {totalCombinations}\n" +
               $"- Valid Combinations: {validCombinations}\n" +
               $"- Unique Ingredients: {uniqueIngredients}\n" +
               $"- Unique Results: {uniqueResults}";
    }

    #region Validation and Debug

    private void OnValidate()
    {
        // Clear cache when recipes are modified in editor
        isCacheInitialized = false;

        // Validate recipes
        for (int i = 0; i < recipes.Count; i++)
        {
            if (!recipes[i].IsValid())
            {
                Debug.LogWarning($"[FoodCombinationDatabase] Invalid recipe at index {i}: {recipes[i].result?.name ?? "Unknown"}");
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[FoodCombinationDatabase] {message}");
    }

    /// <summary>
    /// Log all recipes in the database (useful for debugging)
    /// </summary>
    [ContextMenu("Log All Recipes")]
    public void LogAllRecipes()
    {
        Debug.Log($"[FoodCombinationDatabase] Logging all {recipes.Count} recipes:");

        for (int i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            if (recipe.IsValid())
            {
                Debug.Log($"  Recipe {i + 1}: {recipe.result.name} ({recipe.combinations.Count} combinations)");
                for (int j = 0; j < recipe.combinations.Count; j++)
                {
                    var combo = recipe.combinations[j];
                    if (combo.IsValid())
                    {
                        string ingredientNames = string.Join(" + ", combo.ingredients.Select(ingredient => ingredient.name));
                        Debug.Log($"    {j + 1}: {ingredientNames}");
                    }
                    else
                    {
                        Debug.LogWarning($"    {j + 1}: INVALID COMBINATION");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"  Recipe {i + 1}: INVALID RECIPE");
            }
        }
    }

    #endregion
}

/// <summary>
/// Represents a food recipe - a result item and all the ways to create it
/// </summary>
[System.Serializable]
public class FoodRecipe
{
    [Tooltip("The food item that results from the combinations")]
    public FoodItemData result;

    [Tooltip("All possible ingredient combinations that create this result")]
    public List<IngredientCombination> combinations = new List<IngredientCombination>();

    /// <summary>
    /// Check if this recipe is valid (has a result and at least one valid combination)
    /// </summary>
    public bool IsValid()
    {
        return result != null &&
               combinations != null &&
               combinations.Count > 0 &&
               combinations.Any(combo => combo.IsValid());
    }

    /// <summary>
    /// Get the display name for this recipe
    /// </summary>
    public string DisplayName => result?.DisplayName ?? "Invalid Recipe";

    /// <summary>
    /// Get the number of valid combinations in this recipe
    /// </summary>
    public int ValidCombinationCount => combinations?.Count(combo => combo.IsValid()) ?? 0;
}

/// <summary>
/// Represents a single ingredient combination (always 2 ingredients for this system)
/// </summary>
[System.Serializable]
public class IngredientCombination
{
    [Tooltip("The food items that need to be combined (should always be exactly 2 for this system)")]
    public List<FoodItemData> ingredients = new List<FoodItemData>();

    /// <summary>
    /// Check if this combination is valid (has exactly 2 non-null ingredients)
    /// </summary>
    public bool IsValid()
    {
        return ingredients != null &&
               ingredients.Count == 2 &&
               ingredients.All(ingredient => ingredient != null) &&
               ingredients[0] != ingredients[1]; // Ensure ingredients are different
    }

    /// <summary>
    /// Get the ingredients sorted by name for consistent comparison
    /// </summary>
    public List<FoodItemData> GetSortedIngredients()
    {
        return ingredients?.OrderBy(ingredient => ingredient.name).ToList() ?? new List<FoodItemData>();
    }

    /// <summary>
    /// Get the display name for this combination
    /// </summary>
    public string DisplayName => IsValid()
        ? $"{ingredients[0].DisplayName} + {ingredients[1].DisplayName}"
        : "Invalid Combination";
}

/// <summary>
/// Key class for fast combination lookups (unchanged from previous version)
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