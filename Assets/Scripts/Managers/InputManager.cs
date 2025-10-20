using System;
using Unity.VisualScripting;
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
    private InputActionMap locomotionActionMap;
    private InputActionMap uiActionMap;

    #endregion

    #region Public Properties [Core Input Actions]

    private InputAction moveAction;

    private InputAction pauseAction;

    #endregion

    #region Public Properties

    public Vector2 MovementInput { get; private set; }

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

        if (locomotionActionMap != null)
        {
            locomotionActionMap.Enable();
            DebugLog("Locomotion ActionMap enabled immediately");
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
        locomotionActionMap = inputActions.FindActionMap("Locomotion");

        SetUpUIInputActions();
        SetUpCoreLocomotionActions();

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

    private void SetUpCoreLocomotionActions()
    {
        moveAction = locomotionActionMap.FindAction("Move");
    }

    #region Update Loop

    private void Update()
    {
        // Update input values
        if (locomotionActionMap?.enabled == true)
            UpdateLocomotionInputValues();

    }


    private void UpdateLocomotionInputValues()
    {
        MovementInput = moveAction?.ReadValue<Vector2>().normalized ?? Vector2.zero;
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
        locomotionActionMap.Enable();
    }

    public void DisableGameplayInput()
    {
        locomotionActionMap.Disable();
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