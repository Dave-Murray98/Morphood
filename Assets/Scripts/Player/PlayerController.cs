using UnityEngine;

/// <summary>
/// Controls player input handling and passes information onto other systems (ie player movement, interaction, etc).
/// Now handles combined input from multiple players.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;

    [SerializeField] private Rigidbody rb;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        playerMovement.Initialize(this, rb);
    }

    // Update is called once per frame
    void Update()
    {
        if (!GameManager.Instance || GameManager.Instance.isPaused) return;

        HandleInput();
    }

    private void HandleInput()
    {
        if (InputManager.Instance == null || playerMovement == null) return;

        DebugLog("Handling combined player input");

        // Get the combined input from InputManager (now represents both players)
        Vector2 combinedMovementInput = InputManager.Instance.MovementInput;
        Vector2 combinedRotationInput = InputManager.Instance.RotationInput;

        // Pass the combined inputs to the movement system
        playerMovement.HandleMovement(combinedMovementInput);
        playerMovement.HandleRotation(combinedRotationInput);

        // Debug log when there's significant input
        if (enableDebugLogs && (combinedMovementInput.magnitude > 0.1f || Mathf.Abs(combinedRotationInput.x) > 0.1f))
        {
            DebugLog($"Combined Input - Movement: {combinedMovementInput}, Rotation: {combinedRotationInput.x:F2}");
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerController] {message}");
    }
}