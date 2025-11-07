using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
/// Manages visual and haptic feedback for the player based on input conflicts and processing activities.
/// Reacts to movement and rotation conflicts using separate Feel feedbacks,
/// plus shared particle feedback for any conflict.
/// NEW: Monitors PlayerEnd processing states to trigger chopping and cooking feedback.
/// </summary>
public class PlayerFeedbackManager : MonoBehaviour
{
    [Header("Conflict Feedbacks")]
    [SerializeField] private MMF_Player movementConflictFeedback;
    [Tooltip("MMF Player for movement conflict feedback (shake, audio, visual effects, etc.)")]
    [SerializeField] private MMF_Player onMovementConflictEndFeedback;
    [Tooltip("MMF Player for movement conflict end feedback")]

    [SerializeField] private MMF_Player rotationConflictFeedback;
    [Tooltip("MMF Player for rotation conflict feedback (shake, audio, visual effects, etc.)")]

    [SerializeField] private MMF_Player particleConflictFeedback;
    [Tooltip("MMF Player for particle feedback - plays when any conflict is detected")]

    [Header("Processing Feedbacks")]
    [SerializeField] private MMF_Player choppingFeedback;
    [Tooltip("MMF Player for chopping feedback (sounds, particles, shake, etc.)")]

    [SerializeField] private MMF_Player cookingFeedback;
    [Tooltip("MMF Player for cooking feedback (sounds, particles, shake, etc.)")]


    [Header("General Feedback")]
    [SerializeField] private MMF_Player footstepFeedback;
    [SerializeField] private MMF_Player pickupItemFeedback;
    [SerializeField] private MMF_Player collisionFeedback;

    [Header("Conflict Detection")]
    [SerializeField] private float conflictCheckInterval = 0.1f;
    [Tooltip("How often to check for conflicts (seconds)")]

    [Header("Conflict Types")]
    [SerializeField] private bool reactToMovementConflicts = true;
    [SerializeField] private bool reactToRotationConflicts = true;

    [Header("Processing Feedback")]
    [SerializeField] private bool enableProcessingFeedback = true;
    [Tooltip("Whether to play feedback during food processing")]

    [SerializeField] private float processingCheckInterval = 0.1f;
    [Tooltip("How often to check for processing state changes (seconds)")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal state for conflicts
    private bool wasMovementConflictingLastFrame = false;
    private bool wasRotationConflictingLastFrame = false;
    private bool wasAnyConflictingLastFrame = false;

    // Internal state for processing
    private bool wasChoppingLastFrame = false;
    private bool wasCookingLastFrame = false;
    private bool isChoppingFeedbackPlaying = false;
    private bool isCookingFeedbackPlaying = false;

    // Player references for monitoring
    private PlayerController playerController;
    private PlayerEnd player1End;
    private PlayerEnd player2End;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Validate MMF Players are assigned
        ValidateFeedbackComponents();

        // Find player components
        FindPlayerComponents();

        // Start checking for conflicts and processing states
        InvokeRepeating(nameof(CheckForConflicts), 0f, conflictCheckInterval);

        if (enableProcessingFeedback)
        {
            InvokeRepeating(nameof(CheckForProcessingStates), 0f, processingCheckInterval);
        }

        DebugLog("PlayerFeedbackManager initialized with conflict and processing feedbacks");
    }

    private void ValidateFeedbackComponents()
    {
        if (movementConflictFeedback == null)
        {
            Debug.LogError("[PlayerFeedbackManager] No Movement Conflict MMF Player assigned! Please assign the movement conflict feedback.");
        }

        if (rotationConflictFeedback == null)
        {
            Debug.LogError("[PlayerFeedbackManager] No Rotation Conflict MMF Player assigned! Please assign the rotation conflict feedback.");
        }

        if (particleConflictFeedback == null)
        {
            Debug.LogWarning("[PlayerFeedbackManager] No Particle Conflict MMF Player assigned. Particle feedback will be disabled.");
        }

        if (enableProcessingFeedback)
        {
            if (choppingFeedback == null)
            {
                Debug.LogWarning("[PlayerFeedbackManager] No Chopping MMF Player assigned. Chopping feedback will be disabled.");
            }

            if (cookingFeedback == null)
            {
                Debug.LogWarning("[PlayerFeedbackManager] No Cooking MMF Player assigned. Cooking feedback will be disabled.");
            }
        }

        if (movementConflictFeedback == null && rotationConflictFeedback == null)
        {
            enabled = false;
            return;
        }
    }

    private void FindPlayerComponents()
    {
        // Find the PlayerController in the scene
        playerController = FindFirstObjectByType<PlayerController>();

        if (playerController == null)
        {
            Debug.LogError("[PlayerFeedbackManager] No PlayerController found in scene! Processing feedback will not work.");
            enableProcessingFeedback = false;
            return;
        }

        // Get player ends from the controller
        player1End = playerController.GetPlayerEnd(1);
        player2End = playerController.GetPlayerEnd(2);

        if (player1End == null)
        {
            Debug.LogWarning("[PlayerFeedbackManager] Player 1 End not found! Player 1 processing feedback will not work.");
        }

        if (player2End == null)
        {
            Debug.LogWarning("[PlayerFeedbackManager] Player 2 End not found! Player 2 processing feedback will not work.");
        }

        DebugLog($"Found PlayerController with Player1End: {(player1End != null ? "✓" : "✗")}, Player2End: {(player2End != null ? "✓" : "✗")}");
    }

    #region Conflict Detection (Existing functionality)

    private void CheckForConflicts()
    {
        if (InputManager.Instance == null) return;

        // Check current conflict states
        bool hasMovementConflict = reactToMovementConflicts && InputManager.Instance.IsMovementInputConflicting;
        bool hasRotationConflict = reactToRotationConflicts && InputManager.Instance.IsRotationInputConflicting;
        bool hasAnyConflict = hasMovementConflict || hasRotationConflict;

        // Handle movement conflict changes
        if (hasMovementConflict != wasMovementConflictingLastFrame)
        {
            if (hasMovementConflict)
            {
                StartMovementConflictFeedback();
                DebugLog("Movement conflict detected");
            }
            else
            {
                StopMovementConflictFeedback();
                DebugLog("Movement conflict resolved");
            }
        }

        // Handle rotation conflict changes
        if (hasRotationConflict != wasRotationConflictingLastFrame)
        {
            if (hasRotationConflict)
            {
                StartRotationConflictFeedback();
                DebugLog("Rotation conflict detected");
            }
            else
            {
                StopRotationConflictFeedback();
                DebugLog("Rotation conflict resolved");
            }
        }

        // Handle particle feedback for any conflict
        if (hasAnyConflict != wasAnyConflictingLastFrame)
        {
            if (hasAnyConflict)
            {
                StartParticleConflictFeedback();
                DebugLog("Conflict detected - starting particle feedback");
            }
            else
            {
                StopParticleConflictFeedback();
                PlayOnMovementConflictEndFeedback();
                DebugLog("All conflicts resolved - stopping particle feedback");
            }
        }

        // Update last frame states
        wasMovementConflictingLastFrame = hasMovementConflict;
        wasRotationConflictingLastFrame = hasRotationConflict;
        wasAnyConflictingLastFrame = hasAnyConflict;
    }

    #endregion

    #region Processing State Detection (NEW)

    private void CheckForProcessingStates()
    {
        if (!enableProcessingFeedback) return;

        // Handle chopping state changes
        if (player2End.isChopping != wasChoppingLastFrame)
        {
            if (player2End.isChopping)
            {
                StartChoppingFeedback();
                DebugLog("Chopping started");
            }
            else
            {
                StopChoppingFeedback();
                DebugLog("Chopping stopped");
            }
        }

        // Handle cooking state changes
        if (player1End.isCooking != wasCookingLastFrame)
        {
            if (player1End.isCooking)
            {
                StartCookingFeedback();
                DebugLog("Cooking started");
            }
            else
            {
                StopCookingFeedback();
                DebugLog("Cooking stopped");
            }
        }

        // Update last frame states
        wasChoppingLastFrame = player2End.isChopping;
        wasCookingLastFrame = player1End.isCooking;
    }

    #endregion

    #region Conflict Feedback Methods (Existing functionality)

    private void StartMovementConflictFeedback()
    {
        if (movementConflictFeedback == null) return;

        movementConflictFeedback.PlayFeedbacks();
        DebugLog("Started movement conflict feedback");
    }

    private void StopMovementConflictFeedback()
    {
        if (movementConflictFeedback == null) return;

        movementConflictFeedback.StopFeedbacks();
        DebugLog("Stopped movement conflict feedback");
    }

    private void StartRotationConflictFeedback()
    {
        if (rotationConflictFeedback == null) return;

        rotationConflictFeedback.PlayFeedbacks();
        DebugLog("Started rotation conflict feedback");
    }

    private void StopRotationConflictFeedback()
    {
        if (rotationConflictFeedback == null) return;

        rotationConflictFeedback.StopFeedbacks();
        DebugLog("Stopped rotation conflict feedback");
    }

    private void StartParticleConflictFeedback()
    {
        if (particleConflictFeedback == null) return;

        particleConflictFeedback.PlayFeedbacks();
        DebugLog("Started particle conflict feedback");
    }

    private void StopParticleConflictFeedback()
    {
        if (particleConflictFeedback == null) return;

        particleConflictFeedback.StopFeedbacks();
        DebugLog("Stopped particle conflict feedback");
    }

    private void PlayOnMovementConflictEndFeedback()
    {
        if (onMovementConflictEndFeedback == null) return;

        onMovementConflictEndFeedback.PlayFeedbacks();
        DebugLog("Played movement conflict end feedback");
    }

    #endregion

    #region Processing Feedback Methods (NEW)

    private void StartChoppingFeedback()
    {
        if (choppingFeedback == null || isChoppingFeedbackPlaying) return;

        choppingFeedback.PlayFeedbacks();
        isChoppingFeedbackPlaying = true;
        DebugLog("Started chopping feedback");
    }

    private void StopChoppingFeedback()
    {
        if (choppingFeedback == null || !isChoppingFeedbackPlaying) return;

        choppingFeedback.StopFeedbacks();
        isChoppingFeedbackPlaying = false;
        DebugLog("Stopped chopping feedback");
    }

    private void StartCookingFeedback()
    {
        if (cookingFeedback == null || isCookingFeedbackPlaying) return;

        cookingFeedback.PlayFeedbacks();
        isCookingFeedbackPlaying = true;
        DebugLog("Started cooking feedback");
    }

    private void StopCookingFeedback()
    {
        if (cookingFeedback == null || !isCookingFeedbackPlaying) return;

        cookingFeedback.StopFeedbacks();
        isCookingFeedbackPlaying = false;
        DebugLog("Stopped cooking feedback");
    }

    #endregion

    #region General Feedback Methods

    public void PlayFootstepFeedback()
    {
        if (footstepFeedback == null) return;

        footstepFeedback.PlayFeedbacks();
        DebugLog("Footstep feedback played");
    }

    public void PlayPickupItemFeedback()
    {
        if (pickupItemFeedback == null) return;

        pickupItemFeedback.PlayFeedbacks();
        DebugLog("Pickup item feedback played");
    }

    public void PlayCollisionFeedback()
    {
        if (collisionFeedback == null) return;

        collisionFeedback.PlayFeedbacks();
        DebugLog("Collision feedback played");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Manually trigger the movement conflict feedback (useful for testing)
    /// </summary>
    public void TriggerManualMovementConflictFeedback()
    {
        if (movementConflictFeedback == null) return;

        movementConflictFeedback.PlayFeedbacks();
        DebugLog("Manual movement conflict feedback triggered");
    }

    /// <summary>
    /// Manually trigger the rotation conflict feedback (useful for testing)
    /// </summary>
    public void TriggerManualRotationConflictFeedback()
    {
        if (rotationConflictFeedback == null) return;

        rotationConflictFeedback.PlayFeedbacks();
        DebugLog("Manual rotation conflict feedback triggered");
    }

    /// <summary>
    /// Manually trigger the particle conflict feedback (useful for testing)
    /// </summary>
    public void TriggerManualParticleConflictFeedback()
    {
        if (particleConflictFeedback == null) return;

        particleConflictFeedback.PlayFeedbacks();
        DebugLog("Manual particle conflict feedback triggered");
    }

    /// <summary>
    /// Manually trigger chopping feedback (useful for testing)
    /// </summary>
    public void TriggerManualChoppingFeedback()
    {
        if (choppingFeedback == null) return;

        if (isChoppingFeedbackPlaying)
        {
            StopChoppingFeedback();
        }
        else
        {
            StartChoppingFeedback();
        }
    }

    /// <summary>
    /// Manually trigger cooking feedback (useful for testing)
    /// </summary>
    public void TriggerManualCookingFeedback()
    {
        if (cookingFeedback == null) return;

        if (isCookingFeedbackPlaying)
        {
            StopCookingFeedback();
        }
        else
        {
            StartCookingFeedback();
        }
    }

    /// <summary>
    /// Check if movement conflict feedback is currently playing
    /// </summary>
    public bool IsMovementConflictFeedbackPlaying => movementConflictFeedback != null && movementConflictFeedback.IsPlaying;

    /// <summary>
    /// Check if rotation conflict feedback is currently playing
    /// </summary>
    public bool IsRotationConflictFeedbackPlaying => rotationConflictFeedback != null && rotationConflictFeedback.IsPlaying;

    /// <summary>
    /// Check if particle conflict feedback is currently playing
    /// </summary>
    public bool IsParticleConflictFeedbackPlaying => particleConflictFeedback != null && particleConflictFeedback.IsPlaying;

    /// <summary>
    /// Check if chopping feedback is currently playing
    /// </summary>
    public bool IsChoppingFeedbackPlaying => isChoppingFeedbackPlaying;

    /// <summary>
    /// Check if cooking feedback is currently playing
    /// </summary>
    public bool IsCookingFeedbackPlaying => isCookingFeedbackPlaying;

    /// <summary>
    /// Check if any conflict feedback is currently playing
    /// </summary>
    public bool IsAnyConflictFeedbackPlaying => IsMovementConflictFeedbackPlaying || IsRotationConflictFeedbackPlaying || IsParticleConflictFeedbackPlaying;

    /// <summary>
    /// Check if any processing feedback is currently playing
    /// </summary>
    public bool IsAnyProcessingFeedbackPlaying => IsChoppingFeedbackPlaying || IsCookingFeedbackPlaying;

    /// <summary>
    /// Check if any feedback is currently playing
    /// </summary>
    public bool IsAnyFeedbackPlaying => IsAnyConflictFeedbackPlaying || IsAnyProcessingFeedbackPlaying;

    /// <summary>
    /// Enable or disable processing feedback monitoring
    /// </summary>
    public void SetProcessingFeedbackEnabled(bool enabled)
    {
        enableProcessingFeedback = enabled;

        if (!enabled)
        {
            // Stop any current processing feedback
            StopChoppingFeedback();
            StopCookingFeedback();
        }

        DebugLog($"Processing feedback {(enabled ? "enabled" : "disabled")}");
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Clean up conflict feedbacks
        StopMovementConflictFeedback();
        StopRotationConflictFeedback();
        StopParticleConflictFeedback();

        // Clean up processing feedbacks
        StopChoppingFeedback();
        StopCookingFeedback();

        CancelInvoke();
    }

    private void OnDisable()
    {
        // Stop feedbacks when disabled
        StopMovementConflictFeedback();
        StopRotationConflictFeedback();
        StopParticleConflictFeedback();
        StopChoppingFeedback();
        StopCookingFeedback();
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerFeedbackManager] {message}");
    }

    #region Editor Helpers

    // Validate component setup in editor
    private void OnValidate()
    {
        // Clamp values to reasonable ranges
        conflictCheckInterval = Mathf.Max(0.01f, conflictCheckInterval);
        processingCheckInterval = Mathf.Max(0.01f, processingCheckInterval);
    }

#if UNITY_EDITOR
    [Header("Testing (Editor Only)")]
    [SerializeField] private bool testMovementConflictFeedback = false;
    [SerializeField] private bool testRotationConflictFeedback = false;
    [SerializeField] private bool testParticleConflictFeedback = false;
    [SerializeField] private bool testChoppingFeedback = false;
    [SerializeField] private bool testCookingFeedback = false;

    private void Update()
    {
        // Editor testing
        if (!Application.isPlaying) return;

        if (testMovementConflictFeedback)
        {
            testMovementConflictFeedback = false;
            TriggerManualMovementConflictFeedback();
        }

        if (testRotationConflictFeedback)
        {
            testRotationConflictFeedback = false;
            TriggerManualRotationConflictFeedback();
        }

        if (testParticleConflictFeedback)
        {
            testParticleConflictFeedback = false;
            TriggerManualParticleConflictFeedback();
        }

        if (testChoppingFeedback)
        {
            testChoppingFeedback = false;
            TriggerManualChoppingFeedback();
        }

        if (testCookingFeedback)
        {
            testCookingFeedback = false;
            TriggerManualCookingFeedback();
        }
    }
#endif

    #endregion
}