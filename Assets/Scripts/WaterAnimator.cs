using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Animates sewer water meshes with vertex wave displacement, material UV offset,
/// splash particles, floating debris, surface foam, and bubbling.
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

    [Header("Floating Debris")]
    public int maxDebris = 25;
    public float debrisSpawnInterval = 0.6f;
    public float debrisDriftSpeed = 1.5f;
    public float debrisLifetime = 15f;

    [Header("Surface Effects")]
    public int foamRate = 12;
    public int bubbleRate = 5;
    public int currentLineRate = 8;

    [Header("Player")]
    public Transform player;

    private TurdController _tc;
    private PipeGenerator _pipeGen;
    private float _time;
    private float _lastSplashTime;
    private float _scanTimer;
    private float _debrisSpawnTimer;

    // Cached water mesh data
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
    private ParticleSystem _foamPS;
    private ParticleSystem _bubblePS;
    private ParticleSystem _currentLinePS;

    // Floating debris
    struct DebrisObj
    {
        public GameObject obj;
        public float pathDist;
        public float lateralOffset; // -1 to 1 across water width
        public float spawnTime;
        public int type; // 0=TP, 1=lump, 2=foam, 3=duck
    }
    private List<DebrisObj> _debris = new List<DebrisObj>();

    // Shared debris materials
    private Material _tpMat;
    private Material _lumpMat;
    private Material _foamClusterMat;
    private Material _duckMat;

    void Start()
    {
        if (player != null) _tc = player.GetComponent<TurdController>();
        _pipeGen = GetComponent<PipeGenerator>();
        if (_pipeGen == null) _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();

        CreateDebrisMaterials();
        CreateSplashParticles();
        CreateWakeParticles();
        CreateFoamParticles();
        CreateBubbleParticles();
        CreateCurrentLineParticles();
    }

    void Update()
    {
        _time += Time.deltaTime;

        // Periodically scan for new water meshes (pipe segments spawn at runtime)
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = 1f;
            ScanForWaterMeshes();
        }

        AnimateWaterMeshes();
        HandleSplash();
        UpdateDebris();
        UpdateSurfaceEffects();
    }

    // === WATER MESH ANIMATION ===

    void ScanForWaterMeshes()
    {
        for (int i = _waterMeshes.Count - 1; i >= 0; i--)
        {
            if (_waterMeshes[i].filter == null)
                _waterMeshes.RemoveAt(i);
        }

        var allFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        foreach (var mf in allFilters)
        {
            if (mf.gameObject.name != "SewerWater") continue;
            if (mf.sharedMesh == null || mf.mesh == null) continue;

            bool found = false;
            for (int i = 0; i < _waterMeshes.Count; i++)
            {
                if (_waterMeshes[i].filter == mf) { found = true; break; }
            }
            if (found) continue;

            Mesh mesh = mf.mesh;
            Vector3[] origVerts = mesh.vertices;
            Vector2[] origUVs = mesh.uv;
            if (origVerts == null || origVerts.Length == 0) continue;
            if (origUVs == null || origUVs.Length != origVerts.Length) continue;

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
                // Turbulence wave - irregular chop for sludge feel
                float wave4 = Mathf.Sin(worldPos.x * 11f + _time * 4.5f)
                              * Mathf.Cos(worldPos.z * 9f + _time * 3.2f) * 0.012f;

                displaced[i] = verts[i];
                displaced[i].y += wave1 + wave2 + wave3 + wave4;

                // UV scroll with slight lateral distortion for current feel
                scrolledUVs[i] = origUVs[i];
                scrolledUVs[i].y += _time * uvScrollSpeed;
                scrolledUVs[i].x += Mathf.Sin(_time * 1.5f + origUVs[i].y * 3f) * 0.015f;
            }

            mesh.vertices = displaced;
            mesh.uv = scrolledUVs;
            mesh.RecalculateNormals();

            // Enhanced shimmer - pulsing green with occasional bright flickers
            var mr = data.filter.GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
            {
                float shimmer = 0.06f + Mathf.Sin(_time * 2.5f) * 0.025f;
                // Occasional bright flicker (sludge bubble pop glow)
                float flicker = Mathf.Max(0, Mathf.Sin(_time * 17f) - 0.92f) * 5f * 0.04f;
                shimmer += flicker;
                mr.material.SetColor("_EmissionColor",
                    new Color(shimmer, shimmer * 1.6f, shimmer * 0.5f));
            }
        }
    }

    // === FLOATING DEBRIS ===

    void CreateDebrisMaterials()
    {
        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader urpLit = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        // Toilet paper - dirty white
        _tpMat = new Material(urpLit);
        _tpMat.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.7f));
        _tpMat.SetFloat("_Smoothness", 0.1f);

        // Mysterious brown lumps
        _lumpMat = new Material(urpLit);
        _lumpMat.SetColor("_BaseColor", new Color(0.35f, 0.22f, 0.1f));
        _lumpMat.SetFloat("_Smoothness", 0.6f);
        _lumpMat.SetFloat("_Metallic", 0.1f);

        // Foam cluster - yellowy white
        _foamClusterMat = new Material(urpLit);
        _foamClusterMat.SetColor("_BaseColor", new Color(0.9f, 0.88f, 0.75f));
        _foamClusterMat.SetFloat("_Smoothness", 0.3f);
        _foamClusterMat.EnableKeyword("_EMISSION");
        _foamClusterMat.SetColor("_EmissionColor", new Color(0.05f, 0.05f, 0.03f));

        // Rubber duck - bright yellow (rare!)
        _duckMat = new Material(urpLit);
        _duckMat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.1f));
        _duckMat.SetFloat("_Smoothness", 0.7f);
        _duckMat.EnableKeyword("_EMISSION");
        _duckMat.SetColor("_EmissionColor", new Color(0.15f, 0.12f, 0f));
    }

    void UpdateDebris()
    {
        if (_tc == null || _pipeGen == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        float playerDist = _tc.DistanceTraveled;

        // Spawn new debris
        _debrisSpawnTimer -= Time.deltaTime;
        if (_debrisSpawnTimer <= 0f && _debris.Count < maxDebris)
        {
            _debrisSpawnTimer = debrisSpawnInterval;
            SpawnDebris(playerDist);
        }

        // Update existing debris
        float pipeRadius = _pipeGen.pipeRadius;
        for (int i = _debris.Count - 1; i >= 0; i--)
        {
            var d = _debris[i];

            if (d.obj == null)
            {
                _debris.RemoveAt(i);
                continue;
            }

            // Drift forward with current
            d.pathDist += debrisDriftSpeed * Time.deltaTime;

            // Get position on pipe path
            Vector3 center, forward, right, up;
            _pipeGen.GetPathFrame(d.pathDist, out center, out forward, out right, out up);

            float waterHeight = -pipeRadius * 0.82f;
            Vector3 waterCenter = center + up * waterHeight;
            float waterWidth = pipeRadius * 0.75f;

            Vector3 pos = waterCenter + right * d.lateralOffset * waterWidth;

            // Bob with waves
            Vector3 worldPos = pos;
            float wave = Mathf.Sin(worldPos.x * waveFrequency + _time * waveSpeed * 2f) * waveAmplitude;
            wave += Mathf.Sin(worldPos.z * secondaryWaveFreq + _time * waveSpeed * 3.5f) * secondaryWaveAmp;
            pos.y += wave + 0.03f; // float slightly above water surface

            d.obj.transform.position = pos;

            // Gentle rotation drift
            float rotSpeed = 15f + d.lateralOffset * 10f;
            d.obj.transform.Rotate(Vector3.up * rotSpeed * Time.deltaTime);
            // Tilt with wave
            float tilt = Mathf.Sin(_time * 2f + d.lateralOffset * 5f) * 5f;
            d.obj.transform.rotation = Quaternion.LookRotation(forward, Vector3.up)
                * Quaternion.Euler(tilt, d.obj.transform.eulerAngles.y, tilt * 0.5f);

            _debris[i] = d;

            // Cleanup: too old or too far behind
            if (Time.time - d.spawnTime > debrisLifetime ||
                d.pathDist < playerDist - 30f)
            {
                Destroy(d.obj);
                _debris.RemoveAt(i);
            }
        }
    }

    void SpawnDebris(float playerDist)
    {
        // Spawn ahead of player
        float spawnDist = playerDist + Random.Range(15f, 40f);
        float lateral = Random.Range(-0.8f, 0.8f);

        // Pick type: 55% TP, 25% lump, 15% foam, 5% duck
        float roll = Random.value;
        int type;
        if (roll < 0.55f) type = 0;
        else if (roll < 0.80f) type = 1;
        else if (roll < 0.95f) type = 2;
        else type = 3;

        GameObject obj = CreateDebrisMesh(type);
        if (obj == null) return;
        obj.transform.SetParent(transform);

        _debris.Add(new DebrisObj
        {
            obj = obj,
            pathDist = spawnDist,
            lateralOffset = lateral,
            spawnTime = Time.time,
            type = type
        });
    }

    GameObject CreateDebrisMesh(int type)
    {
        GameObject obj;
        switch (type)
        {
            case 0: // Toilet paper scrap - thin flat quad
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "TPScrap";
                obj.transform.localScale = new Vector3(
                    Random.Range(0.06f, 0.12f),
                    0.005f,
                    Random.Range(0.08f, 0.15f));
                obj.GetComponent<Renderer>().material = _tpMat;
                break;

            case 1: // Brown lump
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.name = "Lump";
                float lumpSize = Random.Range(0.03f, 0.07f);
                obj.transform.localScale = new Vector3(
                    lumpSize * Random.Range(0.8f, 1.5f),
                    lumpSize * Random.Range(0.5f, 0.8f),
                    lumpSize * Random.Range(0.8f, 1.5f));
                obj.GetComponent<Renderer>().material = _lumpMat;
                break;

            case 2: // Foam cluster - group of tiny spheres (just one flattened sphere)
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.name = "FoamCluster";
                float foamSize = Random.Range(0.05f, 0.1f);
                obj.transform.localScale = new Vector3(foamSize, foamSize * 0.3f, foamSize);
                obj.GetComponent<Renderer>().material = _foamClusterMat;
                break;

            case 3: // Rubber duck! (sphere + tiny cube beak)
                obj = new GameObject("RubberDuck");
                var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Body";
                body.transform.SetParent(obj.transform);
                body.transform.localPosition = Vector3.zero;
                body.transform.localScale = new Vector3(0.06f, 0.05f, 0.06f);
                body.GetComponent<Renderer>().material = _duckMat;
                Object.Destroy(body.GetComponent<Collider>());

                // Head
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(obj.transform);
                head.transform.localPosition = new Vector3(0, 0.035f, 0.025f);
                head.transform.localScale = Vector3.one * 0.035f;
                head.GetComponent<Renderer>().material = _duckMat;
                Object.Destroy(head.GetComponent<Collider>());

                // Beak
                var beak = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beak.name = "Beak";
                beak.transform.SetParent(obj.transform);
                beak.transform.localPosition = new Vector3(0, 0.03f, 0.05f);
                beak.transform.localScale = new Vector3(0.015f, 0.008f, 0.02f);
                Shader toonLit2 = Shader.Find("Custom/ToonLit");
                Shader urpLit = toonLit2 != null ? toonLit2 : Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit == null) urpLit = Shader.Find("Standard");
                Material beakMat = new Material(urpLit);
                beakMat.SetColor("_BaseColor", new Color(1f, 0.5f, 0f));
                beak.GetComponent<Renderer>().material = beakMat;
                Object.Destroy(beak.GetComponent<Collider>());
                break;

            default:
                return null;
        }

        // Remove colliders from non-duck types
        if (type != 3)
        {
            var col = obj.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        return obj;
    }

    // === SURFACE EFFECT PARTICLES ===

    void UpdateSurfaceEffects()
    {
        if (_tc == null || _pipeGen == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        float playerDist = _tc.DistanceTraveled;
        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(playerDist, out center, out forward, out right, out up);

        float pipeRadius = _pipeGen.pipeRadius;
        float waterHeight = -pipeRadius * 0.82f;
        Vector3 waterCenter = center + up * waterHeight;

        // Foam follows player along water edges
        if (_foamPS != null)
        {
            _foamPS.transform.position = waterCenter + forward * 3f;
            _foamPS.transform.rotation = Quaternion.LookRotation(forward, up);
            if (!_foamPS.isPlaying)
            {
                _foamPS.gameObject.SetActive(true);
                _foamPS.Play();
            }
        }

        // Bubbles pop up from water ahead of player
        if (_bubblePS != null)
        {
            _bubblePS.transform.position = waterCenter + forward * 5f;
            if (!_bubblePS.isPlaying)
            {
                _bubblePS.gameObject.SetActive(true);
                _bubblePS.Play();
            }
        }

        // Current lines - stretched particles moving along water flow
        if (_currentLinePS != null)
        {
            _currentLinePS.transform.position = waterCenter + forward * 2f;
            _currentLinePS.transform.rotation = Quaternion.LookRotation(forward, up);
            if (!_currentLinePS.isPlaying)
            {
                _currentLinePS.gameObject.SetActive(true);
                _currentLinePS.Play();
            }
        }
    }

    void CreateFoamParticles()
    {
        var go = new GameObject("SurfaceFoam");
        go.transform.SetParent(transform);
        _foamPS = go.AddComponent<ParticleSystem>();

        var main = _foamPS.main;
        main.maxParticles = 150;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.82f, 0.7f, 0.5f),
            new Color(0.75f, 0.7f, 0.55f, 0.3f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = _foamPS.emission;
        emission.rateOverTime = foamRate;

        // Spawn across water width at edges
        var shape = _foamPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(5f, 0.01f, 8f);

        var sizeOverLife = _foamPS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.3f, 1f, 0f));

        var colorOverLife = _foamPS.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.85f, 0.82f, 0.7f), 0f),
                new GradientColorKey(new Color(0.6f, 0.55f, 0.4f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.5f, 0.2f),
                new GradientAlphaKey(0.4f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.85f, 0.82f, 0.7f, 0.5f));

        go.SetActive(false);
    }

    void CreateBubbleParticles()
    {
        var go = new GameObject("Bubbles");
        go.transform.SetParent(transform);
        _bubblePS = go.AddComponent<ParticleSystem>();

        var main = _bubblePS.main;
        main.maxParticles = 60;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.4f, 0.2f, 0.5f),
            new Color(0.5f, 0.55f, 0.35f, 0.3f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f; // rise UP

        var emission = _bubblePS.emission;
        emission.rateOverTime = bubbleRate;

        var shape = _bubblePS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(4f, 0.05f, 6f);

        var sizeOverLife = _bubblePS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.7f, 1f),
                new Keyframe(0.9f, 1.3f),
                new Keyframe(1f, 0f))); // pop!

        var colorOverLife = _bubblePS.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.3f, 0.4f, 0.2f), 0f),
                new GradientColorKey(new Color(0.5f, 0.55f, 0.35f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.4f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.4f, 0.45f, 0.3f, 0.4f));

        go.SetActive(false);
    }

    void CreateCurrentLineParticles()
    {
        var go = new GameObject("CurrentLines");
        go.transform.SetParent(transform);
        _currentLinePS = go.AddComponent<ParticleSystem>();

        var main = _currentLinePS.main;
        main.maxParticles = 100;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.5f, 0.25f, 0.35f),
            new Color(0.55f, 0.6f, 0.35f, 0.2f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = _currentLinePS.emission;
        emission.rateOverTime = currentLineRate;

        var shape = _currentLinePS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(4f, 0.02f, 0.5f);

        // Stretch along velocity for flow lines
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3f;
        renderer.velocityScale = 0.2f;
        renderer.material = MakeParticleMat(new Color(0.45f, 0.55f, 0.3f, 0.3f));

        var colorOverLife = _currentLinePS.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.4f, 0.5f, 0.25f), 0f),
                new GradientColorKey(new Color(0.3f, 0.4f, 0.2f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.35f, 0.15f),
                new GradientAlphaKey(0.3f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        go.SetActive(false);
    }

    // === PLAYER SPLASH ===

    void HandleSplash()
    {
        if (_tc == null || player == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        float angleDelta = Mathf.Abs(Mathf.DeltaAngle(_tc.CurrentAngle, 270f));
        bool inWater = angleDelta < splashAngleThreshold;

        if (inWater)
        {
            if (_wakePS != null)
            {
                if (!_wakePS.isPlaying)
                {
                    _wakePS.gameObject.SetActive(true);
                    _wakePS.Play();
                }
                _wakePS.transform.position = player.position + Vector3.down * 0.1f;
            }

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

    // === PARTICLE CREATION ===

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
