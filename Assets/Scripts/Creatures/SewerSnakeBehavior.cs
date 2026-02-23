using UnityEngine;

/// <summary>
/// Sewer snake that slithers across the pipe in a sine-wave pattern.
/// Coils up defensively when player approaches.
/// Zones: Grimy + Rusty.
/// </summary>
public class SewerSnakeBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.4f, 0.6f, 0.1f); // olive green
    public override bool SplatterOnHit => false;

    [Header("Snake")]
    public float slitherSpeed = 2f;
    public float slitherAmplitude = 0.8f;
    public int segmentCount = 7;

    private Vector3 _startPos;
    private Vector3 _baseScale;
    private float _slitherPhase;
    private Transform[] _segments;
    private bool _coiled;
    private float _coilTimer;
    private bool _reactHissed;

    protected override void Start()
    {
        base.Start();
        _startPos = transform.position;
        _baseScale = transform.localScale;
        _slitherPhase = Random.Range(0f, Mathf.PI * 2f);

        // Find segments
        var segList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
            if (child.name.Contains("Seg")) segList.Add(child);
        _segments = segList.ToArray();
    }

    protected override void DoIdle()
    {
        float t = Time.time;
        _slitherPhase += Time.deltaTime * slitherSpeed;

        // Sine-wave slither: each segment offset in phase
        for (int i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] == null) continue;
            float segPhase = _slitherPhase + i * 0.8f;
            float sway = Mathf.Sin(segPhase) * slitherAmplitude * 0.1f;
            Vector3 localPos = _segments[i].localPosition;
            _segments[i].localPosition = new Vector3(sway, localPos.y, localPos.z);

            // Subtle Y undulation
            float yWave = Mathf.Sin(segPhase * 0.5f) * 0.02f;
            _segments[i].localPosition += Vector3.up * yWave;
        }

        // Gentle breathing
        float breath = CreatureAnimUtils.BreathingScale(t, 0.8f, 0.015f);
        transform.localScale = _baseScale * breath;

        // Tongue flick
        float flickRate = Mathf.Sin(t * 3f);
        if (flickRate > 0.95f)
        {
            // Quick tongue dart would be on a child named "Tongue"
            Transform tongue = transform.Find("Tongue");
            if (tongue != null)
                tongue.localScale = new Vector3(1f, 1f, 1f + flickRate * 2f);
        }
    }

    protected override void DoReact()
    {
        if (!_reactHissed)
        {
            _reactHissed = true;
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlaySnakeHiss();
        }

        // Coil up defensively
        if (!_coiled)
        {
            _coiled = true;
            _coilTimer = 0f;
        }

        _coilTimer += Time.deltaTime;
        float coilT = Mathf.Clamp01(_coilTimer / 0.4f);

        // Segments pull together into a coil
        for (int i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] == null) continue;
            Vector3 localPos = _segments[i].localPosition;
            float targetZ = Mathf.Lerp(localPos.z, localPos.z * 0.3f, coilT);
            _segments[i].localPosition = new Vector3(
                Mathf.Sin(Time.time * 4f + i) * 0.05f, // trembling
                localPos.y,
                targetZ);
        }

        // Puff up body
        transform.localScale = _baseScale * (1f + coilT * 0.2f);

        // Warning shimmer
        float glow = 0.2f + Mathf.Sin(Time.time * 6f) * 0.15f;
        var renderers = GetComponentsInChildren<Renderer>();
        var mpb = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", new Color(glow, glow * 0.8f, 0.05f));
            r.SetPropertyBlock(mpb);
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlaySnakeStrike();
        StartCoroutine(StrikeAnim());
    }

    System.Collections.IEnumerator StrikeAnim()
    {
        // Lunge forward then recoil
        Vector3 startPos = transform.position;
        Vector3 strikeDir = transform.forward;
        float dur = 0.15f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            transform.position = startPos + strikeDir * Mathf.Sin(t * Mathf.PI) * 0.5f;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = startPos;

        // Recoil and flatten
        elapsed = 0f;
        while (elapsed < 0.3f)
        {
            float t = elapsed / 0.3f;
            transform.localScale = _baseScale * (1f - t * 0.3f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    public override void OnPoolReset()
    {
        base.OnPoolReset();
        _coiled = false;
        _coilTimer = 0f;
        _reactHissed = false;
        transform.localScale = _baseScale;
    }
}
