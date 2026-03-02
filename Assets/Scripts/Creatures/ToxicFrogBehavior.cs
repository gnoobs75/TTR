using UnityEngine;

/// <summary>
/// Toxic mutant frog that squats on the pipe wall and leaps periodically.
/// Puffs up its throat, blinks big googly eyes, and hops toward the player.
/// On hit: tongue lashes out, frog inflates, then deflates.
/// </summary>
public class ToxicFrogBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.1f, 0.8f, 0.2f); // slimy green
    public override bool SplatterOnHit => true; // frog slime splat
    [Header("Frog")]
    public float hopInterval = 2.5f;
    public float hopHeight = 0.4f;
    public float hopDuration = 0.4f;
    public float throatPulseSpeed = 1.5f;
    public float throatPulseAmount = 0.3f;

    private Vector3 _startPos;
    private Vector3 _baseScale;
    private float _nextHopTime;
    private float _hopTimer = -1f;
    private float _throatPhase;
    private bool _isHopping;
    private bool _reactCroaked;

    // Body parts
    private Transform _throat;
    private Transform _tongue;
    private Transform[] _legs;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;

    protected override void Start()
    {
        base.Start();
        _startPos = transform.position;
        _baseScale = transform.localScale;
        _nextHopTime = Time.time + Random.Range(1f, hopInterval);
        _throatPhase = Random.Range(0f, Mathf.PI * 2f);
        _mpb = new MaterialPropertyBlock();
        _renderers = GetComponentsInChildren<Renderer>();

        // Find body parts
        var legList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Throat")) _throat = child;
            else if (child.name.Contains("Tongue")) _tongue = child;
            else if (child.name.Contains("Leg")) legList.Add(child);
        }
        _legs = legList.ToArray();

        if (_tongue != null)
            _tongue.localScale = new Vector3(1f, 1f, 0.01f); // hidden by default
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Throat pulse (breathing)
        if (_throat != null)
        {
            float pulse = 1f + Mathf.Sin(t * throatPulseSpeed + _throatPhase) * throatPulseAmount;
            _throat.localScale = new Vector3(pulse, pulse, pulse);
        }

        // Gentle breathing scale
        float breath = CreatureAnimUtils.BreathingScale(t, 0.8f, 0.02f);

        // Periodic hop
        if (!_isHopping && t > _nextHopTime)
        {
            _isHopping = true;
            _hopTimer = 0f;
        }

        if (_isHopping)
        {
            _hopTimer += Time.deltaTime;
            float hopT = _hopTimer / hopDuration;

            if (hopT < 1f)
            {
                // Parabolic hop
                float arc = 4f * hopHeight * hopT * (1f - hopT);
                transform.position = _startPos + transform.up * arc;

                // Squash/stretch during hop
                float stretch = hopT < 0.3f ? 1.2f : (hopT > 0.7f ? 0.85f : 1f);
                transform.localScale = new Vector3(
                    _baseScale.x / Mathf.Sqrt(stretch),
                    _baseScale.y * stretch,
                    _baseScale.z / Mathf.Sqrt(stretch));

                // Legs tuck during hop
                for (int i = 0; i < _legs.Length; i++)
                {
                    if (_legs[i] == null) continue;
                    float tuck = Mathf.Sin(hopT * Mathf.PI) * 30f;
                    _legs[i].localRotation = Quaternion.Euler(-tuck, 0, 0);
                }
            }
            else
            {
                // Land
                _isHopping = false;
                transform.position = _startPos;
                transform.localScale = _baseScale;
                _nextHopTime = t + Random.Range(hopInterval * 0.7f, hopInterval * 1.3f);

                // Landing squash
                StartCoroutine(LandSquash());
            }
        }

        // Slight idle sway
        if (!_isHopping)
        {
            float sway = Mathf.Sin(t * 0.8f + _throatPhase) * 2f;
            transform.localRotation = Quaternion.Euler(sway, 0, sway * 0.5f);
        }
    }

    System.Collections.IEnumerator LandSquash()
    {
        // Landing impact effects
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayLandingDust(transform.position);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayStomp();

        // Quick squash on landing
        float dur = 0.15f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            float squash = 1f - (1f - t) * 0.2f;
            transform.localScale = new Vector3(
                _baseScale.x / squash,
                _baseScale.y * squash,
                _baseScale.z / squash);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    protected override void DoReact()
    {
        float t = Time.time;

        // Warning croak on react entry
        if (!_reactCroaked)
        {
            _reactCroaked = true;
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayFrogCroak();
        }

        // Puff up when player approaches
        float puff = 1f + Mathf.Sin(t * throatPulseSpeed * 3f) * 0.1f;
        transform.localScale = _baseScale * (1.15f * puff);

        // Throat inflates bigger
        if (_throat != null)
        {
            float bigPulse = 1.3f + Mathf.Sin(t * 4f) * 0.2f;
            _throat.localScale = Vector3.one * bigPulse;
        }

        // Legs ready to spring
        for (int i = 0; i < _legs.Length; i++)
        {
            if (_legs[i] == null) continue;
            _legs[i].localRotation = Quaternion.Euler(-15f, 0, 0);
        }

        // Warning glow
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            float glow = 0.3f + Mathf.Sin(t * 6f) * 0.2f;
            _mpb.SetColor("_EmissionColor", new Color(glow, glow * 0.8f, 0.1f));
            r.SetPropertyBlock(_mpb);
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayFrogCroak();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayFrogSplat(transform.position);
        StartCoroutine(TongueAttack());
    }

    System.Collections.IEnumerator TongueAttack()
    {
        // Tongue lashes out
        if (_tongue != null)
        {
            float dur = 0.2f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                float t = elapsed / dur;
                _tongue.localScale = new Vector3(1f, 1f, t * 3f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return Wait015;

            // Retract tongue
            elapsed = 0f;
            while (elapsed < dur)
            {
                float t = elapsed / dur;
                _tongue.localScale = new Vector3(1f, 1f, (1f - t) * 3f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _tongue.localScale = new Vector3(1f, 1f, 0.01f);
        }

        // Inflate after hit
        float inflateTime = 0.3f;
        float e2 = 0f;
        while (e2 < inflateTime)
        {
            float t = e2 / inflateTime;
            transform.localScale = _baseScale * (1f + t * 0.5f);
            e2 += Time.deltaTime;
            yield return null;
        }

        // Deflate back
        yield return Wait02;
        float deflateTime = 0.5f;
        e2 = 0f;
        while (e2 < deflateTime)
        {
            float t = e2 / deflateTime;
            transform.localScale = Vector3.Lerp(_baseScale * 1.5f, _baseScale, t);
            e2 += Time.deltaTime;
            yield return null;
        }
        transform.localScale = _baseScale;
    }
}
