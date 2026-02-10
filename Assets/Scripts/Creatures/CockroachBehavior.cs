using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Cockroach: antenna wiggle, leg twitch. Scatter jolt on approach.
/// </summary>
public class CockroachBehavior : ObstacleBehavior
{
    private List<Transform> _antennae = new List<Transform>();
    private List<Transform> _legs = new List<Transform>();
    private List<Transform> _mandibles = new List<Transform>();
    private Vector3 _originalScale;
    private Vector3 _joltDir;
    private float _joltDecay;

    protected override void Start()
    {
        base.Start();
        CreatureAnimUtils.FindChildrenRecursive(transform, "antenna", _antennae);
        CreatureAnimUtils.FindChildrenRecursive(transform, "leg", _legs);
        CreatureAnimUtils.FindChildrenRecursive(transform, "mandible", _mandibles);
        _originalScale = transform.localScale;
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Antenna wiggle - independent frequencies for each
        for (int i = 0; i < _antennae.Count; i++)
        {
            float freq = 2.5f + i * 0.7f;
            float wobble = CreatureAnimUtils.OrganicWobble(t, freq, freq * 1.3f, 12f, 6f);
            _antennae[i].localRotation = Quaternion.Euler(wobble, wobble * 0.5f, 0);
        }

        // Leg twitches - subtle
        for (int i = 0; i < _legs.Count; i++)
        {
            float offset = i * 0.4f;
            float twitch = CreatureAnimUtils.IdleFidget(t + offset, 1.8f + i * 0.3f, 5f);
            _legs[i].localRotation = Quaternion.Euler(twitch, 0, 0);
        }

        // Mandible chewing
        for (int i = 0; i < _mandibles.Count; i++)
        {
            float chew = CreatureAnimUtils.IdleFidget(t, 3f, 8f);
            float sign = (i % 2 == 0) ? 1f : -1f;
            _mandibles[i].localRotation = Quaternion.Euler(0, chew * sign, 0);
        }

        // Subtle body vibration
        float vib = CreatureAnimUtils.BreathingScale(t, 4f, 0.005f);
        transform.localScale = _originalScale * vib;
    }

    protected override void DoReact()
    {
        float t = Time.time;
        float timeSinceReact = t - _reactTime;
        float flinch = CreatureAnimUtils.FlinchDecay(timeSinceReact, 0.4f);

        // Scatter jolt - quick sideways movement
        if (flinch > 0.9f && _joltDir == Vector3.zero)
        {
            _joltDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        }
        _joltDecay = Mathf.Lerp(_joltDecay, 0f, Time.deltaTime * 4f);
        if (flinch > 0.5f) _joltDecay = flinch * 0.15f;
        transform.position += _joltDir * _joltDecay * Time.deltaTime;

        // Legs splay out frantically
        for (int i = 0; i < _legs.Count; i++)
        {
            float panic = CreatureAnimUtils.IdleFidget(t + i * 0.2f, 8f + i, 20f);
            _legs[i].localRotation = Quaternion.Euler(panic, panic * 0.3f, 0);
        }

        // Antenna panic
        for (int i = 0; i < _antennae.Count; i++)
        {
            float panic = CreatureAnimUtils.IdleFidget(t, 10f, 25f);
            _antennae[i].localRotation = Quaternion.Euler(panic, panic, 0);
        }

        // Eyes widen (oversized already, pulse bigger)
        float eyeScale = 1f + flinch * 0.3f;
        for (int i = 0; i < _pupils.Count; i++)
            _pupils[i].localScale = Vector3.one * eyeScale;

        // Hiss
        if (flinch > 0.8f && TryPlaySound())
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayRoachHiss();
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        StartCoroutine(RoachScurryAnim(player));
    }

    private System.Collections.IEnumerator RoachScurryAnim(Transform player)
    {
        // Roach scurries in panic circle, legs flail
        Vector3 startPos = transform.position;

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayRoachScurry();

        // Frantic circular movement
        float t = 0f;
        float circleSpeed = 12f;
        float circleRadius = 0.3f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float angle = t * circleSpeed;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * circleRadius;
            transform.position = startPos + offset;

            // Frantic legs
            for (int i = 0; i < _legs.Count; i++)
            {
                float panic = CreatureAnimUtils.IdleFidget(Time.time + i * 0.15f, 12f, 30f);
                _legs[i].localRotation = Quaternion.Euler(panic, panic * 0.5f, 0);
            }
            // Antenna flail
            for (int i = 0; i < _antennae.Count; i++)
            {
                float flail = CreatureAnimUtils.IdleFidget(Time.time + i, 15f, 35f);
                _antennae[i].localRotation = Quaternion.Euler(flail, flail, 0);
            }
            yield return null;
        }

        // Settle back
        t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, startPos, t / 0.2f);
            yield return null;
        }
        transform.position = startPos;
    }

    protected override void DoDistantIdle()
    {
        // Reset jolt direction for next approach
        _joltDir = Vector3.zero;
        base.DoDistantIdle();
    }
}
