using UnityEngine;

/// <summary>
/// Toilet paper mummy that stands wrapped in TP.
/// Unfurls and waddles menacingly when player approaches.
/// Zones: Porcelain + Grimy.
/// </summary>
public class ToiletPaperMummyBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.9f, 0.9f, 0.85f); // dirty white
    public override bool SplatterOnHit => false;

    [Header("Mummy")]
    public float waddleSpeed = 3f;
    public float waddleAmount = 8f;
    public float unfurlSpeed = 2f;

    private Vector3 _baseScale;
    private float _waddlePhase;
    private float _unfurlProgress;
    private bool _unfurling;
    private bool _reactGroaned;
    private Transform[] _strips;
    private Transform _arms;

    protected override void Start()
    {
        base.Start();
        _baseScale = transform.localScale;
        _waddlePhase = Random.Range(0f, Mathf.PI * 2f);

        // Find paper strips and arms
        var stripList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Strip")) stripList.Add(child);
            else if (child.name.Contains("Arm")) _arms = child;
        }
        _strips = stripList.ToArray();
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Subtle swaying in place (mummy just stands there wrapped up)
        float sway = Mathf.Sin(t * 0.5f + _waddlePhase) * 2f;
        transform.localRotation = Quaternion.Euler(0, 0, sway);

        // Paper strips flutter slightly
        for (int i = 0; i < _strips.Length; i++)
        {
            if (_strips[i] == null) continue;
            float flutter = Mathf.Sin(t * 2f + i * 1.3f) * 3f;
            _strips[i].localRotation = Quaternion.Euler(flutter, 0, flutter * 0.5f);
        }

        // Breathing
        float breath = CreatureAnimUtils.BreathingScale(t, 0.6f, 0.02f);
        transform.localScale = _baseScale * breath;
    }

    protected override void DoReact()
    {
        float t = Time.time;

        if (!_reactGroaned)
        {
            _reactGroaned = true;
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayMummyGroan();
        }

        // Unfurl animation - strips loosen
        if (!_unfurling) _unfurling = true;
        _unfurlProgress = Mathf.Min(_unfurlProgress + Time.deltaTime * unfurlSpeed, 1f);

        // Strips spread outward as they unfurl
        for (int i = 0; i < _strips.Length; i++)
        {
            if (_strips[i] == null) continue;
            float angle = _unfurlProgress * 45f + Mathf.Sin(t * 3f + i) * 10f;
            _strips[i].localRotation = Quaternion.Euler(angle, i * 30f, angle * 0.5f);
            // Strips extend outward
            Vector3 lp = _strips[i].localPosition;
            _strips[i].localPosition = Vector3.Lerp(lp,
                lp + _strips[i].up * 0.1f * _unfurlProgress, Time.deltaTime * 3f);
        }

        // Waddle toward player (menacing shuffle)
        _waddlePhase += Time.deltaTime * waddleSpeed;
        float waddle = Mathf.Sin(_waddlePhase) * waddleAmount * _unfurlProgress;
        transform.localRotation = Quaternion.Euler(0, 0, waddle);

        // Arms raise
        if (_arms != null)
        {
            float armAngle = Mathf.Lerp(0f, -60f, _unfurlProgress);
            _arms.localRotation = Quaternion.Euler(armAngle, 0, 0);
        }

        // Puff up slightly
        transform.localScale = _baseScale * (1f + _unfurlProgress * 0.1f);
    }

    public override void OnPlayerHit(Transform player)
    {
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayMummyWrap();
        StartCoroutine(WrapAnim());
    }

    System.Collections.IEnumerator WrapAnim()
    {
        // Mummy wraps around player briefly
        float dur = 0.3f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            // Stretch toward player
            transform.localScale = new Vector3(
                startScale.x * (1f + t * 0.5f),
                startScale.y * (1f - t * 0.3f),
                startScale.z * (1f + t * 0.5f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Spring back
        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            float t = elapsed / 0.2f;
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    public override void OnPoolReset()
    {
        base.OnPoolReset();
        _unfurling = false;
        _unfurlProgress = 0f;
        _reactGroaned = false;
        transform.localScale = _baseScale;
        transform.localRotation = Quaternion.identity;
    }
}
