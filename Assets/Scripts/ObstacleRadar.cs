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

    // Proximity warning
    private float _dangerWarningCooldown;
    private const float DANGER_DIST = 6f;      // meters: triggers warning
    private const float DANGER_SPEED = 12f;     // m/s: must be going fast

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

        _radarBgImage.color = new Color(0.05f, 0.08f, 0.04f, 0.4f * _radarAlpha);

        // Scan for obstacles periodically (every 0.15s for performance)
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = 0.15f;
            ScanObstacles();
        }
    }

    void ScanObstacles()
    {
        float playerDist = _tc.DistanceTraveled;
        Vector3 playerPos = _tc.transform.position;
        Vector3 playerFwd = _tc.transform.forward;
        Vector3 playerRight = _tc.transform.right;
        Vector3 playerUp = _tc.transform.up;

        // Find all obstacles in range
        var obstacles = Object.FindObjectsByType<Obstacle>(FindObjectsSortMode.None);

        int blipIdx = 0;
        float closestAhead = float.MaxValue;
        float speed = _tc.CurrentSpeed;

        foreach (var obs in obstacles)
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
            float upDist = Vector3.Dot(toObs, playerUp);

            // Normalize to radar radius
            float radarX = (rightDist / (_pipeGen.pipeRadius * 1.5f)) * RADAR_RADIUS;
            float radarY = (fwdDist / SCAN_RANGE) * RADAR_RADIUS; // forward = up on radar

            // Clamp to radar bounds
            float blipDist = Mathf.Sqrt(radarX * radarX + radarY * radarY);
            if (blipDist > RADAR_RADIUS) continue;

            _blipRTs[blipIdx].anchoredPosition = new Vector2(radarX, radarY);

            // Color based on proximity: far = dim yellow, close = bright red
            float urgency = 1f - (fwdDist / SCAN_RANGE);
            Color blipColor = Color.Lerp(
                new Color(1f, 0.8f, 0.2f, 0.5f),
                new Color(1f, 0.2f, 0.1f, 0.9f),
                urgency);
            blipColor.a *= _radarAlpha;

            // Pulse close obstacles
            if (urgency > 0.6f)
                blipColor.a *= 0.7f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.3f;

            _blips[blipIdx].color = blipColor;

            // Size: closer = bigger
            float size = Mathf.Lerp(4f, 8f, urgency);
            _blipRTs[blipIdx].sizeDelta = new Vector2(size, size);

            blipIdx++;
        }

        // Hide unused blips
        for (int i = blipIdx; i < MAX_BLIPS; i++)
            _blips[i].color = new Color(1f, 0.3f, 0.15f, 0f);

        // Proximity warning: red edge flash when obstacle is dangerously close at speed
        _dangerWarningCooldown -= 0.15f; // scan interval
        if (closestAhead < DANGER_DIST && speed >= DANGER_SPEED && _dangerWarningCooldown <= 0f)
        {
            _dangerWarningCooldown = 0.8f; // don't spam warnings
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerProximityWarning();
            HapticManager.LightTap();
        }
    }
}
