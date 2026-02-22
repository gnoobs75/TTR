using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Cockroach: antenna wiggle, leg twitch. EXPLODES on hit and slows the player.
/// Guts and shell fragments scatter everywhere. Satisfyingly disgusting.
/// </summary>
public class CockroachBehavior : ObstacleBehavior
{
    public override Color HitFlashColor => new Color(0.4f, 0.2f, 0f); // roach guts brown
    private List<Transform> _antennae = new List<Transform>();
    private List<Transform> _legs = new List<Transform>();
    private List<Transform> _mandibles = new List<Transform>();
    private Vector3 _originalScale;
    private Vector3 _joltDir;
    private float _joltDecay;
    private bool _exploded;

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
        if (_exploded) return;
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
        if (_exploded) return;
        float t = Time.time;
        float timeSinceReact = t - _reactTime;
        float flinch = CreatureAnimUtils.FlinchDecay(timeSinceReact, 0.4f);

        // Scatter jolt - quick sideways movement (constrained to local right/forward to stay in pipe)
        if (flinch > 0.9f && _joltDir == Vector3.zero)
        {
            _joltDir = (transform.right * Random.Range(-1f, 1f) + transform.forward * Random.Range(-0.5f, 0.5f)).normalized;
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

        // Eyes widen
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
        if (_exploded) return;
        _exploded = true;
        StartCoroutine(ExplodeAnim());
    }

    private System.Collections.IEnumerator ExplodeAnim()
    {
        // Play explosion effects
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayCelebration(transform.position); // burst effect
        }

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayRoachScurry();

        // Scatter all child parts outward as "guts"
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        Vector3 center = transform.position;
        Vector3[] flyDirs = new Vector3[allRenderers.Length];
        Vector3[] flyStarts = new Vector3[allRenderers.Length];
        float[] spinSpeeds = new float[allRenderers.Length];

        for (int i = 0; i < allRenderers.Length; i++)
        {
            flyStarts[i] = allRenderers[i].transform.position;
            // Random outward direction for each piece
            flyDirs[i] = (allRenderers[i].transform.position - center).normalized;
            if (flyDirs[i].sqrMagnitude < 0.01f)
                flyDirs[i] = Random.onUnitSphere;
            flyDirs[i] += Random.insideUnitSphere * 0.5f;
            flyDirs[i].Normalize();
            spinSpeeds[i] = Random.Range(360f, 1080f);

            // Make the piece emissive green for a gross glow
            if (allRenderers[i].material != null)
            {
                allRenderers[i].material.EnableKeyword("_EMISSION");
                allRenderers[i].material.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 0.1f) * 3f);
            }
        }

        // Disable collider so we don't double-hit
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Fly apart animation
        float t = 0f;
        float duration = 0.6f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;

            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] == null) continue;

                // Fly outward with gravity
                float dist = p * 3f;
                float gravity = p * p * 2f;
                allRenderers[i].transform.position = flyStarts[i]
                    + flyDirs[i] * dist
                    - Vector3.up * gravity;

                // Spin wildly
                allRenderers[i].transform.Rotate(spinSpeeds[i] * Time.deltaTime, spinSpeeds[i] * 0.7f * Time.deltaTime, 0);

                // Shrink as they fly
                float shrink = 1f - p * 0.7f;
                allRenderers[i].transform.localScale *= (1f - Time.deltaTime * 2f);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    public override void OnStomped(Transform player)
    {
        // Stomp also triggers explosion
        if (!_exploded)
        {
            _exploded = true;
            StartCoroutine(ExplodeAnim());
        }
    }

    protected override void DoDistantIdle()
    {
        if (_exploded) return;
        _joltDir = Vector3.zero;
        base.DoDistantIdle();
    }
}
