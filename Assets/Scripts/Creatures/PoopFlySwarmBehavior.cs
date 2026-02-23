using UnityEngine;

/// <summary>
/// Swarm of 8 poop flies that orbit randomly in a loose cloud.
/// Tightens formation and buzzes louder when player approaches.
/// Zones: Rusty + Hellsewer.
/// </summary>
public class PoopFlySwarmBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.3f, 0.25f, 0.1f); // muddy brown
    public override bool SplatterOnHit => false;

    [Header("Swarm")]
    public float orbitRadius = 0.6f;
    public float orbitSpeed = 2f;
    public float tightenSpeed = 3f;

    private Vector3 _baseScale;
    private Transform[] _flies;
    private Vector3[] _flyOffsets;
    private float[] _flyPhases;
    private float _tightenProgress;
    private bool _reactBuzzed;

    protected override void Start()
    {
        base.Start();
        _baseScale = transform.localScale;

        // Find fly children
        var flyList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
            if (child.name.Contains("Fly")) flyList.Add(child);
        _flies = flyList.ToArray();

        // Initialize random orbit parameters per fly
        _flyOffsets = new Vector3[_flies.Length];
        _flyPhases = new float[_flies.Length];
        for (int i = 0; i < _flies.Length; i++)
        {
            _flyOffsets[i] = Random.insideUnitSphere * orbitRadius;
            _flyPhases[i] = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Each fly orbits independently in a lazy pattern
        for (int i = 0; i < _flies.Length; i++)
        {
            if (_flies[i] == null) continue;
            float phase = t * orbitSpeed + _flyPhases[i];

            // Lissajous orbit pattern for organic movement
            float x = Mathf.Sin(phase * 1.0f) * orbitRadius + _flyOffsets[i].x * 0.3f;
            float y = Mathf.Sin(phase * 1.3f + i * 0.7f) * orbitRadius * 0.6f + _flyOffsets[i].y * 0.3f;
            float z = Mathf.Cos(phase * 0.8f + i * 1.1f) * orbitRadius + _flyOffsets[i].z * 0.3f;

            _flies[i].localPosition = new Vector3(x, y, z);

            // Buzz wing animation (rapid Y-axis oscillation on the fly scale)
            float wingBeat = 1f + Mathf.Sin(t * 30f + i * 5f) * 0.15f;
            _flies[i].localScale = new Vector3(wingBeat, 1f, 1f);

            // Face movement direction
            Vector3 vel = new Vector3(
                Mathf.Cos(phase * 1.0f) * orbitSpeed,
                Mathf.Cos(phase * 1.3f) * orbitSpeed * 0.6f,
                -Mathf.Sin(phase * 0.8f) * orbitSpeed);
            if (vel.sqrMagnitude > 0.01f)
                _flies[i].localRotation = Quaternion.LookRotation(vel.normalized);
        }

        // Gentle swarm breathing
        float breath = CreatureAnimUtils.BreathingScale(t, 0.5f, 0.015f);
        transform.localScale = _baseScale * breath;
    }

    protected override void DoReact()
    {
        float t = Time.time;

        if (!_reactBuzzed)
        {
            _reactBuzzed = true;
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlaySwarmBuzz();
        }

        // Tighten formation toward player
        _tightenProgress = Mathf.Min(_tightenProgress + Time.deltaTime * tightenSpeed, 1f);
        float tightRadius = Mathf.Lerp(orbitRadius, orbitRadius * 0.3f, _tightenProgress);
        float fastSpeed = orbitSpeed * (1f + _tightenProgress * 2f);

        for (int i = 0; i < _flies.Length; i++)
        {
            if (_flies[i] == null) continue;
            float phase = t * fastSpeed + _flyPhases[i];

            float x = Mathf.Sin(phase * 1.2f) * tightRadius;
            float y = Mathf.Sin(phase * 1.5f + i * 0.7f) * tightRadius * 0.6f;
            float z = Mathf.Cos(phase * 1.0f + i * 1.1f) * tightRadius;

            _flies[i].localPosition = new Vector3(x, y, z);

            // Faster wing beats when agitated
            float wingBeat = 1f + Mathf.Sin(t * 50f + i * 3f) * 0.2f;
            _flies[i].localScale = new Vector3(wingBeat, 1f, 1f);
        }

        // Swarm pulses larger
        float pulse = 1f + Mathf.Sin(t * 5f) * 0.08f * _tightenProgress;
        transform.localScale = _baseScale * pulse;
    }

    public override void OnPlayerHit(Transform player)
    {
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlaySwarmAttack();
        StartCoroutine(AttackScatter());
    }

    System.Collections.IEnumerator AttackScatter()
    {
        // Flies scatter outward on hit, then reform
        Vector3[] scatterDirs = new Vector3[_flies.Length];
        for (int i = 0; i < _flies.Length; i++)
            scatterDirs[i] = Random.insideUnitSphere.normalized;

        float dur = 0.3f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            for (int i = 0; i < _flies.Length; i++)
            {
                if (_flies[i] == null) continue;
                _flies[i].localPosition += scatterDirs[i] * Time.deltaTime * 3f;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reform
        elapsed = 0f;
        while (elapsed < 0.5f)
        {
            float t = elapsed / 0.5f;
            for (int i = 0; i < _flies.Length; i++)
            {
                if (_flies[i] == null) continue;
                _flies[i].localPosition = Vector3.Lerp(_flies[i].localPosition,
                    _flyOffsets[i] * 0.3f, t);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public override void OnPoolReset()
    {
        base.OnPoolReset();
        _tightenProgress = 0f;
        _reactBuzzed = false;
        transform.localScale = _baseScale;
    }
}
