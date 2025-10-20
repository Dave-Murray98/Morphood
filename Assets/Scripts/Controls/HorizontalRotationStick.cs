using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A custom horizontal-only joystick optimized for rotation controls.
/// Feeds input directly to the InputManager for maximum performance.
/// </summary>
public class HorizontalRotationStick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Joystick Configuration")]
    [SerializeField] private RectTransform knob;
    [SerializeField] private RectTransform background;
    [SerializeField] private float movementRange = 50f;

    [Header("Rotation Settings")]
    [Tooltip("Which player's rotation this controls (1 or 2)")]
    [SerializeField] private int playerNumber = 1;

    [Header("Settings")]
    [SerializeField] private float deadZone = 0.1f;
    [SerializeField] private bool snapToZeroOnRelease = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal state
    private float horizontalInput = 0f;
    private bool isDragging = false;
    private Camera uiCamera;
    private Canvas parentCanvas;

    // Public property for InputManager to read
    public float HorizontalInput => horizontalInput;
    public int PlayerNumber => playerNumber;

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Find the UI camera
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            uiCamera = parentCanvas.worldCamera;
            if (uiCamera == null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                uiCamera = null; // Overlay canvas doesn't need a camera
        }

        // Ensure knob starts at center
        if (knob != null)
        {
            knob.anchoredPosition = Vector2.zero;
        }

        // Register with InputManager
        RegisterWithInputManager();

        DebugLog($"Player {playerNumber} rotation stick initialized");
    }

    private void RegisterWithInputManager()
    {
        // We'll add a method to InputManager to register custom rotation sticks
        if (InputManager.Instance != null)
        {
            InputManager.Instance.RegisterRotationStick(this);
        }
        else
        {
            // If InputManager isn't ready yet, try again next frame
            Invoke(nameof(RegisterWithInputManager), 0.1f);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        OnDrag(eventData);
        DebugLog($"Player {playerNumber} rotation stick - pointer down");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;

        if (snapToZeroOnRelease)
        {
            // Snap back to center
            SetHorizontalInput(0f);
        }

        DebugLog($"Player {playerNumber} rotation stick - pointer up");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || background == null) return;

        // Convert screen position to local position relative to background
        Vector2 localPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, uiCamera, out localPosition))
        {
            // Normalize the position relative to movement range
            float normalizedX = localPosition.x / movementRange;

            // Clamp to -1 to 1 range
            normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);

            // Apply deadzone
            if (Mathf.Abs(normalizedX) < deadZone)
            {
                normalizedX = 0f;
            }

            SetHorizontalInput(normalizedX);
        }
    }

    private void SetHorizontalInput(float value)
    {
        horizontalInput = value;
        UpdateKnobPosition();

        DebugLog($"Player {playerNumber} rotation input: {horizontalInput:F3}");
    }

    private void UpdateKnobPosition()
    {
        if (knob == null) return;

        // Position knob based on input (only horizontal movement)
        Vector2 knobPosition = new Vector2(horizontalInput * movementRange, 0f);
        knob.anchoredPosition = knobPosition;
    }

    void OnDisable()
    {
        isDragging = false;
        SetHorizontalInput(0f);

        // Unregister from InputManager
        if (InputManager.Instance != null)
        {
            InputManager.Instance.UnregisterRotationStick(this);
        }
    }

    void OnDestroy()
    {
        // Ensure we unregister when destroyed
        if (InputManager.Instance != null)
        {
            InputManager.Instance.UnregisterRotationStick(this);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[HorizontalRotationStick] {message}");
    }

    // Editor helper to visualize the joystick area
    void OnDrawGizmosSelected()
    {
        if (background != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 worldPos = background.transform.position;

            // Draw horizontal constraint line
            Gizmos.color = Color.red;
            Vector3 leftPos = worldPos + Vector3.left * movementRange;
            Vector3 rightPos = worldPos + Vector3.right * movementRange;
            Gizmos.DrawLine(leftPos, rightPos);

            // Draw deadzone
            Gizmos.color = Color.blue;
            float deadZoneRange = movementRange * deadZone;
            Vector3 deadZoneLeft = worldPos + Vector3.left * deadZoneRange;
            Vector3 deadZoneRight = worldPos + Vector3.right * deadZoneRange;
            Gizmos.DrawLine(deadZoneLeft, deadZoneRight);
        }
    }
}