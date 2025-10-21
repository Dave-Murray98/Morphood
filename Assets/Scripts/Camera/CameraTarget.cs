using UnityEngine;

/// <summary>
/// A camera target that smoothly follows the player's position without rotating.
/// This GameObject acts as an intermediary between the player and the Cinemachine camera,
/// ensuring the camera maintains a fixed top-down orientation regardless of player rotation.
/// </summary>
public class CameraTarget : MonoBehaviour
{
    [Header("Target to Follow")]
    [SerializeField] private Transform playerTarget;
    [Tooltip("The player transform to follow. If not set, will search for PlayerController in scene")]

    [Header("Follow Settings")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [Tooltip("Static offset from the player position (useful for adjusting camera center point)")]

    [SerializeField] private float followSpeed = 10f;
    [Tooltip("How quickly the camera target follows the player (higher = more responsive)")]

    [SerializeField] private bool useSmoothing = true;
    [Tooltip("Whether to use smooth interpolation or instant following")]

    [Header("Deadzone Settings")]
    [SerializeField] private bool useDeadzone = true;
    [Tooltip("Whether to use a deadzone before the camera starts moving")]

    [SerializeField] private float deadzoneRadius = 1f;
    [Tooltip("Distance the player can move before camera starts following")]

    [SerializeField] private bool showDeadzoneGizmo = true;
    [Tooltip("Show the deadzone area in the Scene view")]

    [Header("Boundary Constraints (Optional)")]
    [SerializeField] private bool useBoundaryConstraints = false;
    [Tooltip("Constrain camera movement within defined bounds")]

    [SerializeField] private Vector3 minBounds = new Vector3(-10f, 0f, -10f);
    [SerializeField] private Vector3 maxBounds = new Vector3(10f, 0f, 10f);

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal state
    private Vector3 targetPosition;
    private Vector3 lastPlayerPosition;
    private bool isInitialized = false;

    // Public properties for other systems to read
    public Transform PlayerTarget => playerTarget;
    public float DeadzoneRadius => deadzoneRadius;
    public bool IsWithinDeadzone { get; private set; }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Auto-find player if not assigned
        if (playerTarget == null)
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerTarget = playerController.transform;
                DebugLog("Auto-found PlayerController as target");
            }
            else
            {
                Debug.LogError("[CameraTarget] No player target assigned and couldn't find PlayerController in scene!");
                return;
            }
        }

        // Initialize positions
        if (playerTarget != null)
        {
            lastPlayerPosition = playerTarget.position;
            targetPosition = CalculateTargetPosition();

            // Start at the correct position
            transform.position = targetPosition;

            DebugLog($"CameraTarget initialized. Following: {playerTarget.name}");
            isInitialized = true;
        }
    }

    private void LateUpdate()
    {
        if (!isInitialized || playerTarget == null) return;

        UpdateCameraTarget();
    }

    private void UpdateCameraTarget()
    {
        Vector3 currentPlayerPosition = playerTarget.position;

        // Calculate the desired target position
        Vector3 desiredPosition = CalculateTargetPosition();

        // Handle deadzone logic
        if (useDeadzone)
        {
            Vector3 currentTargetPosition = transform.position;
            float distanceFromTarget = Vector3.Distance(
                new Vector3(currentPlayerPosition.x, 0f, currentPlayerPosition.z),
                new Vector3(currentTargetPosition.x, 0f, currentTargetPosition.z)
            );

            IsWithinDeadzone = distanceFromTarget <= deadzoneRadius;

            if (IsWithinDeadzone)
            {
                // Player is within deadzone - don't move camera
                DebugLog("Player within deadzone - camera staying put");
                return;
            }
            else
            {
                // Player is outside deadzone - move camera to maintain deadzone edge
                Vector3 directionToPlayer = (currentPlayerPosition - currentTargetPosition).normalized;
                Vector3 deadzoneEdge = currentTargetPosition + directionToPlayer * deadzoneRadius;

                // Only follow in the XZ plane for top-down view
                desiredPosition = new Vector3(
                    currentPlayerPosition.x - directionToPlayer.x * deadzoneRadius,
                    desiredPosition.y, // Keep the calculated Y position
                    currentPlayerPosition.z - directionToPlayer.z * deadzoneRadius
                );
            }
        }

        // Apply boundary constraints if enabled
        if (useBoundaryConstraints)
        {
            desiredPosition = new Vector3(
                Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x),
                desiredPosition.y,
                Mathf.Clamp(desiredPosition.z, minBounds.z, maxBounds.z)
            );
        }

        // Move the camera target towards the desired position
        if (useSmoothing && followSpeed > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = desiredPosition;
        }

        // Update last known position
        lastPlayerPosition = currentPlayerPosition;

        // Debug logging
        if (enableDebugLogs && Vector3.Distance(transform.position, desiredPosition) > 0.01f)
        {
            DebugLog($"Moving to: {desiredPosition}, Current: {transform.position}");
        }
    }

    private Vector3 CalculateTargetPosition()
    {
        // Calculate base position with offset
        Vector3 basePosition = playerTarget.position + positionOffset;

        // For top-down view, we typically want to maintain a fixed Y height
        // but follow X and Z movements
        return new Vector3(basePosition.x, transform.position.y, basePosition.z);
    }

    #region Public Methods

    /// <summary>
    /// Instantly snap the camera target to the player's position (useful for scene transitions)
    /// </summary>
    public void SnapToPlayer()
    {
        if (playerTarget != null)
        {
            targetPosition = CalculateTargetPosition();
            transform.position = targetPosition;
            lastPlayerPosition = playerTarget.position;

            DebugLog("Camera target snapped to player position");
        }
    }

    /// <summary>
    /// Update the player target at runtime
    /// </summary>
    public void SetPlayerTarget(Transform newTarget)
    {
        playerTarget = newTarget;
        if (playerTarget != null)
        {
            SnapToPlayer();
            DebugLog($"Camera target updated to follow: {newTarget.name}");
        }
    }

    /// <summary>
    /// Update follow settings at runtime
    /// </summary>
    public void UpdateFollowSettings(float newFollowSpeed, float newDeadzoneRadius, bool newUseSmoothing = true)
    {
        followSpeed = newFollowSpeed;
        deadzoneRadius = newDeadzoneRadius;
        useSmoothing = newUseSmoothing;

        DebugLog($"Follow settings updated - Speed: {followSpeed}, Deadzone: {deadzoneRadius}, Smoothing: {useSmoothing}");
    }

    /// <summary>
    /// Set boundary constraints at runtime
    /// </summary>
    public void SetBoundaryConstraints(Vector3 minBounds, Vector3 maxBounds, bool enabled = true)
    {
        this.minBounds = minBounds;
        this.maxBounds = maxBounds;
        useBoundaryConstraints = enabled;

        DebugLog($"Boundary constraints updated - Min: {minBounds}, Max: {maxBounds}, Enabled: {enabled}");
    }

    #endregion

    #region Gizmos and Debug

    private void OnDrawGizmos()
    {
        if (!showDeadzoneGizmo || !useDeadzone) return;

        // Draw deadzone circle
        Gizmos.color = IsWithinDeadzone ? Color.green : Color.yellow;

        Vector3 gizmoPosition = Application.isPlaying ? transform.position :
            (playerTarget != null ? playerTarget.position : transform.position);

        // Draw circle in XZ plane for top-down view
        DrawWireCircle(gizmoPosition, deadzoneRadius, Vector3.up);

        // Draw boundary constraints if enabled
        if (useBoundaryConstraints)
        {
            Gizmos.color = Color.red;
            Vector3 size = maxBounds - minBounds;
            Vector3 center = (minBounds + maxBounds) * 0.5f;
            Gizmos.DrawWireCube(center, size);
        }
    }

    private void DrawWireCircle(Vector3 center, float radius, Vector3 normal)
    {
        Vector3 forward = Vector3.Slerp(normal, -normal, 0.5f);
        Vector3 right = Vector3.Cross(normal, forward).normalized * radius;
        forward = Vector3.Cross(right, normal).normalized * radius;

        Matrix4x4 matrix = new Matrix4x4();
        matrix[0] = right.x; matrix[1] = right.y; matrix[2] = right.z;
        matrix[4] = normal.x; matrix[5] = normal.y; matrix[6] = normal.z;
        matrix[8] = forward.x; matrix[9] = forward.y; matrix[10] = forward.z;
        matrix[12] = center.x; matrix[13] = center.y; matrix[14] = center.z;
        matrix[15] = 1;

        Vector3 lastPoint = Vector3.zero;
        Vector3 nextPoint = Vector3.zero;

        float currentAngle = 0f;
        const int segments = 32;

        for (int i = 0; i <= segments; i++)
        {
            nextPoint.x = Mathf.Sin(currentAngle);
            nextPoint.z = Mathf.Cos(currentAngle);
            nextPoint.y = 0f;

            nextPoint = matrix.MultiplyPoint3x4(nextPoint);

            if (i > 0)
                Gizmos.DrawLine(lastPoint, nextPoint);

            lastPoint = nextPoint;
            currentAngle += (2f * Mathf.PI) / segments;
        }
    }

    private void OnValidate()
    {
        // Clamp values to reasonable ranges in the editor
        followSpeed = Mathf.Max(0f, followSpeed);
        deadzoneRadius = Mathf.Max(0f, deadzoneRadius);

        // Ensure min bounds are less than max bounds
        if (useBoundaryConstraints)
        {
            minBounds = new Vector3(
                Mathf.Min(minBounds.x, maxBounds.x),
                minBounds.y,
                Mathf.Min(minBounds.z, maxBounds.z)
            );
        }
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[CameraTarget] {message}");
    }
}