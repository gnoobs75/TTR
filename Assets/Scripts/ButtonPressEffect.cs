using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Adds tactile scale-down feedback when a UI button is pressed.
/// Attach to any GameObject with a Button component for mobile-feel press response.
/// </summary>
public class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 _originalScale;
    private bool _isPressed;
    private float _animTimer;
    private const float PRESS_SCALE = 0.9f;
    private const float ANIM_DURATION = 0.08f;
    private const float RELEASE_DURATION = 0.12f;

    void Awake()
    {
        _originalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        _animTimer = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        _animTimer = 0f;
    }

    void Update()
    {
        _animTimer += Time.unscaledDeltaTime;

        if (_isPressed)
        {
            float t = Mathf.Clamp01(_animTimer / ANIM_DURATION);
            transform.localScale = Vector3.Lerp(_originalScale, _originalScale * PRESS_SCALE, t);
        }
        else
        {
            float t = Mathf.Clamp01(_animTimer / RELEASE_DURATION);
            Vector3 current = transform.localScale;
            // Slight overshoot on release for bounce feel
            float overshoot = t < 0.6f
                ? Mathf.Lerp(PRESS_SCALE, 1.04f, t / 0.6f)
                : Mathf.Lerp(1.04f, 1f, (t - 0.6f) / 0.4f);
            transform.localScale = _originalScale * overshoot;
        }
    }
}
