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

    public void Initialize(PlayerController controller, Rigidbody rigidbody)
    {
        playerController = controller;
        rb = rigidbody;

        // Freeze rotation on X and Z axes to prevent unwanted tilting/rolling
        // Keep Y rotation free if you want the player to face movement direction
        if (rb != null)
        {
            rb.freezeRotation = false; // We'll handle rotation manually
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

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

        // Calculate target velocity from input
        Vector3 targetVelocity = Vector3.zero;

        if (movementInput.magnitude > 0.1f)
        {
            // X input controls X movement (left/right)
            // Y input controls Z movement (forward/backward in world space)
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



    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerMovement] {message}");
    }
}