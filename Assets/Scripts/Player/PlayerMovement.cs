using UnityEngine;

/// <summary>
/// Handles player movement logic.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    private PlayerController playerController;
    private Rigidbody rb;

    private Vector2 movementInput;
    private Vector2 rotationInput;  // NEW: Store rotation input

    [Header("Movement")]
    [Tooltip("This is now controlled by InputManager's speed system")]
    [SerializeField] private float moveSpeed = 5f;  // Fallback/legacy value

    [Tooltip("How quickly the player reaches target speed")]
    public float acceleration = 50f;
    [Tooltip("How quickly the player stops when no input")]
    public float deceleration = 50f;

    [Header("Rotation")]
    [Tooltip("This is now controlled by InputManager's rotation speed system")]
    [SerializeField] private float rotationSpeed = 90f;  // Fallback/legacy value
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

    public void HandleRotation(Vector2 rotateInput)  // NEW: Handle rotation input
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

    private void ApplyRotation()
    {
        if (rb == null) return;

        // Get dynamic rotation speed from InputManager
        float currentRotationSpeed = InputManager.Instance?.CurrentRotationSpeed ?? rotationSpeed;

        // Check if there's significant rotation input and rotation is allowed
        if (rotationInput.magnitude > rotationDeadzone && currentRotationSpeed > 0f)
        {
            // Use X-axis of rotation input for rotation direction
            float rotationDirection = rotationInput.x;

            // Calculate rotation amount for this frame using dynamic speed
            float rotationAmount = rotationDirection * currentRotationSpeed * Time.fixedDeltaTime;

            // Apply rotation around Y-axis (up/down in world space)
            Quaternion rotationDelta = Quaternion.Euler(0, rotationAmount, 0);

            // Apply the rotation to the rigidbody
            rb.MoveRotation(rb.rotation * rotationDelta);

            if (enableDebugLogs)
            {
                DebugLog($"Rotation Input: {rotationInput.x:F2}, Rotation Speed: {currentRotationSpeed:F1}, Amount: {rotationAmount:F2}");
            }
        }
        else if (currentRotationSpeed <= 0f && rotationInput.magnitude > rotationDeadzone)
        {
            // Rotation is blocked due to conflict
            if (enableDebugLogs)
            {
                DebugLog("Rotation blocked due to input conflict");
            }
        }
        // If no input or rotation speed is 0, the player simply stops rotating
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerMovement] {message}");
    }
}