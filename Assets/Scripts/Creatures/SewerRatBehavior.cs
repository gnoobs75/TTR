using UnityEngine;

/// <summary>
/// Sewer rat: runs horizontally around the pipe circumference as a moving obstacle.
/// The rat orbits the pipe center, making it a dynamic threat the player must dodge.
/// Still has snout bob, ear twitch, whisker quiver. Pounces when hit.
/// </summary>
public class SewerRatBehavior : ObstacleBehavior
{
    [Header("Orbit Settings")]
    public float orbitSpeed = 100f; // degrees per second around the pipe
    public float spawnDistToCenter = 2.1f; // set by spawner (actual distance to pipe center)

    private Transform _snout;
    private Transform _leftEar;
    private Transform _rightEar;
    private Transform _tail;
    private Vector3 _originalScale;
    private float _flinchOffset;

    // Orbit state
    private Vector3 _orbitCenter;
    private Vector3 _orbitAxis;
    private Vector3 _initialOffset;
    private float _orbitAngle;
    private bool _orbitInitialized;

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

        // Compute orbit parameters from spawn orientation
        // transform.up points inward toward pipe center (set by spawner LookRotation)
        // transform.forward points along the pipe direction
        _orbitAxis = transform.forward;
        // Use actual spawn distance to center (set by spawner)
        _orbitCenter = transform.position + transform.up * spawnDistToCenter;
        _initialOffset = transform.position - _orbitCenter;
        _orbitAngle = 0f;
        _orbitInitialized = true;

        // Randomize orbit direction (CW or CCW)
        if (Random.value < 0.5f)
            orbitSpeed = -orbitSpeed;
    }

    protected override void Update()
    {
        // Always orbit regardless of player state
        if (_orbitInitialized)
        {
            _orbitAngle += orbitSpeed * Time.deltaTime;
            UpdateOrbitPosition();
        }

        base.Update();
    }

    void UpdateOrbitPosition()
    {
        // Rotate the initial offset around the pipe axis
        Vector3 newOffset = Quaternion.AngleAxis(_orbitAngle, _orbitAxis) * _initialOffset;
        transform.position = _orbitCenter + newOffset;

        // Orient rat: face the orbit tangent direction, feet on pipe surface
        Vector3 tangent = Vector3.Cross(_orbitAxis, newOffset).normalized;
        if (orbitSpeed < 0) tangent = -tangent; // face the direction we're running
        Vector3 inward = -newOffset.normalized;
        if (tangent.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(tangent, inward);
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

        // Running bob animation - rat bounces up and down while orbiting
        float runBob = Mathf.Abs(Mathf.Sin(t * 8f)) * 0.08f;
        transform.localScale = _originalScale * (1f + runBob * 0.3f);
    }

    protected override void DoReact()
    {
        float timeSinceReact = Time.time - _reactTime;

        // Speed up orbit when player approaches - rat panics and runs faster
        float panicMultiplier = 1.5f;
        _orbitAngle += orbitSpeed * panicMultiplier * Time.deltaTime;

        // Widen eyes
        float flinch = CreatureAnimUtils.FlinchDecay(timeSinceReact, 0.6f);
        float eyeScale = 1f + flinch * 0.4f;
        for (int i = 0; i < _pupils.Count; i++)
            _pupils[i].localScale = Vector3.one * eyeScale;

        // Rapid ear movement
        if (_leftEar != null)
            _leftEar.localRotation = Quaternion.Euler(CreatureAnimUtils.IdleFidget(Time.time, 6f, 15f), 0, 0);
        if (_rightEar != null)
            _rightEar.localRotation = Quaternion.Euler(CreatureAnimUtils.IdleFidget(Time.time + 0.3f, 7f, 15f), 0, 0);

        // Frantic tail
        if (_tail != null)
        {
            float sway = CreatureAnimUtils.OrganicWobble(Time.time, 4f, 6f, 25f, 15f);
            _tail.localRotation = Quaternion.Euler(0, sway, 0);
        }

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
        // Temporarily stop orbiting during pounce
        _orbitInitialized = false;

        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        Vector3 pounceTarget = Vector3.Lerp(startPos, player.position, 0.6f);

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayRatPounce();

        // Pounce forward
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = t / 0.25f;
            transform.position = Vector3.Lerp(startPos, pounceTarget, p);
            transform.localScale = startScale * (1f + p * 0.5f);
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        // Snap back and resume orbit from new position
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

        // Resume orbit
        _initialOffset = transform.position - _orbitCenter;
        _orbitAngle = 0f;
        _orbitInitialized = true;
    }
}
