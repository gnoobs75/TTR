using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen overlay effects: hit flash, speed vignette, danger pulse, underwater tint.
/// Sits on a UI canvas over the game view.
/// </summary>
public class ScreenEffects : MonoBehaviour
{
    public static ScreenEffects Instance { get; private set; }

    // Overlay images
    private Image _hitFlash;
    private Image _vignetteOverlay;
    private Image _dangerPulse;
    private Image _speedOverlay;

    // Underwater overlay
    private Image _underwaterTint;

    // State
    private float _hitFlashAlpha;
    private float _vignetteIntensity;
    private float _dangerAlpha;
    private float _speedIntensity;
    private float _underwaterIntensity;

    // Hit flash config
    private Color _hitFlashColor = new Color(1f, 0.15f, 0.05f, 0.6f);
    private float _hitFlashDecay = 3f;

    // Speed overlay
    private float _speedThreshold = 12f;
    private float _maxSpeedOverlay = 0.15f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // Create overlay canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Hit flash - full screen red tint
        _hitFlash = CreateOverlay("HitFlash", new Color(1f, 0.15f, 0.05f, 0f));

        // Vignette - darkened edges (uses a gradient texture or just solid with alpha)
        _vignetteOverlay = CreateOverlay("Vignette", new Color(0, 0, 0, 0));

        // Danger pulse - low health warning
        _dangerPulse = CreateOverlay("DangerPulse", new Color(0.8f, 0f, 0f, 0));

        // Speed overlay - blue-ish tunnel vision tint at high speed
        _speedOverlay = CreateOverlay("SpeedTint", new Color(0.05f, 0.1f, 0.25f, 0));

        // Underwater tint - murky green for drop sections
        _underwaterTint = CreateOverlay("UnderwaterTint", new Color(0.02f, 0.12f, 0.06f, 0));
    }

    Image CreateOverlay(string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Decay hit flash
        if (_hitFlashAlpha > 0.001f)
        {
            _hitFlashAlpha = Mathf.Lerp(_hitFlashAlpha, 0f, dt * _hitFlashDecay);
            if (_hitFlashAlpha < 0.005f) _hitFlashAlpha = 0f;
            Color c = _hitFlashColor;
            c.a = _hitFlashAlpha;
            if (_hitFlash != null) _hitFlash.color = c;
        }

        // Danger pulse (when hit state is active)
        if (_dangerAlpha > 0.001f)
        {
            float pulse = Mathf.Sin(Time.time * 6f) * 0.5f + 0.5f;
            Color dc = new Color(0.8f, 0f, 0f, _dangerAlpha * pulse * 0.15f);
            if (_dangerPulse != null) _dangerPulse.color = dc;
            _dangerAlpha = Mathf.Lerp(_dangerAlpha, 0f, dt * 0.5f);
        }
        else if (_dangerPulse != null)
        {
            _dangerPulse.color = new Color(0.8f, 0f, 0f, 0f);
        }

        // Speed overlay - intensifies at high speed
        if (_speedIntensity > 0.001f)
        {
            float maxA = _maxSpeedOverlay * _speedIntensity;
            float flicker = 1f + Mathf.Sin(Time.time * 12f) * 0.05f;
            Color sc = new Color(0.05f, 0.1f, 0.25f, maxA * flicker);
            if (_speedOverlay != null) _speedOverlay.color = sc;
        }
        else if (_speedOverlay != null)
        {
            _speedOverlay.color = new Color(0.05f, 0.1f, 0.25f, 0f);
        }

        // Underwater tint - murky green with caustic ripple
        if (_underwaterIntensity > 0.001f)
        {
            float caustic = 1f + Mathf.Sin(Time.time * 2.5f) * 0.08f
                              + Mathf.Sin(Time.time * 4.1f) * 0.05f
                              + Mathf.Sin(Time.time * 1.3f) * 0.04f; // extra slow ripple
            float alpha = _underwaterIntensity * 0.28f * caustic;
            Color uc = new Color(0.03f, 0.15f, 0.07f, alpha);
            if (_underwaterTint != null) _underwaterTint.color = uc;
        }
        else if (_underwaterTint != null)
        {
            _underwaterTint.color = new Color(0.02f, 0.12f, 0.06f, 0f);
        }
    }

    // === PUBLIC API ===

    /// <summary>Flash red on hit/stun.</summary>
    public void TriggerHitFlash()
    {
        _hitFlashAlpha = 0.5f;
        _dangerAlpha = 1f;
    }

    /// <summary>Brief yellow flash for invincibility end.</summary>
    public void TriggerInvincibilityFlash()
    {
        _hitFlashAlpha = 0.2f;
        _hitFlashColor = new Color(1f, 0.9f, 0.2f, 0.4f);
        // Reset color after use
        Invoke(nameof(ResetFlashColor), 0.3f);
    }

    void ResetFlashColor()
    {
        _hitFlashColor = new Color(1f, 0.15f, 0.05f, 0.6f);
    }

    /// <summary>Update speed-based overlay intensity. Call each frame with current speed.</summary>
    public void UpdateSpeed(float currentSpeed)
    {
        float t = Mathf.Clamp01((currentSpeed - _speedThreshold) / 8f);
        _speedIntensity = Mathf.Lerp(_speedIntensity, t, Time.deltaTime * 3f);
    }

    /// <summary>Green flash for power-up pickup.</summary>
    public void TriggerPowerUpFlash()
    {
        _hitFlashAlpha = 0.3f;
        _hitFlashColor = new Color(0.1f, 1f, 0.3f, 0.4f);
        Invoke(nameof(ResetFlashColor), 0.3f);
    }

    /// <summary>Golden flash for milestone.</summary>
    public void TriggerMilestoneFlash()
    {
        _hitFlashAlpha = 0.25f;
        _hitFlashColor = new Color(1f, 0.85f, 0.1f, 0.35f);
        Invoke(nameof(ResetFlashColor), 0.4f);
    }

    /// <summary>Enable/disable underwater tint for vertical drop sections.</summary>
    public void SetUnderwater(bool active)
    {
        _underwaterIntensity = active ? 1f : 0f;
    }

    /// <summary>Smooth transition for underwater intensity (0-1).</summary>
    public void UpdateUnderwaterIntensity(float target)
    {
        _underwaterIntensity = Mathf.Lerp(_underwaterIntensity, target, Time.deltaTime * 3f);
    }
}
