using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small circular radar HUD showing upcoming obstacles as blips.
/// Helps mobile players prepare for dodges at high speed.
/// Fades in at speed >= 8 m/s, fully visible at 12+ m/s.
/// </summary>
public class ObstacleRadar : MonoBehaviour
{
    public static ObstacleRadar Instance { get; private set; }

    private RectTransform _radarBg;
    private Image _radarBgImage;
    private Image[] _blips;
    private RectTransform[] _blipRTs;
    private const int MAX_BLIPS = 8;
    private const float SCAN_RANGE = 30f;   // meters ahead to scan
    private const float RADAR_RADIUS = 35f; // UI radius in pixels
    private float _radarAlpha;
    private float _scanTimer;
    private TurdController _tc;
    private PipeGenerator _pipeGen;

    // Proximity warning - escalating beeps + haptics
    private float _closestObstacleDist = float.MaxValue;
    private float _pingCooldown;
    private float _lastPingDist;
    private int _lastHapticTier; // 0=none, 1=light, 2=medium, 3=heavy
    private const float WARNING_RANGE = 15f;   // start pinging at 15m
    private const float WARNING_SPEED = 6f;    // lower threshold: warn at moderate speed too

    // Danger zone flash tracking
    private float _dangerFlashTimer;
    private bool _wasInDangerZone;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _tc = Object.FindFirstObjectByType<TurdController>();
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        CreateRadarUI();
    }

    void CreateRadarUI()
    {
        Canvas canvas = null;
        if (ScreenEffects.Instance != null)
            canvas = ScreenEffects.Instance.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        }
        if (canvas == null) return;

        // Radar circle background (bottom-left, semi-transparent)
        GameObject bgObj = new GameObject("RadarBg");
        _radarBg = bgObj.AddComponent<RectTransform>();
        _radarBg.SetParent(canvas.transform, false);
        _radarBg.anchorMin = new Vector2(0.02f, 0.10f);
        _radarBg.anchorMax = new Vector2(0.02f, 0.10f);
        _radarBg.anchoredPosition = Vector2.zero;
        _radarBg.sizeDelta = new Vector2(RADAR_RADIUS * 2f + 10f, RADAR_RADIUS * 2f + 10f);

        _radarBgImage = bgObj.AddComponent<Image>();
        _radarBgImage.color = new Color(0.05f, 0.08f, 0.04f, 0f);

        // Center dot (player position)
        GameObject centerDot = new GameObject("CenterDot");
        RectTransform crt = centerDot.AddComponent<RectTransform>();
        crt.SetParent(_radarBg, false);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(6, 6);
        Image ci = centerDot.AddComponent<Image>();
        ci.color = new Color(0.2f, 1f, 0.3f, 0.9f);

        // Radar ring
        GameObject ring = new GameObject("RadarRing");
        RectTransform rrt = ring.AddComponent<RectTransform>();
        rrt.SetParent(_radarBg, false);
        rrt.anchoredPosition = Vector2.zero;
        rrt.sizeDelta = new Vector2(RADAR_RADIUS * 2f, RADAR_RADIUS * 2f);
        // Using Outline on an Image for a cheap ring effect
        Image ri = ring.AddComponent<Image>();
        ri.color = new Color(0.15f, 0.25f, 0.1f, 0.3f);
        Outline ringOutline = ring.AddComponent<Outline>();
        ringOutline.effectColor = new Color(0.2f, 0.6f, 0.2f, 0.5f);
        ringOutline.effectDistance = new Vector2(1, -1);

        // Blips (obstacle indicators)
        _blips = new Image[MAX_BLIPS];
        _blipRTs = new RectTransform[MAX_BLIPS];
        for (int i = 0; i < MAX_BLIPS; i++)
        {
            GameObject blipObj = new GameObject("Blip_" + i);
            _blipRTs[i] = blipObj.AddComponent<RectTransform>();
            _blipRTs[i].SetParent(_radarBg, false);
            _blipRTs[i].sizeDelta = new Vector2(5, 5);
            _blipRTs[i].anchoredPosition = Vector2.zero;
            _blips[i] = blipObj.AddComponent<Image>();
            _blips[i].color = new Color(1f, 0.3f, 0.15f, 0f); // hidden initially
        }

        bgObj.SetActive(true);
    }

    void Update()
    {
        if (_tc == null || _pipeGen == null || _radarBg == null) return;
        if (GameManager.Instance == null || !GameManager.Instance.isPlaying) return;

        // Fade radar in/out based on speed (useful at high speed)
        float speed = _tc.CurrentSpeed;
        float targetAlpha = Mathf.Clamp01((speed - 8f) / 4f); // 0 at 8 m/s, 1 at 12+
        _radarAlpha = Mathf.Lerp(_radarAlpha, targetAlpha, Time.deltaTime * 3f);

        if (_radarAlpha < 0.01f)
        {
            _radarBgImage.color = new Color(0.05f, 0.08f, 0.04f, 0f);
            for (int i = 0; i < MAX_BLIPS; i++)
                _blips[i].color = new Color(1f, 0.3f, 0.15f, 0f);
            return;
        }

        // Radar background: flashes red-ish when entering danger zone
        if (_dangerFlashTimer > 0f)
        {
            _dangerFlashTimer -= Time.deltaTime;
            float flashT = _dangerFlashTimer / 0.3f;
            Color bgCol = Color.Lerp(new Color(0.05f, 0.08f, 0.04f), new Color(0.35f, 0.08f, 0.04f), flashT);
            _radarBgImage.color = new Color(bgCol.r, bgCol.g, bgCol.b, Mathf.Lerp(0.4f, 0.65f, flashT) * _radarAlpha);
        }
        else
        {
            _radarBgImage.color = new Color(0.05f, 0.08f, 0.04f, 0.4f * _radarAlpha);
        }

        // Scan for obstacles periodically (every 0.1s for responsiveness)
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = 0.1f;
            ScanObstacles();
        }

        // Escalating proximity warning (runs every frame for smooth timing)
        UpdateProximityWarning(speed);
    }

    void UpdateProximityWarning(float speed)
    {
        if (_closestObstacleDist >= WARNING_RANGE || speed < WARNING_SPEED)
        {
            _lastHapticTier = 0;
            _wasInDangerZone = false;
            return;
        }

        // Flash when first entering danger zone (< 8m)
        bool inDangerZone = _closestObstacleDist < 8f;
        if (inDangerZone && !_wasInDangerZone)
        {
            // Brief radar background flash
            _dangerFlashTimer = 0.3f;
        }
        _wasInDangerZone = inDangerZone;

        _pingCooldown -= Time.deltaTime;

        // Urgency: 0 at 15m → 1 at 0m
        float urgency = 1f - (_closestObstacleDist / WARNING_RANGE);
        urgency *= urgency; // quadratic: ramps up aggressively as distance closes

        // Speed scaling: warnings are more frequent at higher speed
        float speedFactor = Mathf.Clamp01((speed - WARNING_SPEED) / 8f) * 0.4f + 0.6f;

        // Ping interval: 0.6s at edge → 0.08s at point-blank
        float pingInterval = Mathf.Lerp(0.6f, 0.08f, urgency) / speedFactor;

        if (_pingCooldown <= 0f)
        {
            _pingCooldown = pingInterval;

            // Audio ping with rising pitch
            float pitch = Mathf.Lerp(0.9f, 1.8f, urgency);
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayDangerPing(pitch);

            // Tiered haptic feedback
            int hapticTier;
            if (_closestObstacleDist < 4f) hapticTier = 3;       // CLOSE: heavy
            else if (_closestObstacleDist < 8f) hapticTier = 2;  // MEDIUM
            else hapticTier = 1;                                   // FAR: light

            // Only fire haptic when tier escalates or on heavy-tier pings
            if (hapticTier > _lastHapticTier || hapticTier == 3)
            {
                switch (hapticTier)
                {
                    case 1: HapticManager.LightTap(); break;
                    case 2: HapticManager.MediumTap(); break;
                    case 3: HapticManager.HeavyTap(); break;
                }
            }
            _lastHapticTier = hapticTier;

            // Screen-edge danger flash at close range
            if (_closestObstacleDist < 6f && ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerProximityWarning();
        }
    }

    void ScanObstacles()
    {
        float playerDist = _tc.DistanceTraveled;
        Vector3 playerPos = _tc.transform.position;
        Vector3 playerFwd = _tc.transform.forward;
        Vector3 playerRight = _tc.transform.right;
        Vector3 playerUp = _tc.transform.up;

        // Use static registry instead of FindObjectsByType (O(n obstacles) not O(n scene))
        int blipIdx = 0;
        float closestAhead = float.MaxValue;
        float speed = _tc.CurrentSpeed;
        float pipeRadiusScale = _pipeGen != null ? _pipeGen.pipeRadius * 1.5f : 5.25f;

        foreach (var obs in Obstacle.AllObstacles)
        {
            if (blipIdx >= MAX_BLIPS) break;
            if (obs == null) continue;

            Vector3 toObs = obs.transform.position - playerPos;
            float fwdDist = Vector3.Dot(toObs, playerFwd);

            // Track closest obstacle for proximity warning
            if (fwdDist > 0.5f && fwdDist < closestAhead)
                closestAhead = fwdDist;

            // Only show obstacles ahead (within scan range)
            if (fwdDist < 1f || fwdDist > SCAN_RANGE) continue;

            // Project obstacle position into radar space
            float rightDist = Vector3.Dot(toObs, playerRight);

            // Normalize to radar radius
            float radarX = (rightDist / pipeRadiusScale) * RADAR_RADIUS;
            float radarY = (fwdDist / SCAN_RANGE) * RADAR_RADIUS;

            // Clamp to radar bounds
            float blipDist = Mathf.Sqrt(radarX * radarX + radarY * radarY);
            if (blipDist > RADAR_RADIUS) continue;

            _blipRTs[blipIdx].anchoredPosition = new Vector2(radarX, radarY);

            // Type-based color — use cached behavior from Obstacle
            float urgency = 1f - (fwdDist / SCAN_RANGE);
            Color typeColor = new Color(1f, 0.8f, 0.2f);
            var behavior = obs.GetComponent<ObstacleBehavior>();
            if (behavior != null) typeColor = behavior.HitFlashColor;

            Color blipColor = Color.Lerp(
                new Color(typeColor.r, typeColor.g, typeColor.b, 0.5f),
                new Color(Mathf.Min(typeColor.r + 0.3f, 1f), typeColor.g * 0.3f, typeColor.b * 0.2f, 0.95f),
                urgency);
            blipColor.a *= _radarAlpha;

            if (urgency > 0.5f)
            {
                float pulseRate = Mathf.Lerp(4f, 12f, urgency);
                blipColor.a *= 0.6f + Mathf.Abs(Mathf.Sin(Time.time * pulseRate)) * 0.4f;
            }

            _blips[blipIdx].color = blipColor;

            float size = Mathf.Lerp(8f, 14f, urgency);
            _blipRTs[blipIdx].sizeDelta = new Vector2(size, size);

            blipIdx++;
        }

        // Hide unused blips
        for (int i = blipIdx; i < MAX_BLIPS; i++)
            _blips[i].color = new Color(1f, 0.3f, 0.15f, 0f);

        // Update closest obstacle distance for continuous proximity warning
        _closestObstacleDist = closestAhead;
    }
}
