using UnityEngine;

/// <summary>
/// Speed boost pad that doubles player speed for 5 seconds when touched.
/// Glows with animated chevron-like pulsing.
/// </summary>
public class SpeedBoost : MonoBehaviour
{
    public float speedMultiplier = 2f;
    public float duration = 5f;
    public float pulseSpeed = 3f;

    private Renderer _renderer;
    private Color _baseEmission;
    private bool _used = false;

    void Start()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null && _renderer.material != null)
        {
            _renderer.material.EnableKeyword("_EMISSION");
            _baseEmission = _renderer.material.GetColor("_EmissionColor");
        }
    }

    void Update()
    {
        if (_used || _renderer == null) return;

        // Pulsing glow effect
        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Color glow = _baseEmission * (1f + pulse * 2f);
        _renderer.material.SetColor("_EmissionColor", glow);
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
            if (_renderer != null)
                _renderer.material.SetColor("_EmissionColor", Color.white * 5f);

            Destroy(gameObject, 0.3f);
        }
    }
}
