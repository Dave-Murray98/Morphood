using UnityEngine;
using System;

/// <summary>
/// Enhanced ingredient system that tracks cooking states and supports different preparation methods.
/// Extends PickupableItem to add cooking-specific functionality.
/// </summary>
public class CookingIngredient : PickupableItem
{
    [Header("Ingredient Properties")]
    [SerializeField] private IngredientType ingredientType = IngredientType.Onion;
    [Tooltip("What type of ingredient this is")]

    [SerializeField] private IngredientState currentState = IngredientState.Raw;
    [Tooltip("Current preparation state of this ingredient")]

    [Header("Cooking Settings")]
    [SerializeField] private bool canBeChopped = true;
    [Tooltip("Whether this ingredient can be chopped")]

    [SerializeField] private bool canBeCooked = true;
    [Tooltip("Whether this ingredient can be cooked")]

    [SerializeField] private float cookingTime = 5f;
    [Tooltip("Time in seconds to cook this ingredient")]

    [SerializeField] private float burningTime = 3f;
    [Tooltip("Additional time before the ingredient burns")]

    [Header("Visual Feedback")]
    [SerializeField] private Material rawMaterial;
    [SerializeField] private Material choppedMaterial;
    [SerializeField] private Material cookedMaterial;
    [SerializeField] private Material burntMaterial;
    [Tooltip("Materials for different cooking states")]

    [Header("Audio")]
    [SerializeField] private AudioClip choppedSound;
    [SerializeField] private AudioClip cookedSound;
    [SerializeField] private AudioClip burntSound;

    [Header("Debug")]
    [SerializeField] private bool enableCookingDebugLogs = false;

    // Internal cooking state
    private float currentCookingTime = 0f;
    private bool isBeingCooked = false;
    private Renderer ingredientRenderer;

    // Events for other systems to listen to
    public static event Action<CookingIngredient, IngredientState> OnIngredientStateChanged;

    // Public properties
    public IngredientType IngredientType => ingredientType;
    public IngredientState State => currentState;
    public bool CanBeChopped => canBeChopped && currentState == IngredientState.Raw;
    public bool CanBeCooked => canBeCooked && (currentState == IngredientState.Raw || currentState == IngredientState.Chopped);
    public bool IsBeingCooked => isBeingCooked;
    public float CookingProgress => isBeingCooked ? Mathf.Clamp01(currentCookingTime / cookingTime) : 0f;
    public bool IsEdible => currentState == IngredientState.Cooked;
    public bool IsSpoiled => currentState == IngredientState.Burnt;

    protected override void Start()
    {
        base.Start();

        // Set up ingredient-specific properties
        itemType = ItemType.Ingredient;
        itemName = GetIngredientDisplayName();

        // Get renderer for visual feedback
        ingredientRenderer = GetComponent<Renderer>();

        // Apply initial visual state
        UpdateVisuals();

        CookingDebugLog($"Cooking ingredient '{itemName}' initialized in {currentState} state");
    }

    #region State Management

    /// <summary>
    /// Chop this ingredient (only Player 2 can do this)
    /// </summary>
    public bool TryChop()
    {
        if (!CanBeChopped)
        {
            CookingDebugLog($"Cannot chop {itemName} - current state: {currentState}");
            return false;
        }

        ChangeState(IngredientState.Chopped);

        // Play sound effect
        if (choppedSound != null)
        {
            AudioSource.PlayClipAtPoint(choppedSound, transform.position);
        }

        CookingDebugLog($"Chopped {itemName}");
        return true;
    }

    /// <summary>
    /// Start cooking this ingredient
    /// </summary>
    public bool StartCooking()
    {
        if (!CanBeCooked || isBeingCooked)
        {
            CookingDebugLog($"Cannot start cooking {itemName} - CanBeCooked: {CanBeCooked}, IsBeingCooked: {isBeingCooked}");
            return false;
        }

        isBeingCooked = true;
        currentCookingTime = 0f;

        CookingDebugLog($"Started cooking {itemName}");
        return true;
    }

    /// <summary>
    /// Stop cooking this ingredient
    /// </summary>
    public void StopCooking()
    {
        if (!isBeingCooked) return;

        isBeingCooked = false;
        CookingDebugLog($"Stopped cooking {itemName} at {currentCookingTime:F1}s");
    }

    /// <summary>
    /// Update cooking progress (called by cooking stations)
    /// </summary>
    public void UpdateCooking(float deltaTime)
    {
        if (!isBeingCooked) return;

        currentCookingTime += deltaTime;

        // Check if cooking is complete
        if (currentCookingTime >= cookingTime && currentState != IngredientState.Cooked)
        {
            ChangeState(IngredientState.Cooked);

            // Play cooked sound
            if (cookedSound != null)
            {
                AudioSource.PlayClipAtPoint(cookedSound, transform.position);
            }
        }

        // Check if ingredient is burning
        else if (currentCookingTime >= cookingTime + burningTime && currentState != IngredientState.Burnt)
        {
            ChangeState(IngredientState.Burnt);

            // Play burnt sound
            if (burntSound != null)
            {
                AudioSource.PlayClipAtPoint(burntSound, transform.position);
            }
        }
    }

    /// <summary>
    /// Change the ingredient's state and update visuals
    /// </summary>
    private void ChangeState(IngredientState newState)
    {
        if (currentState == newState) return;

        IngredientState previousState = currentState;
        currentState = newState;

        // Update item name to reflect new state
        itemName = GetIngredientDisplayName();

        // Update interaction prompt
        interactionPrompt = $"Pick up {itemName}";

        // Update visuals
        UpdateVisuals();

        // Fire event
        OnIngredientStateChanged?.Invoke(this, newState);

        CookingDebugLog($"Ingredient state changed: {previousState} → {newState}");
    }

    #endregion

    #region Visual and Audio Feedback

    /// <summary>
    /// Update the visual appearance based on current state
    /// </summary>
    private void UpdateVisuals()
    {
        if (ingredientRenderer == null) return;

        Material targetMaterial = GetMaterialForState(currentState);
        if (targetMaterial != null)
        {
            ingredientRenderer.material = targetMaterial;
        }
    }

    /// <summary>
    /// Get the appropriate material for a given state
    /// </summary>
    private Material GetMaterialForState(IngredientState state)
    {
        switch (state)
        {
            case IngredientState.Raw: return rawMaterial;
            case IngredientState.Chopped: return choppedMaterial;
            case IngredientState.Cooked: return cookedMaterial;
            case IngredientState.Burnt: return burntMaterial;
            default: return rawMaterial;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get the display name for this ingredient based on its type and state
    /// </summary>
    private string GetIngredientDisplayName()
    {
        string baseName = GetIngredientTypeName(ingredientType);

        switch (currentState)
        {
            case IngredientState.Raw: return baseName;
            case IngredientState.Chopped: return $"Chopped {baseName}";
            case IngredientState.Cooked: return $"Cooked {baseName}";
            case IngredientState.Burnt: return $"Burnt {baseName}";
            default: return baseName;
        }
    }

    /// <summary>
    /// Get the base name for an ingredient type
    /// </summary>
    private string GetIngredientTypeName(IngredientType type)
    {
        switch (type)
        {
            case IngredientType.Onion: return "Onion";
            case IngredientType.Tomato: return "Tomato";
            case IngredientType.Lettuce: return "Lettuce";
            case IngredientType.Meat: return "Meat";
            case IngredientType.Bread: return "Bread";
            case IngredientType.Cheese: return "Cheese";
            default: return "Ingredient";
        }
    }

    /// <summary>
    /// Check if this ingredient can be combined with another for recipes
    /// </summary>
    public bool CanCombineWith(CookingIngredient other)
    {
        // Basic compatibility check - both should be edible (cooked) or processable
        return (IsEdible || currentState == IngredientState.Chopped) &&
               (other.IsEdible || other.State == IngredientState.Chopped) &&
               !IsSpoiled && !other.IsSpoiled;
    }

    #endregion

    #region Debug and Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw cooking state indicator
        Color stateColor = GetStateColor();
        Gizmos.color = stateColor;
        Vector3 stateIndicatorPos = transform.position + Vector3.up * 3f;
        Gizmos.DrawCube(stateIndicatorPos, Vector3.one * 0.15f);

        // Draw cooking progress if being cooked
        if (isBeingCooked && Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            float progressHeight = CookingProgress * 1f;
            Vector3 progressPos = stateIndicatorPos + Vector3.up * 0.5f;
            Gizmos.DrawCube(progressPos, new Vector3(0.1f, progressHeight, 0.1f));
        }

#if UNITY_EDITOR
        // Show ingredient info in scene view
        if (Application.isPlaying)
        {
            string info = $"{itemName}\n{currentState}";
            if (isBeingCooked)
                info += $"\nCooking: {CookingProgress:P0}";
            UnityEditor.Handles.Label(stateIndicatorPos + Vector3.up * 0.8f, info);
        }
#endif
    }

    private Color GetStateColor()
    {
        switch (currentState)
        {
            case IngredientState.Raw: return Color.white;
            case IngredientState.Chopped: return Color.cyan;
            case IngredientState.Cooked: return Color.green;
            case IngredientState.Burnt: return Color.red;
            default: return Color.gray;
        }
    }

    private void CookingDebugLog(string message)
    {
        if (enableCookingDebugLogs)
            Debug.Log($"[CookingIngredient] {message}");
    }

    #endregion

    #region Validation

    protected override void OnValidate()
    {
        base.OnValidate();

        // Ensure reasonable cooking times
        cookingTime = Mathf.Max(0.5f, cookingTime);
        burningTime = Mathf.Max(0.5f, burningTime);

        // Update display name if changed in editor
        if (Application.isPlaying)
        {
            itemName = GetIngredientDisplayName();
        }
    }

    #endregion
}

/// <summary>
/// Different types of ingredients available
/// </summary>
public enum IngredientType
{
    Onion,
    Tomato,
    Lettuce,
    Meat,
    Bread,
    Cheese
}

/// <summary>
/// Different states an ingredient can be in
/// </summary>
public enum IngredientState
{
    Raw,        // Fresh, unprocessed
    Chopped,    // Cut up, ready to cook
    Cooked,     // Properly cooked, ready to eat
    Burnt       // Overcooked, spoiled
}