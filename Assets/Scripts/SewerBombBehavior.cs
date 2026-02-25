using UnityEngine;

/// <summary>
/// Sewer bomb obstacle spawned during underwater flush phases.
/// Pulsing red emission that speeds up when player is close.
/// Triggers TakeBombHit() for massive stun + slowdown on contact.
/// </summary>
public class SewerBombBehavior : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float basePulseSpeed = 2f;
    public float proximityPulseSpeed = 6f;
    public float proximityRange = 5f;

    private Material _coreMat;
    private float _phase;
    private Color _baseEmission;
    private Transform _player;
    private Vector3 _baseScale;

    void Start()
    {
        _phase = Random.value * Mathf.PI * 2f;
        _baseScale = transform.localScale;

        // Find core child material (the glowing red sphere)
        Transform core = transform.Find("RedCore");
        if (core != null)
        {
            Renderer r = core.GetComponent<Renderer>();
            if (r != null)
            {
                _coreMat = r.material;
                if (_coreMat.HasProperty("_EmissionColor"))
                    _baseEmission = _coreMat.GetColor("_EmissionColor");
            }
        }
    }

    void Update()
    {
        if (_coreMat == null) return;

        // Find player lazily
        if (_player == null)
        {
            TurdController tc = Object.FindFirstObjectByType<TurdController>();
            if (tc != null) _player = tc.transform;
        }

        // Proximity check — pulse faster when player is close
        float dist = _player != null ? Vector3.Distance(transform.position, _player.position) : 999f;
        float pulseSpeed = dist < proximityRange ? proximityPulseSpeed : basePulseSpeed;

        _phase += Time.deltaTime * pulseSpeed;
        float intensity = 0.5f + 0.5f * Mathf.Sin(_phase * Mathf.PI * 2f);

        // Scale emission from dim to bright red
        if (_coreMat.HasProperty("_EmissionColor"))
            _coreMat.SetColor("_EmissionColor", _baseEmission * (0.3f + intensity * 0.7f));

        // Subtle scale pulse on the whole bomb
        float scalePulse = 1f + Mathf.Sin(_phase * Mathf.PI * 2f) * 0.03f;
        transform.localScale = _baseScale * scalePulse;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc == null) return;
        if (tc.IsInvincible) return;

        // Call the special bomb hit (heavier than normal)
        tc.TakeBombHit();

        // Explosion VFX
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayHitExplosion(transform.position, new Color(1f, 0.2f, 0.05f));
            ParticleManager.Instance.PlayHitExplosion(transform.position + Vector3.up * 0.3f, new Color(1f, 0.5f, 0.1f));
        }

        // Disable so it can't hit again
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Destroy after brief delay (let explosion show)
        Destroy(gameObject, 0.5f);
    }
}
