using UnityEngine;
using System.Collections;

/// <summary>
/// Ingredient component that represents a specific ingredient in the world.
/// Uses IngredientData ScriptableObject to define its properties and behavior.
/// This approach allows each ingredient state to be a separate, well-defined asset.
/// </summary>
public class Ingredient : PickupableItem
{
    [Header("Ingredient Configuration")]
    [SerializeField] private IngredientData ingredientData;
    [Tooltip("The ScriptableObject that defines this ingredient's properties")]

    [Header("Visual Feedback")]
    [SerializeField] private Transform visualModel;
    [Tooltip("The visual representation of this ingredient")]

    [SerializeField] private Renderer ingredientRenderer;
    [Tooltip("Renderer for changing materials")]

    [Header("Debug")]
    [SerializeField] private bool enableIngredientDebugLogs = false;

    // Internal state
    private float currentFreshness = 1f; // 1 = completely fresh, 0 = spoiled
    private bool isSpoiled = false;
    private bool isBeingProcessed = false; // Currently being chopped/cooked
    private float processingProgress = 0f;
    private Coroutine processingCoroutine;

    // Events for other systems
    public System.Action<Ingredient> OnIngredientSpoiled;
    public System.Action<Ingredient> OnIngredientProcessed;
    public System.Action<Ingredient, float> OnProcessingProgress;

    // Public properties
    public IngredientData Data => ingredientData;
    public float Freshness => currentFreshness;
    public bool IsSpoiled => isSpoiled || (ingredientData != null && ingredientData.IsSpoiled());
    public bool IsBeingProcessed => isBeingProcessed;
    public float ProcessingProgress => processingProgress;

    // Convenience properties that delegate to IngredientData
    public string IngredientName => ingredientData?.IngredientName ?? "Unknown Ingredient";
    public bool CanBeChopped => ingredientData?.CanBeChopped ?? false;
    public bool CanBeCooked => ingredientData?.CanBeCooked ?? false;
    public bool IsEdible => ingredientData?.IsEdible ?? false && !IsSpoiled;

    protected override void Start()
    {
        base.Start();
        InitializeFromData();
    }

    #region Initialization

    /// <summary>
    /// Initialize this ingredient from its data
    /// </summary>
    private void InitializeFromData()
    {
        if (ingredientData == null)
        {
            Debug.LogError($"[{name}] No IngredientData assigned! This ingredient will not work properly.");
            return;
        }

        // Set up basic properties
        itemType = ItemType.Ingredient;
        itemName = ingredientData.IngredientName;
        interactionPrompt = $"Pick up {itemName}";

        // Set up visual appearance
        UpdateVisualAppearance();

        IngredientDebugLog($"Ingredient '{itemName}' initialized from data: {ingredientData.name}");
    }

    /// <summary>
    /// Update the visual appearance based on ingredient data
    /// </summary>
    private void UpdateVisualAppearance()
    {
        if (ingredientData == null) return;

        // Update material
        if (ingredientRenderer != null && ingredientData.IngredientMaterial != null)
        {
            ingredientRenderer.material = ingredientData.IngredientMaterial;
        }

        // If ingredient data has a prefab, we could swap models here
        // For now, we'll just update the material
    }

    /// <summary>
    /// Set new ingredient data (for transformations)
    /// </summary>
    public void SetIngredientData(IngredientData newData)
    {
        if (newData == null) return;

        IngredientData previousData = ingredientData;
        ingredientData = newData;

        // Update all properties
        itemName = ingredientData.IngredientName;
        interactionPrompt = $"Pick up {itemName}";

        // Update visual appearance
        UpdateVisualAppearance();

        // Play transformation sound
        if (ingredientData.IngredientSound != null)
        {
            AudioSource.PlayClipAtPoint(ingredientData.IngredientSound, transform.position);
        }

        IngredientDebugLog($"Ingredient transformed: {previousData?.IngredientName} → {ingredientData.IngredientName}");
    }

    #endregion

    #region Processing (Chopping/Cooking)

    /// <summary>
    /// Start processing this ingredient (chopping, cooking, etc.)
    /// </summary>
    public bool StartProcessing(TransformationType transformationType)
    {
        if (isBeingProcessed)
        {
            IngredientDebugLog($"Cannot start {transformationType} - already being processed");
            return false;
        }

        if (!CanPerformTransformation(transformationType))
        {
            IngredientDebugLog($"Cannot perform transformation: {transformationType}");
            return false;
        }

        isBeingProcessed = true;
        processingProgress = 0f;

        float processingTime = ingredientData.GetTransformationTime(transformationType);
        processingCoroutine = StartCoroutine(ProcessingCoroutine(transformationType, processingTime));

        IngredientDebugLog($"Started {transformationType} process for {itemName} (duration: {processingTime}s)");
        return true;
    }

    /// <summary>
    /// Stop processing this ingredient
    /// </summary>
    public void StopProcessing()
    {
        if (!isBeingProcessed) return;

        isBeingProcessed = false;
        processingProgress = 0f;

        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

        IngredientDebugLog($"Stopped processing {itemName}");
    }

    /// <summary>
    /// Update processing progress (called by stations)
    /// </summary>
    public void UpdateProcessing(float deltaTime, float totalTime)
    {
        if (!isBeingProcessed) return;

        processingProgress += deltaTime / totalTime;
        processingProgress = Mathf.Clamp01(processingProgress);

        OnProcessingProgress?.Invoke(this, processingProgress);
    }

    /// <summary>
    /// Coroutine that handles the processing over time
    /// </summary>
    private IEnumerator ProcessingCoroutine(TransformationType transformationType, float processingTime)
    {
        float elapsedTime = 0f;

        while (elapsedTime < processingTime && isBeingProcessed)
        {
            yield return new WaitForSeconds(0.1f);
            elapsedTime += 0.1f;

            processingProgress = elapsedTime / processingTime;
            OnProcessingProgress?.Invoke(this, processingProgress);
        }

        // Processing completed
        if (isBeingProcessed)
        {
            CompleteProcessing(transformationType);
        }
    }

    /// <summary>
    /// Complete the processing and transform the ingredient
    /// </summary>
    private void CompleteProcessing(TransformationType transformationType)
    {
        IngredientData resultData = ingredientData.GetTransformationResult(transformationType);

        if (resultData != null)
        {
            SetIngredientData(resultData);
            OnIngredientProcessed?.Invoke(this);
            IngredientDebugLog($"Processing completed: {transformationType} successful");
        }
        else
        {
            IngredientDebugLog($"Processing completed but no result data found for {transformationType}");
        }

        StopProcessing();
    }

    /// <summary>
    /// Check if this ingredient can perform a specific transformation
    /// </summary>
    public bool CanPerformTransformation(TransformationType transformationType)
    {
        if (ingredientData == null || IsSpoiled) return false;

        switch (transformationType)
        {
            case TransformationType.Chopping:
                return ingredientData.CanBeChopped && ingredientData.ChoppedResult != null;
            case TransformationType.Cooking:
                return ingredientData.CanBeCooked && ingredientData.CookedResult != null;
            case TransformationType.Burning:
                return ingredientData.BurntResult != null;
            default:
                return false;
        }
    }
    #endregion

    /// <summary>
    /// Called when the ingredient becomes spoiled
    /// </summary>
    private void BecameSpoiled()
    {
        isSpoiled = true;
        itemName = $"Spoiled {ingredientData.IngredientName}";
        interactionPrompt = $"Pick up {itemName}";

        // Update visual appearance to show spoilage
        if (ingredientRenderer != null)
        {
            // Make it darker/less appealing
            Color spoiledColor = Color.gray;
            ingredientRenderer.material.color = spoiledColor;
        }

        OnIngredientSpoiled?.Invoke(this);
        IngredientDebugLog($"Ingredient {ingredientData.IngredientName} has spoiled!");
    }

    #region Compatibility and Recipe Methods

    /// <summary>
    /// Check if this ingredient is compatible with another for recipes
    /// </summary>
    public bool IsCompatibleWith(Ingredient other)
    {
        if (other?.ingredientData == null || ingredientData == null) return false;
        if (IsSpoiled || other.IsSpoiled) return false;

        return ingredientData.IsCompatibleWith(other.ingredientData);
    }

    /// <summary>
    /// Check if this ingredient has a specific tag
    /// </summary>
    public bool HasTag(string tag)
    {
        return ingredientData?.HasTag(tag) ?? false;
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Create an ingredient GameObject from IngredientData
    /// </summary>
    public static GameObject CreateIngredientFromData(IngredientData data, Vector3 position = default, Transform parent = null)
    {
        if (data == null)
        {
            Debug.LogError("Cannot create ingredient from null data!");
            return null;
        }

        GameObject ingredientObject;

        // Use the prefab if available, otherwise create a basic cube
        if (data.IngredientPrefab != null)
        {
            ingredientObject = Instantiate(data.IngredientPrefab, position, Quaternion.identity, parent);
        }
        else
        {
            ingredientObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ingredientObject.transform.position = position;
            ingredientObject.transform.SetParent(parent);
        }

        // Ensure it has the required components
        if (ingredientObject.GetComponent<Ingredient>() == null)
        {
            ingredientObject.AddComponent<Ingredient>();
        }

        if (ingredientObject.GetComponent<Rigidbody>() == null)
        {
            ingredientObject.AddComponent<Rigidbody>();
        }

        // Set the ingredient data
        Ingredient ingredient = ingredientObject.GetComponent<Ingredient>();
        ingredient.ingredientData = data;
        ingredientObject.name = data.IngredientName;

        return ingredientObject;
    }

    #endregion

    #region Debug and Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (ingredientData == null) return;

        // Draw ingredient category indicator
        Color categoryColor = GetCategoryColor(ingredientData.Category);
        Gizmos.color = categoryColor;
        Vector3 categoryPos = transform.position + Vector3.up * 3f;
        Gizmos.DrawCube(categoryPos, Vector3.one * 0.15f);


        // Draw processing indicator
        if (isBeingProcessed && Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Vector3 processingPos = categoryPos + Vector3.up * 0.6f;
            float processingWidth = processingProgress * 0.5f;
            Gizmos.DrawCube(processingPos, new Vector3(processingWidth, 0.05f, 0.05f));
        }

#if UNITY_EDITOR
        // Show ingredient info in scene view
        if (Application.isPlaying && ingredientData != null)
        {
            string info = $"{ingredientData.IngredientName}\n{ingredientData.PreparationState}";
            if (isBeingProcessed)
                info += $"\nProcessing: {processingProgress:P0}";
            UnityEditor.Handles.Label(categoryPos + Vector3.up * 0.8f, info);
        }
#endif
    }

    private Color GetCategoryColor(IngredientCategory category)
    {
        switch (category)
        {
            case IngredientCategory.Vegetable: return Color.green;
            case IngredientCategory.Meat: return Color.red;
            case IngredientCategory.Dairy: return Color.white;
            case IngredientCategory.Grain: return Color.yellow;
            case IngredientCategory.Liquid: return Color.blue;
            default: return Color.gray;
        }
    }

    private void IngredientDebugLog(string message)
    {
        if (enableIngredientDebugLogs)
            Debug.Log($"[Ingredient] {message}");
    }

    #endregion

    #region Validation

    protected override void OnValidate()
    {
        base.OnValidate();

        // Update name to match ingredient data
        if (ingredientData != null && Application.isPlaying)
        {
            itemName = ingredientData.IngredientName;
            name = ingredientData.IngredientName;
        }

        // Auto-find renderer if not assigned
        if (ingredientRenderer == null)
        {
            ingredientRenderer = GetComponent<Renderer>();
        }
    }

    #endregion
}