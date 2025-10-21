using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Manages the camera system for the top-down multiplayer game.
/// Handles Cinemachine virtual camera setup and provides easy access to camera controls.
/// </summary>
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Camera References")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [Tooltip("The main Cinemachine virtual camera. Will be auto-found if not assigned")]

    [SerializeField] private CameraTarget cameraTarget;
    [Tooltip("The camera target that follows the player. Will be auto-found if not assigned")]

    [Header("Camera Settings")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 8f, -6f);
    [Tooltip("Offset from the camera target (position and angle like Overcooked style)")]

    [Header("Camera Effects")]
    [SerializeField] private bool enableConflictShake = false;
    [Tooltip("Whether to apply camera shake during input conflicts")]

    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float shakeDuration = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal state
    private bool isShaking = false;
    private CinemachineBasicMultiChannelPerlin noiseComponent;

    // Events for other systems to hook into
    public System.Action OnCameraShakeStarted;
    public System.Action OnCameraShakeStopped;

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Auto-find components if not assigned
        if (virtualCamera == null)
        {
            virtualCamera = FindFirstObjectByType<CinemachineCamera>();
            if (virtualCamera != null)
            {
                DebugLog("Auto-found CinemachineCamera");
            }
        }

        if (cameraTarget == null)
        {
            cameraTarget = FindFirstObjectByType<CameraTarget>();
            if (cameraTarget != null)
            {
                DebugLog("Auto-found CameraTarget");
            }
        }

        // Set up the virtual camera
        SetupVirtualCamera();

        // Subscribe to game events for camera effects
        if (enableConflictShake)
        {
            // We'll check for conflicts in Update since we don't have a direct event
            InvokeRepeating(nameof(CheckForConflictShake), 0f, 0.1f);
        }

        DebugLog("CameraManager initialized successfully");
    }

    private void SetupVirtualCamera()
    {
        if (virtualCamera == null)
        {
            Debug.LogError("[CameraManager] No CinemachineCamera found! Please create one or assign it in the inspector.");
            return;
        }

        // Set the camera to follow our camera target
        if (cameraTarget != null)
        {
            virtualCamera.Follow = cameraTarget.transform;
            DebugLog($"Virtual camera set to follow: {cameraTarget.name}");
        }
        else
        {
            Debug.LogWarning("[CameraManager] No CameraTarget found! Camera will not follow the player.");
        }

        // Position the camera above and behind the target (Overcooked style)
        var transposer = virtualCamera.GetComponent<CinemachineFollow>();
        if (transposer != null)
        {
            transposer.FollowOffset = cameraOffset;
            DebugLog($"Camera offset set to: {cameraOffset}");
        }
        else
        {
            // Try to find CinemachinePositionComposer as an alternative
            var positionComposer = virtualCamera.GetComponent<CinemachinePositionComposer>();
            if (positionComposer != null)
            {
                // For CinemachinePositionComposer, we might need to set the camera transform directly
                virtualCamera.transform.position = (cameraTarget != null ? cameraTarget.transform.position : Vector3.zero) + cameraOffset;
                DebugLog("Using CinemachinePositionComposer - camera positioned manually");
            }
            else
            {
                Debug.LogWarning("[CameraManager] No CinemachineFollow component found on virtual camera. Please add a Body component.");
            }
        }

        // Set up noise component for shake effects
        noiseComponent = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (noiseComponent == null && enableConflictShake)
        {
            Debug.LogWarning("[CameraManager] No CinemachineBasicMultiChannelPerlin component found. Camera shake will not work. Add it to your virtual camera for shake effects.");
        }

        // The camera angle/rotation should be set manually in the inspector for the Overcooked-style view
        DebugLog("Camera setup complete. Ensure camera rotation is set manually for desired angle.");
    }

    private void Update()
    {
        // This Update method is now mainly for future extensibility
        // Camera movement is handled by CameraTarget and Cinemachine
    }

    private void CheckForConflictShake()
    {
        if (!enableConflictShake || noiseComponent == null) return;

        if (InputManager.Instance != null)
        {
            bool hasConflict = InputManager.Instance.IsMovementInputConflicting ||
                             InputManager.Instance.IsRotationInputConflicting;

            if (hasConflict && !isShaking)
            {
                StartCameraShake();
            }
            else if (!hasConflict && isShaking)
            {
                StopCameraShake();
            }
        }
    }

    #region Public Methods

    /// <summary>
    /// Manually trigger camera shake (useful for game events)
    /// </summary>
    public void TriggerCameraShake(float intensity = -1f, float duration = -1f)
    {
        if (noiseComponent == null) return;

        float useIntensity = intensity > 0f ? intensity : shakeIntensity;
        float useDuration = duration > 0f ? duration : shakeDuration;

        StartCameraShake(useIntensity);

        // Stop shake after duration
        CancelInvoke(nameof(StopCameraShake));
        Invoke(nameof(StopCameraShake), useDuration);

        DebugLog($"Manual camera shake triggered - Intensity: {useIntensity}, Duration: {useDuration}");
    }

    /// <summary>
    /// Update camera settings at runtime
    /// </summary>
    public void UpdateCameraSettings(Vector3 newOffset)
    {
        cameraOffset = newOffset;

        if (virtualCamera != null)
        {
            var transposer = virtualCamera.GetComponent<CinemachineFollow>();
            if (transposer != null)
            {
                transposer.FollowOffset = cameraOffset;
            }
        }

        DebugLog($"Camera settings updated - Offset: {newOffset}");
    }

    /// <summary>
    /// Snap camera to player instantly (useful for scene transitions)
    /// </summary>
    public void SnapCameraToPlayer()
    {
        if (cameraTarget != null)
        {
            cameraTarget.SnapToPlayer();
            DebugLog("Camera snapped to player position");
        }
    }

    #endregion

    #region Camera Shake

    private void StartCameraShake(float intensity = -1f)
    {
        if (noiseComponent == null) return;

        float useIntensity = intensity > 0f ? intensity : shakeIntensity;

        noiseComponent.AmplitudeGain = useIntensity;
        noiseComponent.FrequencyGain = 1f;
        isShaking = true;

        OnCameraShakeStarted?.Invoke();
        DebugLog($"Camera shake started with intensity: {useIntensity}");
    }

    private void StopCameraShake()
    {
        if (noiseComponent == null) return;

        noiseComponent.AmplitudeGain = 0f;
        noiseComponent.FrequencyGain = 0f;
        isShaking = false;

        DebugLog("Camera shake stopped");
    }

    #endregion

    #region Debug and Validation

    private void OnValidate()
    {
        // Clamp values to reasonable ranges
        shakeIntensity = Mathf.Max(0f, shakeIntensity);
        shakeDuration = Mathf.Max(0.1f, shakeDuration);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Clean up any ongoing shake effects
        CancelInvoke();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[CameraManager] {message}");
    }

    #endregion
}