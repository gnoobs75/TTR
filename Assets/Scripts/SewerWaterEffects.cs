using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns and manages dynamic sewer water effects along the pipe:
/// - Side drain pipes with rushing water pouring in
/// - Ceiling crack waterfalls with drip curtains
/// - Player interaction (screen splash when passing through waterfalls)
/// All effects are procedurally generated and positioned using PipeGenerator coordinates.
/// </summary>
public class SewerWaterEffects : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public PipeGenerator pipeGen;

    [Header("Drain Pipe Settings")]
    public float drainMinSpacing = 25f;
    public float drainMaxSpacing = 45f;
    public float drainSpawnAhead = 100f;

    [Header("Ceiling Waterfall Settings")]
    public float waterfallMinSpacing = 18f;
    public float waterfallMaxSpacing = 35f;
    public float waterfallSpawnAhead = 100f;
    public float gushChance = 0.4f;
    public float gushInterval = 4f;

    [Header("Performance")]
    public float cleanupDistance = 50f;

    [Header("Player Interaction")]
    public float waterfallCollisionRadius = 2f;

    private TurdController _tc;
    private float _nextDrainDist = 30f;
    private float _nextWaterfallDist = 20f;
    private float _pipeRadius;
    private bool _alternateWall = false;

    // Materials (shared)
    private Material _drainPipeMat;
    private Material _waterStreamMat;
    private Material _ceilingCrackMat;

    struct DrainPipeData
    {
        public GameObject root;
        public ParticleSystem waterStream;
        public ParticleSystem splashRing;
        public float spawnDist;
        public Vector3 position;
    }

    struct WaterfallData
    {
        public GameObject root;
        public ParticleSystem dripCurtain;
        public ParticleSystem splashPS;
        public float spawnDist;
        public Vector3 position;
        public Vector3 waterLevelPos;
        public float gushTimer;
        public bool hasGush;
    }

    private List<DrainPipeData> _drains = new List<DrainPipeData>();
    private List<WaterfallData> _waterfalls = new List<WaterfallData>();

    // Screen droplet effect
    private ParticleSystem _screenDropletPS;
    private float _lastScreenSplashTime;

    void Start()
    {
        if (player != null)
            _tc = player.GetComponent<TurdController>();
        if (pipeGen == null)
            pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (pipeGen != null)
            _pipeRadius = pipeGen.pipeRadius;

        CreateMaterials();
        CreateScreenDropletPS();
    }

    void Update()
    {
        if (_tc == null || pipeGen == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        float playerDist = _tc.DistanceTraveled;

        // Spawn drain pipes ahead
        while (_nextDrainDist < playerDist + drainSpawnAhead)
        {
            SpawnDrainPipe(_nextDrainDist);
            _nextDrainDist += Random.Range(drainMinSpacing, drainMaxSpacing);
        }

        // Spawn ceiling waterfalls ahead
        while (_nextWaterfallDist < playerDist + waterfallSpawnAhead)
        {
            SpawnCeilingWaterfall(_nextWaterfallDist);
            _nextWaterfallDist += Random.Range(waterfallMinSpacing, waterfallMaxSpacing);
        }

        // Update waterfall gush timers
        UpdateWaterfallGushes();

        // Check player-waterfall collision
        CheckWaterfallCollision(playerDist);

        // Cleanup behind player
        CleanupBehind(playerDist);
    }

    void CreateMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        // Rusty drain pipe
        _drainPipeMat = new Material(urpLit);
        _drainPipeMat.SetColor("_BaseColor", new Color(0.3f, 0.28f, 0.22f));
        _drainPipeMat.SetFloat("_Metallic", 0.6f);
        _drainPipeMat.SetFloat("_Smoothness", 0.3f);

        // Glowing water stream
        _waterStreamMat = new Material(urpLit);
        _waterStreamMat.SetColor("_BaseColor", new Color(0.15f, 0.25f, 0.06f));
        _waterStreamMat.SetFloat("_Metallic", 0.4f);
        _waterStreamMat.SetFloat("_Smoothness", 0.9f);
        _waterStreamMat.EnableKeyword("_EMISSION");
        _waterStreamMat.SetColor("_EmissionColor", new Color(0.05f, 0.1f, 0.02f));

        // Ceiling crack
        _ceilingCrackMat = new Material(urpLit);
        _ceilingCrackMat.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.08f));
        _ceilingCrackMat.SetFloat("_Metallic", 0.1f);
        _ceilingCrackMat.SetFloat("_Smoothness", 0.2f);
    }

    // === DRAIN PIPES ===

    void SpawnDrainPipe(float dist)
    {
        Vector3 center, forward, right, up;
        pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Alternate left/right walls
        float angle;
        if (_alternateWall)
            angle = Random.Range(330f, 390f); // left wall area
        else
            angle = Random.Range(150f, 210f); // right wall area
        _alternateWall = !_alternateWall;

        float rad = angle * Mathf.Deg2Rad;
        float spawnRadius = _pipeRadius * 0.95f;
        Vector3 wallPos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * spawnRadius;
        Vector3 inward = (center - wallPos).normalized;

        // Water level position (where stream hits the main water)
        float waterHeight = -_pipeRadius * 0.82f;
        Vector3 waterLevelPos = center + up * waterHeight;

        // Create root object
        GameObject root = new GameObject("DrainPipe");
        root.transform.position = wallPos;
        root.transform.rotation = Quaternion.LookRotation(inward, up);
        root.transform.SetParent(transform);

        // Pipe mesh (cylinder pointing inward from wall)
        GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.name = "Pipe";
        pipe.transform.SetParent(root.transform);
        pipe.transform.localPosition = new Vector3(0, 0, -0.3f);
        pipe.transform.localRotation = Quaternion.Euler(90, 0, 0);
        pipe.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
        pipe.GetComponent<Renderer>().material = _drainPipeMat;
        Object.Destroy(pipe.GetComponent<Collider>());

        // Pipe flange at wall connection
        GameObject flange = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flange.name = "Flange";
        flange.transform.SetParent(root.transform);
        flange.transform.localPosition = Vector3.zero;
        flange.transform.localRotation = Quaternion.Euler(90, 0, 0);
        flange.transform.localScale = new Vector3(0.7f, 0.06f, 0.7f);
        flange.GetComponent<Renderer>().material = _drainPipeMat;
        Object.Destroy(flange.GetComponent<Collider>());

        // Rust stain running down from pipe
        GameObject stain = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stain.name = "RustStain";
        stain.transform.SetParent(root.transform);
        stain.transform.localPosition = new Vector3(0, -0.4f, 0.02f);
        stain.transform.localScale = new Vector3(0.3f, 0.6f, 0.02f);
        stain.GetComponent<Renderer>().material = _waterStreamMat;
        Object.Destroy(stain.GetComponent<Collider>());

        // Rushing water particle stream
        ParticleSystem waterPS = CreateRushingWaterPS(root.transform);
        // Position at pipe opening, shoot downward toward water level
        waterPS.transform.position = wallPos + inward * 0.1f;
        Vector3 toWater = (waterLevelPos - waterPS.transform.position).normalized;
        waterPS.transform.rotation = Quaternion.LookRotation(toWater);

        // Splash ring at water level
        ParticleSystem splashPS = CreateSplashRingPS(root.transform);
        splashPS.transform.position = waterLevelPos + (wallPos - center).normalized * 0.5f;

        _drains.Add(new DrainPipeData
        {
            root = root,
            waterStream = waterPS,
            splashRing = splashPS,
            spawnDist = dist,
            position = wallPos
        });
    }

    ParticleSystem CreateRushingWaterPS(Transform parent)
    {
        var go = new GameObject("RushingWater");
        go.transform.SetParent(parent);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 200;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.28f, 0.06f, 0.85f),
            new Color(0.25f, 0.35f, 0.1f, 0.65f));
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.2f;

        var emission = ps.emission;
        emission.rateOverTime = 80;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.12f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.2f, 0.32f, 0.08f), 0f),
                new GradientColorKey(new Color(0.15f, 0.25f, 0.05f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.3f, 1f)
            });
        colorOverLife.color = grad;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 2f;
        noise.scrollSpeed = 1f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.18f, 0.3f, 0.08f, 0.8f));

        return ps;
    }

    ParticleSystem CreateSplashRingPS(Transform parent)
    {
        var go = new GameObject("SplashRing");
        go.transform.SetParent(parent);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 60;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.25f, 0.35f, 0.12f, 0.7f),
            new Color(0.35f, 0.4f, 0.15f, 0.5f));
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;

        var emission = ps.emission;
        emission.rateOverTime = 25;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;
        shape.arc = 360f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.2f, 0.3f, 0.1f, 0.6f));

        return ps;
    }

    // === CEILING WATERFALLS ===

    void SpawnCeilingWaterfall(float dist)
    {
        Vector3 center, forward, right, up;
        pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Position on ceiling (angle 60-120)
        float angle = Random.Range(60f, 120f);
        float rad = angle * Mathf.Deg2Rad;
        float spawnRadius = _pipeRadius * 0.92f;
        Vector3 ceilingPos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * spawnRadius;
        Vector3 inward = (center - ceilingPos).normalized;

        // Water level
        float waterHeight = -_pipeRadius * 0.82f;
        Vector3 waterLevelPos = center + up * waterHeight;
        // Offset slightly toward where the drip falls
        Vector3 dripLandPos = waterLevelPos + (ceilingPos - center).normalized * 0.3f;

        // Root object
        GameObject root = new GameObject("CeilingWaterfall");
        root.transform.position = ceilingPos;
        root.transform.rotation = Quaternion.LookRotation(forward, -inward);
        root.transform.SetParent(transform);

        // Crack/pipe mesh at ceiling
        GameObject crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crack.name = "CeilingCrack";
        crack.transform.SetParent(root.transform);
        crack.transform.localPosition = Vector3.zero;
        crack.transform.localScale = new Vector3(
            Random.Range(0.15f, 0.4f),
            0.03f,
            Random.Range(0.3f, 0.8f));
        crack.GetComponent<Renderer>().material = _ceilingCrackMat;
        Object.Destroy(crack.GetComponent<Collider>());

        // Small pipe stub (50% chance)
        if (Random.value > 0.5f)
        {
            GameObject stub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stub.name = "PipeStub";
            stub.transform.SetParent(root.transform);
            stub.transform.localPosition = new Vector3(0, 0.1f, 0);
            stub.transform.localScale = new Vector3(0.15f, 0.12f, 0.15f);
            stub.GetComponent<Renderer>().material = _drainPipeMat;
            Object.Destroy(stub.GetComponent<Collider>());
        }

        // Drip curtain particles (falling from ceiling to water)
        ParticleSystem dripPS = CreateDripCurtainPS(root.transform, ceilingPos, dripLandPos);

        // Splash at water level
        ParticleSystem splashPS = CreateWaterfallSplashPS(root.transform);
        splashPS.transform.position = dripLandPos;

        // Water stain running down from crack
        GameObject stain = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stain.name = "WaterStain";
        stain.transform.SetParent(root.transform);
        stain.transform.localPosition = new Vector3(0, 0.15f, 0);
        stain.transform.localScale = new Vector3(0.08f, 0.3f, 0.02f);
        stain.GetComponent<Renderer>().material = _waterStreamMat;
        Object.Destroy(stain.GetComponent<Collider>());

        bool hasGush = Random.value < gushChance;

        _waterfalls.Add(new WaterfallData
        {
            root = root,
            dripCurtain = dripPS,
            splashPS = splashPS,
            spawnDist = dist,
            position = ceilingPos,
            waterLevelPos = dripLandPos,
            gushTimer = hasGush ? Random.Range(1f, gushInterval) : -1f,
            hasGush = hasGush
        });
    }

    ParticleSystem CreateDripCurtainPS(Transform parent, Vector3 from, Vector3 to)
    {
        var go = new GameObject("DripCurtain");
        go.transform.SetParent(parent);
        go.transform.position = from;
        var ps = go.AddComponent<ParticleSystem>();

        float fallDist = Vector3.Distance(from, to);
        float fallTime = Mathf.Sqrt(2f * fallDist / 9.8f); // approximate

        var main = ps.main;
        main.maxParticles = 150;
        main.startLifetime = new ParticleSystem.MinMaxCurve(fallTime * 0.8f, fallTime * 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.12f, 0.22f, 0.05f, 0.8f),
            new Color(0.2f, 0.3f, 0.08f, 0.6f));
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f;

        var emission = ps.emission;
        emission.rateOverTime = 35;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.2f, 0.02f, 0.4f);

        // Stretch particles for rain/drip look
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        renderer.velocityScale = 0.1f;
        renderer.material = MakeParticleMat(new Color(0.15f, 0.25f, 0.07f, 0.7f));

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.5f));

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.15f, 0.25f, 0.06f), 0f),
                new GradientColorKey(new Color(0.2f, 0.3f, 0.08f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.4f, 1f)
            });
        colorOverLife.color = grad;

        return ps;
    }

    ParticleSystem CreateWaterfallSplashPS(Transform parent)
    {
        var go = new GameObject("WaterfallSplash");
        go.transform.SetParent(parent);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 50;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.2f, 0.3f, 0.1f, 0.7f),
            new Color(0.3f, 0.38f, 0.15f, 0.5f));
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.8f;

        var emission = ps.emission;
        emission.rateOverTime = 15;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.15f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.2f, 0.3f, 0.1f, 0.6f));

        return ps;
    }

    // === GUSH SYSTEM ===

    void UpdateWaterfallGushes()
    {
        for (int i = 0; i < _waterfalls.Count; i++)
        {
            var wf = _waterfalls[i];
            if (!wf.hasGush || wf.dripCurtain == null) continue;

            wf.gushTimer -= Time.deltaTime;
            if (wf.gushTimer <= 0f)
            {
                // GUSH! Burst of extra water
                wf.dripCurtain.Emit(40);
                if (wf.splashPS != null)
                    wf.splashPS.Emit(20);

                if (ProceduralAudio.Instance != null)
                    ProceduralAudio.Instance.PlayWaterGush();

                wf.gushTimer = gushInterval + Random.Range(-1f, 2f);
            }
            _waterfalls[i] = wf;
        }
    }

    // === PLAYER INTERACTION ===

    void CheckWaterfallCollision(float playerDist)
    {
        if (player == null) return;
        Vector3 playerPos = player.position;

        for (int i = 0; i < _waterfalls.Count; i++)
        {
            var wf = _waterfalls[i];
            if (wf.root == null) continue;

            // Check if player is near the waterfall column
            // Use X/Z distance (ignore vertical) to the drip landing point
            Vector3 toWf = wf.waterLevelPos - playerPos;
            float horizDist = new Vector2(toWf.x, toWf.z).magnitude;

            if (horizDist < waterfallCollisionRadius)
            {
                TriggerScreenSplash(playerPos);
                break;
            }
        }
    }

    void TriggerScreenSplash(Vector3 playerPos)
    {
        if (Time.time - _lastScreenSplashTime < 1f) return;
        _lastScreenSplashTime = Time.time;

        if (_screenDropletPS != null)
        {
            _screenDropletPS.transform.position = playerPos + player.forward * 0.5f + Vector3.up * 0.3f;
            _screenDropletPS.Emit(12);
        }

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayWaterfallSplash();

        if (PipeCamera.Instance != null)
            PipeCamera.Instance.Shake(0.15f);

        HapticManager.LightTap();
    }

    void CreateScreenDropletPS()
    {
        var go = new GameObject("ScreenDroplets");
        go.transform.SetParent(transform);
        _screenDropletPS = go.AddComponent<ParticleSystem>();

        var main = _screenDropletPS.main;
        main.maxParticles = 30;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.25f, 0.06f, 0.6f),
            new Color(0.25f, 0.35f, 0.1f, 0.4f));
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f;

        var emission = _screenDropletPS.emission;
        emission.rateOverTime = 0;

        var shape = _screenDropletPS.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var sizeOverLife = _screenDropletPS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = MakeParticleMat(new Color(0.2f, 0.3f, 0.08f, 0.5f));

        go.SetActive(true); // keep active, we emit manually
    }

    // === CLEANUP ===

    void CleanupBehind(float playerDist)
    {
        // Cleanup drain pipes
        for (int i = _drains.Count - 1; i >= 0; i--)
        {
            if (_drains[i].root == null)
            {
                _drains.RemoveAt(i);
                continue;
            }
            Vector3 toObj = _drains[i].root.transform.position - player.position;
            if (toObj.magnitude > cleanupDistance && Vector3.Dot(toObj, player.forward) < 0)
            {
                Destroy(_drains[i].root);
                _drains.RemoveAt(i);
            }
        }

        // Cleanup waterfalls
        for (int i = _waterfalls.Count - 1; i >= 0; i--)
        {
            if (_waterfalls[i].root == null)
            {
                _waterfalls.RemoveAt(i);
                continue;
            }
            Vector3 toObj = _waterfalls[i].root.transform.position - player.position;
            if (toObj.magnitude > cleanupDistance && Vector3.Dot(toObj, player.forward) < 0)
            {
                Destroy(_waterfalls[i].root);
                _waterfalls.RemoveAt(i);
            }
        }
    }

    // === UTILITY ===

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
