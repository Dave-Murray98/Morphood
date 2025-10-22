using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a dish or plate that can hold multiple ingredients to create meals.
/// Players can add ingredients to dishes and serve completed meals.
/// </summary>
public class Dish : PickupableItem
{
    [Header("Dish Properties")]
    [SerializeField] private DishType dishType = DishType.Plate;
    [Tooltip("What type of dish this is")]

    [SerializeField] private int maxIngredients = 4;
    [Tooltip("Maximum number of ingredients this dish can hold")]

    [Header("Ingredient Management")]
    [SerializeField] private Transform[] ingredientSlots;
    [Tooltip("Positions where ingredients will be placed on the dish")]

    [SerializeField] private Vector3 ingredientScale = Vector3.one * 0.8f;
    [Tooltip("Scale factor for ingredients when placed on dish")]

    [Header("Meal Detection")]
    [SerializeField] private bool autoDetectMeals = true;
    [Tooltip("Whether to automatically detect completed meals")]

    [Header("Visual Feedback")]
    [SerializeField] private Material emptyDishMaterial;
    [SerializeField] private Material dishWithFoodMaterial;
    [SerializeField] private Material completedMealMaterial;
    [Tooltip("Materials for different dish states")]

    [Header("Debug")]
    [SerializeField] private bool enableDishDebugLogs = false;

    // Internal state
    private List<Ingredient> ingredientsOnDish = new List<Ingredient>();
    private DishState currentDishState = DishState.Empty;
    private CompletedMeal detectedMeal = null;
    private Renderer dishRenderer;

    // Events
    public System.Action<Dish, Ingredient> OnIngredientAdded;
    public System.Action<Dish, Ingredient> OnIngredientRemoved;
    public System.Action<Dish, CompletedMeal> OnMealCompleted;

    // Public properties
    public DishType DishType => dishType;
    public DishState State => currentDishState;
    public int IngredientCount => ingredientsOnDish.Count;
    public bool HasSpace => ingredientsOnDish.Count < maxIngredients;
    public bool IsEmpty => ingredientsOnDish.Count == 0;
    public IReadOnlyList<Ingredient> Ingredients => ingredientsOnDish.AsReadOnly();
    public CompletedMeal DetectedMeal => detectedMeal;
    public bool IsCompletedMeal => detectedMeal != null;

    protected override void Start()
    {
        base.Start();

        // Set up dish-specific properties
        itemType = ItemType.Dish;
        itemName = GetDishDisplayName();

        // Get renderer for visual feedback
        dishRenderer = GetComponent<Renderer>();

        // Set up ingredient slots if not configured
        SetupIngredientSlots();

        // Apply initial visual state
        UpdateVisuals();

        DishDebugLog($"Dish '{itemName}' initialized with {maxIngredients} ingredient slots");
    }

    #region Ingredient Management

    /// <summary>
    /// Try to add an ingredient to this dish
    /// </summary>
    public bool TryAddIngredient(Ingredient ingredient)
    {
        if (!CanAddIngredient(ingredient))
        {
            DishDebugLog($"Cannot add {ingredient.IngredientName} to dish - HasSpace: {HasSpace}, IsEdible: {ingredient.IsEdible}");
            return false;
        }

        // Find available slot
        int slotIndex = GetNextAvailableSlot();
        if (slotIndex < 0)
        {
            DishDebugLog($"No available slots for ingredient");
            return false;
        }

        // Add ingredient to dish
        ingredientsOnDish.Add(ingredient);

        // Position the ingredient on the dish
        PositionIngredientOnDish(ingredient, slotIndex);

        // Update dish state
        UpdateDishState();

        // Fire event
        OnIngredientAdded?.Invoke(this, ingredient);

        DishDebugLog($"Added {ingredient.IngredientName} to dish (slot {slotIndex})");
        return true;
    }

    /// <summary>
    /// Try to remove an ingredient from this dish
    /// </summary>
    public bool TryRemoveIngredient(Ingredient ingredient)
    {
        if (!ingredientsOnDish.Contains(ingredient))
        {
            DishDebugLog($"Ingredient {ingredient.IngredientName} not found on dish");
            return false;
        }

        // Remove from dish
        ingredientsOnDish.Remove(ingredient);

        // Restore ingredient's original properties
        RestoreIngredientProperties(ingredient);

        // Update dish state
        UpdateDishState();

        // Fire event
        OnIngredientRemoved?.Invoke(this, ingredient);

        DishDebugLog($"Removed {ingredient.IngredientName} from dish");
        return true;
    }

    /// <summary>
    /// Remove the last added ingredient (for easy removal)
    /// </summary>
    public Ingredient RemoveLastIngredient()
    {
        if (IsEmpty) return null;

        Ingredient lastIngredient = ingredientsOnDish[ingredientsOnDish.Count - 1];
        if (TryRemoveIngredient(lastIngredient))
        {
            return lastIngredient;
        }

        return null;
    }

    /// <summary>
    /// Clear all ingredients from the dish
    /// </summary>
    public List<Ingredient> ClearAllIngredients()
    {
        List<Ingredient> removedIngredients = new List<Ingredient>(ingredientsOnDish);

        foreach (Ingredient ingredient in removedIngredients)
        {
            RestoreIngredientProperties(ingredient);
        }

        ingredientsOnDish.Clear();
        UpdateDishState();

        DishDebugLog($"Cleared all ingredients from dish");
        return removedIngredients;
    }

    /// <summary>
    /// Check if an ingredient can be added to this dish
    /// </summary>
    public bool CanAddIngredient(Ingredient ingredient)
    {
        // Must have space
        if (!HasSpace) return false;

        // Ingredient must be edible or processable
        if (!ingredient.IsEdible && !ingredient.Data.IsEdible) return false;

        // Cannot add spoiled ingredients
        if (ingredient.IsSpoiled) return false;

        // Cannot add duplicate ingredient types (optional rule)
        // if (ingredientsOnDish.Any(i => i.Data.Category == ingredient.Data.Category)) return false;

        return true;
    }

    #endregion

    #region Meal Detection

    /// <summary>
    /// Update the dish state and check for completed meals
    /// </summary>
    private void UpdateDishState()
    {
        DishState previousState = currentDishState;

        if (IsEmpty)
        {
            currentDishState = DishState.Empty;
            detectedMeal = null;
        }
        else
        {
            // Check for completed meals
            if (autoDetectMeals)
            {
                detectedMeal = DetectCompletedMeal();
            }

            if (detectedMeal != null)
            {
                currentDishState = DishState.CompletedMeal;
            }
            else
            {
                currentDishState = DishState.HasIngredients;
            }
        }

        // Update item name and visuals
        itemName = GetDishDisplayName();
        interactionPrompt = $"Pick up {itemName}";
        UpdateVisuals();

        // Fire meal completion event
        if (previousState != DishState.CompletedMeal && currentDishState == DishState.CompletedMeal)
        {
            OnMealCompleted?.Invoke(this, detectedMeal);
            DishDebugLog($"Meal completed: {detectedMeal.Name}");
        }

        DishDebugLog($"Dish state updated: {previousState} → {currentDishState}");
    }

    /// <summary>
    /// Detect if the current ingredients form a completed meal
    /// This is a simplified version - in a full game, you'd reference a recipe database
    /// </summary>
    private CompletedMeal DetectCompletedMeal()
    {
        if (ingredientsOnDish.Count < 2) return null;

        // Get ingredient categories on the dish
        var ingredientCategories = ingredientsOnDish.Select(i => i.Data.Category).OrderBy(c => c).ToList();

        // Simple meal detection logic
        // In a full game, this would query a recipe database

        // Burger: Grain + Meat + (optional: Vegetable, Dairy)
        if (ingredientCategories.Contains(IngredientCategory.Grain) && ingredientCategories.Contains(IngredientCategory.Meat))
        {
            bool hasVegetable = ingredientCategories.Contains(IngredientCategory.Vegetable);
            bool hasDairy = ingredientCategories.Contains(IngredientCategory.Dairy);

            string mealName = "Basic Burger";
            int score = 10;

            if (hasVegetable && hasDairy)
            {
                mealName = "Deluxe Burger";
                score = 30;
            }
            else if (hasVegetable || hasDairy)
            {
                mealName = "Premium Burger";
                score = 20;
            }

            return new CompletedMeal(mealName, score, new List<IngredientCategory>(ingredientCategories));
        }

        // Salad: Multiple vegetables + (optional: Dairy)
        if (ingredientCategories.Count(c => c == IngredientCategory.Vegetable) >= 2)
        {
            string mealName = ingredientCategories.Contains(IngredientCategory.Dairy) ? "Garden Salad with Cheese" : "Garden Salad";
            int score = ingredientCategories.Contains(IngredientCategory.Dairy) ? 20 : 15;

            return new CompletedMeal(mealName, score, new List<IngredientCategory>(ingredientCategories));
        }

        // Add more meal combinations here...

        return null;
    }

    #endregion

    #region Positioning and Visual Management

    /// <summary>
    /// Set up ingredient slots if not configured
    /// </summary>
    private void SetupIngredientSlots()
    {
        if (ingredientSlots == null || ingredientSlots.Length != maxIngredients)
        {
            ingredientSlots = new Transform[maxIngredients];

            // Create default slot positions in a circle around the dish
            for (int i = 0; i < maxIngredients; i++)
            {
                GameObject slot = new GameObject($"IngredientSlot_{i}");
                slot.transform.SetParent(transform);

                // Position slots in a circle
                float angle = (360f / maxIngredients) * i * Mathf.Deg2Rad;
                float radius = 0.3f;
                Vector3 slotPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.2f,
                    Mathf.Sin(angle) * radius
                );

                slot.transform.localPosition = slotPosition;
                ingredientSlots[i] = slot.transform;
            }

            DishDebugLog($"Created {maxIngredients} default ingredient slots");
        }
    }

    /// <summary>
    /// Position an ingredient on the dish at a specific slot
    /// </summary>
    private void PositionIngredientOnDish(Ingredient ingredient, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= ingredientSlots.Length) return;

        // Parent to the dish
        ingredient.transform.SetParent(ingredientSlots[slotIndex]);
        ingredient.transform.localPosition = Vector3.zero;
        ingredient.transform.localRotation = Quaternion.identity;
        ingredient.transform.localScale = ingredientScale;

        // Disable the ingredient's collider so it can't be picked up individually
        ingredient.SetDirectInteractionEnabled(false);

        // Make ingredient kinematic while on dish
        Rigidbody ingredientRb = ingredient.GetComponent<Rigidbody>();
        if (ingredientRb != null)
        {
            ingredientRb.isKinematic = true;
        }
    }

    /// <summary>
    /// Restore an ingredient's original properties when removed from dish
    /// </summary>
    private void RestoreIngredientProperties(Ingredient ingredient)
    {
        // Remove from dish hierarchy
        ingredient.transform.SetParent(null);
        ingredient.transform.localScale = Vector3.one;

        // Re-enable interaction
        ingredient.SetDirectInteractionEnabled(true);

        // Restore physics
        Rigidbody ingredientRb = ingredient.GetComponent<Rigidbody>();
        if (ingredientRb != null)
        {
            ingredientRb.isKinematic = false;
        }
    }

    /// <summary>
    /// Get the next available slot index
    /// </summary>
    private int GetNextAvailableSlot()
    {
        for (int i = 0; i < ingredientSlots.Length; i++)
        {
            if (i >= ingredientsOnDish.Count)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Update visual appearance based on dish state
    /// </summary>
    private void UpdateVisuals()
    {
        if (dishRenderer == null) return;

        Material targetMaterial = GetMaterialForState(currentDishState);
        if (targetMaterial != null)
        {
            dishRenderer.material = targetMaterial;
        }
    }

    /// <summary>
    /// Get the appropriate material for a dish state
    /// </summary>
    private Material GetMaterialForState(DishState state)
    {
        switch (state)
        {
            case DishState.Empty: return emptyDishMaterial;
            case DishState.HasIngredients: return dishWithFoodMaterial;
            case DishState.CompletedMeal: return completedMealMaterial;
            default: return emptyDishMaterial;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get the display name for this dish based on its contents
    /// </summary>
    private string GetDishDisplayName()
    {
        string baseType = GetDishTypeName(dishType);

        switch (currentDishState)
        {
            case DishState.Empty:
                return $"Empty {baseType}";
            case DishState.HasIngredients:
                return $"{baseType} with {IngredientCount} ingredient{(IngredientCount == 1 ? "" : "s")}";
            case DishState.CompletedMeal:
                return detectedMeal != null ? detectedMeal.Name : $"Completed {baseType}";
            default:
                return baseType;
        }
    }

    /// <summary>
    /// Get the base name for a dish type
    /// </summary>
    private string GetDishTypeName(DishType type)
    {
        switch (type)
        {
            case DishType.Plate: return "Plate";
            case DishType.Bowl: return "Bowl";
            case DishType.Tray: return "Tray";
            default: return "Dish";
        }
    }

    #endregion

    #region Debug and Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw dish state indicator
        Color stateColor = GetDishStateColor();
        Gizmos.color = stateColor;
        Vector3 stateIndicatorPos = transform.position + Vector3.up * 3.5f;
        Gizmos.DrawCube(stateIndicatorPos, Vector3.one * 0.2f);

        // Draw ingredient slots
        if (ingredientSlots != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < ingredientSlots.Length; i++)
            {
                if (ingredientSlots[i] != null)
                {
                    Gizmos.DrawWireCube(ingredientSlots[i].position, Vector3.one * 0.1f);
                }
            }
        }

#if UNITY_EDITOR
        // Show dish info in scene view
        if (Application.isPlaying)
        {
            string info = $"{itemName}\n{currentDishState}\n{IngredientCount}/{maxIngredients}";
            if (detectedMeal != null)
                info += $"\nScore: {detectedMeal.Score}";
            UnityEditor.Handles.Label(stateIndicatorPos + Vector3.up * 0.5f, info);
        }
#endif
    }

    private Color GetDishStateColor()
    {
        switch (currentDishState)
        {
            case DishState.Empty: return Color.white;
            case DishState.HasIngredients: return Color.yellow;
            case DishState.CompletedMeal: return Color.green;
            default: return Color.gray;
        }
    }

    private void DishDebugLog(string message)
    {
        if (enableDishDebugLogs)
            Debug.Log($"[Dish] {message}");
    }

    #endregion
}

/// <summary>
/// Different types of dishes
/// </summary>
public enum DishType
{
    Plate,
    Bowl,
    Tray
}

/// <summary>
/// Different states a dish can be in
/// </summary>
public enum DishState
{
    Empty,
    HasIngredients,
    CompletedMeal
}

/// <summary>
/// Represents a completed meal with its properties
/// </summary>
[System.Serializable]
public class CompletedMeal
{
    public string Name { get; private set; }
    public int Score { get; private set; }
    public List<IngredientCategory> RequiredCategories { get; private set; }

    public CompletedMeal(string name, int score, List<IngredientCategory> categories)
    {
        Name = name;
        Score = score;
        RequiredCategories = categories ?? new List<IngredientCategory>();
    }
}