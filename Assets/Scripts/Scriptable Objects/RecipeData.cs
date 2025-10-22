using UnityEngine;

[CreateAssetMenu(fileName = "New Recipe", menuName = "Cooking System/Recipe Data")]
public class RecipeData : ScriptableObject
{
    [Header("Recipe Info")]
    public string recipeName;
    [TextArea]
    public string description;

    [Header("Ingredients")]
    public IngredientData[] requiredIngredients;

    [Header("Result")]
    public Mesh resultMesh;
    public Material resultMaterial;
}
