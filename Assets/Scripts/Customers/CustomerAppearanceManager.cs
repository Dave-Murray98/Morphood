using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Manages the visual appearance of a customer by randomly selecting from available skinned mesh renderers.
/// This component should be attached to the customer prefab and will automatically randomize the appearance
/// when the customer is spawned or when manually triggered.
/// </summary>
public class CustomerAppearanceManager : MonoBehaviour
{
    [Header("Appearance Configuration")]
    [SerializeField, Tooltip("All available skinned mesh renderers that represent different customer appearances")]
    private List<SkinnedMeshRenderer> availableAppearances = new List<SkinnedMeshRenderer>();

    [Header("Randomization Settings")]
    [SerializeField, Tooltip("If true, appearance will be randomized automatically when the GameObject is enabled")]
    private bool randomizeOnEnable = true;

    [SerializeField, Tooltip("If true, the same appearance won't be selected twice in a row (requires at least 2 appearances)")]
    private bool avoidConsecutiveDuplicates = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [ShowInInspector, ReadOnly]
    private int currentAppearanceIndex = -1;

    [ShowInInspector, ReadOnly]
    private int lastAppearanceIndex = -1;

    private void Awake()
    {
        // Auto-populate the appearances list if it's empty
        if (availableAppearances.Count == 0)
        {
            AutoPopulateAppearances();
        }

        // Validate the setup
        ValidateSetup();
    }

    private void OnEnable()
    {
        if (randomizeOnEnable)
        {
            RandomizeAppearance();
        }
    }

    /// <summary>
    /// Automatically finds all SkinnedMeshRenderer components in children and adds them to the appearances list.
    /// This is a helper method that can be called from the inspector or automatically in Awake().
    /// </summary>
    [Button("Auto-Populate Appearances")]
    private void AutoPopulateAppearances()
    {
        availableAppearances.Clear();

        // Find all SkinnedMeshRenderer components in children
        SkinnedMeshRenderer[] foundRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (SkinnedMeshRenderer renderer in foundRenderers)
        {
            availableAppearances.Add(renderer);
        }

        DebugLog($"Auto-populated {availableAppearances.Count} appearances");
    }

    /// <summary>
    /// Validates that the component is set up correctly and logs any issues.
    /// </summary>
    private void ValidateSetup()
    {
        if (availableAppearances.Count == 0)
        {
            Debug.LogWarning($"[CustomerAppearanceManager] No appearances found on {gameObject.name}! " +
                           "Make sure to assign SkinnedMeshRenderer components or use Auto-Populate.");
            return;
        }

        // Check for null references
        for (int i = availableAppearances.Count - 1; i >= 0; i--)
        {
            if (availableAppearances[i] == null)
            {
                Debug.LogWarning($"[CustomerAppearanceManager] Null appearance found at index {i}, removing it.");
                availableAppearances.RemoveAt(i);
            }
        }

        DebugLog($"Setup validated with {availableAppearances.Count} valid appearances");
    }

    /// <summary>
    /// Randomly selects and applies a new appearance to the customer.
    /// This is the main method you'll call to change the customer's look.
    /// </summary>
    public void RandomizeAppearance()
    {
        if (availableAppearances.Count == 0)
        {
            DebugLog("Cannot randomize appearance - no appearances available");
            return;
        }

        if (availableAppearances.Count == 1)
        {
            // Only one appearance available, just use it
            ApplyAppearance(0);
            return;
        }

        int newIndex;

        if (avoidConsecutiveDuplicates && availableAppearances.Count > 1)
        {
            // Pick a random appearance that's different from the last one
            do
            {
                newIndex = Random.Range(0, availableAppearances.Count);
            }
            while (newIndex == lastAppearanceIndex);
        }
        else
        {
            // Pick any random appearance
            newIndex = Random.Range(0, availableAppearances.Count);
        }

        ApplyAppearance(newIndex);
    }

    /// <summary>
    /// Applies a specific appearance by index.
    /// </summary>
    /// <param name="appearanceIndex">The index of the appearance to apply</param>
    public void ApplyAppearance(int appearanceIndex)
    {
        if (appearanceIndex < 0 || appearanceIndex >= availableAppearances.Count)
        {
            Debug.LogError($"[CustomerAppearanceManager] Invalid appearance index: {appearanceIndex}. " +
                          $"Valid range is 0 to {availableAppearances.Count - 1}");
            return;
        }

        // Disable all appearances first
        DisableAllAppearances();

        // Enable the selected appearance
        SkinnedMeshRenderer selectedAppearance = availableAppearances[appearanceIndex];
        if (selectedAppearance != null)
        {
            selectedAppearance.gameObject.SetActive(true);

            // Update tracking variables
            lastAppearanceIndex = currentAppearanceIndex;
            currentAppearanceIndex = appearanceIndex;

            DebugLog($"Applied appearance {appearanceIndex}: {selectedAppearance.gameObject.name}");
        }
        else
        {
            Debug.LogError($"[CustomerAppearanceManager] Appearance at index {appearanceIndex} is null!");
        }
    }

    /// <summary>
    /// Disables all appearance GameObjects.
    /// </summary>
    private void DisableAllAppearances()
    {
        foreach (SkinnedMeshRenderer appearance in availableAppearances)
        {
            if (appearance != null && appearance.gameObject != null)
            {
                appearance.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Gets the currently active appearance index.
    /// Returns -1 if no appearance is currently active.
    /// </summary>
    public int GetCurrentAppearanceIndex()
    {
        return currentAppearanceIndex;
    }

    /// <summary>
    /// Gets the total number of available appearances.
    /// </summary>
    public int GetAppearanceCount()
    {
        return availableAppearances.Count;
    }

    /// <summary>
    /// Resets the appearance tracking when the customer is returned to the pool.
    /// Call this from your Customer.ResetForPooling() method.
    /// </summary>
    public void ResetForPooling()
    {
        currentAppearanceIndex = -1;
        lastAppearanceIndex = -1;
        DisableAllAppearances();
        DebugLog("Reset appearance for pooling");
    }

    /// <summary>
    /// Inspector button for testing appearance randomization in the editor.
    /// </summary>
    [Button("Test Random Appearance")]
    private void TestRandomAppearance()
    {
        RandomizeAppearance();
    }

    /// <summary>
    /// Inspector button for clearing all appearances and auto-populating again.
    /// </summary>
    [Button("Refresh Appearances List")]
    private void RefreshAppearancesList()
    {
        AutoPopulateAppearances();
        ValidateSetup();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CustomerAppearanceManager] {message}");
        }
    }

    #region Inspector Helpers

    /// <summary>
    /// Called when values are changed in the inspector to validate settings.
    /// </summary>
    private void OnValidate()
    {
        // Remove any null entries that might have been added
        if (availableAppearances != null)
        {
            for (int i = availableAppearances.Count - 1; i >= 0; i--)
            {
                if (availableAppearances[i] == null)
                {
                    availableAppearances.RemoveAt(i);
                }
            }
        }
    }

    #endregion
}