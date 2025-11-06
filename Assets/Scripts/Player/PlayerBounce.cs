using UnityEngine;

/// <summary>
/// Enhanced bouncing system that makes the player feel like a bouncy ball or jelly.
/// Handles bounce physics, temporary movement restriction, and optional rotation effects.
/// </summary>
public class PlayerBounce : MonoBehaviour
{
    [Header("Bounce Physics")]
    [Tooltip("How bouncy the player is (0 = no bounce, 1 = perfect bounce, >1 = extra bouncy)")]
    [Range(0f, 2f)]
    public float bounciness = 0.7f;

    [Tooltip("Minimum speed needed to trigger a bounce")]
    public float minimumBounceSpeed = 0.1f;

    [Tooltip("Minimum bounce force regardless of incoming speed")]
    public float minimumBounceForce = 3f;

    [Tooltip("Maximum bounce velocity to prevent excessive bouncing")]
    public float maximumBounceSpeed = 15f;

    [Tooltip("How much of the incoming velocity affects the bounce (0 = ignore speed, 1 = full speed consideration)")]
    [Range(0f, 1f)]
    public float velocityInfluence = 0.8f;

    [Header("Movement Restriction During Bounce")]
    [Tooltip("How long player movement is restricted after a bounce")]
    public float movementRestrictionTime = 0.3f;

    [Tooltip("How much player input is reduced during bounce restriction (0 = no input, 1 = full input)")]
    [Range(0f, 1f)]
    public float inputReductionDuringBounce = 0.2f;

    [Tooltip("Should movement restriction fade out gradually?")]
    public bool fadeOutRestriction = true;

    [Header("Bounce Rotation")]
    [Tooltip("Enable rotation effects during bounces")]
    public bool enableBounceRotation = true;

    [Tooltip("Minimum rotation speed (degrees per second)")]
    public float minRotationSpeed = 720f;

    [Tooltip("Maximum rotation speed (degrees per second)")]
    public float maxRotationSpeed = 1440f;

    [Tooltip("How long the bounce rotation lasts")]
    public float rotationDuration = 0.5f;

    [Tooltip("Type of ease-out curve for rotation (0=Linear, 1=Quadratic, 2=Cubic, 3=Quartic)")]
    [Range(0, 3)]
    public int easeOutType = 1;

    [Header("Bounce Dampening")]
    [Tooltip("How quickly bounces lose energy over time")]
    public float bounceDampening = 0.95f;

    [Tooltip("Minimum velocity before bounce stops")]
    public float minimumVelocityThreshold = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = false;

    // Private variables
    private Rigidbody rb;
    private PlayerMovement playerMovement;
    private PlayerFeedbackManager feedbackManager;

    // Bounce state tracking
    private bool isBouncing = false;
    private float bounceTimer = 0f;
    private float rotationTimer = 0f;
    private Vector3 lastBounceDirection = Vector3.zero;
    private float originalInputMultiplier = 1f;

    // For smooth input restriction
    private float currentInputMultiplier = 1f;

    // For bounce rotation using MoveRotation
    private float initialBounceRotationSpeed = 0f; // Store the original speed for ease-out calculation

    public void Initialize(Rigidbody rigidbody, PlayerMovement movement, PlayerFeedbackManager feedback)
    {
        rb = rigidbody;
        playerMovement = movement;
        feedbackManager = feedback;

        originalInputMultiplier = 1f;
        currentInputMultiplier = 1f;

        DebugLog("ImprovedPlayerBouncing initialized");
    }

    private void Update()
    {
        UpdateBounceState();
    }

    private void FixedUpdate()
    {
        ApplyBounceDampening();
        UpdateRotationEffect(); // Move rotation to FixedUpdate to match PlayerMovement
    }

    /// <summary>
    /// Handles collision and applies improved bounce physics
    /// </summary>
    /// <param name="collision">The collision information</param>
    public void HandleBounceCollision(Collision collision)
    {
        if (rb == null || collision.contactCount == 0) return;

        // Get the average contact normal (direction away from the collision surface)
        Vector3 averageNormal = CalculateAverageNormal(collision);

        // Get current velocity
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);

        // Check if we're moving fast enough to bounce
        float incomingSpeed = horizontalVelocity.magnitude;
        if (incomingSpeed < minimumBounceSpeed)
        {
            DebugLog($"Collision too slow to bounce: {incomingSpeed:F2} < {minimumBounceSpeed}");
            return;
        }

        DebugLog($"Processing bounce - Incoming velocity: {horizontalVelocity}, Speed: {incomingSpeed:F2}, Normal: {averageNormal}");

        // Calculate bounce velocity using reflection physics
        Vector3 bounceVelocity = CalculateBounceVelocity(horizontalVelocity, averageNormal, incomingSpeed);

        // Apply the bounce velocity
        ApplyBounceVelocity(bounceVelocity);

        // Start bounce state management
        StartBounceState(bounceVelocity);

        // Apply rotation effect if enabled
        if (enableBounceRotation)
        {
            ApplyBounceRotation(bounceVelocity, averageNormal);
        }

        // Play feedback
        feedbackManager?.PlayCollisionFeedback();

        DebugLog($"Bounce applied! Incoming: {incomingSpeed:F2}, Outgoing: {bounceVelocity.magnitude:F2}, Direction: {bounceVelocity.normalized}, Min Force: {minimumBounceForce}");
    }

    /// <summary>
    /// Calculates the average normal from all collision contacts
    /// </summary>
    private Vector3 CalculateAverageNormal(Collision collision)
    {
        Vector3 averageNormal = Vector3.zero;
        int validContacts = 0;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 normal = contact.normal;

            // Only use normals that are reasonably horizontal (for top-down gameplay)
            if (Mathf.Abs(normal.y) < 0.7f) // Ignore mostly vertical surfaces
            {
                averageNormal += normal;
                validContacts++;
            }
        }

        if (validContacts == 0)
        {
            // Fallback: use the first contact normal
            averageNormal = collision.GetContact(0).normal;
            DebugLog("No valid horizontal contacts found, using first contact normal");
        }
        else
        {
            averageNormal = averageNormal / validContacts;
        }

        // Make sure it's horizontal and normalized
        averageNormal.y = 0;
        averageNormal = averageNormal.normalized;

        DebugLog($"Calculated normal from {validContacts}/{collision.contactCount} contacts: {averageNormal}");

        return averageNormal;
    }

    /// <summary>
    /// Calculates realistic bounce velocity using physics reflection
    /// </summary>
    private Vector3 CalculateBounceVelocity(Vector3 horizontalVelocity, Vector3 normal, float incomingSpeed)
    {
        // Ensure we have valid input vectors
        if (horizontalVelocity.magnitude < 0.001f || normal.magnitude < 0.001f)
        {
            DebugLog("Invalid vectors for bounce calculation, using normal as bounce direction");
            return normal * minimumBounceForce;
        }

        // Reflect the velocity off the surface normal
        Vector3 reflectedVelocity = Vector3.Reflect(horizontalVelocity, normal);

        // Calculate bounce speed based on incoming speed and bounciness
        float bounceSpeed = incomingSpeed * bounciness;

        // Apply velocity influence (how much the incoming speed affects the bounce)
        bounceSpeed = Mathf.Lerp(minimumBounceForce, bounceSpeed, velocityInfluence);

        // Ensure minimum bounce force regardless of incoming speed
        bounceSpeed = Mathf.Max(bounceSpeed, minimumBounceForce);

        // Clamp to maximum bounce speed
        bounceSpeed = Mathf.Min(bounceSpeed, maximumBounceSpeed);

        // Get the bounce direction from reflection
        Vector3 bounceDirection = reflectedVelocity.normalized;

        // Validate the bounce direction - it should be pointing away from the surface
        float dotProduct = Vector3.Dot(bounceDirection, normal);
        if (dotProduct < 0.1f) // If not pointing away from surface enough
        {
            DebugLog($"Reflection gave bad direction (dot: {dotProduct:F3}), using normal instead");
            bounceDirection = normal; // Use the surface normal as bounce direction
        }

        Vector3 bounceVelocity = bounceDirection * bounceSpeed;

        DebugLog($"Reflection: incoming={horizontalVelocity}, normal={normal}, reflected={reflectedVelocity}, final_direction={bounceDirection}");

        return bounceVelocity;
    }

    /// <summary>
    /// Applies the bounce velocity to the rigidbody
    /// </summary>
    private void ApplyBounceVelocity(Vector3 bounceVelocity)
    {
        // Set velocity directly instead of adding force for immediate response
        Vector3 newVelocity = new Vector3(bounceVelocity.x, rb.linearVelocity.y, bounceVelocity.z);
        rb.linearVelocity = newVelocity;
    }

    /// <summary>
    /// Starts the bounce state and movement restriction
    /// </summary>
    private void StartBounceState(Vector3 bounceVelocity)
    {
        isBouncing = true;
        bounceTimer = 0f;
        lastBounceDirection = bounceVelocity.normalized;

        // Set input restriction
        currentInputMultiplier = inputReductionDuringBounce;

        DebugLog($"Bounce state started, input reduced to {inputReductionDuringBounce * 100f}%");
    }

    /// <summary>
    /// Applies rotation effect during bounce
    /// </summary>
    private void ApplyBounceRotation(Vector3 bounceVelocity, Vector3 normal)
    {
        if (!enableBounceRotation) return;

        // Generate random rotation speed within bounds
        float rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);

        // Random direction (left or right)
        float rotationDirection = Random.Range(-1f, 1f) > 0 ? 1f : -1f;

        // Store the initial rotation speed for ease-out calculations
        initialBounceRotationSpeed = rotationDirection * rotationSpeed;
        rotationTimer = 0f;

        DebugLog($"Bounce rotation started: {initialBounceRotationSpeed:F2} deg/s (random between {minRotationSpeed}-{maxRotationSpeed})");
    }

    /// <summary>
    /// Updates bounce state and movement restrictions
    /// </summary>
    private void UpdateBounceState()
    {
        if (!isBouncing) return;

        bounceTimer += Time.deltaTime;

        // Update input multiplier during bounce restriction
        if (fadeOutRestriction)
        {
            // Gradually restore input over time
            float restrictionProgress = bounceTimer / movementRestrictionTime;
            currentInputMultiplier = Mathf.Lerp(inputReductionDuringBounce, originalInputMultiplier, restrictionProgress);
        }

        // End bounce state when timer expires
        if (bounceTimer >= movementRestrictionTime)
        {
            EndBounceState();
        }
    }

    /// <summary>
    /// Updates rotation effects during bounce with smooth ease-out
    /// </summary>
    private void UpdateRotationEffect()
    {
        if (!enableBounceRotation || rb == null) return;

        rotationTimer += Time.fixedDeltaTime;

        // Apply rotation using MoveRotation (same as PlayerMovement)
        if (rotationTimer < rotationDuration && Mathf.Abs(initialBounceRotationSpeed) > 0.1f)
        {
            // Calculate ease-out progress (0 to 1)
            float progress = rotationTimer / rotationDuration;

            // Apply proper ease-out curve - this should feel like natural deceleration
            float easeOutFactor = CalculateEaseOut(progress, easeOutType);

            // Calculate rotation amount using the ease-out factor
            // The rotation starts at full speed and smoothly slows down to zero
            float effectiveRotationSpeed = initialBounceRotationSpeed * easeOutFactor;
            float rotationAmount = effectiveRotationSpeed * Time.fixedDeltaTime;

            // Apply rotation around Y-axis using MoveRotation
            Quaternion rotationDelta = Quaternion.Euler(0, rotationAmount, 0);
            rb.MoveRotation(rb.rotation * rotationDelta);

            if (enableDebugLogs && rotationTimer % 0.1f < Time.fixedDeltaTime) // Log every ~0.1 seconds
            {
                DebugLog($"Bounce rotation: progress={progress:F2}, ease-out={easeOutFactor:F2}, speed={effectiveRotationSpeed:F1}");
            }
        }
        else if (rotationTimer >= rotationDuration)
        {
            // Stop rotation completely
            initialBounceRotationSpeed = 0f;
            DebugLog("Bounce rotation ended");
        }
    }

    /// <summary>
    /// Calculates different types of ease-out curves for natural-feeling rotation slowdown
    /// </summary>
    /// <param name="t">Progress from 0 to 1</param>
    /// <param name="type">Ease-out type (0=Linear, 1=Quadratic, 2=Cubic, 3=Quartic)</param>
    /// <returns>Ease-out factor from 1 to 0</returns>
    private float CalculateEaseOut(float t, int type)
    {
        // Clamp input to valid range
        t = Mathf.Clamp01(t);

        switch (type)
        {
            case 0: // Linear - constant slowdown
                return 1f - t;

            case 1: // Quadratic ease-out - starts fast, slows down smoothly
                float inverted = 1f - t;
                return inverted * inverted;

            case 2: // Cubic ease-out - more dramatic slowdown curve
                float inv2 = 1f - t;
                return inv2 * inv2 * inv2;

            case 3: // Quartic ease-out - very smooth, natural deceleration
                float inv3 = 1f - t;
                return inv3 * inv3 * inv3 * inv3;

            default:
                float defaultInv = 1f - t;
                return defaultInv * defaultInv; // Default to quadratic
        }
    }

    /// <summary>
    /// Applies gradual dampening to bounce velocity
    /// </summary>
    private void ApplyBounceDampening()
    {
        if (!isBouncing) return;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);

        // Apply dampening
        if (horizontalVelocity.magnitude > minimumVelocityThreshold)
        {
            Vector3 dampenedVelocity = horizontalVelocity * bounceDampening;
            rb.linearVelocity = new Vector3(dampenedVelocity.x, currentVelocity.y, dampenedVelocity.z);
        }
    }

    /// <summary>
    /// Ends the bounce state and restores normal movement
    /// </summary>
    private void EndBounceState()
    {
        isBouncing = false;
        currentInputMultiplier = originalInputMultiplier;

        // Stop bounce rotation
        initialBounceRotationSpeed = 0f;

        DebugLog("Bounce state ended, movement and rotation fully restored");
    }

    /// <summary>
    /// Gets the current input multiplier (used by PlayerMovement to reduce input during bounces)
    /// </summary>
    public float GetInputMultiplier()
    {
        return currentInputMultiplier;
    }

    /// <summary>
    /// Checks if the player is currently in a bounce state
    /// </summary>
    public bool IsBouncing => isBouncing;

    /// <summary>
    /// Gets the current bounce direction
    /// </summary>
    public Vector3 BounceDirection => lastBounceDirection;

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[ImprovedPlayerBouncing] {message}");
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !isBouncing) return;

        // Draw bounce direction
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, lastBounceDirection * 3f);

        // Draw bounce state indicator
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
    }
}