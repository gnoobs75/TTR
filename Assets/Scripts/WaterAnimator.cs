using UnityEngine;
using System.Collections;
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

    [Header("Floating Poop Buddies")]
    public int maxFloatingPoops = 8;
    public float poopSpawnInterval = 3f;
    public float poopDriftSpeed = 2f;
    public float poopLifetime = 20f;

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

    // Enhanced water interaction particles
    private ParticleSystem _entryPlumePS;   // big upward splash burst on entry
    private ParticleSystem _exitDripsPS;    // murky drips falling off poop on exit
    private ParticleSystem _rippleRingPS;   // expanding rings on water surface
    private ParticleSystem _bowWavePS;      // front-facing wedge pushing water aside
    private ParticleSystem _sludgeCurtainPS; // thick curtain of murk kicked up behind

    // Water interaction state
    private bool _wasInWaterLocal = false;
    private float _waterEntryTime = 0f;

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

    // Floating poop buddies
    struct FloatingPoop
    {
        public GameObject obj;
        public Transform leftEye;
        public Transform rightEye;
        public Transform mouth;
        public float pathDist;
        public float lateralOffset;
        public float spawnTime;
        public float bobPhase;      // random phase offset for bobbing
        public float wobbleSpeed;   // unique wobble speed
        public int faceType;        // 0=happy, 1=derp, 2=worried, 3=sleeping
    }
    private List<FloatingPoop> _floatingPoops = new List<FloatingPoop>();
    private float _poopSpawnTimer;
    private Material _poopBodyMat;
    private Material _poopEyeWhiteMat;
    private Material _poopPupilMat;

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

        // Enhanced water interaction effects
        CreateEntryPlume();
        CreateExitDrips();
        CreateRippleRing();
        CreateBowWave();
        CreateSludgeCurtain();

        // Floating poop buddy materials
        CreatePoopBuddyMaterials();
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
        UpdateFloatingPoops();
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

        // --- WATER ENTRY ---
        if (inWater && !_wasInWaterLocal)
        {
            _waterEntryTime = Time.time;
            Vector3 splashPos = player.position + Vector3.down * 0.05f;

            // Big upward plume of murky water
            if (_entryPlumePS != null)
            {
                _entryPlumePS.gameObject.SetActive(true);
                _entryPlumePS.transform.position = splashPos;
                _entryPlumePS.Emit(Random.Range(25, 40));
            }

            // Expanding ripple ring on surface
            if (_rippleRingPS != null)
            {
                _rippleRingPS.gameObject.SetActive(true);
                _rippleRingPS.transform.position = splashPos;
                _rippleRingPS.Emit(Random.Range(8, 14));
            }

            // Regular splash too
            if (_splashPS != null)
            {
                _splashPS.gameObject.SetActive(true);
                _splashPS.transform.position = splashPos;
                _splashPS.Emit(Random.Range(8, 15));
            }
        }

        // --- WATER EXIT ---
        if (!inWater && _wasInWaterLocal)
        {
            // Drips falling off the poop
            if (_exitDripsPS != null)
            {
                _exitDripsPS.gameObject.SetActive(true);
                _exitDripsPS.transform.SetParent(player);
                _exitDripsPS.transform.localPosition = Vector3.down * 0.15f;
                _exitDripsPS.Play();
                // Stop after a short time
                StartCoroutine(StopAfterDelay(_exitDripsPS, 1.5f));
            }

            // Exit ripple ring
            if (_rippleRingPS != null)
            {
                _rippleRingPS.gameObject.SetActive(true);
                _rippleRingPS.transform.position = player.position + Vector3.down * 0.1f;
                _rippleRingPS.Emit(Random.Range(5, 8));
            }
        }

        // --- WHILE IN WATER ---
        if (inWater)
        {
            // Wake behind player
            if (_wakePS != null)
            {
                if (!_wakePS.isPlaying)
                {
                    _wakePS.gameObject.SetActive(true);
                    _wakePS.Play();
                }
                _wakePS.transform.position = player.position + Vector3.down * 0.1f;
            }

            // Bow wave at front of player
            if (_bowWavePS != null)
            {
                if (!_bowWavePS.isPlaying)
                {
                    _bowWavePS.gameObject.SetActive(true);
                    _bowWavePS.Play();
                }
                _bowWavePS.transform.position = player.position + player.forward * 0.25f + Vector3.down * 0.08f;
                _bowWavePS.transform.rotation = Quaternion.LookRotation(player.forward, Vector3.up);
            }

            // Sludge curtain kicked up behind at higher speeds
            if (_sludgeCurtainPS != null && _tc.CurrentSpeed > 5f)
            {
                if (!_sludgeCurtainPS.isPlaying)
                {
                    _sludgeCurtainPS.gameObject.SetActive(true);
                    _sludgeCurtainPS.Play();
                }
                _sludgeCurtainPS.transform.position = player.position - player.forward * 0.3f + Vector3.down * 0.05f;
                _sludgeCurtainPS.transform.rotation = Quaternion.LookRotation(-player.forward, Vector3.up);

                // Scale emission rate with speed
                var em = _sludgeCurtainPS.emission;
                em.rateOverTime = Mathf.Lerp(8f, 30f, (_tc.CurrentSpeed - 5f) / 8f);
            }
            else if (_sludgeCurtainPS != null && _sludgeCurtainPS.isPlaying && _tc.CurrentSpeed <= 5f)
            {
                _sludgeCurtainPS.Stop();
            }

            // Periodic splashes while moving through water
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
            if (_wakePS != null && _wakePS.isPlaying) _wakePS.Stop();
            if (_bowWavePS != null && _bowWavePS.isPlaying) _bowWavePS.Stop();
            if (_sludgeCurtainPS != null && _sludgeCurtainPS.isPlaying) _sludgeCurtainPS.Stop();
        }

        _wasInWaterLocal = inWater;
    }

    System.Collections.IEnumerator StopAfterDelay(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ps != null)
        {
            ps.Stop();
            ps.transform.SetParent(transform);
        }
    }

    // === PARTICLE CREATION ===

    void CreateSplashParticles()
    {
        var go = new GameObject("WaterSplash");
        go.transform.SetParent(transform);
        _splashPS = go.AddComponent<ParticleSystem>();

        var main = _splashPS.main;
        main.maxParticles = 80;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.2f, 0.3f, 0.08f, 0.85f),
            new Color(0.12f, 0.2f, 0.04f, 0.6f));
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 2.0f; // heavy gravity for arcing droplets

        var emission = _splashPS.emission;
        emission.rateOverTime = 0;

        var shape = _splashPS.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.2f;

        var sizeOverLife = _splashPS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0f)));

        // Stretch along velocity so they look like water droplets, not circles
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.5f;
        renderer.velocityScale = 0.08f;
        renderer.material = MakeParticleMat(new Color(0.18f, 0.28f, 0.08f, 0.8f));

        go.SetActive(false);
    }

    void CreateWakeParticles()
    {
        var go = new GameObject("WaterWake");
        go.transform.SetParent(transform);
        _wakePS = go.AddComponent<ParticleSystem>();

        var main = _wakePS.main;
        main.maxParticles = 120;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.22f, 0.32f, 0.1f, 0.6f),
            new Color(0.15f, 0.25f, 0.06f, 0.35f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.15f; // slight gravity for settling

        var emission = _wakePS.emission;
        emission.rateOverTime = 35;

        // V-shaped wake spreading outward behind player
        var shape = _wakePS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f; // wider V-shape
        shape.radius = 0.15f;

        var sizeOverLife = _wakePS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.2f, 1f),
                new Keyframe(1f, 0f)));

        var colorOverLife = _wakePS.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.25f, 0.35f, 0.1f), 0f),
                new GradientColorKey(new Color(0.15f, 0.22f, 0.05f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.3f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.2f, 0.3f, 0.08f, 0.55f));

        go.SetActive(false);
    }

    // === ENHANCED WATER INTERACTION PARTICLES ===

    void CreateEntryPlume()
    {
        var go = new GameObject("EntryPlume");
        go.transform.SetParent(transform);
        _entryPlumePS = go.AddComponent<ParticleSystem>();

        var main = _entryPlumePS.main;
        main.maxParticles = 100;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f); // big upward burst
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.18f, 0.28f, 0.06f, 0.9f),  // thick murky green
            new Color(0.25f, 0.35f, 0.1f, 0.7f));
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 2.5f; // heavy - arcs up then comes crashing down

        var emission = _entryPlumePS.emission;
        emission.rateOverTime = 0;

        // Upward-facing hemisphere burst
        var shape = _entryPlumePS.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.25f;

        var sizeOverLife = _entryPlumePS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.15f, 1f),
                new Keyframe(0.6f, 0.7f),
                new Keyframe(1f, 0f)));

        // Stretch along velocity for droplet trails
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3f;
        renderer.velocityScale = 0.1f;
        renderer.material = MakeParticleMat(new Color(0.15f, 0.25f, 0.05f, 0.85f));

        go.SetActive(false);
    }

    void CreateExitDrips()
    {
        var go = new GameObject("ExitDrips");
        go.transform.SetParent(transform);
        _exitDripsPS = go.AddComponent<ParticleSystem>();

        var main = _exitDripsPS.main;
        main.maxParticles = 40;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.2f, 0.3f, 0.08f, 0.8f),
            new Color(0.15f, 0.22f, 0.05f, 0.5f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 3f; // heavy drips fall fast

        var emission = _exitDripsPS.emission;
        emission.rateOverTime = 20;

        // Emit from around the player shape
        var shape = _exitDripsPS.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var sizeOverLife = _exitDripsPS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 0.6f),
                new Keyframe(0.9f, 0.3f),
                new Keyframe(1f, 0f)));

        // Stretch to look like dripping strands
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 4f;
        renderer.velocityScale = 0.15f;
        renderer.material = MakeParticleMat(new Color(0.18f, 0.25f, 0.06f, 0.7f));

        go.SetActive(false);
    }

    void CreateRippleRing()
    {
        var go = new GameObject("RippleRing");
        go.transform.SetParent(transform);
        _rippleRingPS = go.AddComponent<ParticleSystem>();

        var main = _rippleRingPS.main;
        main.maxParticles = 30;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f); // expand outward
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.4f, 0.15f, 0.6f),
            new Color(0.4f, 0.5f, 0.2f, 0.4f));
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f; // stays on water surface

        var emission = _rippleRingPS.emission;
        emission.rateOverTime = 0;

        // Ring shape - particles expand outward from center
        var shape = _rippleRingPS.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        shape.arc = 360f;
        shape.radiusThickness = 0f; // emit from edge only = ring

        var sizeOverLife = _rippleRingPS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.3f, 1.2f),
                new Keyframe(1f, 0f)));

        var colorOverLife = _rippleRingPS.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.35f, 0.45f, 0.2f), 0f),
                new GradientColorKey(new Color(0.2f, 0.3f, 0.1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.4f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        // Stretch horizontally for ring-like appearance
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.5f;
        renderer.velocityScale = 0.2f;
        renderer.material = MakeParticleMat(new Color(0.3f, 0.4f, 0.15f, 0.5f));

        go.SetActive(false);
    }

    void CreateBowWave()
    {
        var go = new GameObject("BowWave");
        go.transform.SetParent(transform);
        _bowWavePS = go.AddComponent<ParticleSystem>();

        var main = _bowWavePS.main;
        main.maxParticles = 60;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.25f, 0.38f, 0.12f, 0.7f),
            new Color(0.18f, 0.28f, 0.08f, 0.45f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.8f;

        var emission = _bowWavePS.emission;
        emission.rateOverTime = 20;

        // Forward-facing wedge shape
        var shape = _bowWavePS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f; // wide V at front
        shape.radius = 0.08f;

        var sizeOverLife = _bowWavePS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.8f, 1f, 0f));

        var colorOverLife = _bowWavePS.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.28f, 0.4f, 0.15f), 0f),
                new GradientColorKey(new Color(0.15f, 0.22f, 0.06f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.22f, 0.35f, 0.1f, 0.6f));

        go.SetActive(false);
    }

    void CreateSludgeCurtain()
    {
        var go = new GameObject("SludgeCurtain");
        go.transform.SetParent(transform);
        _sludgeCurtainPS = go.AddComponent<ParticleSystem>();

        var main = _sludgeCurtainPS.main;
        main.maxParticles = 100;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.22f, 0.05f, 0.75f),  // thick dark sludge
            new Color(0.22f, 0.3f, 0.08f, 0.5f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f; // arcs up then falls back

        var emission = _sludgeCurtainPS.emission;
        emission.rateOverTime = 15; // dynamic - scaled with speed in HandleSplash

        // Fan out behind player
        var shape = _sludgeCurtainPS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.15f;

        var sizeOverLife = _sludgeCurtainPS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.7f, 0.8f),
                new Keyframe(1f, 0f)));

        var colorOverLife = _sludgeCurtainPS.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.18f, 0.25f, 0.06f), 0f),
                new GradientColorKey(new Color(0.1f, 0.15f, 0.03f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0.4f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        // Stretch for sludge spray look
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        renderer.velocityScale = 0.1f;
        renderer.material = MakeParticleMat(new Color(0.15f, 0.2f, 0.04f, 0.7f));

        go.SetActive(false);
    }

    // === FLOATING POOP BUDDIES ===

    void CreatePoopBuddyMaterials()
    {
        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader lit = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");

        _poopBodyMat = new Material(lit);
        _poopBodyMat.SetColor("_BaseColor", new Color(0.4f, 0.25f, 0.1f));
        _poopBodyMat.SetFloat("_Smoothness", 0.65f);
        _poopBodyMat.SetFloat("_Metallic", 0.05f);

        _poopEyeWhiteMat = new Material(lit);
        _poopEyeWhiteMat.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.92f));
        _poopEyeWhiteMat.SetFloat("_Smoothness", 0.8f);

        _poopPupilMat = new Material(lit);
        _poopPupilMat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
        _poopPupilMat.SetFloat("_Smoothness", 0.9f);
    }

    void UpdateFloatingPoops()
    {
        if (_tc == null || _pipeGen == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        float playerDist = _tc.DistanceTraveled;
        float pipeRadius = _pipeGen.pipeRadius;

        // Spawn new poop buddies
        _poopSpawnTimer -= Time.deltaTime;
        if (_poopSpawnTimer <= 0f && _floatingPoops.Count < maxFloatingPoops)
        {
            _poopSpawnTimer = poopSpawnInterval + Random.Range(-0.5f, 1f);
            SpawnFloatingPoop(playerDist);
        }

        // Update existing poop buddies
        for (int i = _floatingPoops.Count - 1; i >= 0; i--)
        {
            var fp = _floatingPoops[i];
            if (fp.obj == null) { _floatingPoops.RemoveAt(i); continue; }

            // Drift forward
            fp.pathDist += poopDriftSpeed * Time.deltaTime;

            // Get pipe position
            Vector3 center, forward, right, up;
            _pipeGen.GetPathFrame(fp.pathDist, out center, out forward, out right, out up);

            float waterHeight = -pipeRadius * 0.82f;
            Vector3 waterCenter = center + up * waterHeight;
            float waterWidth = pipeRadius * 0.65f;

            Vector3 pos = waterCenter + right * fp.lateralOffset * waterWidth;

            // Bob with waves + unique phase
            float wave = Mathf.Sin(pos.x * waveFrequency + _time * waveSpeed * 2f + fp.bobPhase) * waveAmplitude;
            wave += Mathf.Sin(pos.z * secondaryWaveFreq + _time * waveSpeed * 3.5f + fp.bobPhase) * secondaryWaveAmp;
            pos.y += wave + 0.04f; // float on surface

            fp.obj.transform.position = pos;

            // Organic wobble rotation - each poop wobbles differently
            float wobbleX = Mathf.Sin(_time * fp.wobbleSpeed + fp.bobPhase) * 8f;
            float wobbleZ = Mathf.Sin(_time * fp.wobbleSpeed * 0.7f + fp.bobPhase * 1.3f) * 6f;
            float driftYaw = Mathf.Sin(_time * 0.5f + fp.bobPhase) * 20f;
            fp.obj.transform.rotation = Quaternion.LookRotation(forward, Vector3.up)
                * Quaternion.Euler(wobbleX, driftYaw, wobbleZ);

            // Animate eyes - look around randomly, blink occasionally
            AnimatePoopFace(fp);

            _floatingPoops[i] = fp;

            // Cleanup
            if (Time.time - fp.spawnTime > poopLifetime || fp.pathDist < playerDist - 25f)
            {
                Destroy(fp.obj);
                _floatingPoops.RemoveAt(i);
            }
        }
    }

    void SpawnFloatingPoop(float playerDist)
    {
        float spawnDist = playerDist + Random.Range(20f, 45f);
        float lateral = Random.Range(-0.6f, 0.6f);
        int faceType = Random.Range(0, 4);

        GameObject poop = CreatePoopBuddyMesh(faceType);
        if (poop == null) return;
        poop.transform.SetParent(transform);

        // Find eye transforms
        Transform leftEye = poop.transform.Find("LeftEye");
        Transform rightEye = poop.transform.Find("RightEye");
        Transform mouth = poop.transform.Find("Mouth");

        _floatingPoops.Add(new FloatingPoop
        {
            obj = poop,
            leftEye = leftEye,
            rightEye = rightEye,
            mouth = mouth,
            pathDist = spawnDist,
            lateralOffset = lateral,
            spawnTime = Time.time,
            bobPhase = Random.Range(0f, Mathf.PI * 2f),
            wobbleSpeed = Random.Range(1.5f, 3f),
            faceType = faceType
        });
    }

    GameObject CreatePoopBuddyMesh(int faceType)
    {
        var root = new GameObject("PoopBuddy");

        // Poop body - stacked spheres like classic poop emoji
        float scale = Random.Range(0.06f, 0.1f);

        // Bottom blob (widest)
        var bottom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bottom.name = "Bottom";
        bottom.transform.SetParent(root.transform);
        bottom.transform.localPosition = Vector3.zero;
        bottom.transform.localScale = new Vector3(scale, scale * 0.55f, scale);
        bottom.GetComponent<Renderer>().material = _poopBodyMat;
        Object.Destroy(bottom.GetComponent<Collider>());

        // Middle blob
        var middle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        middle.name = "Middle";
        middle.transform.SetParent(root.transform);
        middle.transform.localPosition = new Vector3(scale * 0.05f, scale * 0.35f, 0);
        middle.transform.localScale = new Vector3(scale * 0.8f, scale * 0.45f, scale * 0.8f);
        middle.GetComponent<Renderer>().material = _poopBodyMat;
        Object.Destroy(middle.GetComponent<Collider>());

        // Top blob (smallest, offset)
        var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        top.name = "Top";
        top.transform.SetParent(root.transform);
        top.transform.localPosition = new Vector3(-scale * 0.05f, scale * 0.6f, 0);
        top.transform.localScale = new Vector3(scale * 0.55f, scale * 0.4f, scale * 0.55f);
        top.GetComponent<Renderer>().material = _poopBodyMat;
        Object.Destroy(top.GetComponent<Collider>());

        // Tip (curly)
        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Tip";
        tip.transform.SetParent(root.transform);
        tip.transform.localPosition = new Vector3(-scale * 0.12f, scale * 0.82f, scale * 0.02f);
        tip.transform.localScale = new Vector3(scale * 0.3f, scale * 0.25f, scale * 0.3f);
        tip.transform.localRotation = Quaternion.Euler(0, 0, 20f);
        tip.GetComponent<Renderer>().material = _poopBodyMat;
        Object.Destroy(tip.GetComponent<Collider>());

        // === EYES ===
        float eyeSize = scale * 0.22f;
        float eyeHeight = scale * 0.45f;
        float eyeSpread = scale * 0.22f;
        float eyeForward = scale * 0.38f;

        // Left eye
        var leftEyeObj = new GameObject("LeftEye");
        leftEyeObj.transform.SetParent(root.transform);
        leftEyeObj.transform.localPosition = new Vector3(-eyeSpread, eyeHeight, eyeForward);

        var leftWhite = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftWhite.name = "White";
        leftWhite.transform.SetParent(leftEyeObj.transform);
        leftWhite.transform.localPosition = Vector3.zero;
        leftWhite.transform.localScale = Vector3.one * eyeSize;
        leftWhite.GetComponent<Renderer>().material = _poopEyeWhiteMat;
        Object.Destroy(leftWhite.GetComponent<Collider>());

        var leftPupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftPupil.name = "Pupil";
        leftPupil.transform.SetParent(leftEyeObj.transform);
        leftPupil.transform.localPosition = new Vector3(0, 0, eyeSize * 0.35f);
        leftPupil.transform.localScale = Vector3.one * eyeSize * 0.55f;
        leftPupil.GetComponent<Renderer>().material = _poopPupilMat;
        Object.Destroy(leftPupil.GetComponent<Collider>());

        // Right eye
        var rightEyeObj = new GameObject("RightEye");
        rightEyeObj.transform.SetParent(root.transform);
        rightEyeObj.transform.localPosition = new Vector3(eyeSpread, eyeHeight, eyeForward);

        var rightWhite = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightWhite.name = "White";
        rightWhite.transform.SetParent(rightEyeObj.transform);
        rightWhite.transform.localPosition = Vector3.zero;
        rightWhite.transform.localScale = Vector3.one * eyeSize;
        rightWhite.GetComponent<Renderer>().material = _poopEyeWhiteMat;
        Object.Destroy(rightWhite.GetComponent<Collider>());

        var rightPupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightPupil.name = "Pupil";
        rightPupil.transform.SetParent(rightEyeObj.transform);
        rightPupil.transform.localPosition = new Vector3(0, 0, eyeSize * 0.35f);
        rightPupil.transform.localScale = Vector3.one * eyeSize * 0.55f;
        rightPupil.GetComponent<Renderer>().material = _poopPupilMat;
        Object.Destroy(rightPupil.GetComponent<Collider>());

        // Face type variations: adjust eye size/position
        switch (faceType)
        {
            case 1: // Derp - one eye bigger, cross-eyed
                leftWhite.transform.localScale = Vector3.one * eyeSize * 1.3f;
                leftPupil.transform.localPosition = new Vector3(eyeSize * 0.15f, -eyeSize * 0.1f, eyeSize * 0.4f);
                rightPupil.transform.localPosition = new Vector3(-eyeSize * 0.2f, eyeSize * 0.05f, eyeSize * 0.4f);
                break;
            case 2: // Worried - eyes slightly higher, tilted
                leftEyeObj.transform.localPosition += Vector3.up * scale * 0.05f;
                rightEyeObj.transform.localPosition += Vector3.up * scale * 0.05f;
                leftEyeObj.transform.localRotation = Quaternion.Euler(0, 0, 15f);
                rightEyeObj.transform.localRotation = Quaternion.Euler(0, 0, -15f);
                break;
            case 3: // Sleeping - squished eyes (nearly closed)
                leftWhite.transform.localScale = new Vector3(eyeSize, eyeSize * 0.3f, eyeSize);
                rightWhite.transform.localScale = new Vector3(eyeSize, eyeSize * 0.3f, eyeSize);
                leftPupil.transform.localScale = Vector3.one * eyeSize * 0.15f;
                rightPupil.transform.localScale = Vector3.one * eyeSize * 0.15f;
                break;
        }

        // === MOUTH ===
        var mouthObj = new GameObject("Mouth");
        mouthObj.transform.SetParent(root.transform);
        mouthObj.transform.localPosition = new Vector3(0, eyeHeight - scale * 0.15f, eyeForward + scale * 0.02f);

        var mouthMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mouthMesh.name = "MouthShape";
        mouthMesh.transform.SetParent(mouthObj.transform);

        switch (faceType)
        {
            case 0: // Happy - wide smile
                mouthMesh.transform.localScale = new Vector3(scale * 0.25f, scale * 0.04f, scale * 0.04f);
                mouthMesh.transform.localRotation = Quaternion.Euler(0, 0, 0);
                break;
            case 1: // Derp - open O mouth
                Object.Destroy(mouthMesh);
                var oMouth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                oMouth.name = "MouthShape";
                oMouth.transform.SetParent(mouthObj.transform);
                oMouth.transform.localPosition = Vector3.zero;
                oMouth.transform.localScale = new Vector3(scale * 0.1f, scale * 0.12f, scale * 0.06f);
                oMouth.GetComponent<Renderer>().material = _poopPupilMat;
                Object.Destroy(oMouth.GetComponent<Collider>());
                break;
            case 2: // Worried - wavy line
                mouthMesh.transform.localScale = new Vector3(scale * 0.2f, scale * 0.03f, scale * 0.03f);
                mouthMesh.transform.localRotation = Quaternion.Euler(0, 0, 8f);
                break;
            case 3: // Sleeping - tiny peaceful line
                mouthMesh.transform.localScale = new Vector3(scale * 0.12f, scale * 0.02f, scale * 0.03f);
                break;
        }

        if (mouthMesh != null)
        {
            var mouthRend = mouthMesh.GetComponent<Renderer>();
            if (mouthRend != null) mouthRend.material = _poopPupilMat;
            var mouthCol = mouthMesh.GetComponent<Collider>();
            if (mouthCol != null) Object.Destroy(mouthCol);
        }

        // Add pickup collider so player can collect this buddy
        var pickupCol = root.AddComponent<SphereCollider>();
        pickupCol.radius = scale * 2f; // generous pickup radius
        pickupCol.isTrigger = true;
        root.AddComponent<PoopBuddyPickup>();

        return root;
    }

    void AnimatePoopFace(FloatingPoop fp)
    {
        if (fp.leftEye == null || fp.rightEye == null) return;

        // Pupil look direction - slowly drift around
        float lookX = Mathf.Sin(_time * 1.2f + fp.bobPhase) * 0.002f;
        float lookY = Mathf.Sin(_time * 0.8f + fp.bobPhase * 1.5f) * 0.001f;
        Vector3 lookOffset = new Vector3(lookX, lookY, 0);

        // Blink every 3-6 seconds
        float blinkCycle = Mathf.Repeat(_time + fp.bobPhase * 2f, Random.Range(3f, 6f));
        float blinkScale = 1f;
        if (blinkCycle < 0.15f)
            blinkScale = Mathf.Abs(Mathf.Sin(blinkCycle / 0.15f * Mathf.PI));

        // Apply to pupils
        Transform leftPupil = fp.leftEye.Find("Pupil");
        Transform rightPupil = fp.rightEye.Find("Pupil");
        if (leftPupil != null)
        {
            Vector3 basePos = leftPupil.localPosition;
            basePos.x = lookOffset.x;
            basePos.y = lookOffset.y;
            leftPupil.localPosition = basePos;
        }
        if (rightPupil != null)
        {
            Vector3 basePos = rightPupil.localPosition;
            basePos.x = lookOffset.x;
            basePos.y = lookOffset.y;
            rightPupil.localPosition = basePos;
        }

        // Blink by squishing eye whites
        Transform leftWhite = fp.leftEye.Find("White");
        Transform rightWhite = fp.rightEye.Find("White");
        if (leftWhite != null && fp.faceType != 3) // sleeping don't blink
        {
            Vector3 s = leftWhite.localScale;
            s.y = s.x * blinkScale;
            leftWhite.localScale = s;
        }
        if (rightWhite != null && fp.faceType != 3)
        {
            Vector3 s = rightWhite.localScale;
            s.y = s.x * blinkScale;
            rightWhite.localScale = s;
        }
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

        // Enable alpha blending so particles fade properly instead of rendering as solid stars
        mat.SetFloat("_Surface", 1); // 0=Opaque, 1=Transparent
        mat.SetFloat("_Blend", 0);   // Alpha blend
        mat.SetFloat("_ZWrite", 0);
        mat.SetFloat("_Cull", 0);    // No culling
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        return mat;
    }
}
