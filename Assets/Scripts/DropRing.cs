using UnityEngine;

/// <summary>
/// Collectible ring during vertical drop freefall sections.
/// Glowing ring that spins and pulses. Scores points and builds combo.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DropRing : MonoBehaviour
{
    public float rotateSpeed = 180f;
    public float bobAmplitude = 0.1f;
    public float bobFrequency = 1.5f;
    public int scoreValue = 25;

    private Vector3 _startPos;
    private float _bobOffset;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private bool _collected;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        gameObject.tag = "Collectible";

        _startPos = transform.position;
        _bobOffset = Random.value * Mathf.PI * 2f;
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (_collected) return;

        // Spin
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);

        // Bob
        float bob = Mathf.Sin((Time.time + _bobOffset) * bobFrequency * Mathf.PI) * bobAmplitude;
        transform.position = _startPos + Vector3.up * bob;

        // Glow pulse
        float pulse = 0.5f + Mathf.Sin(Time.time * 3f + _bobOffset) * 0.3f;
        Color glow = new Color(0.2f, 0.8f, 1f) * pulse; // cyan glow
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glow);
            r.SetPropertyBlock(_mpb);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;

        _collected = true;

        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreValue);

        if (ComboSystem.Instance != null)
            ComboSystem.Instance.RegisterEvent(ComboSystem.EventType.CoinCollect);

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowCoin(transform.position, scoreValue);

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayCoinCollect();

        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayCoinCollect(transform.position);

        HapticManager.LightTap();

        Destroy(gameObject);
    }
}
