using UnityEngine;

/// <summary>
/// Spinning toilet seat speed boost that doubles player speed for 5 seconds when touched.
/// Stands upright and spins like a Sonic ring, bobs, and pulses with glowing energy.
/// </summary>
public class SpeedBoost : MonoBehaviour
{
    public float speedMultiplier = 2f;
    public float duration = 5f;
    public float pulseSpeed = 3f;
    public float spinSpeed = 180f;      // degrees per second
    public float bobAmplitude = 0.2f;   // meters up/down
    public float bobSpeed = 2f;         // cycles per second
    public float floatHeight = 0.5f;    // float off surface

    private Renderer[] _renderers;
    private Renderer[] _arrowRenderers;
    private Color[] _baseEmissions;
    private bool _used = false;
    private Vector3 _startLocalPos;
    private float _bobPhase;
    private float _arrowTimer;
    private Quaternion _baseRotation;

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

        // Float off the pipe surface
        transform.position += transform.up * floatHeight;

        _startLocalPos = transform.localPosition;
        _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        _baseRotation = transform.rotation;

        // Find arrow renderers for sequential chase animation
        var arrows = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.gameObject.name.StartsWith("BoostArrow"))
                arrows.Add(r);
        }
        _arrowRenderers = arrows.ToArray();
    }

    void Update()
    {
        if (_used) return;

        // Stand upright and spin like a Sonic ring
        float spin = Time.time * spinSpeed;
        transform.rotation = _baseRotation * Quaternion.Euler(90f, spin, 0f);

        // Bobbing hover
        float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f + _bobPhase) * bobAmplitude;
        if (transform.parent != null)
            transform.localPosition = _startLocalPos + transform.parent.InverseTransformDirection(Vector3.up) * bob;

        // Pulsing glow on all renderers
        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Color glow = _baseEmissions[i] * (1f + pulse * 2f);
            _renderers[i].material.SetColor("_EmissionColor", glow);
        }

        // Sequential arrow chase animation
        if (_arrowRenderers != null && _arrowRenderers.Length > 0)
        {
            _arrowTimer += Time.deltaTime;
            for (int i = 0; i < _arrowRenderers.Length; i++)
            {
                int pairIdx = i / 2;
                float phase = (_arrowTimer * 3.5f - pairIdx * 0.35f) % 1f;
                float brightness = Mathf.Max(0.2f, Mathf.Pow(Mathf.Max(0, 1f - phase * 2f), 2f));
                Color emitColor = new Color(0.1f, 0.9f, 1f) * (5f * brightness);
                if (_arrowRenderers[i] != null && _arrowRenderers[i].material != null)
                    _arrowRenderers[i].material.SetColor("_EmissionColor", emitColor);
            }
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

            // Score popup
            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowMilestone(transform.position + Vector3.up * 1.5f, "SPEED BOOST!");

            // Screen flash for boost pickup
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerPowerUpFlash();

            // Camera punch
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.PunchFOV(6f);
                PipeCamera.Instance.Shake(0.1f);
            }

            // Particle burst at pickup
            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayCoinCollect(transform.position);

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlaySpeedBoost();

            HapticManager.HeavyTap();

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
