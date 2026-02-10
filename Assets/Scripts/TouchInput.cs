using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Abstracts input for both keyboard and touch/tilt controls.
/// Other scripts read SteerInput (-1 to 1) instead of checking devices directly.
/// </summary>
public class TouchInput : MonoBehaviour
{
    public static TouchInput Instance { get; private set; }

    public enum ControlScheme { TouchZones, Swipe, Tilt, Keyboard }

    [Header("Settings")]
    public ControlScheme controlScheme = ControlScheme.Keyboard;
    public float swipeSensitivity = 3f;
    public float tiltSensitivity = 2.5f;
    public float tiltDeadZone = 0.08f;

    /// <summary>Steering value from -1 (right) to +1 (left). Read by TurdController.</summary>
    public float SteerInput { get; private set; }

    /// <summary>True the frame the player taps/presses to start or restart.</summary>
    public bool ActionPressed { get; private set; }

    // Swipe tracking
    private Vector2 _swipeStart;
    private bool _isSwiping;
    private float _swipeSteer;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Auto-detect platform
#if UNITY_EDITOR
        controlScheme = ControlScheme.Keyboard;
#elif UNITY_IOS || UNITY_ANDROID
        controlScheme = ControlScheme.TouchZones;
#endif
    }

    void Update()
    {
        ActionPressed = false;
        SteerInput = 0f;

        switch (controlScheme)
        {
            case ControlScheme.Keyboard:
                UpdateKeyboard();
                break;
            case ControlScheme.TouchZones:
                UpdateTouchZones();
                break;
            case ControlScheme.Swipe:
                UpdateSwipe();
                break;
            case ControlScheme.Tilt:
                UpdateTilt();
                break;
        }
    }

    void UpdateKeyboard()
    {
        if (Keyboard.current == null) return;

        float raw = 0f;
        // NOTE: Cross(forward, up) gives LEFT vector, so increasing angle = visual LEFT
        if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
            raw += 1f;
        if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
            raw -= 1f;

        SteerInput = raw;
        ActionPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    void UpdateTouchZones()
    {
        if (Touchscreen.current == null) return;

        var touches = Touchscreen.current.touches;
        bool anyTouch = false;

        for (int i = 0; i < touches.Count; i++)
        {
            var touch = touches[i];
            if (!touch.press.isPressed) continue;
            anyTouch = true;

            Vector2 pos = touch.position.ReadValue();
            float screenHalf = Screen.width * 0.5f;

            // Left half = steer left, right half = steer right
            if (pos.x < screenHalf)
                SteerInput += 1f; // Left (increases angle = visual left)
            else
                SteerInput -= 1f; // Right

            // Tap detection for action
            if (touch.press.wasPressedThisFrame)
                ActionPressed = true;
        }

        SteerInput = Mathf.Clamp(SteerInput, -1f, 1f);
    }

    void UpdateSwipe()
    {
        if (Touchscreen.current == null) return;

        var primaryTouch = Touchscreen.current.primaryTouch;

        if (primaryTouch.press.wasPressedThisFrame)
        {
            _swipeStart = primaryTouch.position.ReadValue();
            _isSwiping = true;
            ActionPressed = true;
        }

        if (_isSwiping && primaryTouch.press.isPressed)
        {
            Vector2 current = primaryTouch.position.ReadValue();
            float deltaX = (current.x - _swipeStart.x) / Screen.width;
            // Negative deltaX = swipe left = steer left = positive input
            _swipeSteer = Mathf.Lerp(_swipeSteer, -deltaX * swipeSensitivity, Time.deltaTime * 10f);
            SteerInput = Mathf.Clamp(_swipeSteer, -1f, 1f);
        }

        if (primaryTouch.press.wasReleasedThisFrame)
        {
            _isSwiping = false;
            _swipeSteer = 0f;
        }
    }

    void UpdateTilt()
    {
        if (Accelerometer.current == null) return;

        // Enable if needed
        if (!Accelerometer.current.enabled)
            InputSystem.EnableDevice(Accelerometer.current);

        Vector3 accel = Accelerometer.current.acceleration.ReadValue();

        // Tilt X axis maps to steering (phone tilted left/right)
        float tilt = accel.x;

        // Dead zone
        if (Mathf.Abs(tilt) < tiltDeadZone)
            tilt = 0f;
        else
            tilt = (tilt - Mathf.Sign(tilt) * tiltDeadZone) / (1f - tiltDeadZone);

        // Negative tilt (tilt left) = steer left = positive input
        SteerInput = Mathf.Clamp(-tilt * tiltSensitivity, -1f, 1f);

        // Any screen tap = action
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            ActionPressed = true;
    }

    /// <summary>Switch control scheme at runtime (from settings menu).</summary>
    public void SetControlScheme(ControlScheme scheme)
    {
        controlScheme = scheme;
        _swipeSteer = 0f;
        _isSwiping = false;
    }
}
