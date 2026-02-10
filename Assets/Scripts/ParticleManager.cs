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
}
