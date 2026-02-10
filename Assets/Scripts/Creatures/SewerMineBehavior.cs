using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sewer mine: warning light blink, hover bob. Rapid blink + spike extension on approach.
/// </summary>
public class SewerMineBehavior : ObstacleBehavior
{
    private Transform _warningLight;
    private List<Transform> _spikes = new List<Transform>();
    private Renderer _lightRenderer;
    private MaterialPropertyBlock _mpb;
    private Vector3 _originalPos;
    private float _hoverPhase;
    private float _spikeExtension;

    protected override void Start()
    {
        base.Start();
        _warningLight = CreatureAnimUtils.FindChildRecursive(transform, "warning");
        if (_warningLight == null) _warningLight = CreatureAnimUtils.FindChildRecursive(transform, "light");
        if (_warningLight != null)
            _lightRenderer = _warningLight.GetComponent<Renderer>();
        CreatureAnimUtils.FindChildrenRecursive(transform, "spike", _spikes);
        _mpb = new MaterialPropertyBlock();
        _originalPos = transform.localPosition;
        _hoverPhase = Random.value * Mathf.PI * 2f;
    }

    protected override void DoIdle()
    {
        float t = Time.time + _hoverPhase;

        // Hover bob
        float bob = CreatureAnimUtils.BreathingOffset(t, 0.6f, 0.05f);
        Vector3 pos = _originalPos;
        pos.y += bob;
        transform.localPosition = pos;

        // Slow menacing spin
        transform.Rotate(Vector3.up, 8f * Time.deltaTime, Space.Self);

        // Warning light blink
        if (_lightRenderer != null)
        {
            float blink = CreatureAnimUtils.BlinkPattern(t, 2f, 0.3f);
            Color glow = Color.Lerp(Color.red * 0.3f, Color.red * 3f, blink);
            _lightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glow);
            _lightRenderer.SetPropertyBlock(_mpb);
        }

        // Retract spikes smoothly
        _spikeExtension = Mathf.Lerp(_spikeExtension, 1f, Time.deltaTime * 2f);
        ApplySpikeScale(_spikeExtension);
    }

    protected override void DoReact()
    {
        float t = Time.time;

        // Faster spin
        transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.Self);

        // Rapid warning blink
        if (_lightRenderer != null)
        {
            float blink = CreatureAnimUtils.RapidBlink(t, 10f);
            Color glow = Color.Lerp(Color.red * 0.5f, Color.red * 5f, blink);
            _lightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glow);
            _lightRenderer.SetPropertyBlock(_mpb);
        }

        // Extend spikes
        _spikeExtension = Mathf.Lerp(_spikeExtension, 1.4f, Time.deltaTime * 5f);
        ApplySpikeScale(_spikeExtension);

        // Proximity beep
        float timeSinceReact = t - _reactTime;
        if (timeSinceReact < 0.1f && TryPlaySound())
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayBarrelBeep(); // Reuse beep for mines too
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        StartCoroutine(MineDetonateAnim());
    }

    private System.Collections.IEnumerator MineDetonateAnim()
    {
        // Mine detonates - spikes burst out, flash, then shrink
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayMineExplosion();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayMineExplosion(transform.position);

        Vector3 startScale = transform.localScale;

        // Spike burst + scale up
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float p = t / 0.2f;
            transform.localScale = startScale * (1f + p * 1f); // double size
            ApplySpikeScale(1f + p * 0.8f); // spikes extend

            // Flash the warning light rapidly
            if (_lightRenderer != null)
            {
                Color flash = Color.Lerp(Color.red * 5f, Color.yellow * 3f, Mathf.Sin(t * 60f) > 0 ? 1f : 0f);
                _lightRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", flash);
                _lightRenderer.SetPropertyBlock(_mpb);
            }
            yield return null;
        }

        // Hold explosion
        yield return new WaitForSeconds(0.15f);

        // Shrink back (mine is "spent" but doesn't destroy - still an obstacle)
        t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float p = t / 0.4f;
            transform.localScale = Vector3.Lerp(startScale * 2f, startScale * 0.8f, p);
            ApplySpikeScale(Mathf.Lerp(1.8f, 0.6f, p));
            yield return null;
        }

        transform.localScale = startScale * 0.8f;
    }

    void ApplySpikeScale(float scale)
    {
        foreach (var spike in _spikes)
        {
            if (spike != null)
                spike.localScale = new Vector3(spike.localScale.x, scale, spike.localScale.z);
        }
    }
}
