using UnityEngine;

/// <summary>
/// Spinning toilet seat speed boost that doubles player speed for 5 seconds when touched.
/// Spins, bobs, and pulses with glowing porcelain energy.
/// </summary>
public class SpeedBoost : MonoBehaviour
{
    public float speedMultiplier = 2f;
    public float duration = 5f;
    public float pulseSpeed = 3f;
    public float spinSpeed = 90f;       // degrees per second
    public float bobAmplitude = 0.15f;  // meters up/down
    public float bobSpeed = 2f;         // cycles per second

    private Renderer[] _renderers;
    private Color[] _baseEmissions;
    private bool _used = false;
    private Vector3 _startPos;
    private float _bobPhase;

    void Start()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _baseEmissions = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _renderers[i].material != null)
            {
                _renderers[i].material.EnableKeyword("_EMISSION");
                _baseEmissions[i] = _renderers[i].material.GetColor("_EmissionColor");
            }
        }

        _startPos = transform.localPosition;
        _bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        if (_used) return;

        // Spinning rotation around local Y
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        // Bobbing hover
        float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f + _bobPhase) * bobAmplitude;
        Vector3 pos = _startPos;
        pos.y += bob;
        transform.localPosition = pos;

        // Pulsing glow on all renderers
        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Color glow = _baseEmissions[i] * (1f + pulse * 2f);
            _renderers[i].material.SetColor("_EmissionColor", glow);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_used) return;
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc != null)
        {
            tc.ApplySpeedBoost(speedMultiplier, duration);
            _used = true;

            // Flash bright then fade out
            foreach (Renderer r in _renderers)
            {
                if (r != null)
                    r.material.SetColor("_EmissionColor", Color.white * 5f);
            }

            Destroy(gameObject, 0.3f);
        }
    }
}
