using UnityEngine;

/// <summary>
/// Translucent sewer jellyfish that pulses and drifts in the pipe.
/// Tentacles trail behind, body pulses like breathing.
/// Glows with toxic bioluminescence. Stings on contact.
/// </summary>
public class SewerJellyfishBehavior : ObstacleBehavior
{
    [Header("Jellyfish")]
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.15f;
    public float driftSpeed = 0.3f;
    public float driftRange = 0.5f;
    public float glowPulseSpeed = 3f;

    private Vector3 _startPos;
    private Vector3 _baseScale;
    private float _pulsePhase;
    private float _driftPhase;
    private MaterialPropertyBlock _mpb;
    private Renderer[] _renderers;

    // Tentacle tracking
    private Transform[] _tentacles;
    private float[] _tentaclePhases;

    protected override void Start()
    {
        base.Start();
        _startPos = transform.position;
        _baseScale = transform.localScale;
        _pulsePhase = Random.Range(0f, Mathf.PI * 2f);
        _driftPhase = Random.Range(0f, Mathf.PI * 2f);
        _mpb = new MaterialPropertyBlock();
        _renderers = GetComponentsInChildren<Renderer>();

        // Find tentacles
        var tentList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Tentacle"))
                tentList.Add(child);
        }
        _tentacles = tentList.ToArray();
        _tentaclePhases = new float[_tentacles.Length];
        for (int i = 0; i < _tentaclePhases.Length; i++)
            _tentaclePhases[i] = Random.Range(0f, Mathf.PI * 2f);
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Jellyfish pulse: expand/contract like breathing
        float pulse = 1f + Mathf.Sin(t * pulseSpeed + _pulsePhase) * pulseAmount;
        float inversePulse = 1f + Mathf.Sin(t * pulseSpeed + _pulsePhase + Mathf.PI) * pulseAmount * 0.5f;
        transform.localScale = new Vector3(
            _baseScale.x * pulse,
            _baseScale.y * inversePulse, // height contracts when width expands
            _baseScale.z * pulse);

        // Gentle drift
        float driftX = Mathf.Sin(t * driftSpeed + _driftPhase) * driftRange;
        float driftY = Mathf.Cos(t * driftSpeed * 0.7f + _driftPhase) * driftRange * 0.5f;
        transform.position = _startPos + transform.right * driftX + transform.up * driftY;

        // Glow pulse on emission
        float glow = 0.3f + Mathf.Sin(t * glowPulseSpeed + _pulsePhase) * 0.2f;
        Color glowColor = new Color(0.1f, glow, glow * 0.5f);
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glowColor);
            r.SetPropertyBlock(_mpb);
        }

        // Tentacle sway
        for (int i = 0; i < _tentacles.Length; i++)
        {
            if (_tentacles[i] == null) continue;
            float sway = Mathf.Sin(t * 1.5f + _tentaclePhases[i]) * 12f;
            float curl = Mathf.Sin(t * 0.8f + _tentaclePhases[i] * 0.5f) * 8f;
            _tentacles[i].localRotation = Quaternion.Euler(sway, curl, 0);
        }
    }

    protected override void DoReact()
    {
        // Faster pulsing when player is near
        float t = Time.time;
        float pulse = 1f + Mathf.Sin(t * pulseSpeed * 2.5f + _pulsePhase) * pulseAmount * 1.5f;
        transform.localScale = new Vector3(
            _baseScale.x * pulse,
            _baseScale.y * (2f - pulse),
            _baseScale.z * pulse);

        // Intense glow
        float glow = 0.5f + Mathf.Sin(t * glowPulseSpeed * 2f) * 0.4f;
        Color glowColor = new Color(0.2f, glow, glow * 0.7f);
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glowColor);
            r.SetPropertyBlock(_mpb);
        }

        // Tentacles curl inward defensively
        for (int i = 0; i < _tentacles.Length; i++)
        {
            if (_tentacles[i] == null) continue;
            float curl = Mathf.Sin(t * 3f + _tentaclePhases[i]) * 25f;
            _tentacles[i].localRotation = Quaternion.Euler(curl, 0, curl * 0.3f);
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayJellyZap();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayJellyZap(transform.position);
        StartCoroutine(StingAnimation());
    }

    System.Collections.IEnumerator StingAnimation()
    {
        // Flash bright
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", new Color(0.3f, 1f, 0.8f));
            r.SetPropertyBlock(_mpb);
        }

        // Tentacles spread outward
        for (int i = 0; i < _tentacles.Length; i++)
        {
            if (_tentacles[i] == null) continue;
            _tentacles[i].localRotation = Quaternion.Euler(0, i * (360f / _tentacles.Length), 60f);
        }

        yield return new WaitForSeconds(0.3f);

        // Contract after sting
        transform.localScale = _baseScale * 0.7f;

        yield return new WaitForSeconds(0.5f);

        // Return to normal
        transform.localScale = _baseScale;
    }
}
