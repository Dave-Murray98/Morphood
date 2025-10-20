using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Receives and processes player input, passing it to the player controller and other systems as needed.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public InputActionAsset inputActions;

    #region Action Maps
    private InputActionMap gameplayActionMap;
    private InputActionMap uiActionMap;

    #endregion

    #region Public Properties [Core Input Actions]

    private InputAction moveAction;
    private InputAction rotateAction;

    private InputAction pauseAction;

    #endregion

    #region Public Properties

    public Vector2 MovementInput { get; private set; }
    public Vector2 RotationInput { get; private set; }

    #endregion

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
        DebugLog("InputManager initialized");
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
            DebugLog("Gameplay ActionMap enabled immediately");
        }
    }

    private void SetUpInputActions()
    {
        DebugLog("Setting up input actions");

        if (inputActions == null)
        {
            Debug.LogError("[InputManager] InputActionAsset is not assigned! Input will not work!");
            return;
        }

        uiActionMap = inputActions.FindActionMap("UI");
        gameplayActionMap = inputActions.FindActionMap("Gameplay");

        SetUpUIInputActions();
        SetUpCoreGameplayActions();

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

    private void SetUpCoreGameplayActions()
    {
        moveAction = gameplayActionMap.FindAction("Move");


        rotateAction = gameplayActionMap.FindAction("Rotate");
        if (rotateAction == null)
        {
            Debug.LogError("[InputManager] Rotate action not found in Locomotion ActionMap!");
        }
    }

    #region Update Loop

    private void Update()
    {
        // Update input values
        if (gameplayActionMap?.enabled == true)
            UpdateLocomotionInputValues();
    }

    private void UpdateLocomotionInputValues()
    {
        MovementInput = moveAction?.ReadValue<Vector2>().normalized ?? Vector2.zero;
        RotationInput = rotateAction?.ReadValue<Vector2>() ?? Vector2.zero;  // Read rotation input

        // Optional: Debug the rotation input
        if (enableDebugLogs && RotationInput.magnitude > 0.1f)
        {
            DebugLog($"Rotation Input: {RotationInput}");
        }
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