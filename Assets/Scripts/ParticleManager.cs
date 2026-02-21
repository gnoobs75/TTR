using UnityEngine;

/// <summary>
/// Creates and manages particle effects for game events.
/// Uses pooled ParticleSystems - one per effect type, repositioned and replayed.
/// </summary>
public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance { get; private set; }

    private ParticleSystem _coinBurst;
    private ParticleSystem _nearMissStreak;
    private ParticleSystem _boostTrail;
    private ParticleSystem _hitExplosion;
    private ParticleSystem _comboFlash;

    // Per-obstacle hit effects
    private ParticleSystem _barrelSplash;
    private ParticleSystem _blobSquish;
    private ParticleSystem _mineExplosion;

    // New feature effects
    private ParticleSystem _dustMotes;
    private ParticleSystem _sewerBubbles;
    private ParticleSystem _celebration;
    private ParticleSystem _speedLines;
    private ParticleSystem _coinMagnetTrail;
    private ParticleSystem _waterSplash;
    private ParticleSystem _wakeSpray;

    // New creature hit effects
    private ParticleSystem _frogSplat;
    private ParticleSystem _jellyZap;
    private ParticleSystem _spiderWeb;
    private ParticleSystem _stompSquash;

    // Underwater dive effects (continuous while submerged)
    private ParticleSystem _underwaterBubbles;
    private ParticleSystem _underwaterDebris;

    // Signature poop effects
    private ParticleSystem _stinkCloud;
    private ParticleSystem _sewerFlies;

    // Zone-colored ambient motion trail
    private ParticleSystem _zoneTrail;
    private Color _zoneTrailTargetColor = new Color(0.6f, 0.55f, 0.45f, 0.5f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        _coinBurst = CreateBurst("CoinBurst",
            new Color(1f, 0.85f, 0.1f), new Color(1f, 0.6f, 0f),
            15, 3f, 0.6f, 0.2f);

        _nearMissStreak = CreateBurst("NearMissStreak",
            new Color(0.3f, 1f, 0.9f), new Color(0.6f, 0.9f, 1f),
            20, 4f, 0.5f, 0.15f);

        _hitExplosion = CreateBurst("HitExplosion",
            new Color(1f, 0.3f, 0.1f), new Color(0.4f, 0.1f, 0f),
            30, 5f, 0.8f, 0.3f);

        _comboFlash = CreateBurst("ComboFlash",
            new Color(1f, 1f, 0.3f), new Color(1f, 0.5f, 1f),
            25, 6f, 0.4f, 0.12f);

        _boostTrail = CreateTrail("BoostTrail",
            new Color(0f, 0.9f, 1f, 0.8f), new Color(0f, 0.4f, 1f, 0f),
            40, 3f, 0.8f, 0.1f);

        // Per-obstacle hit particles
        _barrelSplash = CreateBurst("BarrelSplash",
            new Color(0.2f, 0.8f, 0.1f, 0.8f), new Color(0.1f, 0.5f, 0f, 0.5f),
            25, 4f, 0.7f, 0.15f);

        _blobSquish = CreateBurst("BlobSquish",
            new Color(0.4f, 0.25f, 0.1f, 0.9f), new Color(0.3f, 0.15f, 0.05f, 0.6f),
            20, 3f, 0.8f, 0.2f);

        _mineExplosion = CreateBurst("MineExplosion",
            new Color(0.9f, 0.5f, 0.1f), new Color(0.3f, 0.3f, 0.3f),
            35, 7f, 0.6f, 0.25f);

        // === NEW FEATURE EFFECTS ===

        // Atmospheric dust motes - always active, follows camera
        _dustMotes = CreateDustMotes();

        // Sewer gas bubbles rising from water
        _sewerBubbles = CreateSewerBubbles();

        // Distance milestone celebration - confetti burst
        _celebration = CreateBurst("Celebration",
            new Color(1f, 0.9f, 0.2f), new Color(0.9f, 0.3f, 1f),
            50, 8f, 1.5f, 0.15f);

        // Speed lines - radial streaks during boost
        _speedLines = CreateSpeedLines();

        // Coin magnet sparkle trail
        _coinMagnetTrail = CreateTrail("CoinMagnetTrail",
            new Color(1f, 0.85f, 0.1f, 0.9f), new Color(1f, 0.6f, 0f, 0f),
            30, 2f, 0.5f, 0.06f);

        // Water splash burst
        _waterSplash = CreateBurst("WaterSplash",
            new Color(0.3f, 0.5f, 0.2f, 0.7f), new Color(0.15f, 0.35f, 0.1f, 0.3f),
            20, 4f, 0.6f, 0.12f);

        // Wake spray - V-shaped behind player in water
        _wakeSpray = CreateTrail("WakeSpray",
            new Color(0.25f, 0.4f, 0.15f, 0.6f), new Color(0.2f, 0.35f, 0.1f, 0f),
            25, 2f, 0.7f, 0.08f);

        // === NEW CREATURE HIT EFFECTS ===

        // Frog splat: green toxic goop burst
        _frogSplat = CreateBurst("FrogSplat",
            new Color(0.1f, 0.8f, 0.2f, 0.9f), new Color(0.3f, 0.6f, 0.05f, 0.5f),
            20, 3.5f, 0.7f, 0.18f);

        // Jellyfish zap: electric cyan/white sparks
        _jellyZap = CreateBurst("JellyZap",
            new Color(0.2f, 1f, 0.9f, 0.9f), new Color(0.8f, 1f, 1f, 0.5f),
            30, 6f, 0.4f, 0.08f);

        // Spider web: white stringy burst
        _spiderWeb = CreateBurst("SpiderWeb",
            new Color(0.9f, 0.9f, 0.9f, 0.8f), new Color(0.6f, 0.6f, 0.65f, 0.4f),
            15, 2.5f, 1.0f, 0.12f);

        // Stomp squash: satisfying poof
        _stompSquash = CreateBurst("StompSquash",
            new Color(1f, 0.9f, 0.3f, 0.9f), new Color(1f, 0.5f, 0.1f, 0.4f),
            25, 5f, 0.5f, 0.15f);

        // === UNDERWATER DIVE EFFECTS ===
        _underwaterBubbles = CreateUnderwaterBubbles();
        _underwaterDebris = CreateUnderwaterDebris();

        // === SIGNATURE POOP EFFECTS ===
        _stinkCloud = CreateStinkCloud();
        _sewerFlies = CreateSewerFlies();

        // Zone-colored ambient motion trail (subtle whispy trail behind player)
        _zoneTrail = CreateZoneTrail();
    }

    ParticleSystem CreateDustMotes()
    {
        var go = new GameObject("DustMotes");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 80;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 0.55f, 0.4f, 0.3f),
            new Color(0.4f, 0.5f, 0.3f, 0.15f));
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f; // slowly float upward

        var emission = ps.emission;
        emission.rateOverTime = 8;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(6f, 4f, 10f); // big box around player

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(new Color(0.6f, 0.55f, 0.4f, 0.3f));

        go.SetActive(true); // always on
        return ps;
    }

    ParticleSystem CreateSewerBubbles()
    {
        var go = new GameObject("SewerBubbles");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 30;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.2f, 0.5f, 0.15f, 0.5f),
            new Color(0.3f, 0.6f, 0.2f, 0.3f));
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f; // float upward

        var emission = ps.emission;
        emission.rateOverTime = 4;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(5f, 0.3f, 8f);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.8f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(new Color(0.3f, 0.6f, 0.2f, 0.4f));

        go.SetActive(true);
        return ps;
    }

    ParticleSystem CreateSpeedLines()
    {
        var go = new GameObject("SpeedLines");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 60;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 0.4f),
            new Color(0.6f, 0.8f, 1f, 0.2f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = 50;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 3f;
        shape.radius = 2f;

        // Stretch particles along velocity for speed line look
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(new Color(1f, 1f, 1f, 0.3f));
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5f;
        renderer.velocityScale = 0.1f;

        go.SetActive(false);
        return ps;
    }

    ParticleSystem CreateBurst(string name, Color colorA, Color colorB,
        int count, float speed, float lifetime, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = count * 2;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(colorA);

        go.SetActive(false);
        return ps;
    }

    ParticleSystem CreateTrail(string name, Color colorA, Color colorB,
        int ratePerSec, float speed, float lifetime, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 200;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = ratePerSec;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.1f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(colorA, 0f), new GradientColorKey(colorB, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(colorA);

        go.SetActive(false);
        return ps;
    }

    Material GetParticleMaterial(Color color)
    {
        // Try URP particles shader first
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color); // fallback property name

        // Enable alpha blending so particles fade properly instead of rendering as solid squares
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

    void PlayAt(ParticleSystem ps, Vector3 position)
    {
        if (ps == null) return;
        ps.gameObject.SetActive(true);
        ps.transform.position = position;
        ps.Clear();
        ps.Play();
    }

    public void PlayCoinCollect(Vector3 position)
    {
        PlayAt(_coinBurst, position);
    }

    public void PlayNearMiss(Vector3 position)
    {
        PlayAt(_nearMissStreak, position);
    }

    public void PlayHitExplosion(Vector3 position)
    {
        PlayAt(_hitExplosion, position);
    }

    public void PlayComboFlash(Vector3 position)
    {
        PlayAt(_comboFlash, position);
    }

    public void StartBoostTrail(Transform follow)
    {
        if (_boostTrail == null) return;
        _boostTrail.gameObject.SetActive(true);
        _boostTrail.transform.SetParent(follow);
        _boostTrail.transform.localPosition = Vector3.back * 0.3f;
        _boostTrail.Play();
    }

    public void StopBoostTrail()
    {
        if (_boostTrail == null) return;
        _boostTrail.Stop();
        _boostTrail.transform.SetParent(transform);
    }

    // Per-obstacle hit VFX
    public void PlayBarrelSplash(Vector3 position) => PlayAt(_barrelSplash, position);
    public void PlayBlobSquish(Vector3 position) => PlayAt(_blobSquish, position);
    public void PlayMineExplosion(Vector3 position) => PlayAt(_mineExplosion, position);

    // === NEW FEATURE VFX ===

    public void PlayCelebration(Vector3 position) => PlayAt(_celebration, position);
    public void PlayWaterSplash(Vector3 position) => PlayAt(_waterSplash, position);

    public void UpdateDustMotes(Vector3 playerPos)
    {
        if (_dustMotes != null)
            _dustMotes.transform.position = playerPos;
    }

    public void UpdateSewerBubbles(Vector3 waterPos)
    {
        if (_sewerBubbles != null)
            _sewerBubbles.transform.position = waterPos;
    }

    public void StartSpeedLines(Transform cam)
    {
        if (_speedLines == null) return;
        _speedLines.gameObject.SetActive(true);
        _speedLines.transform.SetParent(cam);
        _speedLines.transform.localPosition = Vector3.forward * 3f;
        _speedLines.transform.localRotation = Quaternion.Euler(0, 180, 0);
        _speedLines.Play();
    }

    public void StopSpeedLines()
    {
        if (_speedLines == null) return;
        _speedLines.Stop();
        _speedLines.transform.SetParent(transform);
    }

    public void StartWakeSpray(Transform player)
    {
        if (_wakeSpray == null) return;
        _wakeSpray.gameObject.SetActive(true);
        _wakeSpray.transform.SetParent(player);
        _wakeSpray.transform.localPosition = Vector3.back * 0.4f + Vector3.down * 0.1f;
        _wakeSpray.Play();
    }

    public void StopWakeSpray()
    {
        if (_wakeSpray == null) return;
        _wakeSpray.Stop();
        _wakeSpray.transform.SetParent(transform);
    }

    public void StartCoinMagnet(Transform player)
    {
        if (_coinMagnetTrail == null) return;
        _coinMagnetTrail.gameObject.SetActive(true);
        _coinMagnetTrail.transform.SetParent(player);
        _coinMagnetTrail.transform.localPosition = Vector3.zero;
        _coinMagnetTrail.Play();
    }

    public void StopCoinMagnet()
    {
        if (_coinMagnetTrail == null) return;
        _coinMagnetTrail.Stop();
        _coinMagnetTrail.transform.SetParent(transform);
    }

    // === NEW CREATURE HIT VFX ===
    public void PlayFrogSplat(Vector3 position) => PlayAt(_frogSplat, position);
    public void PlayJellyZap(Vector3 position) => PlayAt(_jellyZap, position);
    public void PlaySpiderWeb(Vector3 position) => PlayAt(_spiderWeb, position);
    public void PlayStompSquash(Vector3 position) => PlayAt(_stompSquash, position);

    // === UNDERWATER DIVE VFX ===

    ParticleSystem CreateUnderwaterBubbles()
    {
        var go = new GameObject("UW_Bubbles");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 120;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 0.8f, 0.6f, 0.5f),
            new Color(0.7f, 0.95f, 0.8f, 0.3f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.4f; // bubbles float upward

        var emission = ps.emission;
        emission.rateOverTime = 25;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.5f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f)); // grow slightly as they rise

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.5f, 0.8f, 0.6f), 0f),
                new GradientColorKey(new Color(0.7f, 0.95f, 0.8f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.4f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(new Color(0.6f, 0.9f, 0.7f, 0.4f));

        go.SetActive(false);
        return ps;
    }

    ParticleSystem CreateUnderwaterDebris()
    {
        var go = new GameObject("UW_Debris");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 80;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.25f, 0.12f, 0.6f),  // murky brown
            new Color(0.2f, 0.4f, 0.15f, 0.4f));    // sickly green
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f; // barely float
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var emission = ps.emission;
        emission.rateOverTime = 15;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(4f, 4f, 6f); // spread around player

        // Gentle random drift
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.2f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var rotOverLife = ps.rotationOverLifetime;
        rotOverLife.enabled = true;
        rotOverLife.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.8f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(new Color(0.3f, 0.25f, 0.12f, 0.5f));

        go.SetActive(false);
        return ps;
    }

    public void StartUnderwaterEffects(Transform player)
    {
        if (_underwaterBubbles != null)
        {
            _underwaterBubbles.gameObject.SetActive(true);
            _underwaterBubbles.transform.SetParent(player);
            _underwaterBubbles.transform.localPosition = Vector3.zero;
            _underwaterBubbles.Play();
        }
        if (_underwaterDebris != null)
        {
            _underwaterDebris.gameObject.SetActive(true);
            _underwaterDebris.transform.SetParent(player);
            _underwaterDebris.transform.localPosition = Vector3.zero;
            _underwaterDebris.Play();
        }
    }

    public void StopUnderwaterEffects()
    {
        if (_underwaterBubbles != null)
        {
            _underwaterBubbles.Stop();
            _underwaterBubbles.transform.SetParent(transform);
        }
        if (_underwaterDebris != null)
        {
            _underwaterDebris.Stop();
            _underwaterDebris.transform.SetParent(transform);
        }
    }

    // === SIGNATURE POOP EFFECTS ===

    ParticleSystem CreateStinkCloud()
    {
        var go = new GameObject("StinkCloud");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 60;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.55f, 0.1f, 0.25f),   // sickly yellow-green
            new Color(0.5f, 0.6f, 0.15f, 0.12f));    // lighter pea-soup green
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.08f; // stink rises
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var emission = ps.emission;
        emission.rateOverTime = 12;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        // Grow then fade
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.3f, 1.0f),
                new Keyframe(1f, 0.6f)));

        // Fade out over lifetime
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.4f, 0.55f, 0.12f), 0f),
                new GradientColorKey(new Color(0.5f, 0.6f, 0.18f), 0.4f),
                new GradientColorKey(new Color(0.35f, 0.45f, 0.1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.0f, 0f),
                new GradientAlphaKey(0.25f, 0.15f),
                new GradientAlphaKey(0.2f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        // Gentle swirl
        var rotOverLife = ps.rotationOverLifetime;
        rotOverLife.enabled = true;
        rotOverLife.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(new Color(0.4f, 0.55f, 0.12f, 0.2f));

        go.SetActive(false);
        return ps;
    }

    ParticleSystem CreateSewerFlies()
    {
        var go = new GameObject("SewerFlies");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 20;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
        main.startColor = new Color(0.05f, 0.05f, 0.02f, 0.9f); // tiny dark specks
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 3;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 3f; // around the player area

        // Erratic buzzing motion
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        noise.frequency = 4f;
        noise.scrollSpeed = 2f;
        noise.octaveCount = 2;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(new Color(0.05f, 0.05f, 0.02f, 0.9f));

        go.SetActive(false);
        return ps;
    }

    /// <summary>Start the stink cloud trail behind the turd.</summary>
    public void StartStinkCloud(Transform player)
    {
        if (_stinkCloud == null) return;
        _stinkCloud.gameObject.SetActive(true);
        _stinkCloud.transform.SetParent(player);
        _stinkCloud.transform.localPosition = Vector3.back * 0.4f;
        _stinkCloud.Play();
    }

    /// <summary>Update stink intensity based on speed (more speed = more stink).</summary>
    public void UpdateStinkIntensity(float speed)
    {
        if (_stinkCloud == null) return;
        var emission = _stinkCloud.emission;
        float intensity = Mathf.Lerp(5f, 25f, Mathf.Clamp01((speed - 4f) / 10f));
        emission.rateOverTime = intensity;
    }

    /// <summary>Start ambient sewer flies around the player.</summary>
    public void StartSewerFlies(Transform player)
    {
        if (_sewerFlies == null) return;
        _sewerFlies.gameObject.SetActive(true);
        _sewerFlies.transform.SetParent(player);
        _sewerFlies.transform.localPosition = Vector3.zero;
        _sewerFlies.Play();
    }

    public void StopStinkCloud()
    {
        if (_stinkCloud == null) return;
        _stinkCloud.Stop();
        _stinkCloud.transform.SetParent(transform);
    }

    // === ZONE TRAIL ===

    ParticleSystem CreateZoneTrail()
    {
        var go = new GameObject("ZoneTrail");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 120;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new Color(0.6f, 0.55f, 0.45f, 0.5f);
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 20;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.15f;

        // Fade and shrink over lifetime
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.8f, 1f, 0f));

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[] {
                new GradientAlphaKey(0.4f, 0f),
                new GradientAlphaKey(0.2f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        // Gentle drift via noise
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 0.5f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial(new Color(0.6f, 0.55f, 0.45f, 0.5f));

        go.SetActive(false);
        return ps;
    }

    /// <summary>Start the zone-colored ambient trail behind the player.</summary>
    public void StartZoneTrail(Transform player)
    {
        if (_zoneTrail == null) return;
        _zoneTrail.gameObject.SetActive(true);
        _zoneTrail.transform.SetParent(player);
        _zoneTrail.transform.localPosition = Vector3.back * 0.5f;
        _zoneTrail.Play();
    }

    /// <summary>Update zone trail color based on current pipe zone.</summary>
    public void UpdateZoneTrailColor(Color zoneColor)
    {
        if (_zoneTrail == null) return;

        // Smooth color transition
        _zoneTrailTargetColor = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.5f);
        var main = _zoneTrail.main;
        Color current = main.startColor.color;
        main.startColor = Color.Lerp(current, _zoneTrailTargetColor, Time.deltaTime * 2f);

        // Update material emission for a subtle glow
        var renderer = _zoneTrail.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.SetColor("_BaseColor", _zoneTrailTargetColor);
            if (renderer.material.HasProperty("_EmissionColor"))
                renderer.material.SetColor("_EmissionColor", zoneColor * 0.3f);
        }
    }

    /// <summary>Adjust zone trail intensity based on speed.</summary>
    public void UpdateZoneTrailIntensity(float speed)
    {
        if (_zoneTrail == null) return;
        var emission = _zoneTrail.emission;
        // More particles at higher speed, subtle at low speed
        float intensity = Mathf.Lerp(8f, 35f, Mathf.Clamp01((speed - 4f) / 10f));
        emission.rateOverTime = intensity;

        // Bigger particles at higher speed
        var main = _zoneTrail.main;
        float sizeScale = Mathf.Lerp(0.06f, 0.15f, Mathf.Clamp01((speed - 4f) / 10f));
        main.startSize = new ParticleSystem.MinMaxCurve(sizeScale * 0.5f, sizeScale);
    }
}
