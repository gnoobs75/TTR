using UnityEngine;

/// <summary>
/// Giant sewer spider that hangs from pipe ceiling on a web strand.
/// Drops down when player approaches, scuttles sideways, hops toward player.
/// On hit: wraps player in web (visual shake effect).
/// </summary>
public class SewerSpiderBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.5f, 0.1f, 0.6f); // creepy purple
    [Header("Spider")]
    public float scuttleSpeed = 2f;
    public float dropSpeed = 4f;
    public float legWiggleSpeed = 8f;
    public float legWiggleAmount = 15f;

    private Vector3 _startPos;
    private Vector3 _baseScale;
    private float _scuttlePhase;
    private bool _hasDropped;
    private bool _reactHissed;
    private float _dropProgress;
    private float _dropStart;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;

    // Body parts
    private Transform[] _legs;

    protected override void Start()
    {
        base.Start();
        _startPos = transform.position;
        _baseScale = transform.localScale;
        _scuttlePhase = Random.Range(0f, Mathf.PI * 2f);
        _mpb = new MaterialPropertyBlock();
        _renderers = GetComponentsInChildren<Renderer>();

        // Find legs
        var legList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Leg")) legList.Add(child);
        }
        _legs = legList.ToArray();
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Subtle body bob (hanging from web)
        float bob = Mathf.Sin(t * 1.2f + _scuttlePhase) * 0.05f;
        transform.position = _startPos + transform.up * bob;

        // Leg wiggle (creepy fidgeting)
        for (int i = 0; i < _legs.Length; i++)
        {
            if (_legs[i] == null) continue;
            float wiggle = Mathf.Sin(t * legWiggleSpeed * 0.3f + i * 0.8f) * legWiggleAmount * 0.3f;
            float curl = Mathf.Sin(t * 0.5f + i * 1.2f) * 5f;
            _legs[i].localRotation = Quaternion.Euler(wiggle, curl, 0);
        }

        // Slight body rotation (looking around)
        float look = Mathf.Sin(t * 0.4f + _scuttlePhase) * 8f;
        transform.localRotation *= Quaternion.Euler(0, look * Time.deltaTime, 0);
    }

    protected override void DoReact()
    {
        float t = Time.time;

        // Warning hiss on react entry
        if (!_reactHissed)
        {
            _reactHissed = true;
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlaySpiderHiss();
        }

        // Drop down toward player path
        if (!_hasDropped)
        {
            _hasDropped = true;
            _dropStart = t;
        }

        float dropT = Mathf.Clamp01((t - _dropStart) * dropSpeed * 0.3f);
        Vector3 dropOffset = -transform.up * dropT * 1.5f;
        transform.position = _startPos + dropOffset;

        // Frantic leg movement when reacting
        for (int i = 0; i < _legs.Length; i++)
        {
            if (_legs[i] == null) continue;
            float wiggle = Mathf.Sin(t * legWiggleSpeed + i * 1.3f) * legWiggleAmount;
            float spread = Mathf.Sin(t * 3f + i * 0.5f) * 10f;
            _legs[i].localRotation = Quaternion.Euler(wiggle, spread, 0);
        }

        // Pulsing red eyes (danger warning)
        float glow = 0.3f + Mathf.Sin(t * 8f) * 0.3f;
        Color glowColor = new Color(glow, 0.02f, 0.02f);
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            // Only apply to eye parts
            if (r.gameObject.name.Contains("Eye"))
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", glowColor);
                r.SetPropertyBlock(_mpb);
            }
        }

        // Body throb (threatening)
        float throb = 1f + Mathf.Sin(t * 4f) * 0.08f;
        transform.localScale = _baseScale * throb;
    }

    public override void OnPlayerHit(Transform player)
    {
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlaySpiderHiss();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlaySpiderWeb(transform.position);
        StartCoroutine(WebWrapAttack());
    }

    System.Collections.IEnumerator WebWrapAttack()
    {
        // Spider lunges forward
        Vector3 lungeDir = _player != null ? (_player.position - transform.position).normalized : transform.forward;
        float dur = 0.2f;
        float elapsed = 0f;
        Vector3 startP = transform.position;

        while (elapsed < dur)
        {
            float t = elapsed / dur;
            transform.position = Vector3.Lerp(startP, startP + lungeDir * 0.8f, t);
            // Legs spread wide during lunge
            for (int i = 0; i < _legs.Length; i++)
            {
                if (_legs[i] == null) continue;
                _legs[i].localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 1 : -1) * 45f * t);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Hold pose
        yield return Wait03;

        // Retract
        elapsed = 0f;
        float retractDur = 0.5f;
        while (elapsed < retractDur)
        {
            float t = elapsed / retractDur;
            transform.position = Vector3.Lerp(transform.position, _startPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = _startPos;
        _hasDropped = false;
    }
}
