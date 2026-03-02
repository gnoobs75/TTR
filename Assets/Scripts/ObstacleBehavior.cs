using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Base class for creature/obstacle behaviors.
/// Handles player proximity detection, idle/react state machine, eye tracking.
/// Subclasses override DoIdle() and DoReact() for per-type personality.
/// </summary>
public abstract class ObstacleBehavior : MonoBehaviour
{
    [Header("Behavior Settings")]
    public float nearbyRange = 15f;
    public float reactRange = 8f;
    public float soundCooldown = 6f;
    [Tooltip("Rotate to face the player when nearby")]
    public bool facePlayer = true;

    /// <summary>Color used for screen flash when this obstacle hits the player.</summary>
    public virtual Color HitFlashColor => new Color(1f, 0.15f, 0.05f); // default red

    /// <summary>Whether this creature triggers a screen splatter on hit (messy/explosive types).</summary>
    public virtual bool SplatterOnHit => false;

    protected Transform _player;
    protected bool _playerNearby;
    protected bool _playerApproaching;
    protected float _distSqr;
    protected float _lastSoundTime = -10f;
    protected int _frameSkip;

    // Eye tracking
    protected List<Transform> _pupils = new List<Transform>();
    protected List<Transform> _eyes = new List<Transform>();

    // Reaction state
    protected float _reactTime = -1f;
    protected bool _hasReacted;

    // Blink animation
    private float _nextBlinkTime;
    private float _blinkTimer;
    private bool _isBlinking;
    private static readonly float BLINK_DURATION = 0.1f;

    // Cached WaitForSeconds (shared across all creature behaviors to avoid GC)
    protected static readonly WaitForSeconds Wait01 = new WaitForSeconds(0.1f);
    protected static readonly WaitForSeconds Wait015 = new WaitForSeconds(0.15f);
    protected static readonly WaitForSeconds Wait02 = new WaitForSeconds(0.2f);
    protected static readonly WaitForSeconds Wait03 = new WaitForSeconds(0.3f);
    protected static readonly WaitForSeconds Wait04 = new WaitForSeconds(0.4f);
    protected static readonly WaitForSeconds Wait05 = new WaitForSeconds(0.5f);
    protected static readonly WaitForSeconds Wait08 = new WaitForSeconds(0.8f);

    protected virtual void Start()
    {
        // Find player
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            _player = GameManager.Instance.player.transform;

        // Find eye parts for tracking
        CreatureAnimUtils.FindChildrenRecursive(transform, "pupil", _pupils);
        CreatureAnimUtils.FindChildrenRecursive(transform, "eye", _eyes);

        // Stagger frame skip so not all creatures update same frame
        _frameSkip = Random.Range(0, 3);

        // Random first blink time
        _nextBlinkTime = Time.time + Random.Range(1f, 4f);
    }

    protected virtual void Update()
    {
        if (_player == null)
        {
            // Try to find player if it was null at Start
            if (GameManager.Instance != null && GameManager.Instance.player != null)
                _player = GameManager.Instance.player.transform;
            else
                return;
        }

        // Performance: skip expensive updates every 3rd frame when far
        _distSqr = (transform.position - _player.position).sqrMagnitude;
        bool wasFar = _distSqr > nearbyRange * nearbyRange * 4f;
        if (wasFar && (Time.frameCount + _frameSkip) % 3 != 0)
            return;

        _playerNearby = _distSqr < nearbyRange * nearbyRange;
        _playerApproaching = _distSqr < reactRange * reactRange;

        // Eye tracking - always do when nearby (it's cheap)
        if (_playerNearby)
        {
            UpdateEyeTracking();
            if (facePlayer)
                FacePlayer();
        }

        // State machine
        if (_playerApproaching)
        {
            if (!_hasReacted)
            {
                _hasReacted = true;
                _reactTime = Time.time;
#if UNITY_EDITOR
                Debug.Log($"[REACT] {gameObject.name} entering REACT state at dist={Mathf.Sqrt(_distSqr):F1}m");
#endif
            }
            DoReact();
        }
        else if (_playerNearby)
        {
            DoIdle();
        }
        else
        {
            DoDistantIdle();
        }
    }

    protected virtual void UpdateEyeTracking()
    {
        if (_player == null) return;
        Vector3 target = _player.position;

        // Track each pupil toward player
        for (int i = 0; i < _pupils.Count; i++)
        {
            Transform eye = (i < _eyes.Count) ? _eyes[i] : _pupils[i].parent;
            CreatureAnimUtils.TrackEyeTarget(_pupils[i], eye, target, 0.08f);
        }

        // Periodic blink: briefly squash eyes on Y axis
        UpdateBlink();
    }

    private void UpdateBlink()
    {
        if (_eyes.Count == 0) return;

        if (_isBlinking)
        {
            _blinkTimer += Time.deltaTime;
            // Close eyes (squash Y to near-zero, then reopen)
            float blinkT = _blinkTimer / BLINK_DURATION;
            float eyeScaleY;
            if (blinkT < 0.5f)
                eyeScaleY = Mathf.Lerp(1f, 0.05f, blinkT * 2f); // closing
            else
                eyeScaleY = Mathf.Lerp(0.05f, 1f, (blinkT - 0.5f) * 2f); // opening

            foreach (var eye in _eyes)
            {
                if (eye == null) continue;
                Vector3 s = eye.localScale;
                eye.localScale = new Vector3(s.x, eyeScaleY * Mathf.Abs(s.x), s.z);
            }

            if (_blinkTimer >= BLINK_DURATION)
            {
                _isBlinking = false;
                // Restore normal eye scale
                foreach (var eye in _eyes)
                {
                    if (eye == null) continue;
                    Vector3 s = eye.localScale;
                    eye.localScale = new Vector3(s.x, Mathf.Abs(s.x), s.z);
                }
                _nextBlinkTime = Time.time + Random.Range(2f, 5f);
            }
        }
        else if (Time.time >= _nextBlinkTime)
        {
            _isBlinking = true;
            _blinkTimer = 0f;
        }
    }

    /// <summary>Rotate body to face the player while preserving pipe surface up.</summary>
    protected virtual void FacePlayer()
    {
        if (_player == null) return;
        Vector3 toPlayer = _player.position - transform.position;
        if (toPlayer.sqrMagnitude < 0.01f) return;
        Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized, transform.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
    }

    protected bool TryPlaySound()
    {
        if (Time.time - _lastSoundTime < soundCooldown) return false;
        _lastSoundTime = Time.time;
        return true;
    }

    /// <summary>Called when player is far away (>nearbyRange). Minimal animation.</summary>
    protected virtual void DoDistantIdle()
    {
        // Base: subtle breathing only
        float breath = CreatureAnimUtils.BreathingScale(Time.time + _frameSkip, 0.6f, 0.02f);
        transform.localScale = Vector3.one * breath;
    }

    /// <summary>Called when player is nearby (<nearbyRange) but not approaching.</summary>
    protected abstract void DoIdle();

    /// <summary>Called when player is approaching (<reactRange).</summary>
    protected abstract void DoReact();

    /// <summary>Called when player actually hits this obstacle. Play unique hit animation/sound.</summary>
    public virtual void OnPlayerHit(Transform player) { }

    /// <summary>Called when this obstacle is returned to the pool. Reset all state for reuse.</summary>
    public virtual void OnPoolReset()
    {
        _playerNearby = false;
        _playerApproaching = false;
        _hasReacted = false;
        _reactTime = -1f;
        _isBlinking = false;
        _lastSoundTime = -10f;
        StopAllCoroutines();
    }

    /// <summary>Called when player stomps this obstacle from a jump. Squash it!</summary>
    public virtual void OnStomped(Transform player)
    {
        // Default: flatten and make a gross face
        StartCoroutine(DefaultStompAnim());
    }

    private System.Collections.IEnumerator DefaultStompAnim()
    {
        Vector3 startScale = transform.localScale;
        float t = 0f;

        // Quick squash flat
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float p = t / 0.15f;
            transform.localScale = new Vector3(
                startScale.x * (1f + p * 0.8f),  // spread wide
                startScale.y * (1f - p * 0.7f),   // flatten
                startScale.z * (1f + p * 0.8f)
            );
            yield return null;
        }

        // Widen eyes in shock
        foreach (var pupil in _pupils)
            if (pupil != null) pupil.localScale = Vector3.one * 2f;

        // Spring-bounce recovery: brief upward pop before settling
        Vector3 flatScale = transform.localScale;
        t = 0f;
        float bounceDur = 0.15f;
        while (t < bounceDur)
        {
            t += Time.deltaTime;
            float p = t / bounceDur;
            // Elastic overshoot: pops up then settles back flat
            float bounce = Mathf.Sin(p * Mathf.PI) * 0.4f;
            transform.localScale = new Vector3(
                flatScale.x * (1f - bounce * 0.3f),
                flatScale.y + startScale.y * bounce,
                flatScale.z * (1f - bounce * 0.3f)
            );
            yield return null;
        }
        transform.localScale = flatScale;

        // Stay squashed until destroyed
        yield return Wait08;
    }
}
