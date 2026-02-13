using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AI racer for the Brown Town Grand Prix.
/// Each racer has a unique personality affecting speed, steering, and behavior.
/// Follows the pipe path, rubber-bands to the pack, stumbles on obstacles.
/// </summary>
public class RacerAI : MonoBehaviour
{
    [Header("Identity")]
    public string racerName = "Racer";
    public Color racerColor = Color.white;
    public int racerIndex; // 0-3 for AI, player is tracked separately

    [Header("Movement")]
    public float baseSpeed = 7f;
    public float maxSpeed = 13f;
    public float acceleration = 2f;

    [Header("Steering")]
    public float steerSpeed = 2f;
    public float steerChangeInterval = 1.5f;
    public float steerAggressiveness = 1f; // how sharply they steer

    [Header("Personality")]
    [Range(0f, 1f)] public float consistency = 0.5f;    // 1 = steady, 0 = erratic
    [Range(0f, 1f)] public float aggression = 0.5f;     // affects rubber-band behavior
    [Range(0f, 1f)] public float stumbleChance = 0.02f;  // chance per obstacle encounter

    [Header("Rubber Banding")]
    public float catchUpMultiplier = 1.25f;
    public float slowDownMultiplier = 0.82f;
    public float rubberBandDistance = 12f;

    [Header("Pipe")]
    public float pipeRadius = 3f;
    public PipeGenerator pipeGen;

    [Header("Obstacle Dodge")]
    public float dodgeLookAhead = 6f;
    public float dodgeSteerStrength = 3f;

    // State
    private float _distanceAlongPath = 0f;
    private float _currentAngle = 270f;
    private float _currentSpeed;
    private float _targetSteer;
    private float _steerInput;
    private float _nextSteerChange;
    private float _angularVelocity;
    private bool _finished;
    private float _finishTime;

    // Stumble
    private bool _stumbling;
    private float _stumbleTimer;
    private float _stumbleSpeedMult = 1f;
    private const float STUMBLE_DURATION = 1.2f;

    // Burst (personality-driven speed surges)
    private float _burstTimer;
    private float _burstSpeedMult = 1f;

    // Slither animation
    private TurdSlither _slither;

    // Eye tracking
    private List<Transform> _pupils = new List<Transform>();
    private List<Transform> _eyes = new List<Transform>();

    // Public accessors
    public float DistanceTraveled => _distanceAlongPath;
    public bool IsFinished => _finished;
    public float FinishTime => _finishTime;
    public float CurrentSpeed => _currentSpeed;

    void Start()
    {
        _currentSpeed = baseSpeed * 0.5f; // start slow, accelerate
        if (pipeGen == null)
            pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        _slither = GetComponent<TurdSlither>();
        _nextSteerChange = Time.time + Random.Range(0.5f, steerChangeInterval);

        // Find eye parts for tracking
        CreatureAnimUtils.FindChildrenRecursive(transform, "pupil", _pupils);
        CreatureAnimUtils.FindChildrenRecursive(transform, "eye", _eyes);

        // Stagger starting angle slightly per racer
        _currentAngle = 270f + (racerIndex - 1.5f) * 15f;
    }

    void Update()
    {
        if (_finished) return;
        if (pipeGen == null) return;

        // Don't move until race starts
        var rm = RaceManager.Instance;
        if (rm == null || rm.RaceState != RaceManager.State.Racing) return;

        float dt = Time.deltaTime;

        // === STEERING ===
        if (Time.time > _nextSteerChange)
        {
            // Personality: consistent racers make smaller steer changes
            float range = Mathf.Lerp(1f, 0.4f, consistency);
            _targetSteer = Random.Range(-range, range) * steerAggressiveness;
            float interval = Mathf.Lerp(0.6f, steerChangeInterval * 2f, consistency);
            _nextSteerChange = Time.time + Random.Range(interval * 0.5f, interval);
        }

        // Obstacle dodge
        float dodgeSteer = 0f;
        Collider[] nearby = Physics.OverlapSphere(
            transform.position + transform.forward * dodgeLookAhead, 2f);
        foreach (var col in nearby)
        {
            if (col == null) continue;
            if (col.CompareTag("Obstacle") && col.transform.root != transform)
            {
                Vector3 toObs = col.transform.position - transform.position;
                Vector3 localObs = transform.InverseTransformDirection(toObs);
                dodgeSteer = localObs.x > 0 ? -dodgeSteerStrength : dodgeSteerStrength;

                // Stumble check: personality-based chance to hit obstacle
                if (!_stumbling && Random.value < stumbleChance)
                    TriggerStumble();
                break;
            }
        }

        float combinedSteer = _targetSteer + dodgeSteer;
        _steerInput = Mathf.Lerp(_steerInput, combinedSteer, dt * 3f);
        _angularVelocity = Mathf.Lerp(_angularVelocity, _steerInput * steerSpeed * 40f, dt * 4f);
        _currentAngle += _angularVelocity * dt;

        // Gravity pull toward bottom (270°)
        float angleDelta = Mathf.DeltaAngle(_currentAngle, 270f);
        _angularVelocity += angleDelta * 0.08f * dt;

        // === SPEED ===
        float targetSpeed = baseSpeed + (_distanceAlongPath * 0.008f);

        // Rubber-band to pack leader
        if (rm != null)
        {
            float leaderDist = rm.LeaderDistance;
            float myDist = _distanceAlongPath;
            float diff = leaderDist - myDist;

            if (diff > rubberBandDistance)
                targetSpeed *= catchUpMultiplier;
            else if (diff < -rubberBandDistance * 0.5f)
                targetSpeed *= slowDownMultiplier;

            // Aggressive racers push harder when behind
            if (diff > 5f)
                targetSpeed *= 1f + aggression * 0.15f;
        }

        targetSpeed = Mathf.Clamp(targetSpeed, baseSpeed * 0.4f, maxSpeed);

        // Personality bursts (erratic racers get speed surges)
        UpdateBurst(dt);
        targetSpeed *= _burstSpeedMult;

        // Stumble slowdown
        UpdateStumble(dt);
        targetSpeed *= _stumbleSpeedMult;

        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, dt * acceleration);
        _distanceAlongPath += _currentSpeed * dt;

        // === POSITION ON PIPE ===
        Vector3 center, forward, right, up;
        pipeGen.GetPathFrame(_distanceAlongPath, out center, out forward, out right, out up);

        float rad = _currentAngle * Mathf.Deg2Rad;
        Vector3 offset = (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * pipeRadius;
        Vector3 targetPos = center + offset;

        // Stumble wobble
        if (_stumbling)
        {
            float wobble = Mathf.Sin(Time.time * 25f) * 0.3f * (1f - _stumbleTimer / STUMBLE_DURATION);
            targetPos += right * wobble;
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, dt * 6f);

        // Face forward with surface roll
        float surfaceAngle = _currentAngle - 90f;
        Quaternion pathRot = Quaternion.LookRotation(forward, up);
        Quaternion surfaceRoll = Quaternion.Euler(0, 0, surfaceAngle);
        Quaternion targetRot = pathRot * surfaceRoll;

        // Stumble tilt
        if (_stumbling)
        {
            float tilt = Mathf.Sin(Time.time * 15f) * 15f * (1f - _stumbleTimer / STUMBLE_DURATION);
            targetRot *= Quaternion.Euler(tilt, 0, tilt * 0.5f);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 5f);

        // === SLITHER ANIMATION ===
        if (_slither != null)
        {
            _slither.currentSpeed = _currentSpeed / baseSpeed;
            _slither.turnInput = _steerInput;
        }

        // === EYE TRACKING (look at nearest racer or player) ===
        if (rm != null && rm.PlayerController != null)
        {
            Vector3 lookTarget = rm.PlayerController.transform.position;
            for (int i = 0; i < _pupils.Count; i++)
            {
                Transform eye = (i < _eyes.Count) ? _eyes[i] : _pupils[i].parent;
                if (eye != null && _pupils[i] != null)
                    CreatureAnimUtils.TrackEyeTarget(_pupils[i], eye, lookTarget, 0.06f);
            }
        }
    }

    void UpdateStumble(float dt)
    {
        if (!_stumbling) return;
        _stumbleTimer += dt;
        float t = _stumbleTimer / STUMBLE_DURATION;
        // Quick slowdown, gradual recovery
        if (t < 0.2f)
            _stumbleSpeedMult = Mathf.Lerp(1f, 0.4f, t / 0.2f);
        else
            _stumbleSpeedMult = Mathf.Lerp(0.4f, 1f, (t - 0.2f) / 0.8f);

        if (_stumbleTimer >= STUMBLE_DURATION)
        {
            _stumbling = false;
            _stumbleSpeedMult = 1f;
        }
    }

    void UpdateBurst(float dt)
    {
        // Erratic racers get random speed surges
        float erratic = 1f - consistency;
        if (erratic < 0.3f)
        {
            _burstSpeedMult = 1f;
            return;
        }

        _burstTimer -= dt;
        if (_burstTimer <= 0f)
        {
            if (Random.value < erratic * 0.3f)
                _burstSpeedMult = 1f + Random.Range(0.1f, 0.3f) * erratic;
            else
                _burstSpeedMult = 1f;
            _burstTimer = Random.Range(1f, 4f);
        }
    }

    public void TriggerStumble()
    {
        if (_stumbling || _finished) return;
        _stumbling = true;
        _stumbleTimer = 0f;
        _stumbleSpeedMult = 1f;

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayObstacleHit();
    }

    /// <summary>Called by RaceManager when this racer crosses the finish line.</summary>
    public void OnFinish(float raceTime)
    {
        _finished = true;
        _finishTime = raceTime;
        _currentSpeed *= 0.3f; // slow to a crawl
    }

    /// <summary>Set starting distance offset for staggered grid.</summary>
    public void SetStartOffset(float offset)
    {
        _distanceAlongPath = offset;
    }

    // === RACER PERSONALITY PRESETS ===

    public static void ApplyPreset(RacerAI ai, string preset)
    {
        switch (preset)
        {
            case "SkidmarkSteve":
                ai.racerName = "Skidmark Steve";
                ai.racerColor = new Color(0.7f, 0.35f, 0.1f); // burnt orange
                ai.baseSpeed = 7.2f;
                ai.maxSpeed = 13.5f;
                ai.steerSpeed = 2.5f;
                ai.steerAggressiveness = 1.4f;
                ai.consistency = 0.3f;
                ai.aggression = 0.8f;
                ai.stumbleChance = 0.04f; // sloppy, stumbles more
                ai.catchUpMultiplier = 1.35f;
                break;

            case "PrincessPlop":
                ai.racerName = "Princess Plop";
                ai.racerColor = new Color(0.85f, 0.5f, 0.75f); // pink
                ai.baseSpeed = 6.8f;
                ai.maxSpeed = 12.8f;
                ai.steerSpeed = 1.8f;
                ai.steerAggressiveness = 0.8f;
                ai.consistency = 0.85f;
                ai.aggression = 0.3f;
                ai.stumbleChance = 0.01f; // smooth, rarely stumbles
                ai.slowDownMultiplier = 0.88f; // drafts well, doesn't slow much
                break;

            case "TheLog":
                ai.racerName = "The Log";
                ai.racerColor = new Color(0.4f, 0.25f, 0.1f); // dark wood brown
                ai.baseSpeed = 6.2f;
                ai.maxSpeed = 14f; // highest top speed
                ai.steerSpeed = 1.2f;
                ai.steerAggressiveness = 0.6f;
                ai.consistency = 0.95f; // extremely steady
                ai.aggression = 0.6f;
                ai.stumbleChance = 0.005f; // almost never stumbles
                ai.acceleration = 1.2f; // slow to accelerate
                ai.catchUpMultiplier = 1.15f; // doesn't rubber-band hard
                break;

            case "LilSquirt":
                ai.racerName = "Lil Squirt";
                ai.racerColor = new Color(0.9f, 0.8f, 0.3f); // yellow-brown
                ai.baseSpeed = 7.5f; // fastest base
                ai.maxSpeed = 12.5f;
                ai.steerSpeed = 3f;
                ai.steerAggressiveness = 1.6f;
                ai.consistency = 0.15f; // very erratic
                ai.aggression = 0.5f;
                ai.stumbleChance = 0.03f;
                ai.steerChangeInterval = 0.8f; // changes direction rapidly
                break;
        }
    }
}
