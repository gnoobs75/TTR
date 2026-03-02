using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Poop blob: breathing squish, sad eye droop. Recoils and cries when approached.
/// </summary>
public class PoopBlobBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.5f, 0.3f, 0.1f); // brown squish
    public override bool SplatterOnHit => true; // poop splats!
    private Vector3 _originalScale;
    private List<Transform> _tears = new List<Transform>();
    private List<Transform> _flies = new List<Transform>();
    private float _squishPhase;

    protected override void Start()
    {
        base.Start();
        _originalScale = transform.localScale;
        _squishPhase = Random.value * Mathf.PI * 2f;
        CreatureAnimUtils.FindChildrenRecursive(transform, "tear", _tears);
        CreatureAnimUtils.FindChildrenRecursive(transform, "fly", _flies);
    }

    protected override void DoIdle()
    {
        float t = Time.time + _squishPhase;

        // Breathing squish - wider when breathing in, taller when breathing out
        float breathX = CreatureAnimUtils.BreathingScale(t, 0.9f, 0.06f);
        float breathY = CreatureAnimUtils.BreathingScale(t + 0.5f, 0.9f, 0.04f); // slightly offset
        transform.localScale = new Vector3(
            _originalScale.x * breathX,
            _originalScale.y * breathY,
            _originalScale.z * breathX
        );

        // Sad eye droop - eyes look downward slightly
        foreach (var pupil in _pupils)
        {
            float droop = CreatureAnimUtils.OrganicWobble(t, 0.3f, 0.7f, 0.01f, 0.005f);
            pupil.localPosition = new Vector3(pupil.localPosition.x, -0.02f + droop, pupil.localPosition.z);
        }

        // Flies buzzing
        AnimateFlies(t, 1f);
    }

    protected override void DoReact()
    {
        float t = Time.time;
        float timeSinceReact = t - _reactTime;
        float flinch = CreatureAnimUtils.FlinchDecay(timeSinceReact, 0.8f);

        // Recoil squish - flatten
        float squish = 1f - flinch * 0.25f;
        float stretch = 1f + flinch * 0.15f;
        transform.localScale = new Vector3(
            _originalScale.x * stretch,
            _originalScale.y * squish,
            _originalScale.z * stretch
        );

        // Tears scale up when scared
        float tearScale = 1f + flinch * 2f;
        foreach (var tear in _tears)
        {
            if (tear != null)
                tear.localScale = Vector3.one * tearScale;
        }

        // Eyes widen in fear
        foreach (var pupil in _pupils)
        {
            float widen = 1f + flinch * 0.5f;
            pupil.localScale = Vector3.one * widen;
        }

        // Faster fly buzzing (panicked)
        AnimateFlies(t, 3f);

        // Sad groan
        if (flinch > 0.8f && TryPlaySound())
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayBlobGroan();
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        StartCoroutine(BlobSplatAnim());
    }

    private System.Collections.IEnumerator BlobSplatAnim()
    {
        // Blob splatters - flattens dramatically, tears fly out
        Vector3 startScale = _originalScale;

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayBlobSquish();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayBlobSquish(transform.position);

        // Splat - flatten hard
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float p = t / 0.2f;
            transform.localScale = new Vector3(
                startScale.x * (1f + p * 0.6f),
                startScale.y * (1f - p * 0.5f),
                startScale.z * (1f + p * 0.6f)
            );
            // Tears scale huge
            foreach (var tear in _tears)
                if (tear != null) tear.localScale = Vector3.one * (1f + p * 4f);
            yield return null;
        }

        // Hold splat
        yield return Wait04;

        // Reform slowly
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float p = t / 0.5f;
            transform.localScale = Vector3.Lerp(
                new Vector3(startScale.x * 1.6f, startScale.y * 0.5f, startScale.z * 1.6f),
                startScale, p);
            foreach (var tear in _tears)
                if (tear != null) tear.localScale = Vector3.Lerp(Vector3.one * 5f, Vector3.one, p);
            yield return null;
        }

        _originalScale = startScale;
        transform.localScale = startScale;
    }

    void AnimateFlies(float t, float speedMult)
    {
        for (int i = 0; i < _flies.Count; i++)
        {
            if (_flies[i] == null) continue;
            float offset = i * 1.256f; // golden angle spacing
            float radius = 0.3f + Mathf.Sin(t * 2f * speedMult + offset) * 0.1f;
            float angle = t * 3f * speedMult + offset;
            float height = Mathf.Sin(t * 4f * speedMult + offset * 2f) * 0.15f;
            _flies[i].localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                0.5f + height,
                Mathf.Sin(angle) * radius
            );
        }
    }
}
