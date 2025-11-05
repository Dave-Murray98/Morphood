using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
/// Manages visual and haptic feedback for the player based on input conflicts.
/// Reacts to movement and rotation conflicts using separate Feel feedbacks,
/// plus shared particle feedback for any conflict.
/// </summary>
public class PlayerFeedbackManager : MonoBehaviour
{
    [Header("Feedbacks")]
    [SerializeField] private MMF_Player movementConflictFeedback;
    [Tooltip("MMF Player for movement conflict feedback (shake, audio, visual effects, etc.)")]

    [SerializeField] private MMF_Player rotationConflictFeedback;
    [Tooltip("MMF Player for rotation conflict feedback (shake, audio, visual effects, etc.)")]

    [SerializeField] private MMF_Player particleConflictFeedback;
    [Tooltip("MMF Player for particle feedback - plays when any conflict is detected")]

    [Header("Conflict Detection")]
    [SerializeField] private float conflictCheckInterval = 0.1f;
    [Tooltip("How often to check for conflicts (seconds)")]

    [Header("Conflict Types")]
    [SerializeField] private bool reactToMovementConflicts = true;
    [SerializeField] private bool reactToRotationConflicts = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal state
    private bool wasMovementConflictingLastFrame = false;
    private bool wasRotationConflictingLastFrame = false;
    private bool wasAnyConflictingLastFrame = false;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Validate MMF Players are assigned
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

        if (movementConflictFeedback == null && rotationConflictFeedback == null)
        {
            enabled = false;
            return;
        }

        // Start checking for conflicts
        InvokeRepeating(nameof(CheckForConflicts), 0f, conflictCheckInterval);

        DebugLog("PlayerFeedbackManager initialized with Feel feedbacks");
    }

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
                DebugLog("All conflicts resolved - stopping particle feedback");
            }
        }

        // Update last frame states
        wasMovementConflictingLastFrame = hasMovementConflict;
        wasRotationConflictingLastFrame = hasRotationConflict;
        wasAnyConflictingLastFrame = hasAnyConflict;
    }

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

    private void OnDestroy()
    {
        // Clean up
        StopMovementConflictFeedback();
        StopRotationConflictFeedback();
        StopParticleConflictFeedback();
        CancelInvoke();
    }

    private void OnDisable()
    {
        // Stop feedbacks when disabled
        StopMovementConflictFeedback();
        StopRotationConflictFeedback();
        StopParticleConflictFeedback();
    }

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
    /// Check if any conflict feedback is currently playing
    /// </summary>
    public bool IsAnyConflictFeedbackPlaying => IsMovementConflictFeedbackPlaying || IsRotationConflictFeedbackPlaying || IsParticleConflictFeedbackPlaying;

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
    }

#if UNITY_EDITOR
    [Header("Testing (Editor Only)")]
    [SerializeField] private bool testMovementConflictFeedback = false;
    [SerializeField] private bool testRotationConflictFeedback = false;
    [SerializeField] private bool testParticleConflictFeedback = false;

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
    }
#endif

    #endregion
}