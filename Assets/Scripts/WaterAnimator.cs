using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Animates sewer water meshes with vertex wave displacement, material UV offset,
/// and splash particles when the player rides through the water.
/// Attaches to the PipeGenerator GameObject. Discovers water meshes as they spawn.
/// </summary>
public class WaterAnimator : MonoBehaviour
{
    [Header("Wave Settings")]
    public float waveSpeed = 1.2f;
    public float waveAmplitude = 0.06f;
    public float waveFrequency = 3f;
    public float secondaryWaveFreq = 7f;
    public float secondaryWaveAmp = 0.02f;

    [Header("UV Scroll")]
    public float uvScrollSpeed = 0.4f;

    [Header("Splash")]
    public float splashAngleThreshold = 30f;
    public float splashCooldown = 0.15f;

    [Header("Player")]
    public Transform player;

    private TurdController _tc;
    private float _time;
    private float _lastSplashTime;
    private float _scanTimer;

    // Cached water mesh data: stores original positions so we can displace cleanly
    struct WaterMeshData
    {
        public MeshFilter filter;
        public Vector3[] originalVerts;
        public Vector2[] originalUVs;
    }
    private List<WaterMeshData> _waterMeshes = new List<WaterMeshData>();

    // Particle systems
    private ParticleSystem _splashPS;
    private ParticleSystem _wakePS;

    void Start()
    {
        if (player != null) _tc = player.GetComponent<TurdController>();
        CreateSplashParticles();
        CreateWakeParticles();
    }

    void Update()
    {
        _time += Time.deltaTime;

        // Periodically scan for new water meshes (pipe segments spawn at runtime)
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = 1f; // scan every second
            ScanForWaterMeshes();
        }

        AnimateWaterMeshes();
        HandleSplash();
    }

    void ScanForWaterMeshes()
    {
        // Remove destroyed entries
        for (int i = _waterMeshes.Count - 1; i >= 0; i--)
        {
            if (_waterMeshes[i].filter == null)
                _waterMeshes.RemoveAt(i);
        }

        // Find any new water meshes
        var allFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        foreach (var mf in allFilters)
        {
            if (mf.gameObject.name != "SewerWater") continue;
            if (mf.sharedMesh == null && mf.mesh == null) continue;

            // Already tracked?
            bool found = false;
            for (int i = 0; i < _waterMeshes.Count; i++)
            {
                if (_waterMeshes[i].filter == mf) { found = true; break; }
            }
            if (found) continue;

            // Cache original vertex positions and UVs
            Mesh mesh = mf.mesh; // this creates instance if shared
            Vector3[] origVerts = mesh.vertices;
            Vector2[] origUVs = mesh.uv;
            if (origVerts == null || origVerts.Length == 0) continue;

            _waterMeshes.Add(new WaterMeshData
            {
                filter = mf,
                originalVerts = (Vector3[])origVerts.Clone(),
                originalUVs = (Vector2[])origUVs.Clone()
            });
        }
    }

    void AnimateWaterMeshes()
    {
        for (int w = 0; w < _waterMeshes.Count; w++)
        {
            var data = _waterMeshes[w];
            if (data.filter == null) continue;

            Mesh mesh = data.filter.mesh;
            Vector3[] verts = data.originalVerts;
            Vector2[] origUVs = data.originalUVs;
            int count = verts.Length;

            Vector3[] displaced = new Vector3[count];
            Vector2[] scrolledUVs = new Vector2[count];
            Transform tf = data.filter.transform;

            for (int i = 0; i < count; i++)
            {
                // Get approximate world position for wave calculation
                Vector3 worldPos = tf.TransformPoint(verts[i]);

                // Primary wave - big slow swell
                float wave1 = Mathf.Sin(worldPos.x * waveFrequency + _time * waveSpeed * 2f)
                              * waveAmplitude;
                // Secondary wave - choppier
                float wave2 = Mathf.Sin(worldPos.z * secondaryWaveFreq + _time * waveSpeed * 3.5f)
                              * secondaryWaveAmp;
                // Cross wave - diagonal ripple
                float wave3 = Mathf.Sin((worldPos.x + worldPos.z) * waveFrequency * 0.7f
                              + _time * waveSpeed * 1.3f) * secondaryWaveAmp * 0.7f;

                // Apply displacement to original position (no accumulation)
                displaced[i] = verts[i];
                // Displace along the mesh's local up direction
                // Water mesh normals point up toward pipe center, so local Y is "up" for the water
                displaced[i].y += wave1 + wave2 + wave3;

                // UV scroll - offset from original UVs (no drift)
                scrolledUVs[i] = origUVs[i];
                scrolledUVs[i].y += _time * uvScrollSpeed;
            }

            mesh.vertices = displaced;
            mesh.uv = scrolledUVs;
            mesh.RecalculateNormals();

            // Shimmer the material emission
            var mr = data.filter.GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
            {
                float shimmer = 0.06f + Mathf.Sin(_time * 2.5f) * 0.025f;
                mr.material.SetColor("_EmissionColor",
                    new Color(shimmer, shimmer * 1.6f, shimmer * 0.5f));
            }
        }
    }

    void HandleSplash()
    {
        if (_tc == null || player == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        float angleDelta = Mathf.Abs(Mathf.DeltaAngle(_tc.CurrentAngle, 270f));
        bool inWater = angleDelta < splashAngleThreshold;

        if (inWater)
        {
            // Continuous wake trail
            if (_wakePS != null)
            {
                if (!_wakePS.isPlaying)
                {
                    _wakePS.gameObject.SetActive(true);
                    _wakePS.Play();
                }
                _wakePS.transform.position = player.position + Vector3.down * 0.1f;
            }

            // Periodic splash bursts based on speed
            if (_tc.CurrentSpeed > 4f && Time.time - _lastSplashTime > splashCooldown)
            {
                _lastSplashTime = Time.time;
                if (_splashPS != null)
                {
                    _splashPS.gameObject.SetActive(true);
                    _splashPS.transform.position = player.position + Vector3.down * 0.05f;
                    _splashPS.Emit(Random.Range(2, 5));
                }
            }
        }
        else
        {
            if (_wakePS != null && _wakePS.isPlaying)
                _wakePS.Stop();
        }
    }

    void CreateSplashParticles()
    {
        var go = new GameObject("WaterSplash");
        go.transform.SetParent(transform);
        _splashPS = go.AddComponent<ParticleSystem>();

        var main = _splashPS.main;
        main.maxParticles = 50;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.25f, 0.35f, 0.12f, 0.8f),
            new Color(0.15f, 0.25f, 0.05f, 0.6f));
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f;

        var emission = _splashPS.emission;
        emission.rateOverTime = 0;

        var shape = _splashPS.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.15f;

        var sizeOverLife = _splashPS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.2f, 0.3f, 0.1f, 0.7f));

        go.SetActive(false);
    }

    void CreateWakeParticles()
    {
        var go = new GameObject("WaterWake");
        go.transform.SetParent(transform);
        _wakePS = go.AddComponent<ParticleSystem>();

        var main = _wakePS.main;
        main.maxParticles = 80;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.4f, 0.15f, 0.5f),
            new Color(0.2f, 0.3f, 0.08f, 0.3f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;

        var emission = _wakePS.emission;
        emission.rateOverTime = 25;

        var shape = _wakePS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.2f;

        var sizeOverLife = _wakePS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        var colorOverLife = _wakePS.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.3f, 0.4f, 0.15f), 0f),
                new GradientColorKey(new Color(0.2f, 0.3f, 0.08f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.25f, 0.35f, 0.1f, 0.5f));

        go.SetActive(false);
    }

    Material MakeParticleMat(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        return mat;
    }
}
