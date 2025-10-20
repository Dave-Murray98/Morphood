using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Receives and processes player input from multiple players, combining them according to multiplayer rules.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public InputActionAsset inputActions;

    #region Action Maps
    private InputActionMap gameplayActionMap;
    private InputActionMap uiActionMap;

    #endregion

    #region Custom Rotation Stick Management

    public void RegisterRotationStick(HorizontalRotationStick rotationStick)
    {
        if (rotationStick.PlayerNumber == 1)
        {
            player1RotationStick = rotationStick;
            DebugLog("Player 1 rotation stick registered");
        }
        else if (rotationStick.PlayerNumber == 2)
        {
            player2RotationStick = rotationStick;
            DebugLog("Player 2 rotation stick registered");
        }
        else
        {
            Debug.LogWarning($"[InputManager] Invalid player number for rotation stick: {rotationStick.PlayerNumber}");
        }
    }

    public void UnregisterRotationStick(HorizontalRotationStick rotationStick)
    {
        if (rotationStick.PlayerNumber == 1 && player1RotationStick == rotationStick)
        {
            player1RotationStick = null;
            DebugLog("Player 1 rotation stick unregistered");
        }
        else if (rotationStick.PlayerNumber == 2 && player2RotationStick == rotationStick)
        {
            player2RotationStick = null;
            DebugLog("Player 2 rotation stick unregistered");
        }
    }

    #endregion

    #region Input Actions

    // Player Movement Actions (using Input System)
    private InputAction player1MoveAction;
    private InputAction player2MoveAction;

    // UI Actions
    private InputAction pauseAction;

    #endregion

    #region Custom Rotation Sticks

    private HorizontalRotationStick player1RotationStick;
    private HorizontalRotationStick player2RotationStick;

    #endregion

    #region Raw Input Values (from individual players)

    private Vector2 player1MovementInput;
    private Vector2 player2MovementInput;
    private float player1RotationInput;  // -1 to 1 (left to right)
    private float player2RotationInput;  // -1 to 1 (left to right)

    #endregion

    #region Combined Input Properties (exposed to other systems)

    public Vector2 MovementInput { get; private set; }
    public Vector2 RotationInput { get; private set; }  // We'll use X component for rotation value

    #endregion

    [Header("Multiplayer Settings")]
    [SerializeField] private float rotationSpeedMultiplier = 2f;  // When both players rotate same direction
    [Tooltip("Minimum input value to register as intentional input")]
    [SerializeField] private float inputDeadzone = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        Initialize();
    }

    private void Initialize()
    {
        DebugLog("InputManager initialized with multiplayer support");
        SetUpInputActions();

        EnableCoreGameplayInput();

        GameEvents.OnGamePaused += DisableGameplayInput;
        GameEvents.OnGameResumed += EnableGameplayInput;
    }

    private void EnableCoreGameplayInput()
    {
        if (uiActionMap != null)
        {
            uiActionMap.Enable();
            DebugLog("UI ActionMap enabled immediately");
        }

        if (gameplayActionMap != null)
        {
            gameplayActionMap.Enable();
            DebugLog("Locomotion ActionMap enabled immediately");
        }
    }

    private void SetUpInputActions()
    {
        DebugLog("Setting up multiplayer input actions");

        if (inputActions == null)
        {
            Debug.LogError("[InputManager] InputActionAsset is not assigned! Input will not work!");
            return;
        }

        uiActionMap = inputActions.FindActionMap("UI");
        gameplayActionMap = inputActions.FindActionMap("Gameplay");

        SetUpUIInputActions();
        SetUpLocomotionActions();

        // Subscribe to events
        SubscribeToInputActions();
    }

    private void SetUpUIInputActions()
    {
        pauseAction = uiActionMap.FindAction("Pause");
        if (pauseAction == null)
        {
            Debug.LogError("[InputManager] Pause action not found in UI ActionMap!");
        }
    }

    private void SetUpLocomotionActions()
    {
        // Player Movement Actions
        player1MoveAction = gameplayActionMap.FindAction("Move1");
        if (player1MoveAction == null)
        {
            Debug.LogError("[InputManager] Move action not found in Locomotion ActionMap!");
        }

        player2MoveAction = gameplayActionMap.FindAction("Move2");
        if (player2MoveAction == null)
        {
            Debug.LogError("[InputManager] Move2 action not found in Locomotion ActionMap! Make sure to add this action.");
        }

        // Rotation will be handled by custom HorizontalRotationStick components
        DebugLog("Locomotion actions set up. Rotation will be handled by custom sticks.");
    }

    #region Update Loop

    private void Update()
    {
        // Update input values
        if (gameplayActionMap?.enabled == true)
        {
            UpdateRawInputValues();
            CombineInputs();
        }
    }

    private void UpdateRawInputValues()
    {
        // Read movement input from Input Actions
        player1MovementInput = player1MoveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        player2MovementInput = player2MoveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // Read rotation input from custom rotation sticks
        player1RotationInput = player1RotationStick?.HorizontalInput ?? 0f;
        player2RotationInput = player2RotationStick?.HorizontalInput ?? 0f;
    }

    private void CombineInputs()
    {
        // Combine Movement Inputs
        CombineMovementInputs();

        // Combine Rotation Inputs
        CombineRotationInputs();

        // Debug logging
        if (enableDebugLogs && (MovementInput.magnitude > 0.1f || Mathf.Abs(RotationInput.x) > 0.1f))
        {
            DebugLog($"Combined - Movement: {MovementInput}, Rotation: {RotationInput.x:F2}");
        }
    }

    private void CombineMovementInputs()
    {
        // Normalize individual inputs first to ensure fair contribution
        Vector2 normalizedPlayer1 = player1MovementInput.magnitude > inputDeadzone ? player1MovementInput.normalized : Vector2.zero;
        Vector2 normalizedPlayer2 = player2MovementInput.magnitude > inputDeadzone ? player2MovementInput.normalized : Vector2.zero;

        // Combine and normalize the result
        Vector2 combinedMovement = normalizedPlayer1 + normalizedPlayer2;
        MovementInput = combinedMovement.magnitude > inputDeadzone ? combinedMovement.normalized : Vector2.zero;

        if (enableDebugLogs && MovementInput.magnitude > 0.1f)
        {
            DebugLog($"Movement - P1: {normalizedPlayer1}, P2: {normalizedPlayer2}, Combined: {MovementInput}");
        }
    }

    private void CombineRotationInputs()
    {
        // Apply deadzone to rotation inputs
        float p1Rotation = Mathf.Abs(player1RotationInput) > inputDeadzone ? player1RotationInput : 0f;
        float p2Rotation = Mathf.Abs(player2RotationInput) > inputDeadzone ? player2RotationInput : 0f;

        float combinedRotationValue = 0f;

        // Check if both players are providing input
        bool player1HasInput = Mathf.Abs(p1Rotation) > 0f;
        bool player2HasInput = Mathf.Abs(p2Rotation) > 0f;

        if (player1HasInput && player2HasInput)
        {
            // Both players have input - check if they're in the same direction
            bool sameDirection = (p1Rotation > 0 && p2Rotation > 0) || (p1Rotation < 0 && p2Rotation < 0);

            if (sameDirection)
            {
                // Same direction: 2x speed (average input * multiplier)
                float averageInput = (p1Rotation + p2Rotation) * 0.5f;
                combinedRotationValue = averageInput * rotationSpeedMultiplier;

                if (enableDebugLogs)
                {
                    DebugLog($"Rotation - Same direction: P1({p1Rotation:F2}) + P2({p2Rotation:F2}) = {combinedRotationValue:F2} (2x speed)");
                }
            }
            else
            {
                // Opposite directions: cancel out (0x speed)
                combinedRotationValue = 0f;

                if (enableDebugLogs)
                {
                    DebugLog($"Rotation - Opposite directions: P1({p1Rotation:F2}) vs P2({p2Rotation:F2}) = 0 (canceled)");
                }
            }
        }
        else if (player1HasInput || player2HasInput)
        {
            // Only one player has input: 1x speed
            combinedRotationValue = p1Rotation + p2Rotation;  // One will be 0, so this gives us the active input

            if (enableDebugLogs)
            {
                DebugLog($"Rotation - Single input: P1({p1Rotation:F2}) + P2({p2Rotation:F2}) = {combinedRotationValue:F2} (1x speed)");
            }
        }

        // Clamp the result to reasonable bounds
        combinedRotationValue = Mathf.Clamp(combinedRotationValue, -1f, 1f);

        // Store in RotationInput.x (PlayerMovement expects Vector2 but only uses X component)
        RotationInput = new Vector2(combinedRotationValue, 0f);
    }

    #endregion

    #region Event Subscription

    private void SubscribeToInputActions()
    {
        SubscribeToUIInputActions();
    }

    private void SubscribeToUIInputActions()
    {
        DebugLog("Subscribing to UI Input Actions");
        if (pauseAction != null)
        {
            pauseAction.performed += OnPausePerformed;
        }
    }

    #endregion

    #region Event Handlers

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        DebugLog("Pause input detected!");

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.isPaused)
                GameManager.Instance.ResumeGame();
            else
                GameManager.Instance.PauseGame();
        }
        else
        {
            Debug.LogWarning("GameManager.Instance is null - cannot handle pause");
        }
    }

    #endregion

    public void EnableGameplayInput()
    {
        gameplayActionMap.Enable();
    }

    public void DisableGameplayInput()
    {
        gameplayActionMap.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            DebugLog("Singleton destroyed");
            Instance = null;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[InputManager] {message}");
    }
}