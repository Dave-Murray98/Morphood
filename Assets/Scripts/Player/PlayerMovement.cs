using UnityEngine;

/// <summary>
/// Handles player movement logic.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    private PlayerController playerController;
    private Rigidbody rb;

    private Vector2 movementInput;
    private Vector2 rotationInput;  // Store rotation input

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("How quickly the player reaches target speed")]
    public float acceleration = 50f;
    [Tooltip("How quickly the player stops when no input")]
    public float deceleration = 50f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 90f;  // Degrees per second
    [Tooltip("Minimum joystick input required to rotate")]
    [SerializeField] private float rotationDeadzone = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public void Initialize(PlayerController controller, Rigidbody rigidbody)
    {
        playerController = controller;
        rb = rigidbody;

        // IMPORTANT: Freeze ALL rotation axes - we'll handle rotation manually
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationY |
                           RigidbodyConstraints.FreezeRotationZ;
        }

        DebugLog("PlayerMovement initialized with manual rotation control");
    }

    public void HandleMovement(Vector2 moveInput)
    {
        movementInput = moveInput;
    }

    public void HandleRotation(Vector2 rotateInput)  // Handle rotation input
    {
        rotationInput = rotateInput;
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        ApplyRotation();  // NEW: Apply rotation in FixedUpdate for physics consistency
    }

    private void ApplyMovement()
    {
        if (rb == null) return;

        // Calculate target velocity from input
        Vector3 targetVelocity = Vector3.zero;

        if (movementInput.magnitude > 0.1f)
        {
            // Map 2D input to 3D movement for top-down view
            Vector3 moveDirection = new Vector3(movementInput.x, 0, movementInput.y).normalized;
            targetVelocity = moveDirection * moveSpeed;

            DebugLog($"Movement Input: {movementInput}, Move Direction: {moveDirection}");
        }

        // Get current horizontal velocity (ignore Y component for gravity)
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        Vector3 targetHorizontalVelocity = new Vector3(targetVelocity.x, 0, targetVelocity.z);

        // Calculate velocity difference
        Vector3 horizontalVelocityDifference = targetHorizontalVelocity - currentHorizontalVelocity;

        // Choose acceleration or deceleration based on input
        float currentAcceleration = movementInput.magnitude > 0.1f ? acceleration : deceleration;

        // Calculate force from velocity difference
        Vector3 force = horizontalVelocityDifference * currentAcceleration;

        DebugLog($"Applying Movement Force: {force}");

        // Apply force
        rb.AddForce(force, ForceMode.Acceleration);
    }

    private void ApplyRotation()  // NEW: Handle joystick-based rotation
    {
        if (rb == null) return;

        // Check if there's significant rotation input
        if (rotationInput.magnitude > rotationDeadzone)
        {
            // Use X-axis of right joystick for rotation
            // Positive X = rotate clockwise, Negative X = rotate counter-clockwise
            float rotationDirection = rotationInput.x;

            // Calculate rotation amount for this frame
            float rotationAmount = rotationDirection * rotationSpeed * Time.fixedDeltaTime;

            // Apply rotation around Y-axis (up/down in world space)
            Quaternion rotationDelta = Quaternion.Euler(0, rotationAmount, 0);

            // Apply the rotation to the rigidbody
            rb.MoveRotation(rb.rotation * rotationDelta);

            if (enableDebugLogs)
            {
                DebugLog($"Rotation Input: {rotationInput.x:F2}, Rotation Amount: {rotationAmount:F2}");
            }
        }
        // If no input, the player simply stops rotating (no additional code needed)
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerMovement] {message}");
    }
}