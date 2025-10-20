using UnityEngine;

/// <summary>
/// Handles player movement logic.
/// </summary>
public class PlayerMovement : MonoBehaviour
{

    private PlayerController playerController;
    private Rigidbody rb;

    private Vector2 movementInput;


    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("How quickly the player reaches target speed")]
    public float acceleration = 50f;
    [Tooltip("How quickly the player stops when no input")]
    public float deceleration = 50f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Initialize(PlayerController controller, Rigidbody rigidbody)
    {
        playerController = controller;
        rb = rigidbody;

        DebugLog("PlayerMovement initialized");
    }

    public void HandleMovement(Vector2 moveInput)
    {
        movementInput = moveInput;
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    private void ApplyMovement()
    {
        if (rb == null) return;

        // Step 2: Calculate target velocity from input
        Vector3 targetVelocity = Vector3.zero;

        if (movementInput.magnitude > 0.1f)
        {
            // Create movement direction from input
            Vector3 moveDirection = movementInput.normalized;

            targetVelocity = moveDirection * moveSpeed;
        }

        // Step 3: Get current horizontal velocity (ignore Y component)
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        Vector3 targetHorizontalVelocity = new Vector3(targetVelocity.x, 0, targetVelocity.z);

        // Step 4: Calculate velocity difference
        Vector3 horizontalVelocityDifference = targetHorizontalVelocity - currentHorizontalVelocity;

        // Step 5: Choose acceleration or deceleration based on input
        float currentAcceleration = movementInput.magnitude > 0.1f ? acceleration : deceleration;

        // Step 6: Calculate force from velocity difference
        Vector3 force = horizontalVelocityDifference * currentAcceleration;

        DebugLog($"Applying Movement Force: {force}");

        // Step 7: Apply force
        rb.AddForce(force, ForceMode.Acceleration);

    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerMovement] {message}");
    }
}
