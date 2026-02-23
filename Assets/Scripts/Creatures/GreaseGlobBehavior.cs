using UnityEngine;

/// <summary>
/// Grease glob that slides along walls and drools.
/// Puffs up on react, leaving a slippery trail effect.
/// Zones: Toxic + Rusty.
/// </summary>
public class GreaseGlobBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.7f, 0.6f, 0.2f); // greasy yellow-brown
    public override bool SplatterOnHit => true; // greasy splat

    [Header("Grease Glob")]
    public float slideSpeed = 0.5f;
    public float dripInterval = 1.5f;
    public float puffAmount = 0.3f;

    private Vector3 _baseScale;
    private float _slidePhase;
    private float _nextDripTime;
    private float _puffProgress;
    private bool _reactSquelched;
    private Transform[] _drips;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;

    protected override void Start()
    {
        base.Start();
        _baseScale = transform.localScale;
        _slidePhase = Random.Range(0f, Mathf.PI * 2f);
        _nextDripTime = Time.time + Random.Range(0.5f, dripInterval);
        _mpb = new MaterialPropertyBlock();
        _renderers = GetComponentsInChildren<Renderer>();

        // Find drip children
        var dripList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
            if (child.name.Contains("Drip")) dripList.Add(child);
        _drips = dripList.ToArray();
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Slow organic wobble (jiggly like jello)
        float wobbleX = 1f + Mathf.Sin(t * 1.2f + _slidePhase) * 0.04f;
        float wobbleY = 1f + Mathf.Sin(t * 1.5f + _slidePhase * 1.3f) * 0.03f;
        float wobbleZ = 1f + Mathf.Sin(t * 0.9f + _slidePhase * 0.7f) * 0.04f;
        transform.localScale = new Vector3(
            _baseScale.x * wobbleX,
            _baseScale.y * wobbleY,
            _baseScale.z * wobbleZ);

        // Drip animation - drips extend downward periodically
        if (t > _nextDripTime)
        {
            _nextDripTime = t + Random.Range(dripInterval * 0.7f, dripInterval * 1.3f);
            // Animate a random drip
            if (_drips.Length > 0)
            {
                int dripIdx = Random.Range(0, _drips.Length);
                if (_drips[dripIdx] != null)
                    StartCoroutine(DripAnim(_drips[dripIdx]));
            }
        }

        // Subtle iridescent shimmer
        float hueShift = Mathf.Sin(t * 0.3f) * 0.05f;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", new Color(0.15f + hueShift, 0.12f + hueShift * 0.5f, 0.03f));
            r.SetPropertyBlock(_mpb);
        }
    }

    System.Collections.IEnumerator DripAnim(Transform drip)
    {
        Vector3 startScale = drip.localScale;
        Vector3 startPos = drip.localPosition;
        float dur = 0.6f;
        float elapsed = 0f;

        // Drip extends downward
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            drip.localScale = new Vector3(startScale.x * (1f - t * 0.3f),
                startScale.y * (1f + t * 1.5f), startScale.z * (1f - t * 0.3f));
            drip.localPosition = startPos + Vector3.down * t * 0.15f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap back (drip "falls off")
        drip.localScale = startScale;
        drip.localPosition = startPos;
    }

    protected override void DoReact()
    {
        float t = Time.time;

        if (!_reactSquelched)
        {
            _reactSquelched = true;
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayGlobSquelch();
        }

        // Puff up threateningly
        _puffProgress = Mathf.Min(_puffProgress + Time.deltaTime * 2f, 1f);
        float puff = 1f + _puffProgress * puffAmount;

        // Angry wobble (faster, bigger)
        float wobbleX = puff + Mathf.Sin(t * 4f) * 0.08f;
        float wobbleY = puff + Mathf.Sin(t * 5f) * 0.06f;
        float wobbleZ = puff + Mathf.Sin(t * 3.5f) * 0.08f;
        transform.localScale = new Vector3(
            _baseScale.x * wobbleX,
            _baseScale.y * wobbleY,
            _baseScale.z * wobbleZ);

        // Warning glow intensifies
        float glow = 0.3f + Mathf.Sin(t * 5f) * 0.2f;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", new Color(glow, glow * 0.7f, 0.05f));
            r.SetPropertyBlock(_mpb);
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayGlobSplat();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayFrogSplat(transform.position); // reuse splat particles
        StartCoroutine(SplatAnim());
    }

    System.Collections.IEnumerator SplatAnim()
    {
        // Flatten and spread on hit
        float dur = 0.2f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            transform.localScale = new Vector3(
                _baseScale.x * (1f + t * 1.2f),
                _baseScale.y * (1f - t * 0.6f),
                _baseScale.z * (1f + t * 1.2f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Slowly reform
        yield return new WaitForSeconds(0.3f);
        elapsed = 0f;
        while (elapsed < 0.5f)
        {
            float t = elapsed / 0.5f;
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    public override void OnPoolReset()
    {
        base.OnPoolReset();
        _puffProgress = 0f;
        _reactSquelched = false;
        transform.localScale = _baseScale;
    }
}
