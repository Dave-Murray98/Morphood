using UnityEngine;
using DG.Tweening;

/// <summary>
/// Manages visual and haptic feedback for the player based on input conflicts.
/// Reacts to movement and rotation conflicts by shaking the player's body.
/// </summary>
public class PlayerFeedbackManager : MonoBehaviour
{
    [Header("Shake Target")]
    [SerializeField] private Transform playerBody;
    [Tooltip("If not assigned, will use this GameObject's transform")]

    [Header("Shake Settings")]
    [SerializeField] private float shakeStrength = 0.3f;
    [Tooltip("How strong the shake effect is")]

    [SerializeField] private int shakeVibrato = 10;
    [Tooltip("How many shake vibrations per second")]

    [SerializeField] private float shakeRandomness = 90f;
    [Tooltip("How random the shake direction is (0-180 degrees)")]

    [SerializeField] private bool snapping = false;
    [Tooltip("Whether the shake should snap to positions or be smooth")]

    [SerializeField] private bool fadeOut = true;
    [Tooltip("Whether shake should fade out over time")]

    [Header("Shake Duration")]
    [SerializeField] private float conflictCheckInterval = 0.1f;
    [Tooltip("How often to check for conflicts (seconds)")]

    [SerializeField] private float shakeDuration = 0.2f;
    [Tooltip("Duration of each shake burst")]

    [Header("Conflict Types")]
    [SerializeField] private bool reactToMovementConflicts = true;
    [SerializeField] private bool reactToRotationConflicts = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal state
    private Vector3 originalPosition;
    private bool isCurrentlyShaking = false;
    private bool wasConflictingLastFrame = false;
    private Tween currentShakeTween;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Use this transform if no player body is assigned
        if (playerBody == null)
        {
            playerBody = transform;
            DebugLog("No player body assigned, using this GameObject's transform");
        }

        // Store the original position for shake reset
        originalPosition = playerBody.localPosition;

        // Start checking for conflicts
        InvokeRepeating(nameof(CheckForConflicts), 0f, conflictCheckInterval);

        DebugLog("PlayerFeedbackManager initialized");
    }

    private void CheckForConflicts()
    {
        if (InputManager.Instance == null) return;

        // Check current conflict state
        bool hasMovementConflict = reactToMovementConflicts && InputManager.Instance.IsMovementInputConflicting;
        bool hasRotationConflict = reactToRotationConflicts && InputManager.Instance.IsRotationInputConflicting;
        bool hasAnyConflict = hasMovementConflict || hasRotationConflict;

        // Log conflict state changes
        if (enableDebugLogs && hasAnyConflict != wasConflictingLastFrame)
        {
            if (hasAnyConflict)
            {
                string conflictType = "";
                if (hasMovementConflict && hasRotationConflict)
                    conflictType = "Movement + Rotation";
                else if (hasMovementConflict)
                    conflictType = "Movement";
                else if (hasRotationConflict)
                    conflictType = "Rotation";

                DebugLog($"Conflict detected: {conflictType}");
            }
            else
            {
                DebugLog("Conflict resolved");
            }
        }

        // React to conflict state
        if (hasAnyConflict && !wasConflictingLastFrame)
        {
            // Conflict just started
            StartShaking();
        }
        else if (!hasAnyConflict && wasConflictingLastFrame)
        {
            // Conflict just ended
            StopShaking();
        }
        else if (hasAnyConflict && isCurrentlyShaking)
        {
            // Conflict is ongoing - continue shaking if current shake finished
            if (currentShakeTween == null || !currentShakeTween.IsActive())
            {
                StartShaking();
            }
        }

        wasConflictingLastFrame = hasAnyConflict;
    }

    private void StartShaking()
    {
        if (playerBody == null) return;

        // Stop any existing shake
        StopShaking();

        // Start new shake tween
        currentShakeTween = playerBody.DOShakePosition(
            duration: shakeDuration,
            strength: shakeStrength,
            vibrato: shakeVibrato,
            randomness: shakeRandomness,
            snapping: snapping,
            fadeOut: fadeOut
        ).SetLoops(-1, LoopType.Restart); // Loop indefinitely until stopped

        isCurrentlyShaking = true;

        DebugLog($"Started shaking with strength: {shakeStrength}");
    }

    private void StopShaking()
    {
        // Kill the current shake tween
        if (currentShakeTween != null && currentShakeTween.IsActive())
        {
            currentShakeTween.Kill();
        }

        // Return to original position smoothly
        if (playerBody != null)
        {
            playerBody.DOLocalMove(originalPosition, 0.1f).SetEase(Ease.OutQuad);
        }

        isCurrentlyShaking = false;
        currentShakeTween = null;

        DebugLog("Stopped shaking");
    }

    private void OnDestroy()
    {
        // Clean up tweens when destroyed
        StopShaking();
        CancelInvoke();
    }

    private void OnDisable()
    {
        // Stop shaking when disabled
        StopShaking();
    }

    private void OnEnable()
    {
        // Reset original position reference when re-enabled
        if (playerBody != null)
        {
            originalPosition = playerBody.localPosition;
        }
    }

    #region Public Methods

    /// <summary>
    /// Manually trigger a shake effect (useful for testing or other feedback)
    /// </summary>
    public void TriggerManualShake(float duration = -1f)
    {
        if (duration < 0f)
            duration = shakeDuration;

        StopShaking();

        if (playerBody != null)
        {
            playerBody.DOShakePosition(
                duration: duration,
                strength: shakeStrength,
                vibrato: shakeVibrato,
                randomness: shakeRandomness,
                snapping: snapping,
                fadeOut: fadeOut
            ).OnComplete(() =>
            {
                playerBody.DOLocalMove(originalPosition, 0.1f).SetEase(Ease.OutQuad);
            });
        }

        DebugLog($"Manual shake triggered for {duration} seconds");
    }

    /// <summary>
    /// Update shake settings at runtime
    /// </summary>
    public void UpdateShakeSettings(float strength, int vibrato, float randomness)
    {
        shakeStrength = strength;
        shakeVibrato = vibrato;
        shakeRandomness = randomness;

        DebugLog($"Shake settings updated - Strength: {strength}, Vibrato: {vibrato}, Randomness: {randomness}");
    }

    /// <summary>
    /// Check if currently shaking
    /// </summary>
    public bool IsShaking => isCurrentlyShaking;

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
        if (playerBody == null && Application.isPlaying)
        {
            playerBody = transform;
        }

        // Clamp values to reasonable ranges
        shakeStrength = Mathf.Max(0f, shakeStrength);
        shakeVibrato = Mathf.Max(1, shakeVibrato);
        shakeRandomness = Mathf.Clamp(shakeRandomness, 0f, 180f);
        conflictCheckInterval = Mathf.Max(0.01f, conflictCheckInterval);
        shakeDuration = Mathf.Max(0.1f, shakeDuration);
    }

#if UNITY_EDITOR
    [Header("Testing (Editor Only)")]
    [SerializeField] private bool testShake = false;

    private void Update()
    {
        // Editor testing
        if (!Application.isPlaying) return;

        if (testShake)
        {
            testShake = false;
            TriggerManualShake();
        }
    }
#endif

    #endregion
}