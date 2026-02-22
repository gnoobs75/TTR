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

    // Zone vignette (atmospheric colored edges per zone)
    private Image _zoneVignette;

    // Speed streaks (radial lines from center at high speed)
    private Image _speedStreaks;
    private float _speedStreakIntensity;

    // Film grain (noisy overlay at high speed for intensity feel)
    private Image _filmGrain;
    private Texture2D _grainTex;
    private float _grainIntensity;
    private float _grainTimer;

    // State
    private float _hitFlashAlpha;
    private float _vignetteIntensity;
    private float _dangerAlpha;
    private float _speedIntensity;
    private float _underwaterIntensity;
    private Color _zoneVignetteColor = Color.clear;
    private float _zoneVignetteAlpha;

    // Hit flash config
    private Color _hitFlashColor = new Color(1f, 0.15f, 0.05f, 0.6f);
    private float _hitFlashDecay = 3f;

    // Speed overlay
    private float _speedThreshold = 12f;
    private float _maxSpeedOverlay = 0.15f;

    // Hit chromatic aberration (color-shifted overlays)
    private Image _chromaticLeft;
    private Image _chromaticRight;
    private float _chromaticIntensity;

    // Speed chromatic (sustained at high speed, separate from hit)
    private float _speedChromaticTarget;

    // Impact splatter (cracks/splat on heavy hits)
    private Image _splatter;

    // Invincibility shimmer (golden edge pulse during i-frames)
    private Image _invincShimmer;
    private float _invincShimmerAlpha;
    private float _splatterAlpha;
    private Color _splatterColor = Color.white;
    private float _splatterDecay = 1.5f; // seconds to fully fade

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

        // Vignette - darkened edges with procedural radial gradient
        _vignetteOverlay = CreateOverlay("Vignette", new Color(0, 0, 0, 0));
        ApplyVignetteTexture(_vignetteOverlay);

        // Chromatic aberration overlays (red/cyan fringing on hit)
        _chromaticLeft = CreateOverlay("ChromaticL", new Color(1f, 0f, 0f, 0f));
        _chromaticRight = CreateOverlay("ChromaticR", new Color(0f, 0.8f, 1f, 0f));
        // Offset them slightly for the fringe effect
        if (_chromaticLeft != null)
        {
            RectTransform rt = _chromaticLeft.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(-4, 0);
            rt.offsetMax = new Vector2(-4, 0);
        }
        if (_chromaticRight != null)
        {
            RectTransform rt = _chromaticRight.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(4, 0);
            rt.offsetMax = new Vector2(4, 0);
        }

        // Danger pulse - low health warning
        _dangerPulse = CreateOverlay("DangerPulse", new Color(0.8f, 0f, 0f, 0));

        // Speed overlay - blue-ish tunnel vision tint at high speed
        _speedOverlay = CreateOverlay("SpeedTint", new Color(0.05f, 0.1f, 0.25f, 0));

        // Underwater tint - murky green for drop sections
        _underwaterTint = CreateOverlay("UnderwaterTint", new Color(0.02f, 0.12f, 0.06f, 0));

        // Zone vignette - atmospheric colored tint per zone
        _zoneVignette = CreateOverlay("ZoneVignette", new Color(0, 0, 0, 0));

        // Speed streaks - radial lines from center when going fast
        _speedStreaks = CreateOverlay("SpeedStreaks", new Color(1, 1, 1, 0));
        ApplySpeedStreakTexture(_speedStreaks);

        // Film grain - noisy overlay for intensity at speed
        _filmGrain = CreateOverlay("FilmGrain", new Color(1, 1, 1, 0));
        _grainTex = CreateGrainTexture(128);
        if (_filmGrain != null)
        {
            Sprite spr = Sprite.Create(_grainTex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            _filmGrain.sprite = spr;
            _filmGrain.type = Image.Type.Tiled;
        }

        // Impact splatter - radial cracks on heavy hits
        _splatter = CreateOverlay("Splatter", new Color(1, 1, 1, 0));
        ApplySplatterTexture(_splatter);

        // Invincibility shimmer - golden vignette glow during i-frames
        _invincShimmer = CreateOverlay("InvincShimmer", new Color(1f, 0.85f, 0.2f, 0f));
        ApplyInvincShimmerTexture(_invincShimmer);
    }

    void ApplyVignetteTexture(Image img)
    {
        if (img == null) return;
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((dist - 0.35f) / 0.65f);
                alpha = alpha * alpha; // quadratic falloff for natural vignette
                tex.SetPixel(x, y, new Color(0, 0, 0, alpha));
            }
        }
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        img.sprite = spr;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
    }

    void ApplySpeedStreakTexture(Image img)
    {
        if (img == null) return;
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Only show at edges (radial streaks from center)
                float edgeMask = Mathf.Clamp01((dist - 0.4f) / 0.5f);
                edgeMask = edgeMask * edgeMask;

                // Radial line pattern (angular stripes)
                float angle = Mathf.Atan2(dy, dx);
                float lines = Mathf.Abs(Mathf.Sin(angle * 18f)); // 36 radial lines
                lines = Mathf.Pow(lines, 3f); // sharp lines
                lines *= edgeMask;

                // Slight fade at very edge to avoid hard border
                float outerFade = 1f - Mathf.Clamp01((dist - 0.9f) / 0.1f);

                float alpha = lines * outerFade;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        img.sprite = spr;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
    }

    Texture2D CreateGrainTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // crispy grain
        tex.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = Random.value;
                // Bias toward midtones so grain doesn't blow out
                float v = Mathf.Lerp(0.35f, 0.65f, noise);
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        }
        tex.Apply();
        return tex;
    }

    void RefreshGrainTexture()
    {
        if (_grainTex == null) return;
        int size = _grainTex.width;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = Random.value;
                float v = Mathf.Lerp(0.35f, 0.65f, noise);
                _grainTex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        }
        _grainTex.Apply();
    }

    void ApplySplatterTexture(Image img)
    {
        if (img == null) return;
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;

        // Seed for consistent crack pattern
        Random.State oldState = Random.state;
        Random.InitState(42);

        // Generate 6-8 radial crack lines from center
        int numCracks = Random.Range(6, 9);
        float[] crackAngles = new float[numCracks];
        float[] crackLengths = new float[numCracks];
        for (int i = 0; i < numCracks; i++)
        {
            crackAngles[i] = Random.Range(0f, Mathf.PI * 2f);
            crackLengths[i] = Random.Range(0.4f, 0.85f);
        }

        // Generate splatter blobs around edges
        int numBlobs = Random.Range(8, 14);
        Vector2[] blobPos = new Vector2[numBlobs];
        float[] blobSize = new float[numBlobs];
        for (int i = 0; i < numBlobs; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(0.3f, 0.9f);
            blobPos[i] = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
            blobSize[i] = Random.Range(0.04f, 0.12f);
        }

        Random.state = oldState;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float pixelAngle = Mathf.Atan2(dy, dx);

                float alpha = 0f;

                // Central impact ring (cracked glass look)
                float ring = Mathf.Abs(dist - 0.15f);
                if (ring < 0.03f)
                    alpha = Mathf.Max(alpha, (1f - ring / 0.03f) * 0.6f);

                // Radial crack lines
                for (int c = 0; c < numCracks; c++)
                {
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(pixelAngle * Mathf.Rad2Deg, crackAngles[c] * Mathf.Rad2Deg));
                    float width = 1.2f + dist * 1.5f; // cracks widen outward
                    if (angleDiff < width && dist < crackLengths[c] && dist > 0.05f)
                    {
                        float crackAlpha = (1f - angleDiff / width) * (1f - dist / crackLengths[c]) * 0.7f;
                        alpha = Mathf.Max(alpha, crackAlpha);
                    }
                }

                // Splatter blobs (irregular splat marks)
                for (int b = 0; b < numBlobs; b++)
                {
                    float bdx = dx - blobPos[b].x;
                    float bdy = dy - blobPos[b].y;
                    float bDist = Mathf.Sqrt(bdx * bdx + bdy * bdy);
                    if (bDist < blobSize[b])
                    {
                        float blobAlpha = (1f - bDist / blobSize[b]);
                        blobAlpha = blobAlpha * blobAlpha * 0.5f; // soft edges
                        alpha = Mathf.Max(alpha, blobAlpha);
                    }
                }

                // Edge drips (gravity-pulled streaks at bottom half)
                if (dy > 0.1f && dist > 0.3f)
                {
                    float dripChance = Mathf.Sin(dx * 37f) * 0.5f + 0.5f;
                    if (dripChance > 0.7f)
                    {
                        float dripAlpha = (dy - 0.1f) * 0.3f * (1f - Mathf.Abs(dx) * 0.5f);
                        float dripWidth = Mathf.Abs(Mathf.Sin(dx * 53f)) * 0.02f;
                        float dripMask = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(dx * 53f + 0.5f) - 0.5f) / (dripWidth + 0.01f));
                        alpha = Mathf.Max(alpha, dripAlpha * dripMask);
                    }
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        img.sprite = spr;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
    }

    void ApplyInvincShimmerTexture(Image img)
    {
        if (img == null) return;
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Edge glow: visible only near screen edges (like a protective aura)
                float edgeX = Mathf.Max(0f, Mathf.Abs(dx) - 0.65f) / 0.35f;
                float edgeY = Mathf.Max(0f, Mathf.Abs(dy) - 0.65f) / 0.35f;
                float edge = Mathf.Max(edgeX, edgeY);
                edge = edge * edge; // quadratic for soft falloff

                tex.SetPixel(x, y, new Color(1f, 0.9f, 0.3f, Mathf.Clamp01(edge * 0.8f)));
            }
        }
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        img.sprite = spr;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
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

        // Vignette overlay - darkened edges at speed or after hit
        if (_vignetteIntensity > 0.001f)
        {
            if (_vignetteOverlay != null)
                _vignetteOverlay.color = new Color(1f, 1f, 1f, _vignetteIntensity);
            _vignetteIntensity = Mathf.Lerp(_vignetteIntensity, 0f, dt * 2f);
        }
        else if (_vignetteOverlay != null)
        {
            _vignetteOverlay.color = new Color(1f, 1f, 1f, 0f);
        }

        // Chromatic aberration - red/cyan fringe on hit + sustained at speed
        float chromaTotal = Mathf.Max(_chromaticIntensity, _speedChromaticTarget);
        if (chromaTotal > 0.005f)
        {
            float a = chromaTotal * 0.12f;
            // At high speed, widen the fringe offset for more dramatic effect
            float offset = 4f + _speedChromaticTarget * 3f;
            if (_chromaticLeft != null)
            {
                _chromaticLeft.color = new Color(1f, 0f, 0f, a);
                RectTransform rt = _chromaticLeft.GetComponent<RectTransform>();
                rt.offsetMin = new Vector2(-offset, 0);
                rt.offsetMax = new Vector2(-offset, 0);
            }
            if (_chromaticRight != null)
            {
                _chromaticRight.color = new Color(0f, 0.8f, 1f, a);
                RectTransform rt = _chromaticRight.GetComponent<RectTransform>();
                rt.offsetMin = new Vector2(offset, 0);
                rt.offsetMax = new Vector2(offset, 0);
            }
            _chromaticIntensity = Mathf.Lerp(_chromaticIntensity, 0f, dt * 8f);
        }
        else
        {
            _chromaticIntensity = 0f;
            if (_chromaticLeft != null) _chromaticLeft.color = new Color(1f, 0f, 0f, 0f);
            if (_chromaticRight != null) _chromaticRight.color = new Color(0f, 0.8f, 1f, 0f);
        }

        // Film grain - intensifies at high speed, refreshes periodically
        if (_grainIntensity > 0.005f)
        {
            _grainTimer += dt;
            // Refresh grain pattern every 0.08s for that flickering film look
            if (_grainTimer >= 0.08f)
            {
                _grainTimer = 0f;
                RefreshGrainTexture();
            }
            // Grain alpha scales with speed intensity, subtle but present
            float grainAlpha = _grainIntensity * 0.06f;
            // Slight flicker for organic feel
            grainAlpha *= 0.85f + Mathf.Sin(Time.time * 23f) * 0.15f;
            if (_filmGrain != null) _filmGrain.color = new Color(0.5f, 0.5f, 0.5f, grainAlpha);
        }
        else if (_filmGrain != null)
        {
            _filmGrain.color = new Color(0.5f, 0.5f, 0.5f, 0f);
        }

        // Zone vignette - atmospheric colored tint at screen edges
        if (_zoneVignetteAlpha > 0.001f && _zoneVignette != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 0.7f) * 0.15f; // slow atmospheric pulse
            Color zc = _zoneVignetteColor;
            zc.a = _zoneVignetteAlpha * 0.08f * pulse; // very subtle
            _zoneVignette.color = zc;
        }
        else if (_zoneVignette != null)
        {
            _zoneVignette.color = Color.clear;
        }

        // Speed streaks - radial lines pulsing at high speed
        if (_speedStreakIntensity > 0.005f && _speedStreaks != null)
        {
            // Pulse for dynamic feel
            float streakPulse = 0.7f + Mathf.Sin(Time.time * 15f) * 0.3f;
            float alpha = _speedStreakIntensity * 0.12f * streakPulse;
            _speedStreaks.color = new Color(0.9f, 0.95f, 1f, alpha);
        }
        else if (_speedStreaks != null)
        {
            _speedStreaks.color = new Color(0.9f, 0.95f, 1f, 0f);
        }

        // Splatter overlay - impact cracks that fade out
        if (_splatterAlpha > 0.005f && _splatter != null)
        {
            _splatterAlpha -= dt / _splatterDecay;
            if (_splatterAlpha < 0f) _splatterAlpha = 0f;
            // Slight wobble as it fades for organic feel
            float wobble = 1f + Mathf.Sin(Time.time * 6f) * 0.02f * _splatterAlpha;
            Color sc = _splatterColor;
            sc.a = _splatterAlpha * 0.45f * wobble;
            _splatter.color = sc;
            // Slight scale pulse on initial impact
            RectTransform srt = _splatter.GetComponent<RectTransform>();
            float impactScale = 1f + Mathf.Max(0f, _splatterAlpha - 0.7f) * 0.1f;
            srt.localScale = Vector3.one * impactScale;
        }
        else if (_splatter != null)
        {
            _splatter.color = new Color(1f, 1f, 1f, 0f);
        }

        // Invincibility shimmer - golden edge glow during i-frames
        if (_invincShimmerAlpha > 0.005f && _invincShimmer != null)
        {
            // Pulsing golden aura at screen edges
            float shimPulse = 0.6f + Mathf.Sin(Time.time * 4.5f) * 0.3f
                                   + Mathf.Sin(Time.time * 7.2f) * 0.1f;
            float shimAlpha = _invincShimmerAlpha * shimPulse;
            // Color shifts from gold to slightly white for sparkle
            float sparkle = Mathf.Sin(Time.time * 12f) * 0.5f + 0.5f;
            Color shimColor = Color.Lerp(
                new Color(1f, 0.85f, 0.2f, shimAlpha * 0.35f),
                new Color(1f, 1f, 0.7f, shimAlpha * 0.5f),
                sparkle * 0.3f);
            _invincShimmer.color = shimColor;
        }
        else if (_invincShimmer != null)
        {
            _invincShimmer.color = new Color(1f, 0.85f, 0.2f, 0f);
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

    /// <summary>Flash red on hit/stun with chromatic aberration.</summary>
    public void TriggerHitFlash()
    {
        _hitFlashAlpha = 0.5f;
        _dangerAlpha = 1f;
        _chromaticIntensity = 1f;  // chromatic fringe on hit
        _vignetteIntensity = Mathf.Max(_vignetteIntensity, 0.6f); // darken edges on hit
    }

    /// <summary>Obstacle-type-specific colored hit flash.</summary>
    public void TriggerHitFlash(Color obstacleColor)
    {
        _hitFlashAlpha = 0.5f;
        _hitFlashColor = new Color(obstacleColor.r, obstacleColor.g, obstacleColor.b, 0.5f);
        _dangerAlpha = 1f;
        _chromaticIntensity = 1f;
        _vignetteIntensity = Mathf.Max(_vignetteIntensity, 0.6f);
        Invoke(nameof(ResetFlashColor), 0.4f);
    }

    /// <summary>Set invincibility shimmer intensity (0-1). Call with 1 when entering i-frames, 0 when done.</summary>
    public void SetInvincShimmer(float intensity)
    {
        _invincShimmerAlpha = Mathf.Clamp01(intensity);
    }

    /// <summary>Brief yellow flash for invincibility end.</summary>
    public void TriggerInvincibilityFlash()
    {
        _hitFlashAlpha = 0.2f;
        _hitFlashColor = new Color(1f, 0.9f, 0.2f, 0.4f);
        _invincShimmerAlpha = 0f; // clear shimmer on invincibility end
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
        // Speed vignette: progressive tunnel vision (stronger curve at extreme speed)
        float vigTarget = t * t * 0.45f; // quadratic ramp for more dramatic extreme-speed feel
        _vignetteIntensity = Mathf.Max(_vignetteIntensity, vigTarget);
        // Speed streaks: radial lines at high speed
        _speedStreakIntensity = Mathf.Lerp(_speedStreakIntensity, t, Time.deltaTime * 4f);
        // Film grain: starts at moderate speed, intensifies
        float grainTarget = Mathf.Clamp01((currentSpeed - 9f) / 8f); // kicks in earlier than other effects
        _grainIntensity = Mathf.Lerp(_grainIntensity, grainTarget, Time.deltaTime * 3f);
        // Speed chromatic: subtle sustained aberration at very high speed
        _speedChromaticTarget = Mathf.Lerp(_speedChromaticTarget, t * 0.4f, Time.deltaTime * 3f);
    }

    /// <summary>Flash speed streaks briefly (e.g. race start, boost pickup).</summary>
    public void FlashSpeedStreaks(float intensity = 1f)
    {
        _speedStreakIntensity = Mathf.Max(_speedStreakIntensity, intensity);
    }

    /// <summary>Green flash for power-up pickup with chromatic burst.</summary>
    public void TriggerPowerUpFlash()
    {
        _hitFlashAlpha = 0.3f;
        _hitFlashColor = new Color(0.1f, 1f, 0.3f, 0.4f);
        _chromaticIntensity = Mathf.Max(_chromaticIntensity, 0.6f); // chromatic burst on boost
        Invoke(nameof(ResetFlashColor), 0.3f);
    }

    /// <summary>Golden flash for milestone.</summary>
    public void TriggerMilestoneFlash()
    {
        _hitFlashAlpha = 0.25f;
        _hitFlashColor = new Color(1f, 0.85f, 0.1f, 0.35f);
        Invoke(nameof(ResetFlashColor), 0.4f);
    }

    /// <summary>Zone-colored flash for zone transitions.</summary>
    public void TriggerZoneFlash(Color zoneColor)
    {
        _hitFlashAlpha = 0.3f;
        _hitFlashColor = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.4f);
        _vignetteIntensity = Mathf.Max(_vignetteIntensity, 0.4f);
        Invoke(nameof(ResetFlashColor), 0.5f);
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

    /// <summary>Trigger impact splatter overlay (heavy/messy hits).</summary>
    public void TriggerSplatter(Color color)
    {
        _splatterAlpha = 1f;
        _splatterColor = color;
    }

    /// <summary>Set zone atmosphere vignette color. Called by PipeZoneSystem.</summary>
    public void UpdateZoneVignette(Color zoneColor, float intensity)
    {
        _zoneVignetteColor = zoneColor;
        _zoneVignetteAlpha = Mathf.Lerp(_zoneVignetteAlpha, intensity, Time.deltaTime * 2f);
    }
}
