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
            // Pre-assign final dish to all combinations in final stage
            foreach (var combo in nextStage.combinations)
            {
                combo.result = finalDish;
            }
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

        // For each Stage 1 result, find what base ingredients are still available to combine with it
        foreach (var combo1 in stage1.combinations.Where(c => c.result != null))
        {
            // Get the ingredients used in this Stage 1 combination
            var usedIngredients = new List<FoodItemData> { combo1.ingredient1, combo1.ingredient2 };

            // Find remaining base ingredients (not used in this specific combination)
            var remainingBaseIngredients = GetRemainingBaseIngredients(usedIngredients);

            // Combine this Stage 1 result with each remaining base ingredient
            foreach (var baseIngredient in remainingBaseIngredients)
            {
                AddCombinationIfUnique(stage2, combo1.result, baseIngredient, generatedCombinations);
            }
        }

        // Also combine Stage 1 results with each other (if they use different base ingredients)
        for (int i = 0; i < stage1.combinations.Count; i++)
        {
            for (int j = i + 1; j < stage1.combinations.Count; j++)
            {
                var combo1 = stage1.combinations[i];
                var combo2 = stage1.combinations[j];

                if (combo1.result != null && combo2.result != null)
                {
                    // Check if these combinations use different sets of base ingredients
                    var ingredients1 = new List<FoodItemData> { combo1.ingredient1, combo1.ingredient2 };
                    var ingredients2 = new List<FoodItemData> { combo2.ingredient1, combo2.ingredient2 };

                    if (!IngredientsOverlap(ingredients1, ingredients2))
                    {
                        AddCombinationIfUnique(stage2, combo1.result, combo2.result, generatedCombinations);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generate combinations for Stage 3 and beyond
    /// </summary>
    private void GenerateLaterStageCombinations(RecipeStage newStage, HashSet<string> generatedCombinations)
    {
        var previousStage = recipeStages[recipeStages.Count - 1];
        var previousResults = previousStage.combinations
            .Where(combo => combo.result != null)
            .Select(combo => combo.result)
            .ToList();

        // Combine all previous stage results with each other
        for (int i = 0; i < previousResults.Count; i++)
        {
            for (int j = i + 1; j < previousResults.Count; j++)
            {
                AddCombinationIfUnique(newStage, previousResults[i], previousResults[j], generatedCombinations);
            }
        }

        // If there are still unused base ingredients, combine them with previous results
        // This is more complex and might need additional logic based on your specific needs
        // For now, we'll keep it simple and just combine previous results
    }

    /// <summary>
    /// Get remaining base ingredients that haven't been used in a specific combination path
    /// </summary>
    private List<FoodItemData> GetRemainingBaseIngredients(List<FoodItemData> usedIngredients)
    {
        var remainingIngredients = new List<FoodItemData>();
        var availableBaseIngredients = new List<FoodItemData>(baseIngredients);

        // Remove used ingredients from the available list
        foreach (var usedIngredient in usedIngredients)
        {
            if (availableBaseIngredients.Contains(usedIngredient))
            {
                availableBaseIngredients.Remove(usedIngredient);
            }
        }

        return availableBaseIngredients;
    }

    /// <summary>
    /// Check if two ingredient lists overlap (share any common ingredients)
    /// </summary>
    private bool IngredientsOverlap(List<FoodItemData> ingredients1, List<FoodItemData> ingredients2)
    {
        return ingredients1.Any(ingredient => ingredients2.Contains(ingredient));
    }

    /// <summary>
    /// Add a combination to the stage if it's unique
    /// </summary>
    private void AddCombinationIfUnique(RecipeStage stage, FoodItemData ingredient1, FoodItemData ingredient2, HashSet<string> generatedCombinations)
    {
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
        }
    }

    /// <summary>
    /// Check if the stage should be marked as final
    /// </summary>
    private bool ShouldBeFinalStage(RecipeStage stage)
    {
        // If we have 2 or fewer total ingredients available, this should be the final stage
        // This is a simplified check - you might want more sophisticated logic
        return stage.combinations.Count <= 3; // Arbitrary threshold for now
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

        // For later stages, check if we have enough results to continue
        var previousResults = lastStage.combinations
            .Where(combo => combo.result != null)
            .Select(combo => combo.result)
            .ToList();

        return previousResults.Count >= 2;
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

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        DebugLog("Cleared all stages");
    }

    /// <summary>
    /// Validate the current setup
    /// </summary>
    [Button("Validate Setup")]
    public void ValidateSetup()
    {
        var issues = new List<string>();

        if (finalDish == null)
            issues.Add("Final dish is not assigned");

        if (baseIngredients.Count < 2)
            issues.Add("Need at least 2 base ingredients");

        if (baseIngredients.Any(ingredient => ingredient == null))
            issues.Add("Some base ingredients are null");

        if (combinationDatabase == null)
            issues.Add("Combination database is not assigned");

        // Check stage completion
        for (int i = 0; i < recipeStages.Count; i++)
        {
            var stage = recipeStages[i];
            var unassignedInStage = stage.combinations.Count(combo => combo.result == null);
            if (unassignedInStage > 0)
                issues.Add($"Stage {stage.stageNumber} has {unassignedInStage} unassigned combinations");
        }

        if (issues.Count == 0)
        {
            Debug.Log("[RecipeCombinationGenerator] Setup validation passed! ✓");
        }
        else
        {
            Debug.LogWarning($"[RecipeCombinationGenerator] Setup has {issues.Count} issues:\n- " + string.Join("\n- ", issues));
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

        string statusMessage = $"{completedStages}/{recipeStages.Count} stages complete | {assignedCombinations}/{totalCombinations} combinations assigned";

        if (CanGenerateNextStage())
            statusMessage += " | Ready to generate next stage";
        else if (recipeStages.Count > 0 && recipeStages[recipeStages.Count - 1].isFinalStage && IsStageComplete(recipeStages[recipeStages.Count - 1]))
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