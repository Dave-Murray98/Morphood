using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using System.Linq;

/// <summary>
/// Custom editor for RecipeCombinationGenerator that provides validation and helpful messages
/// </summary>
[CustomEditor(typeof(RecipeCombinationGenerator))]
public class RecipeCombinationGeneratorEditor : OdinEditor
{
    private RecipeCombinationGenerator generator;

    protected override void OnEnable()
    {
        base.OnEnable();
        generator = (RecipeCombinationGenerator)target;
    }

    public override void OnInspectorGUI()
    {
        // Draw the default Odin inspector
        base.OnInspectorGUI();

        // Add helpful information at the bottom
        if (generator.recipeStages != null && generator.recipeStages.Count > 0)
        {
            DrawHelpfulTips();
        }
    }

    private void DrawHelpfulTips()
    {
        EditorGUILayout.Space(10);

        using (new EditorGUILayout.VerticalScope("helpbox"))
        {
            EditorGUILayout.LabelField("💡 Tips", EditorStyles.boldLabel);

            var totalCombinations = generator.recipeStages.Sum(stage => stage.combinations.Count);
            var unassignedCount = generator.recipeStages.Sum(stage => stage.combinations.Count(c => c.result == null));

            if (unassignedCount > 0)
            {
                EditorGUILayout.LabelField($"• {unassignedCount}/{totalCombinations} combinations need results assigned");
                EditorGUILayout.LabelField("• Each stage represents one step in the recipe");
                EditorGUILayout.LabelField("• Drag FoodItemData assets to the 'Result' fields above");
                EditorGUILayout.LabelField("• Create intermediate FoodItemData assets if needed");
            }
            else
            {
                EditorGUILayout.LabelField("✓ All combinations assigned!");
                EditorGUILayout.LabelField("• Click 'Add All Combinations to Database' when ready");
            }
        }
    }
}