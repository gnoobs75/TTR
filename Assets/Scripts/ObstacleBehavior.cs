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

        // Stay squashed until destroyed
        yield return new WaitForSeconds(1f);
    }
}
