using System;
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
    public float CurrentMovementSpeed { get; private set; }
    public float CurrentRotationSpeed { get; private set; }

    // Conflict detection properties
    public bool IsMovementInputConflicting { get; private set; }
    public bool IsRotationInputConflicting { get; private set; }

    #endregion

    [Header("Movement Settings")]
    [SerializeField] private bool useVaryingSpeeds = true;
    [Tooltip("Speed when players move in similar directions")]
    [SerializeField] private float fastMovementSpeed = 8f;
    [Tooltip("Speed when players move in different (but not opposite) directions")]
    [SerializeField] private float slowMovementSpeed = 4f;
    [Tooltip("Single speed used when useVaryingSpeeds is false")]
    [SerializeField] private float moveSpeed = 6f;
    [Tooltip("Threshold for determining opposite movement (0 = exactly opposite, 1 = any difference)")]
    [Range(0f, 1f)]
    [SerializeField] private float oppositeDirectionThreshold = 0.3f;
    [Tooltip("Threshold for determining similar movement directions (0 = exactly same, 1 = any similarity)")]
    [Range(0f, 1f)]
    [SerializeField] private float similarDirectionThreshold = 0.7f;

    [Header("Rotation Settings")]
    [Tooltip("Rotation speed when only one player is rotating")]
    [SerializeField] private float slowRotationSpeed = 90f;  // degrees per second
    [Tooltip("Rotation speed when both players rotate in same direction")]
    [SerializeField] private float fastRotationSpeed = 180f; // degrees per second

    [Header("Input Settings")]
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

        // Read rotation input from custom rotation sticks (now vertical input)
        player1RotationInput = player1RotationStick?.VerticalInput ?? 0f;
        player2RotationInput = player2RotationStick?.VerticalInput ?? 0f;
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
        // Apply deadzone to movement inputs
        Vector2 p1Movement = player1MovementInput.magnitude > inputDeadzone ? player1MovementInput.normalized : Vector2.zero;
        Vector2 p2Movement = player2MovementInput.magnitude > inputDeadzone ? player2MovementInput.normalized : Vector2.zero;

        // Check if both players have input
        bool player1HasMovement = p1Movement.magnitude > 0f;
        bool player2HasMovement = p2Movement.magnitude > 0f;

        Vector2 combinedMovement = Vector2.zero;
        float appliedSpeed = moveSpeed; // Default speed
        IsMovementInputConflicting = false;

        if (player1HasMovement && player2HasMovement)
        {
            // Both players have input - analyze their relationship
            float dotProduct = Vector2.Dot(p1Movement, p2Movement);

            if (dotProduct <= -1f + oppositeDirectionThreshold)
            {
                // Opposite directions - no movement, conflict detected
                combinedMovement = Vector2.zero;
                appliedSpeed = 0f;
                IsMovementInputConflicting = true;

                DebugLog($"Movement - Opposite directions: P1({p1Movement}) vs P2({p2Movement}), dot: {dotProduct:F2} - CONFLICT");

            }
            else if (dotProduct >= similarDirectionThreshold)
            {
                // Similar directions - fast speed
                combinedMovement = (p1Movement + p2Movement).normalized;
                appliedSpeed = useVaryingSpeeds ? fastMovementSpeed : moveSpeed;

                DebugLog($"Movement - Similar directions: P1({p1Movement}) + P2({p2Movement}), dot: {dotProduct:F2} - FAST");

            }
            else
            {
                // Different but not opposite directions - slow speed
                combinedMovement = (p1Movement + p2Movement).normalized;
                appliedSpeed = useVaryingSpeeds ? slowMovementSpeed : moveSpeed;

                DebugLog($"Movement - Different directions: P1({p1Movement}) + P2({p2Movement}), dot: {dotProduct:F2} - SLOW");

            }
        }
        else if (player1HasMovement || player2HasMovement)
        {
            // Only one player has input - use their direction with appropriate speed
            combinedMovement = (p1Movement + p2Movement).normalized; // One will be zero
            appliedSpeed = useVaryingSpeeds ? slowMovementSpeed : moveSpeed;

            DebugLog($"Movement - Single input: P1({p1Movement}) + P2({p2Movement}) - SINGLE");

        }

        Debug.Log($"IsMovementInputConflicting = {IsMovementInputConflicting}");

        // If neither player has input, everything stays at zero (default values)

        MovementInput = combinedMovement;
        CurrentMovementSpeed = appliedSpeed;
    }

    private void CombineRotationInputs()
    {
        // Apply deadzone to rotation inputs
        float p1Rotation = Mathf.Abs(player1RotationInput) > inputDeadzone ? player1RotationInput : 0f;
        float p2Rotation = Mathf.Abs(player2RotationInput) > inputDeadzone ? player2RotationInput : 0f;

        float combinedRotationValue = 0f;
        float appliedRotationSpeed = 0f;
        IsRotationInputConflicting = false;

        // Check if both players are providing input
        bool player1HasRotation = Mathf.Abs(p1Rotation) > 0f;
        bool player2HasRotation = Mathf.Abs(p2Rotation) > 0f;

        if (player1HasRotation && player2HasRotation)
        {
            // Both players have input - check if they're in the same direction
            bool sameDirection = (p1Rotation > 0 && p2Rotation > 0) || (p1Rotation < 0 && p2Rotation < 0);

            if (sameDirection)
            {
                // Same direction: fast rotation speed
                float averageInput = (p1Rotation + p2Rotation) * 0.5f;
                combinedRotationValue = averageInput;
                appliedRotationSpeed = fastRotationSpeed;

                DebugLog($"Rotation - Same direction: P1({p1Rotation:F2}) + P2({p2Rotation:F2}) = {combinedRotationValue:F2} - FAST");

            }
            else
            {
                // Opposite directions: no rotation, conflict detected
                combinedRotationValue = 0f;
                appliedRotationSpeed = 0f;
                IsRotationInputConflicting = true;

                DebugLog($"Rotation - Opposite directions: P1({p1Rotation:F2}) vs P2({p2Rotation:F2}) - CONFLICT");
            }
        }
        else if (player1HasRotation || player2HasRotation)
        {
            // Only one player has input: slow rotation speed
            combinedRotationValue = p1Rotation + p2Rotation;  // One will be 0
            appliedRotationSpeed = slowRotationSpeed;

            DebugLog($"Rotation - Single input: P1({p1Rotation:F2}) + P2({p2Rotation:F2}) = {combinedRotationValue:F2} - SLOW");

        }
        // If neither player has input, everything stays at zero (default values)

        // Clamp the result to reasonable bounds
        combinedRotationValue = Mathf.Clamp(combinedRotationValue, -1f, 1f);

        // Store results
        RotationInput = new Vector2(combinedRotationValue, 0f);
        CurrentRotationSpeed = appliedRotationSpeed;

        Debug.Log($"IsRotationInputConflicting = {IsRotationInputConflicting}");

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