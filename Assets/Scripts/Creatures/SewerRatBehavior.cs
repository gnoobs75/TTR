using UnityEngine;

/// <summary>
/// Sewer rat: snout bob, ear twitch, whisker quiver. Flinches on approach.
/// </summary>
public class SewerRatBehavior : ObstacleBehavior
{
    private Transform _snout;
    private Transform _leftEar;
    private Transform _rightEar;
    private Transform _tail;
    private Vector3 _originalScale;
    private float _flinchOffset;

    protected override void Start()
    {
        base.Start();
        _snout = CreatureAnimUtils.FindChildRecursive(transform, "snout");
        _leftEar = CreatureAnimUtils.FindChildRecursive(transform, "leftear");
        if (_leftEar == null) _leftEar = CreatureAnimUtils.FindChildRecursive(transform, "ear_l");
        _rightEar = CreatureAnimUtils.FindChildRecursive(transform, "rightear");
        if (_rightEar == null) _rightEar = CreatureAnimUtils.FindChildRecursive(transform, "ear_r");
        _tail = CreatureAnimUtils.FindChildRecursive(transform, "tail");
        _originalScale = transform.localScale;
    }

    protected override void DoIdle()
    {
        float t = Time.time;

        // Snout bob
        if (_snout != null)
        {
            float bob = CreatureAnimUtils.OrganicWobble(t, 2.5f, 3.8f, 0.02f, 0.01f);
            _snout.localPosition = new Vector3(_snout.localPosition.x, bob, _snout.localPosition.z);
        }

        // Ear twitches (alternating)
        if (_leftEar != null)
        {
            float twitch = CreatureAnimUtils.IdleFidget(t, 1.7f, 8f);
            _leftEar.localRotation = Quaternion.Euler(twitch, 0, 0);
        }
        if (_rightEar != null)
        {
            float twitch = CreatureAnimUtils.IdleFidget(t + 0.5f, 2.1f, 8f);
            _rightEar.localRotation = Quaternion.Euler(twitch, 0, 0);
        }

        // Tail sway
        if (_tail != null)
        {
            float sway = CreatureAnimUtils.OrganicWobble(t, 0.8f, 1.5f, 15f, 8f);
            _tail.localRotation = Quaternion.Euler(0, sway, 0);
        }

        // Breathing
        float breath = CreatureAnimUtils.BreathingScale(t, 1.5f, 0.03f);
        transform.localScale = _originalScale * breath;
    }

    protected override void DoReact()
    {
        float timeSinceReact = Time.time - _reactTime;

        // Flinch backward
        float flinch = CreatureAnimUtils.FlinchDecay(timeSinceReact, 0.6f);
        if (_player != null)
        {
            Vector3 awayDir = (transform.position - _player.position).normalized;
            _flinchOffset = Mathf.Lerp(_flinchOffset, flinch * 0.3f, Time.deltaTime * 8f);
            transform.position += awayDir * _flinchOffset * Time.deltaTime;
        }

        // Widen eyes (scale pupils bigger)
        float eyeScale = 1f + flinch * 0.4f;
        for (int i = 0; i < _pupils.Count; i++)
            _pupils[i].localScale = Vector3.one * eyeScale;

        // Rapid ear movement
        if (_leftEar != null)
            _leftEar.localRotation = Quaternion.Euler(CreatureAnimUtils.IdleFidget(Time.time, 6f, 15f), 0, 0);
        if (_rightEar != null)
            _rightEar.localRotation = Quaternion.Euler(CreatureAnimUtils.IdleFidget(Time.time + 0.3f, 7f, 15f), 0, 0);

        // Squeak sound
        if (flinch > 0.8f && TryPlaySound())
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayRatSqueak();
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        StartCoroutine(RatPounceAnim(player));
    }

    private System.Collections.IEnumerator RatPounceAnim(Transform player)
    {
        // Rat pounces toward player, scales up, then snaps back
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        Vector3 pounceTarget = Vector3.Lerp(startPos, player.position, 0.6f);

        // Play pounce sound
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayRatPounce();

        // Pounce forward
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = t / 0.25f;
            transform.position = Vector3.Lerp(startPos, pounceTarget, p);
            transform.localScale = startScale * (1f + p * 0.5f); // grow bigger
            yield return null;
        }

        // Hold on player briefly
        yield return new WaitForSeconds(0.3f);

        // Snap back
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float p = t / 0.3f;
            transform.position = Vector3.Lerp(pounceTarget, startPos, p);
            transform.localScale = Vector3.Lerp(startScale * 1.5f, startScale, p);
            yield return null;
        }

        transform.position = startPos;
        transform.localScale = startScale;
    }
}
