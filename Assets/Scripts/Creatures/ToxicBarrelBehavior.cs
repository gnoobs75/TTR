using UnityEngine;

/// <summary>
/// Toxic barrel: eye glow pulse, slime wobble, slow spin. Glows intensely on approach.
/// </summary>
public class ToxicBarrelBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.2f, 0.9f, 0.1f); // toxic green splash
    private Transform _skull;
    private Transform _slime;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Vector3 _originalScale;
    private float _glowIntensity;

    protected override void Start()
    {
        base.Start();
        _skull = CreatureAnimUtils.FindChildRecursive(transform, "skull");
        _slime = CreatureAnimUtils.FindChildRecursive(transform, "slime");
        if (_slime == null) _slime = CreatureAnimUtils.FindChildRecursive(transform, "ooze");
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _originalScale = transform.localScale;
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Subtle sway (stays in place, no drift)
        float sway = Mathf.Sin(Time.time * 0.8f) * 3f;
        transform.localRotation *= Quaternion.Euler(0, sway * Time.deltaTime, 0);

        // Eye glow pulse
        _glowIntensity = Mathf.Lerp(_glowIntensity, 0.5f + Mathf.Sin(t * 2f) * 0.3f, Time.deltaTime * 3f);
        ApplyGlow(_glowIntensity);

        // Slime drip wobble
        if (_slime != null)
        {
            float wobble = CreatureAnimUtils.OrganicWobble(t, 0.7f, 1.3f, 0.02f, 0.01f);
            _slime.localPosition = new Vector3(_slime.localPosition.x, wobble, _slime.localPosition.z);
        }

        // Skull blink
        if (_skull != null)
        {
            float blink = CreatureAnimUtils.BlinkPattern(t, 4f, 0.2f);
            float scale = 1f - blink * 0.3f; // eyes narrow slightly during blink
            _skull.localScale = new Vector3(_skull.localScale.x, scale, _skull.localScale.z);
        }

        // Breathing
        float breath = CreatureAnimUtils.BreathingScale(t, 0.8f, 0.015f);
        transform.localScale = _originalScale * breath;
    }

    protected override void DoReact()
    {
        float t = Time.time;

        // Intense glow
        _glowIntensity = Mathf.Lerp(_glowIntensity, 2f + Mathf.Sin(t * 8f) * 0.5f, Time.deltaTime * 6f);
        ApplyGlow(_glowIntensity);

        // Agitated wobble (stays in place)
        float wobble = Mathf.Sin(Time.time * 4f) * 8f;
        transform.localRotation *= Quaternion.Euler(wobble * Time.deltaTime, 0, wobble * 0.5f * Time.deltaTime);

        // Rapid blink
        if (_skull != null)
        {
            float blink = CreatureAnimUtils.RapidBlink(t, 6f);
            float scale = 1f - blink * 0.5f;
            _skull.localScale = new Vector3(_skull.localScale.x, scale, _skull.localScale.z);
        }

        // Warning beep
        float timeSinceReact = Time.time - _reactTime;
        if (timeSinceReact < 0.1f && TryPlaySound())
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayBarrelBeep();
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        StartCoroutine(BarrelSplashAnim());
    }

    private System.Collections.IEnumerator BarrelSplashAnim()
    {
        // Barrel rocks violently, glow spikes, toxic splash
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayBarrelSplash();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayBarrelSplash(transform.position);

        float startY = transform.localRotation.eulerAngles.y;

        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float p = t / 0.6f;
            // Rock back and forth with decay
            float rock = Mathf.Sin(t * 25f) * (1f - p) * 20f;
            transform.localRotation = Quaternion.Euler(rock, startY + t * 120f, rock * 0.5f);

            // Intense glow spike
            float glowPulse = 3f + Mathf.Sin(t * 20f) * 2f;
            ApplyGlow(glowPulse * (1f - p));
            yield return null;
        }

        ApplyGlow(0.5f);
    }

    void ApplyGlow(float intensity)
    {
        Color glow = new Color(0.2f, 1f, 0.1f) * intensity;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glow);
            r.SetPropertyBlock(_mpb);
        }
    }
}
