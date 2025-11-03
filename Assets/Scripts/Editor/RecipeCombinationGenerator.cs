using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using System.Linq;

/// <summary>
/// Tool for generating progressive staged combinations needed to create a final recipe.
/// Each stage builds on the results from the previous stage, creating a dynamic workflow.
/// </summary>
[CreateAssetMenu(fileName = "Recipe Combination Generator", menuName = "Food System/Recipe Combination Generator")]
public class RecipeCombinationGenerator : ScriptableObject
{
    [Header("Recipe Setup")]
    [Tooltip("The final dish that will be created")]
    public FoodItemData finalDish;

    [Tooltip("All the base ingredients needed to make this dish")]
    public List<FoodItemData> baseIngredients = new List<FoodItemData>();

    [Header("Database Reference")]
    [Tooltip("The combination database to add the generated combinations to")]
    public FoodCombinationDatabase combinationDatabase;

    [Header("Progressive Recipe Stages")]
    [InfoBox("@GetStageInfoMessage()")]
    [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "StageName")]
    public List<RecipeStage> recipeStages = new List<RecipeStage>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Track which combinations create the final dish for proper handling
    private HashSet<StageCombination> finalDishCombinations = new HashSet<StageCombination>();

    /// <summary>
    /// Generate the first stage combinations and initialize the progressive system
    /// </summary>
    [Button("Generate Stage 1 Combinations", ButtonSizes.Large)]
    [GUIColor(0.4f, 0.8f, 1f)]
    public void GenerateStage1Combinations()
    {
        if (finalDish == null)
        {
            Debug.LogError("[RecipeCombinationGenerator] Final dish is not set!");
            return;
        }

        if (baseIngredients.Count < 2)
        {
            Debug.LogError("[RecipeCombinationGenerator] Need at least 2 base ingredients!");
            return;
        }

        // Clear previous results
        recipeStages.Clear();
        finalDishCombinations.Clear();

        DebugLog($"Generating Stage 1 combinations for {finalDish.DisplayName} with {baseIngredients.Count} base ingredients");

        // Generate Stage 1 with base ingredients only
        var stage1 = GenerateStageFromIngredients(1, baseIngredients);
        recipeStages.Add(stage1);

        DebugLog($"Generated Stage 1 with {stage1.combinations.Count} unique combinations");

        // Mark the asset as dirty so changes are saved
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Generate the next stage based on previous stage results
    /// </summary>
    [Button("Generate Next Stage", ButtonSizes.Large)]
    [GUIColor(0.6f, 0.9f, 0.6f)]
    [ShowIf("@CanGenerateNextStage()")]
    public void GenerateNextStage()
    {
        if (recipeStages.Count == 0)
        {
            Debug.LogError("[RecipeCombinationGenerator] No previous stages found. Generate Stage 1 first.");
            return;
        }

        var currentStage = recipeStages[recipeStages.Count - 1];

        // Check if current stage is complete
        if (!IsStageComplete(currentStage))
        {
            Debug.LogError($"[RecipeCombinationGenerator] Stage {currentStage.stageNumber} is not complete. Assign all results before generating the next stage.");
            return;
        }

        // Generate combinations for the next stage
        int nextStageNumber = recipeStages.Count + 1;
        var nextStage = new RecipeStage
        {
            stageNumber = nextStageNumber,
            isFinalStage = false
        };

        // Generate combinations based on the current stage
        var generatedCombinations = new HashSet<string>(); // To avoid duplicates

        if (recipeStages.Count == 1)
        {
            // Stage 2: Combine Stage 1 results with remaining base ingredients and with each other
            GenerateStage2Combinations(nextStage, generatedCombinations);
        }
        else
        {
            // Stage 3+: Combine previous stage results with each other and remaining ingredients
            GenerateLaterStageCombinations(nextStage, generatedCombinations);
        }

        // Check if this should be the final stage
        if (ShouldBeFinalStage(nextStage))
        {
            nextStage.isFinalStage = true;
        }

        recipeStages.Add(nextStage);

        DebugLog($"Generated Stage {nextStageNumber} with {nextStage.combinations.Count} combinations");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Generate Stage 2 combinations: Stage 1 results + remaining base ingredients + Stage 1 results with each other
    /// </summary>
    private void GenerateStage2Combinations(RecipeStage stage2, HashSet<string> generatedCombinations)
    {
        var stage1 = recipeStages[0];
        var stage1Results = stage1.combinations
            .Where(combo => combo.result != null)
            .Select(combo => combo.result)
            .ToList();

        DebugLog($"Generating Stage 2 with {stage1Results.Count} Stage 1 results");

        // Part 1: Combine each Stage 1 result with remaining base ingredients
        foreach (var combo1 in stage1.combinations.Where(c => c.result != null))
        {
            // Get remaining base ingredients after this Stage 1 result consumes its ingredients
            var remainingBaseIngredients = GetRemainingBaseIngredientsAfter(combo1.result);

            DebugLog($"Stage 1 result {combo1.result.DisplayName} can combine with {remainingBaseIngredients.Count} remaining base ingredients");

            // Combine this Stage 1 result with each remaining base ingredient
            foreach (var baseIngredient in remainingBaseIngredients)
            {
                var newCombo = AddCombinationIfUnique(stage2, combo1.result, baseIngredient, generatedCombinations);
                if (newCombo != null)
                {
                    CheckAndMarkAsFinalDish(newCombo);
                }
            }
        }

        // Part 2: Combine Stage 1 results with each other (if they don't exceed ingredient limits)
        for (int i = 0; i < stage1.combinations.Count; i++)
        {
            for (int j = i + 1; j < stage1.combinations.Count; j++)
            {
                var combo1 = stage1.combinations[i];
                var combo2 = stage1.combinations[j];

                if (combo1.result != null && combo2.result != null)
                {
                    // The ingredient consumption check is now handled in AddCombinationIfUnique
                    var newCombo = AddCombinationIfUnique(stage2, combo1.result, combo2.result, generatedCombinations);
                    if (newCombo != null)
                    {
                        CheckAndMarkAsFinalDish(newCombo);
                    }
                }
            }
        }

        DebugLog($"Generated {stage2.combinations.Count} Stage 2 combinations");
    }

    /// <summary>
    /// Generate combinations for Stage 3 and beyond
    /// </summary>
    private void GenerateLaterStageCombinations(RecipeStage newStage, HashSet<string> generatedCombinations)
    {
        var previousStage = recipeStages[recipeStages.Count - 1];

        // Get all previous stage results that are NOT the final dish
        var previousResults = previousStage.combinations
            .Where(combo => combo.result != null && combo.result != finalDish)
            .Select(combo => combo.result)
            .ToList();

        DebugLog($"Generating Stage {newStage.stageNumber} with {previousResults.Count} non-final results from previous stage");

        // Part 1: Combine each previous result with remaining base ingredients
        foreach (var previousResult in previousResults)
        {
            var remainingBaseIngredients = GetRemainingBaseIngredientsAfter(previousResult);

            DebugLog($"Result {previousResult.DisplayName} can combine with {remainingBaseIngredients.Count} remaining base ingredients");

            foreach (var baseIngredient in remainingBaseIngredients)
            {
                var newCombo = AddCombinationIfUnique(newStage, previousResult, baseIngredient, generatedCombinations);
                if (newCombo != null)
                {
                    CheckAndMarkAsFinalDish(newCombo);
                }
            }
        }

        // Part 2: Combine previous stage results with each other (only if valid)
        for (int i = 0; i < previousResults.Count; i++)
        {
            for (int j = i + 1; j < previousResults.Count; j++)
            {
                // The ingredient consumption check is now handled in AddCombinationIfUnique
                var newCombo = AddCombinationIfUnique(newStage, previousResults[i], previousResults[j], generatedCombinations);
                if (newCombo != null)
                {
                    CheckAndMarkAsFinalDish(newCombo);
                }
            }
        }

        DebugLog($"Generated {newStage.combinations.Count} valid combinations for Stage {newStage.stageNumber}");
    }

    /// <summary>
    /// Get all base ingredients that have been used in any combination so far
    /// This handles duplicate ingredients properly by counting usage
    /// </summary>
    private List<FoodItemData> GetAllUsedBaseIngredients()
    {
        var usedIngredients = new List<FoodItemData>();

        foreach (var stage in recipeStages)
        {
            foreach (var combo in stage.combinations)
            {
                // Only count base ingredients (not intermediate results)
                if (baseIngredients.Contains(combo.ingredient1))
                    usedIngredients.Add(combo.ingredient1);
                if (baseIngredients.Contains(combo.ingredient2))
                    usedIngredients.Add(combo.ingredient2);
            }
        }

        return usedIngredients;
    }

    /// <summary>
    /// Get the count of how many times each base ingredient has been used
    /// </summary>
    private Dictionary<FoodItemData, int> GetBaseIngredientUsageCount()
    {
        var usageCount = new Dictionary<FoodItemData, int>();

        // Initialize with zero counts
        foreach (var ingredient in baseIngredients.Distinct())
        {
            usageCount[ingredient] = 0;
        }

        // Count usage across all stages
        foreach (var stage in recipeStages)
        {
            foreach (var combo in stage.combinations)
            {
                if (baseIngredients.Contains(combo.ingredient1))
                    usageCount[combo.ingredient1]++;
                if (baseIngredients.Contains(combo.ingredient2))
                    usageCount[combo.ingredient2]++;
            }
        }

        return usageCount;
    }

    /// <summary>
    /// Get available instances of base ingredients (considering duplicates)
    /// </summary>
    private List<FoodItemData> GetAvailableBaseIngredients()
    {
        var usageCount = GetBaseIngredientUsageCount();
        var availableIngredients = new List<FoodItemData>();

        foreach (var ingredient in baseIngredients.Distinct())
        {
            int totalAvailable = baseIngredients.Count(x => x == ingredient);
            int used = usageCount[ingredient];
            int remaining = totalAvailable - used;

            for (int i = 0; i < remaining; i++)
            {
                availableIngredients.Add(ingredient);
            }
        }

        return availableIngredients;
    }

    /// <summary>
    /// Get the base ingredients consumed by a specific food item (recursively trace back)
    /// </summary>
    private Dictionary<FoodItemData, int> GetIngredientsConsumedBy(FoodItemData foodItem)
    {
        var consumed = new Dictionary<FoodItemData, int>();

        // If this is a base ingredient, it consumes itself
        if (baseIngredients.Contains(foodItem))
        {
            consumed[foodItem] = 1;
            return consumed;
        }

        // If this is an intermediate result, find the combination that created it and trace back
        foreach (var stage in recipeStages)
        {
            foreach (var combo in stage.combinations)
            {
                if (combo.result == foodItem)
                {
                    // Found the combination that created this result
                    var ingredients1 = GetIngredientsConsumedBy(combo.ingredient1);
                    var ingredients2 = GetIngredientsConsumedBy(combo.ingredient2);

                    // Combine the consumption counts
                    foreach (var kvp in ingredients1)
                    {
                        consumed[kvp.Key] = consumed.GetValueOrDefault(kvp.Key, 0) + kvp.Value;
                    }
                    foreach (var kvp in ingredients2)
                    {
                        consumed[kvp.Key] = consumed.GetValueOrDefault(kvp.Key, 0) + kvp.Value;
                    }

                    return consumed;
                }
            }
        }

        // If we get here, we couldn't trace back the ingredients (shouldn't happen)
        DebugLog($"Warning: Could not trace back ingredients for {foodItem?.DisplayName}");
        return consumed;
    }

    /// <summary>
    /// Check if combining two food items would exceed available base ingredients
    /// </summary>
    private bool WouldExceedAvailableIngredients(FoodItemData item1, FoodItemData item2)
    {
        var consumed1 = GetIngredientsConsumedBy(item1);
        var consumed2 = GetIngredientsConsumedBy(item2);

        // Combine consumption counts
        var totalConsumed = new Dictionary<FoodItemData, int>();
        foreach (var kvp in consumed1)
        {
            totalConsumed[kvp.Key] = totalConsumed.GetValueOrDefault(kvp.Key, 0) + kvp.Value;
        }
        foreach (var kvp in consumed2)
        {
            totalConsumed[kvp.Key] = totalConsumed.GetValueOrDefault(kvp.Key, 0) + kvp.Value;
        }

        // Check if any ingredient would be exceeded
        foreach (var kvp in totalConsumed)
        {
            int available = baseIngredients.Count(x => x == kvp.Key);
            if (kvp.Value > available)
            {
                DebugLog($"Combination would exceed available {kvp.Key.DisplayName}: needs {kvp.Value}, have {available}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get remaining base ingredients after accounting for what's consumed by a specific food item
    /// </summary>
    private List<FoodItemData> GetRemainingBaseIngredientsAfter(FoodItemData consumingItem)
    {
        var consumed = GetIngredientsConsumedBy(consumingItem);
        var remaining = new List<FoodItemData>();

        // For each base ingredient type, add the remaining instances
        foreach (var baseIngredient in baseIngredients.Distinct())
        {
            int totalAvailable = baseIngredients.Count(x => x == baseIngredient);
            int consumedCount = consumed.GetValueOrDefault(baseIngredient, 0);
            int remainingCount = totalAvailable - consumedCount;

            for (int i = 0; i < remainingCount; i++)
            {
                remaining.Add(baseIngredient);
            }
        }

        return remaining;
    }

    /// <summary>
    /// Check if a combination creates the final dish and mark it accordingly
    /// </summary>
    private void CheckAndMarkAsFinalDish(StageCombination combination)
    {
        // For now, we'll leave this for the designer to manually assign
        // But we can add logic here if needed to auto-detect final dishes
        // based on ingredient counting or other rules

        // The designer will assign the result, and if it matches finalDish,
        // we'll handle it in the validation phase
    }

    /// <summary>
    /// Get remaining base ingredients that haven't been used in a specific combination path (legacy method for compatibility)
    /// </summary>
    private List<FoodItemData> GetRemainingBaseIngredients(List<FoodItemData> usedIngredients)
    {
        return baseIngredients.Except(usedIngredients).ToList();
    }

    /// <summary>
    /// Check if two ingredient lists overlap (share any common ingredients)
    /// </summary>
    private bool IngredientsOverlap(List<FoodItemData> ingredients1, List<FoodItemData> ingredients2)
    {
        return ingredients1.Any(ingredient => ingredients2.Contains(ingredient));
    }

    /// <summary>
    /// Add a combination to the stage if it's unique and doesn't exceed available ingredients
    /// </summary>
    private StageCombination AddCombinationIfUnique(RecipeStage stage, FoodItemData ingredient1, FoodItemData ingredient2, HashSet<string> generatedCombinations)
    {
        // First check if this combination would exceed available ingredients
        if (WouldExceedAvailableIngredients(ingredient1, ingredient2))
        {
            DebugLog($"Skipping combination {ingredient1.DisplayName} + {ingredient2.DisplayName} - would exceed available ingredients");
            return null;
        }

        // Create a unique key for this combination (sorted to avoid duplicates)
        var sortedNames = new[] { ingredient1.name, ingredient2.name }.OrderBy(n => n).ToArray();
        var combinationKey = string.Join("|", sortedNames);

        // Only add if we haven't seen this combination before
        if (!generatedCombinations.Contains(combinationKey))
        {
            generatedCombinations.Add(combinationKey);

            var combination = new StageCombination
            {
                ingredient1 = ingredient1,
                ingredient2 = ingredient2,
                result = null
            };
            combination.UpdateDisplay();
            stage.combinations.Add(combination);

            DebugLog($"Added valid combination: {ingredient1.DisplayName} + {ingredient2.DisplayName}");
            return combination;
        }

        return null;
    }

    /// <summary>
    /// Check if the stage should be marked as final
    /// </summary>
    private bool ShouldBeFinalStage(RecipeStage stage)
    {
        // A stage should be final if no more valid combinations are possible
        // This is now handled by CanGenerateNextStage(), so we'll be more conservative here

        // Only mark as final if we have very few combinations AND those combinations use most ingredients
        if (stage.combinations.Count <= 1)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generate all remaining stages automatically (if previous stages are complete)
    /// </summary>
    [Button("Auto-Generate All Remaining Stages", ButtonSizes.Large)]
    [GUIColor(0.8f, 0.6f, 1f)]
    [ShowIf("@CanGenerateNextStage()")]
    public void AutoGenerateAllStages()
    {
        while (CanGenerateNextStage())
        {
            GenerateNextStage();

            // Safety check to prevent infinite loops
            if (recipeStages.Count > 10)
            {
                Debug.LogWarning("[RecipeCombinationGenerator] Safety limit reached. Stopping auto-generation.");
                break;
            }
        }

        DebugLog("Auto-generation complete. Check if recipe has all desired final dish combinations.");
    }

    /// <summary>
    /// Check if any combination in any stage creates the final dish
    /// </summary>
    private bool HasAnyFinalDishCombinations()
    {
        return recipeStages.Any(stage =>
            stage.combinations.Any(combo => combo.result == finalDish));
    }

    /// <summary>
    /// Generate combinations for a stage from a list of available ingredients
    /// </summary>
    private RecipeStage GenerateStageFromIngredients(int stageNumber, List<FoodItemData> availableIngredients)
    {
        var stage = new RecipeStage
        {
            stageNumber = stageNumber,
            isFinalStage = false
        };

        // Generate all possible unique 2-item combinations
        var uniqueCombinations = new HashSet<string>();

        for (int i = 0; i < availableIngredients.Count; i++)
        {
            for (int j = i + 1; j < availableIngredients.Count; j++)
            {
                var ingredient1 = availableIngredients[i];
                var ingredient2 = availableIngredients[j];

                // Create a unique key for this combination (sorted to avoid duplicates)
                var sortedNames = new[] { ingredient1.name, ingredient2.name }.OrderBy(n => n).ToArray();
                var combinationKey = string.Join("|", sortedNames);

                // Only add if we haven't seen this combination before
                if (!uniqueCombinations.Contains(combinationKey))
                {
                    uniqueCombinations.Add(combinationKey);

                    var combination = new StageCombination
                    {
                        ingredient1 = ingredient1,
                        ingredient2 = ingredient2,
                        result = null
                    };
                    combination.UpdateDisplay();
                    stage.combinations.Add(combination);
                }
            }
        }

        DebugLog($"Stage {stageNumber}: Generated {stage.combinations.Count} unique combinations from {availableIngredients.Count} available ingredients");

        return stage;
    }

    /// <summary>
    /// Check if a stage is complete (all combinations have results assigned)
    /// </summary>
    private bool IsStageComplete(RecipeStage stage)
    {
        return stage.combinations.All(combo => combo.result != null);
    }

    /// <summary>
    /// Check if we can generate the next stage
    /// </summary>
    private bool CanGenerateNextStage()
    {
        if (recipeStages.Count == 0) return false;

        var lastStage = recipeStages[recipeStages.Count - 1];

        // Can't generate next stage if current stage isn't complete
        if (!IsStageComplete(lastStage)) return false;

        // Can't generate next stage if we're already at the final stage
        if (lastStage.isFinalStage) return false;

        // For Stage 1 -> Stage 2, we can always generate if Stage 1 is complete
        if (recipeStages.Count == 1) return true;

        // For later stages, check if we have any non-final results that can still be combined
        var previousResults = lastStage.combinations
            .Where(combo => combo.result != null && combo.result != finalDish)
            .Select(combo => combo.result)
            .ToList();

        // Check if any previous result can still be combined with remaining base ingredients
        foreach (var result in previousResults)
        {
            var remainingForThisResult = GetRemainingBaseIngredientsAfter(result);
            if (remainingForThisResult.Count > 0)
            {
                return true; // Can combine this result with at least one remaining ingredient
            }
        }

        // Check if any two previous results can be combined without exceeding ingredient limits
        for (int i = 0; i < previousResults.Count; i++)
        {
            for (int j = i + 1; j < previousResults.Count; j++)
            {
                if (!WouldExceedAvailableIngredients(previousResults[i], previousResults[j]))
                {
                    return true; // Can combine these two results
                }
            }
        }

        // No valid combinations possible
        return false;
    }

    /// <summary>
    /// Add all configured combinations to the database
    /// </summary>
    [Button("Add All Combinations to Database", ButtonSizes.Large)]
    [GUIColor(0.4f, 1f, 0.4f)]
    [ShowIf("@AreAllStagesComplete()")]
    public void AddCombinationsToDatabase()
    {
        if (combinationDatabase == null)
        {
            Debug.LogError("[RecipeCombinationGenerator] No combination database assigned!");
            return;
        }

        int addedCount = 0;
        int skippedCount = 0;

        foreach (var stage in recipeStages)
        {
            foreach (var combo in stage.combinations)
            {
                if (combo.result == null)
                {
                    Debug.LogWarning($"[RecipeCombinationGenerator] Skipping Stage {stage.stageNumber} combination {combo.CombinationName} - no result assigned");
                    skippedCount++;
                    continue;
                }

                var ingredients = new List<FoodItemData> { combo.ingredient1, combo.ingredient2 };

                // Check if combination already exists
                if (combinationDatabase.HasCombination(ingredients))
                {
                    DebugLog($"Combination already exists: {combo.CombinationName}");
                    skippedCount++;
                    continue;
                }

                // Add the combination
                if (combinationDatabase.AddCombination(ingredients, combo.result))
                {
                    DebugLog($"Added combination: {combo.CombinationName} = {combo.result.DisplayName}");
                    addedCount++;
                }
                else
                {
                    Debug.LogWarning($"Failed to add combination: {combo.CombinationName}");
                    skippedCount++;
                }
            }
        }

        Debug.Log($"[RecipeCombinationGenerator] Added {addedCount} combinations, skipped {skippedCount}");

        // Mark the database as dirty
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(combinationDatabase);
#endif
    }

    /// <summary>
    /// Check if all generated stages are complete
    /// </summary>
    private bool AreAllStagesComplete()
    {
        return recipeStages.Count > 0 && recipeStages.All(stage => IsStageComplete(stage));
    }

    /// <summary>
    /// Clear all generated combinations and start over
    /// </summary>
    [Button("Clear All Stages")]
    [GUIColor(1f, 0.4f, 0.4f)]
    public void ClearGeneratedCombinations()
    {
        recipeStages.Clear();
        finalDishCombinations.Clear();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        DebugLog("Cleared all stages");
    }

    /// <summary>
    /// Validate the current setup and check for final dish combinations
    /// </summary>
    [Button("Validate Setup")]
    public void ValidateSetup()
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        // Basic validation
        if (finalDish == null)
            issues.Add("Final dish is not assigned");

        if (baseIngredients.Count < 2)
            issues.Add("Need at least 2 base ingredients");

        if (baseIngredients.Any(ingredient => ingredient == null))
            issues.Add("Some base ingredients are null");

        if (combinationDatabase == null)
            issues.Add("Combination database is not assigned");

        // Check stage completion and final dish detection
        int finalDishCount = 0;
        for (int i = 0; i < recipeStages.Count; i++)
        {
            var stage = recipeStages[i];
            var unassignedInStage = stage.combinations.Count(combo => combo.result == null);
            if (unassignedInStage > 0)
                issues.Add($"Stage {stage.stageNumber} has {unassignedInStage} unassigned combinations");

            // Count final dish occurrences
            var finalDishInStage = stage.combinations.Count(combo => combo.result == finalDish);
            finalDishCount += finalDishInStage;

            if (finalDishInStage > 0)
            {
                warnings.Add($"Stage {stage.stageNumber} has {finalDishInStage} combinations that create the final dish");
            }
        }

        // Check if we have the right number of final dish combinations
        if (finalDishCount == 0 && recipeStages.Count > 0)
        {
            warnings.Add("No combinations create the final dish yet - this is expected if recipe is incomplete");
        }
        else if (finalDishCount > 1)
        {
            warnings.Add($"Multiple combinations ({finalDishCount}) create the final dish - this may be intentional for complex recipes");
        }

        // Report results
        if (issues.Count == 0 && warnings.Count == 0)
        {
            Debug.Log("[RecipeCombinationGenerator] Setup validation passed! ✓");
        }
        else
        {
            if (issues.Count > 0)
            {
                Debug.LogError($"[RecipeCombinationGenerator] Setup has {issues.Count} issues:\n- " + string.Join("\n- ", issues));
            }

            if (warnings.Count > 0)
            {
                Debug.LogWarning($"[RecipeCombinationGenerator] Setup has {warnings.Count} warnings:\n- " + string.Join("\n- ", warnings));
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[RecipeCombinationGenerator] {message}");
    }

    private string GetStageInfoMessage()
    {
        if (baseIngredients.Count < 2)
            return "Add base ingredients first, then generate Stage 1 combinations.";

        if (recipeStages.Count == 0)
            return "Click 'Generate Stage 1 Combinations' to begin the progressive recipe setup.";

        var completedStages = recipeStages.Count(stage => IsStageComplete(stage));
        var totalCombinations = recipeStages.Sum(stage => stage.combinations.Count);
        var assignedCombinations = recipeStages.Sum(stage => stage.combinations.Count(combo => combo.result != null));
        var finalDishCombos = recipeStages.Sum(stage => stage.combinations.Count(combo => combo.result == finalDish));

        string statusMessage = $"{completedStages}/{recipeStages.Count} stages complete | {assignedCombinations}/{totalCombinations} combinations assigned";

        if (finalDishCombos > 0)
            statusMessage += $" | {finalDishCombos} final dish combinations found";

        if (CanGenerateNextStage())
            statusMessage += " | Ready to generate next stage";
        else if (recipeStages.Count > 0 && (recipeStages[recipeStages.Count - 1].isFinalStage || HasAnyFinalDishCombinations()))
            statusMessage += " | Recipe complete!";

        return statusMessage;
    }
}

/// <summary>
/// Represents a stage in the recipe with all possible combinations for that stage
/// </summary>
[System.Serializable]
public class RecipeStage
{
    [HideInInspector]
    public int stageNumber;

    [HideInInspector]
    public bool isFinalStage;

    [InfoBox("@GetStageInfo()")]
    [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "CombinationName")]
    public List<StageCombination> combinations = new List<StageCombination>();

    public string StageName => $"Stage {stageNumber}{(isFinalStage ? " (Final)" : "")}";

    private string GetStageInfo()
    {
        var assignedCount = combinations.Count(c => c.result != null);
        var unassignedCount = combinations.Count - assignedCount;

        string stageInfo = $"Stage {stageNumber}";
        if (isFinalStage)
            stageInfo += " - Final combinations to create the target dish";
        else
            stageInfo += " - Combine any 2 available ingredients";

        if (combinations.Count > 0)
        {
            stageInfo += $"\nProgress: {assignedCount}/{combinations.Count} assigned";
            if (unassignedCount > 0)
                stageInfo += $" ({unassignedCount} remaining)";
            else
                stageInfo += " ✓ Complete";
        }

        return stageInfo;
    }
}

/// <summary>
/// Represents a single combination within a stage (always 2 ingredients)
/// </summary>
[System.Serializable]
public class StageCombination
{
    [HorizontalGroup("Ingredients", Width = 0.4f)]
    [ReadOnly, HideLabel]
    [DisplayAsString]
    public string ingredientDisplay;

    [HideInInspector]
    public FoodItemData ingredient1;

    [HideInInspector]
    public FoodItemData ingredient2;

    [HorizontalGroup("Result", Width = 0.4f)]
    [LabelWidth(50)]
    public FoodItemData result;

    [HorizontalGroup("Status", Width = 0.2f)]
    [ShowInInspector, ReadOnly, HideLabel]
    private string StatusIcon => result != null ? "✓" : "⚠";

    public string CombinationName => ingredient1 != null && ingredient2 != null
        ? $"{ingredient1.DisplayName} + {ingredient2.DisplayName}"
        : "Invalid Combination";

    public void UpdateDisplay()
    {
        ingredientDisplay = CombinationName;
    }
}