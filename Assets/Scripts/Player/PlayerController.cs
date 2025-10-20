using UnityEngine;

/// <summary>
/// Controls player input handling and passes information onto other systems (ie player movement, interaction, etc).
/// </summary>
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;

    [SerializeField] private Rigidbody rb;

    // Properties for movement direction calculation
    public Vector3 Forward => transform.forward;
    public Vector3 Right => transform.right;

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

        DebugLog("Handling player input");

        playerMovement.HandleMovement(InputManager.Instance.MovementInput);

    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerController] {message}");
    }
}
