using UnityEngine;

/// <summary>
/// Controls player input handling and passes information onto other systems (ie player movement, interaction, etc).
/// Now handles combined input from multiple players and manages the PlayerEnd components.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Rigidbody rb;

    [Header("Player Ends")]
    [SerializeField] private PlayerEnd player1End;
    [SerializeField] private PlayerEnd player2End;
    [Tooltip("The two ends of the player character that handle interactions")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        InitializeComponents();
        SetupPlayerEnds();
        SubscribeToInputEvents();
    }

    private void InitializeComponents()
    {
        // Initialize movement system (existing code)
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        playerMovement.Initialize(this, rb);
    }

    private void SetupPlayerEnds()
    {
        // Auto-find PlayerEnds if not assigned
        if (player1End == null)
        {
            PlayerEnd[] playerEnds = GetComponentsInChildren<PlayerEnd>();
            foreach (PlayerEnd end in playerEnds)
            {
                if (end.PlayerNumber == 1)
                {
                    player1End = end;
                    break;
                }
            }
        }

        if (player2End == null)
        {
            PlayerEnd[] playerEnds = GetComponentsInChildren<PlayerEnd>();
            foreach (PlayerEnd end in playerEnds)
            {
                if (end.PlayerNumber == 2)
                {
                    player2End = end;
                    break;
                }
            }
        }

        // Validate setup
        if (player1End == null)
        {
            Debug.LogError("[PlayerController] Player 1 End not found! Please assign or create a PlayerEnd with playerNumber = 1");
        }

        if (player2End == null)
        {
            Debug.LogError("[PlayerController] Player 2 End not found! Please assign or create a PlayerEnd with playerNumber = 2");
        }

        DebugLog($"Player ends setup - P1: {(player1End != null ? "✓" : "✗")}, P2: {(player2End != null ? "✓" : "✗")}");
    }

    private void SubscribeToInputEvents()
    {
        // Subscribe to InputManager interaction events
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPlayer1InteractPressed += HandlePlayer1InteractPressed;
            InputManager.Instance.OnPlayer1InteractReleased += HandlePlayer1InteractReleased;
            InputManager.Instance.OnPlayer2InteractPressed += HandlePlayer2InteractPressed;
            InputManager.Instance.OnPlayer2InteractReleased += HandlePlayer2InteractReleased;

            DebugLog("Subscribed to InputManager interaction events");
        }
        else
        {
            // Try again next frame if InputManager isn't ready
            Invoke(nameof(SubscribeToInputEvents), 0.1f);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!GameManager.Instance || GameManager.Instance.isPaused) return;

        HandleMovementInput();
        HandleInteractionHolds();
    }

    #region Movement Input (existing functionality)

    private void HandleMovementInput()
    {
        if (InputManager.Instance == null || playerMovement == null) return;

        //DebugLog("Handling combined player input");

        // Get the combined input from InputManager (now represents both players)
        Vector2 combinedMovementInput = InputManager.Instance.MovementInput;
        Vector2 combinedRotationInput = InputManager.Instance.RotationInput;

        // Pass the combined inputs to the movement system
        playerMovement.HandleMovement(combinedMovementInput);
        playerMovement.HandleRotation(combinedRotationInput);

        // Debug log when there's significant input
        // if (enableDebugLogs && (combinedMovementInput.magnitude > 0.1f || Mathf.Abs(combinedRotationInput.x) > 0.1f))
        // {
        //     DebugLog($"Combined Input - Movement: {combinedMovementInput}, Rotation: {combinedRotationInput.x:F2}");
        // }
    }

    #endregion

    #region Interaction Input Handling

    private void HandleInteractionHolds()
    {
        if (InputManager.Instance == null) return;

        // Handle hold interactions for both players
        if (InputManager.Instance.Player1InteractHeld && player1End != null)
        {
            player1End.OnInteractHeld();
        }

        if (InputManager.Instance.Player2InteractHeld && player2End != null)
        {
            player2End.OnInteractHeld();
        }
    }

    #region Interaction Event Handlers

    private void HandlePlayer1InteractPressed()
    {
        if (player1End != null)
        {
            player1End.OnInteractPressed();
            DebugLog("Player 1 interact forwarded to PlayerEnd");
        }
        else
        {
            DebugLog("Player 1 interact received but no PlayerEnd available");
        }
    }

    private void HandlePlayer1InteractReleased()
    {
        if (player1End != null)
        {
            player1End.OnInteractReleased();
            DebugLog("Player 1 interact release forwarded to PlayerEnd");
        }
    }

    private void HandlePlayer2InteractPressed()
    {
        if (player2End != null)
        {
            player2End.OnInteractPressed();
            DebugLog("Player 2 interact forwarded to PlayerEnd");
        }
        else
        {
            DebugLog("Player 2 interact received but no PlayerEnd available");
        }
    }

    private void HandlePlayer2InteractReleased()
    {
        if (player2End != null)
        {
            player2End.OnInteractReleased();
            DebugLog("Player 2 interact release forwarded to PlayerEnd");
        }
    }

    #endregion

    #endregion

    #region Public Methods for PlayerEnd Access

    /// <summary>
    /// Get the PlayerEnd for a specific player number
    /// </summary>
    public PlayerEnd GetPlayerEnd(int playerNumber)
    {
        switch (playerNumber)
        {
            case 1: return player1End;
            case 2: return player2End;
            default: return null;
        }
    }

    /// <summary>
    /// Check if both player ends are properly set up
    /// </summary>
    public bool ArePlayerEndsReady()
    {
        return player1End != null && player2End != null;
    }

    /// <summary>
    /// Get information about what both players are currently carrying
    /// </summary>
    public (int player1Items, int player2Items) GetCarriedItemCounts()
    {
        int p1Count = player1End?.CarriedItemCount ?? 0;
        int p2Count = player2End?.CarriedItemCount ?? 0;
        return (p1Count, p2Count);
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Unsubscribe from InputManager events
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPlayer1InteractPressed -= HandlePlayer1InteractPressed;
            InputManager.Instance.OnPlayer1InteractReleased -= HandlePlayer1InteractReleased;
            InputManager.Instance.OnPlayer2InteractPressed -= HandlePlayer2InteractPressed;
            InputManager.Instance.OnPlayer2InteractReleased -= HandlePlayer2InteractReleased;
        }
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerController] {message}");
    }
}