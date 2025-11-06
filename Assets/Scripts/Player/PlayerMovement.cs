using UnityEngine;

/// <summary>
/// Handles player movement logic.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    private PlayerController playerController;
    private PlayerFeedbackManager feedbackManager;
    private Rigidbody rb;

    private Vector2 movementInput;
    private Vector2 rotationInput;

    [Header("Movement")]
    [Tooltip("How quickly the player reaches target speed")]
    public float acceleration = 50f;
    [Tooltip("How quickly the player stops when no input")]
    public float deceleration = 50f;

    [Header("Movement Speeds")]
    [SerializeField] private bool useVaryingSpeeds = true;
    [Tooltip("Speed when players move in similar directions")]
    [SerializeField] private float fastMovementSpeed = 8f;
    [Tooltip("Speed when only one player moves or players move in different directions")]
    [SerializeField] private float slowMovementSpeed = 4f;
    [Tooltip("Single speed used when useVaryingSpeeds is false")]
    [SerializeField] private float singleMovementSpeed = 6f;

    [Header("Rotation")]
    [Tooltip("Minimum joystick input required to rotate")]
    [SerializeField] private float rotationDeadzone = 0.1f;

    [Header("Rotation Speeds")]
    [Tooltip("Rotation speed when both players rotate in same direction")]
    [SerializeField] private float fastRotationSpeed = 180f; // degrees per second
    [Tooltip("Rotation speed when only one player is rotating")]
    [SerializeField] private float slowRotationSpeed = 90f;  // degrees per second
    [Tooltip("Single rotation speed used when useVaryingSpeeds is false")]
    [SerializeField] private float singleRotationSpeed = 135f; // degrees per second

    [Header("Footstep Feedback")]
    [Tooltip("How often footstep feedback plays during slow movement/rotation (in seconds)")]
    [SerializeField] private float slowFootstepInterval = 0.6f;
    [Tooltip("How often footstep feedback plays during fast movement/rotation (in seconds)")]
    [SerializeField] private float fastFootstepInterval = 0.3f;

    private float footstepFeedbackTimer = 0f;
    private float currentFootstepInterval;

    [Header("Bounce Settings")]
    [Tooltip("Multiplier for bounce force when colliding with walls")]
    [SerializeField] private float bounceForce = 5f;
    [Tooltip("How much of the incoming velocity is preserved during bounce (0-1)")]
    [SerializeField] [Range(0f, 1f)] private float bounceRestitution = 0.8f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Current speed states (set by InputManager)
    private bool isFastMovement = false;
    private bool isFastRotation = false;

    // Calculated speeds based on current state
    private float currentMovementSpeed;
    private float currentRotationSpeed;

    public void Initialize(PlayerController controller, Rigidbody rigidbody)
    {
        playerController = controller;
        rb = rigidbody;

        if (feedbackManager == null)
            feedbackManager = playerController.GetComponent<PlayerFeedbackManager>();

        // IMPORTANT: Freeze ALL rotation axes - we'll handle rotation manually
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationY |
                           RigidbodyConstraints.FreezeRotationZ;
        }

        // Initialize speeds and footstep interval
        UpdateCurrentSpeeds();

        DebugLog("PlayerMovement initialized with manual rotation control and dynamic speeds");
    }

    public void HandleMovement(Vector2 moveInput)
    {
        movementInput = moveInput;
    }

    public void HandleRotation(Vector2 rotateInput)
    {
        rotationInput = rotateInput;
    }

    /// <summary>
    /// Called by InputManager to set movement speed type
    /// </summary>
    /// <param name="useFastSpeed">True for fast movement, false for slow movement</param>
    public void SetMovementSpeedType(bool useFastSpeed)
    {
        if (!useVaryingSpeeds)
        {
            // When not using varying speeds, ignore speed type changes
            return;
        }

        if (isFastMovement != useFastSpeed)
        {
            isFastMovement = useFastSpeed;
            UpdateCurrentSpeeds();
            DebugLog($"Movement speed set to: {(useFastSpeed ? "FAST" : "SLOW")} ({currentMovementSpeed})");
        }
    }

    /// <summary>
    /// Called by InputManager to set rotation speed type
    /// </summary>
    /// <param name="useFastSpeed">True for fast rotation, false for slow rotation</param>
    public void SetRotationSpeedType(bool useFastSpeed)
    {
        if (!useVaryingSpeeds)
        {
            // When not using varying speeds, ignore speed type changes
            return;
        }

        if (isFastRotation != useFastSpeed)
        {
            isFastRotation = useFastSpeed;
            UpdateCurrentSpeeds();
            DebugLog($"Rotation speed set to: {(useFastSpeed ? "FAST" : "SLOW")} ({currentRotationSpeed})");
        }
    }

    /// <summary>
    /// Updates the current speeds and footstep interval based on current speed types
    /// </summary>
    private void UpdateCurrentSpeeds()
    {
        if (useVaryingSpeeds)
        {
            // Use the varying speed system
            currentMovementSpeed = isFastMovement ? fastMovementSpeed : slowMovementSpeed;
            currentRotationSpeed = isFastRotation ? fastRotationSpeed : slowRotationSpeed;

            // Update footstep interval based on whether we're using any fast speeds
            bool usingFastSpeed = isFastMovement || isFastRotation;
            currentFootstepInterval = usingFastSpeed ? fastFootstepInterval : slowFootstepInterval;
        }
        else
        {
            // Use single fixed speeds regardless of cooperation
            currentMovementSpeed = singleMovementSpeed;
            currentRotationSpeed = singleRotationSpeed;

            // Use slow footstep interval since we're not distinguishing between fast/slow
            currentFootstepInterval = slowFootstepInterval;
        }

        DebugLog($"Speeds updated (VaryingSpeeds: {useVaryingSpeeds}) - Movement: {currentMovementSpeed}, Rotation: {currentRotationSpeed}, Footstep Interval: {currentFootstepInterval}");
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        ApplyRotation();
        UpdateFootstepFeedback();
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
            targetVelocity = moveDirection * currentMovementSpeed;

            DebugLog($"Movement Input: {movementInput}, Move Direction: {moveDirection}, Speed: {currentMovementSpeed}");
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

        // Check if there's significant rotation input and rotation is allowed
        if (rotationInput.magnitude > rotationDeadzone && currentRotationSpeed > 0f)
        {
            // Use X-axis of rotation input for rotation direction
            float rotationDirection = rotationInput.x;

            // Calculate rotation amount for this frame using current rotation speed
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

    /// <summary>
    /// Handles footstep feedback timing when player is moving or rotating.
    /// Uses faster intervals for fast speeds and slower intervals for slow speeds.
    /// </summary>
    private void UpdateFootstepFeedback()
    {
        // Check if player is currently moving or rotating
        bool isMoving = movementInput.magnitude > 0.1f;
        bool isRotating = rotationInput.magnitude > rotationDeadzone;
        bool isActive = isMoving || isRotating;

        if (isActive)
        {
            // Update the timer
            footstepFeedbackTimer += Time.fixedDeltaTime;

            // Check if it's time to play footstep feedback using dynamic interval
            if (footstepFeedbackTimer >= currentFootstepInterval)
            {
                // Play the footstep feedback
                feedbackManager?.PlayFootstepFeedback();

                // Reset the timer
                footstepFeedbackTimer = 0f;

                DebugLog($"Footstep feedback played (interval: {currentFootstepInterval:F2}s, fast movement: {isFastMovement}, fast rotation: {isFastRotation})");
            }
        }
        else
        {
            // Reset timer when not moving/rotating so footsteps start immediately when movement resumes
            footstepFeedbackTimer = 0f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Play collision feedback on collision
        feedbackManager?.PlayCollisionFeedback();

        // Apply bounce force
        if (rb != null && collision.contactCount > 0)
        {
            // Get the average contact normal (direction away from the collision surface)
            Vector3 averageNormal = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                averageNormal += collision.GetContact(i).normal;
            }
            averageNormal = averageNormal.normalized;

            // Get current velocity
            Vector3 currentVelocity = rb.linearVelocity;

            // Calculate the velocity component along the collision normal
            float velocityAlongNormal = Vector3.Dot(currentVelocity, averageNormal);

            // Only bounce if moving towards the surface (negative dot product)
            if (velocityAlongNormal < 0)
            {
                // Calculate reflection: reflected = velocity - 2 * (velocity · normal) * normal
                // Apply restitution to simulate energy loss during bounce
                Vector3 reflectedVelocity = currentVelocity - (1f + bounceRestitution) * velocityAlongNormal * averageNormal;

                // Apply the bounce by adding the reflected velocity with the bounce force multiplier
                Vector3 bounceVelocity = reflectedVelocity * bounceForce;
                rb.linearVelocity = bounceVelocity;

                DebugLog($"Bounce applied! Normal: {averageNormal}, Bounce velocity: {bounceVelocity}");
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerMovement] {message}");
    }

    #region Public Properties (for other systems to read current state)

    /// <summary>
    /// Current movement speed being used
    /// </summary>
    public float CurrentMovementSpeed => currentMovementSpeed;

    /// <summary>
    /// Current rotation speed being used
    /// </summary>
    public float CurrentRotationSpeed => currentRotationSpeed;

    /// <summary>
    /// Whether currently using fast movement speed
    /// </summary>
    public bool IsFastMovement => isFastMovement;

    /// <summary>
    /// Whether currently using fast rotation speed
    /// </summary>
    public bool IsFastRotation => isFastRotation;

    #endregion
}