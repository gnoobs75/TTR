using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hair wad: strand sway, drip wobble. Compresses and squelches when approached.
/// </summary>
public class HairWadBehavior : ObstacleBehavior
{
    private List<Transform> _strands = new List<Transform>();
    private Transform _mouth;
    private Vector3 _originalScale;
    private float _wobblePhase;

    protected override void Start()
    {
        base.Start();
        CreatureAnimUtils.FindChildrenRecursive(transform, "strand", _strands);
        if (_strands.Count == 0)
            CreatureAnimUtils.FindChildrenRecursive(transform, "hair", _strands);
        _mouth = CreatureAnimUtils.FindChildRecursive(transform, "mouth");
        _originalScale = transform.localScale;
        _wobblePhase = Random.value * Mathf.PI * 2f;
    }

    protected override void DoIdle()
    {
        float t = Time.time + _wobblePhase;

        // Strand sway - each strand sways independently
        for (int i = 0; i < _strands.Count; i++)
        {
            float freq = 0.6f + i * 0.15f;
            float sway = CreatureAnimUtils.OrganicWobble(t, freq, freq * 1.6f, 8f, 4f);
            _strands[i].localRotation = Quaternion.Euler(sway, sway * 0.3f, sway * 0.5f);
        }

        // Drip wobble (whole body jiggles like gelatin)
        float jiggleX = CreatureAnimUtils.OrganicWobble(t, 1.1f, 1.7f, 0.015f, 0.008f);
        float jiggleZ = CreatureAnimUtils.OrganicWobble(t + 1f, 0.9f, 1.5f, 0.015f, 0.008f);
        transform.localPosition += new Vector3(jiggleX, 0, jiggleZ) * Time.deltaTime;

        // Mouth open/close slowly (breathing)
        if (_mouth != null)
        {
            float breathScale = CreatureAnimUtils.BreathingScale(t, 0.7f, 0.1f);
            _mouth.localScale = new Vector3(_mouth.localScale.x, breathScale, _mouth.localScale.z);
        }

        // Breathing
        float breath = CreatureAnimUtils.BreathingScale(t, 0.8f, 0.03f);
        transform.localScale = _originalScale * breath;
    }

    protected override void DoReact()
    {
        float t = Time.time;
        float timeSinceReact = t - _reactTime;
        float flinch = CreatureAnimUtils.FlinchDecay(timeSinceReact, 0.6f);

        // Compress (squish down, widen)
        float squish = 1f - flinch * 0.2f;
        float spread = 1f + flinch * 0.15f;
        transform.localScale = new Vector3(
            _originalScale.x * spread,
            _originalScale.y * squish,
            _originalScale.z * spread
        );

        // Mouth opens wide in scream
        if (_mouth != null)
        {
            float screamScale = 1f + flinch * 0.8f;
            _mouth.localScale = new Vector3(_mouth.localScale.x, screamScale, _mouth.localScale.z);
        }

        // Strands go rigid (stick out)
        for (int i = 0; i < _strands.Count; i++)
        {
            float rigid = flinch * 15f;
            float angle = (i / (float)Mathf.Max(1, _strands.Count)) * 360f;
            _strands[i].localRotation = Quaternion.Euler(rigid, angle, 0);
        }

        // Eyes widen
        float eyeScale = 1f + flinch * 0.4f;
        for (int i = 0; i < _pupils.Count; i++)
            _pupils[i].localScale = Vector3.one * eyeScale;

        // Wet squelch sound
        if (flinch > 0.8f && TryPlaySound())
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayBlobGroan();
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        StartCoroutine(HairWrapAnim(player));
    }

    private System.Collections.IEnumerator HairWrapAnim(Transform player)
    {
        // Hair wad wraps around player, strands reach out
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayHairWrap();

        // Stretch strands toward player
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float p = t / 0.4f;
            // Flatten and spread
            transform.localScale = new Vector3(
                startScale.x * (1f + p * 0.4f),
                startScale.y * (1f - p * 0.3f),
                startScale.z * (1f + p * 0.4f)
            );
            // Strands reach toward player
            for (int i = 0; i < _strands.Count; i++)
            {
                Vector3 dir = (player.position - _strands[i].position).normalized;
                _strands[i].localRotation = Quaternion.Euler(
                    dir.y * 30f * p, dir.x * 30f * p, 0);
            }
            yield return null;
        }

        // Hold wrapped
        yield return new WaitForSeconds(0.5f);

        // Release - spring back
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float p = t / 0.3f;
            transform.localScale = Vector3.Lerp(
                new Vector3(startScale.x * 1.4f, startScale.y * 0.7f, startScale.z * 1.4f),
                startScale, p);
            yield return null;
        }

        transform.localScale = startScale;
    }
}
