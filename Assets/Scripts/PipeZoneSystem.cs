using UnityEngine;

/// <summary>
/// Manages themed pipe zones that change visuals as player progresses deeper.
/// Zones: Porcelain → Grimy → Toxic → Rusty Industrial → Hellsewer
/// Provides zone-based colors, emission, and atmosphere to PipeGenerator and lighting.
/// </summary>
public class PipeZoneSystem : MonoBehaviour
{
    public static PipeZoneSystem Instance { get; private set; }

    public enum Zone { Porcelain, Grimy, Toxic, Rusty, Hellsewer }

    [System.Serializable]
    public struct ZoneData
    {
        public string name;
        public float startDistance;
        public Color pipeColor;
        public Color pipeEmission;
        public Color waterColor;
        public Color waterEmission;
        public Color fogColor;
        public float fogDensity;
        public Color ambientColor;
        public Color lightColor;
        public float lightIntensity;
        public float bumpScale;
        public float emissionBoost;    // multiplier on pipeEmission intensity
        public float detailDensity;    // 0-1, how many pipe wall details to spawn
    }

    public ZoneData[] zones = new ZoneData[]
    {
        new ZoneData {
            name = "Porcelain Bowl",
            startDistance = 0f,
            pipeColor = new Color(0.92f, 0.90f, 0.85f),        // Clean concrete - subtle warm white
            pipeEmission = new Color(0.08f, 0.07f, 0.06f),
            waterColor = new Color(0.15f, 0.22f, 0.18f),        // Murky gray-green (sewer water, not neon)
            waterEmission = new Color(0.02f, 0.04f, 0.03f),
            fogColor = new Color(0.10f, 0.11f, 0.12f),
            fogDensity = 0.008f,
            ambientColor = new Color(0.28f, 0.30f, 0.32f),
            lightColor = new Color(0.95f, 0.92f, 0.88f),
            lightIntensity = 1.5f,
            bumpScale = 0.8f,
            emissionBoost = 1.0f,
            detailDensity = 0.3f
        },
        new ZoneData {
            name = "Grimy Pipes",
            startDistance = 80f,
            pipeColor = new Color(0.65f, 0.58f, 0.48f),        // Warm dirty concrete (subtle brown tint)
            pipeEmission = new Color(0.04f, 0.035f, 0.02f),
            waterColor = new Color(0.12f, 0.18f, 0.08f),        // Darker green-brown murk
            waterEmission = new Color(0.03f, 0.05f, 0.02f),
            fogColor = new Color(0.06f, 0.07f, 0.04f),
            fogDensity = 0.012f,
            ambientColor = new Color(0.20f, 0.22f, 0.16f),
            lightColor = new Color(0.88f, 0.80f, 0.65f),
            lightIntensity = 1.3f,
            bumpScale = 1.2f,
            emissionBoost = 0.8f,
            detailDensity = 0.6f
        },
        new ZoneData {
            name = "Toxic Tunnels",
            startDistance = 250f,
            pipeColor = new Color(0.55f, 0.62f, 0.45f),        // Subtle green tint on concrete (not neon)
            pipeEmission = new Color(0.02f, 0.07f, 0.015f),
            waterColor = new Color(0.10f, 0.25f, 0.06f),        // Dark toxic green (not neon)
            waterEmission = new Color(0.03f, 0.12f, 0.015f),
            fogColor = new Color(0.03f, 0.08f, 0.02f),          // Murkier toxic green fog
            fogDensity = 0.015f,
            ambientColor = new Color(0.10f, 0.18f, 0.06f),      // Darker, more saturated green
            lightColor = new Color(0.55f, 0.88f, 0.45f),        // More saturated toxic green light
            lightIntensity = 0.9f,                               // Dimmer - feels more dangerous
            bumpScale = 1.4f,
            emissionBoost = 1.5f,                                // Pipes glow more in darkness
            detailDensity = 0.8f
        },
        new ZoneData {
            name = "Rusty Works",
            startDistance = 500f,
            pipeColor = new Color(0.65f, 0.50f, 0.38f),        // Warm rust-brown (not bright orange)
            pipeEmission = new Color(0.06f, 0.03f, 0.01f),
            waterColor = new Color(0.18f, 0.12f, 0.06f),        // Dark brown water
            waterEmission = new Color(0.05f, 0.03f, 0.01f),
            fogColor = new Color(0.08f, 0.05f, 0.02f),
            fogDensity = 0.012f,
            ambientColor = new Color(0.22f, 0.16f, 0.10f),
            lightColor = new Color(0.95f, 0.72f, 0.45f),
            lightIntensity = 1.2f,
            bumpScale = 1.6f,
            emissionBoost = 1.0f,
            detailDensity = 0.9f
        },
        new ZoneData {
            name = "Hellsewer",
            startDistance = 800f,
            pipeColor = new Color(0.45f, 0.22f, 0.15f),        // Dark reddish-brown (not bright crimson)
            pipeEmission = new Color(0.14f, 0.03f, 0.01f),      // Stronger hellish glow
            waterColor = new Color(0.25f, 0.08f, 0.04f),        // Dark blood-brown
            waterEmission = new Color(0.12f, 0.02f, 0.008f),    // Blood-red glow
            fogColor = new Color(0.10f, 0.02f, 0.008f),         // Blood-red murk
            fogDensity = 0.02f,                                  // Dense, claustrophobic
            ambientColor = new Color(0.14f, 0.06f, 0.03f),      // Much darker, oppressive
            lightColor = new Color(1.0f, 0.38f, 0.18f),         // Deep crimson light
            lightIntensity = 0.75f,                              // Darker, more oppressive
            bumpScale = 1.8f,
            emissionBoost = 2.0f,                                // Pipes glow more in the dark
            detailDensity = 1.0f
        }
    };

    private TurdController _tc;
    private int _currentZoneIndex = 0;
    private float _zoneBlend = 0f; // 0-1 blend between current and next zone
    private Light _mainLight;

    // Current interpolated values for other systems to read
    public Color CurrentPipeColor { get; private set; }
    public Color CurrentPipeEmission { get; private set; }
    public Color CurrentWaterColor { get; private set; }
    public Color CurrentWaterEmission { get; private set; }
    public Color CurrentPipeShadowColor { get; private set; }
    public Color CurrentWaterShadowColor { get; private set; }
    public float CurrentBumpScale { get; private set; }
    public float CurrentEmissionBoost { get; private set; }
    public int CurrentZoneIndex => _currentZoneIndex;
    public string CurrentZoneName => zones[_currentZoneIndex].name;
    public float ZoneBlend => _zoneBlend;

    static Color ComputeShadowColor(Color baseCol)
    {
        float h, s, v;
        Color.RGBToHSV(baseCol, out h, out s, out v);
        return Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.3f);
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _tc = Object.FindFirstObjectByType<TurdController>();
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
            if (l.type == LightType.Directional) { _mainLight = l; break; }

        CurrentPipeColor = zones[0].pipeColor;
        CurrentPipeEmission = zones[0].pipeEmission;
        CurrentWaterColor = zones[0].waterColor;
        CurrentWaterEmission = zones[0].waterEmission;
        CurrentPipeShadowColor = ComputeShadowColor(zones[0].pipeColor);
        CurrentWaterShadowColor = ComputeShadowColor(zones[0].waterColor);
        CurrentBumpScale = zones[0].bumpScale;
        CurrentEmissionBoost = zones[0].emissionBoost;
    }

    void Update()
    {
        if (_tc == null) return;
        float dist = _tc.DistanceTraveled;

        // Find which zone we're in
        int zoneIdx = 0;
        for (int i = zones.Length - 1; i >= 0; i--)
        {
            if (dist >= zones[i].startDistance) { zoneIdx = i; break; }
        }

        // Calculate blend to next zone (transitions over 30m)
        float transitionLength = 30f;
        float blend = 0f;
        if (zoneIdx < zones.Length - 1)
        {
            float nextStart = zones[zoneIdx + 1].startDistance;
            float blendStart = nextStart - transitionLength;
            if (dist > blendStart)
                blend = Mathf.Clamp01((dist - blendStart) / transitionLength);
        }

        bool zoneChanged = zoneIdx != _currentZoneIndex;
        _currentZoneIndex = zoneIdx;
        _zoneBlend = blend;

        // Interpolate between current zone and next
        ZoneData current = zones[zoneIdx];
        ZoneData next = (zoneIdx < zones.Length - 1) ? zones[zoneIdx + 1] : current;

        CurrentPipeColor = Color.Lerp(current.pipeColor, next.pipeColor, blend);
        CurrentPipeEmission = Color.Lerp(current.pipeEmission, next.pipeEmission, blend);
        CurrentWaterColor = Color.Lerp(current.waterColor, next.waterColor, blend);
        CurrentWaterEmission = Color.Lerp(current.waterEmission, next.waterEmission, blend);
        CurrentPipeShadowColor = Color.Lerp(ComputeShadowColor(current.pipeColor), ComputeShadowColor(next.pipeColor), blend);
        CurrentWaterShadowColor = Color.Lerp(ComputeShadowColor(current.waterColor), ComputeShadowColor(next.waterColor), blend);
        CurrentBumpScale = Mathf.Lerp(current.bumpScale, next.bumpScale, blend);
        CurrentEmissionBoost = Mathf.Lerp(current.emissionBoost, next.emissionBoost, blend);

        // Update fog
        Color fogCol = Color.Lerp(current.fogColor, next.fogColor, blend);
        float fogDensity = Mathf.Lerp(current.fogDensity, next.fogDensity, blend);
        RenderSettings.fogColor = fogCol;
        RenderSettings.fogDensity = fogDensity;

        // Update ambient
        Color ambient = Color.Lerp(current.ambientColor, next.ambientColor, blend);
        RenderSettings.ambientLight = ambient;

        // Update main light
        if (_mainLight != null)
        {
            _mainLight.color = Color.Lerp(current.lightColor, next.lightColor, blend);
            _mainLight.intensity = Mathf.Lerp(current.lightIntensity, next.lightIntensity, blend);
        }

        // Light flicker in dark zones (Rusty + Hellsewer) for aging infrastructure feel
        if (_mainLight != null && zoneIdx >= 3)
        {
            float flickerStrength = (zoneIdx == 4) ? 0.08f : 0.04f;
            float flicker = 1f + (Mathf.PerlinNoise(Time.time * 8f, 0f) - 0.5f) * flickerStrength
                              + (Mathf.PerlinNoise(Time.time * 23f, 5f) - 0.5f) * flickerStrength * 0.5f;
            _mainLight.intensity *= flicker;
        }

        // Zone vignette: subtle atmosphere tint at screen edges (stronger in deeper zones)
        if (ScreenEffects.Instance != null)
        {
            float vignetteStr = Mathf.Lerp(0f, 1f, zoneIdx / (float)(zones.Length - 1));
            ScreenEffects.Instance.UpdateZoneVignette(fogCol, vignetteStr);
        }

        // Zone trail color + intensity follows the current zone's light color
        if (ParticleManager.Instance != null)
        {
            Color trailCol = Color.Lerp(current.lightColor, next.lightColor, blend);
            ParticleManager.Instance.UpdateZoneTrailColor(trailCol);
            if (_tc != null)
                ParticleManager.Instance.UpdateZoneTrailIntensity(_tc.CurrentSpeed);
        }

        // Announce zone change with full fanfare
        if (zoneChanged)
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayZoneTransition();
            // Show zone name with sewer-themed subtitle
            if (ScorePopup.Instance != null && _tc != null)
            {
                string subtitle = ZoneSubtitle(zoneIdx);
                ScorePopup.Instance.ShowMilestone(
                    _tc.transform.position + Vector3.up * 2.5f,
                    zones[zoneIdx].name.ToUpper() + "\n" + subtitle);
            }
            // Cheer overlay for zone transitions
            if (CheerOverlay.Instance != null)
                CheerOverlay.Instance.ShowCheer(zones[zoneIdx].name.ToUpper(), zones[zoneIdx].lightColor, zoneIdx >= 3);
            // Camera rumble for zone transition (stronger in later zones)
            if (PipeCamera.Instance != null)
                PipeCamera.Instance.Shake(0.2f + zoneIdx * 0.05f);
            // Zone-colored screen flash instead of generic gold
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerZoneFlash(zones[zoneIdx].lightColor);
            HapticManager.MediumTap();
        }
    }

    static readonly string[][] ZoneSubtitles = {
        new[] { "\"Clean-ish\"", "\"It only goes down from here\"", "\"Don't touch the walls\"" },
        new[] { "\"What IS that smell?\"", "\"Pipes don't clean themselves\"", "\"Watch your step\"" },
        new[] { "\"Don't breathe\"", "\"Glow = bad\"", "\"EPA was never here\"" },
        new[] { "\"Industrial strength stink\"", "\"Tetanus not included\"", "\"Mind the rust\"" },
        new[] { "\"Abandon hope, all ye who flush\"", "\"The final frontier of sewage\"", "\"It's worse than it sounds\"" },
    };

    static string ZoneSubtitle(int zoneIdx)
    {
        var subs = ZoneSubtitles[Mathf.Clamp(zoneIdx, 0, ZoneSubtitles.Length - 1)];
        return subs[Random.Range(0, subs.Length)];
    }
}
