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

    // Fork tracking
    private PipeFork _currentFork;
    private int _forkBranch = -1;

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
        float targetSpeed = baseSpeed + (_distanceAlongPath * 0.012f);

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
            if (diff > 3f)
                targetSpeed *= 1f + aggression * 0.25f;

            // Draft boost: close to leader = small speed bump (slipstream)
            if (diff > 0f && diff < rubberBandDistance * 0.5f)
                targetSpeed *= 1.05f;
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

        // === FORK CHECK (visual only - density tracking) ===
        PipeFork fork = pipeGen.GetForkAtDistance(_distanceAlongPath);
        if (fork != null && _currentFork != fork)
        {
            _currentFork = fork;
            _forkBranch = fork.GetAIBranch(aggression);
        }
        else if (fork == null && _currentFork != null)
        {
            _currentFork = null;
            _forkBranch = -1;
        }

        // === POSITION ON PIPE (branch-aware) ===
        Vector3 center, forward, right, up;

        if (_currentFork != null && _forkBranch >= 0)
        {
            Vector3 mainC, mainF, mainR, mainU;
            pipeGen.GetPathFrame(_distanceAlongPath, out mainC, out mainF, out mainR, out mainU);

            Vector3 bC, bF, bR, bU;
            if (_currentFork.GetBranchFrame(_forkBranch, _distanceAlongPath,
                out bC, out bF, out bR, out bU))
            {
                float blend = _currentFork.GetBranchBlend(_distanceAlongPath);
                center = Vector3.Lerp(mainC, bC, blend);
                forward = Vector3.Slerp(mainF, bF, blend).normalized;
                right = Vector3.Slerp(mainR, bR, blend).normalized;
                up = Vector3.Slerp(mainU, bU, blend).normalized;
            }
            else
            {
                center = mainC; forward = mainF; right = mainR; up = mainU;
            }
        }
        else
        {
            pipeGen.GetPathFrame(_distanceAlongPath, out center, out forward, out right, out up);
        }

        float activeRadius = (_currentFork != null && _forkBranch >= 0)
            ? Mathf.Lerp(pipeRadius, _currentFork.branchPipeRadius,
                _currentFork.GetBranchBlend(_distanceAlongPath))
            : pipeRadius;

        float rad = _currentAngle * Mathf.Deg2Rad;
        Vector3 offset = (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * activeRadius;
        Vector3 targetPos = center + offset;

        // Stumble wobble
        if (_stumbling)
        {
            float wobble = Mathf.Sin(Time.time * 25f) * 0.3f * (1f - _stumbleTimer / STUMBLE_DURATION);
            targetPos += right * wobble;
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, dt * 6f);

        // Face forward with surface roll
        float surfaceAngle = _currentAngle - 90f + 180f;
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
                ai.baseSpeed = 8.5f;
                ai.maxSpeed = 15f;
                ai.acceleration = 2.5f;
                ai.steerSpeed = 2.5f;
                ai.steerAggressiveness = 1.4f;
                ai.consistency = 0.3f;
                ai.aggression = 0.9f;
                ai.stumbleChance = 0.025f; // sloppy but not as much
                ai.catchUpMultiplier = 1.45f; // closes gaps fast
                ai.rubberBandDistance = 8f; // kicks in sooner
                break;

            case "PrincessPlop":
                ai.racerName = "Princess Plop";
                ai.racerColor = new Color(0.85f, 0.5f, 0.75f); // pink
                ai.baseSpeed = 8f;
                ai.maxSpeed = 14.5f;
                ai.acceleration = 2.8f;
                ai.steerSpeed = 1.8f;
                ai.steerAggressiveness = 0.8f;
                ai.consistency = 0.85f;
                ai.aggression = 0.6f;
                ai.stumbleChance = 0.005f; // super smooth, almost never stumbles
                ai.slowDownMultiplier = 0.92f; // barely slows when ahead
                ai.catchUpMultiplier = 1.3f;
                ai.rubberBandDistance = 8f;
                break;

            case "TheLog":
                ai.racerName = "The Log";
                ai.racerColor = new Color(0.4f, 0.25f, 0.1f); // dark wood brown
                ai.baseSpeed = 7.5f;
                ai.maxSpeed = 15.5f; // highest top speed - scary late game
                ai.steerSpeed = 1.2f;
                ai.steerAggressiveness = 0.6f;
                ai.consistency = 0.95f; // extremely steady
                ai.aggression = 0.75f;
                ai.stumbleChance = 0.003f; // almost never stumbles
                ai.acceleration = 1.8f; // still slow to accelerate but not as bad
                ai.catchUpMultiplier = 1.3f;
                ai.rubberBandDistance = 10f;
                break;

            case "LilSquirt":
                ai.racerName = "Lil Squirt";
                ai.racerColor = new Color(0.9f, 0.8f, 0.3f); // yellow-brown
                ai.baseSpeed = 9f; // fastest base - always nipping at your heels
                ai.maxSpeed = 14f;
                ai.acceleration = 3f;
                ai.steerSpeed = 3f;
                ai.steerAggressiveness = 1.6f;
                ai.consistency = 0.15f; // very erratic
                ai.aggression = 0.7f;
                ai.stumbleChance = 0.02f;
                ai.steerChangeInterval = 0.8f; // changes direction rapidly
                ai.catchUpMultiplier = 1.4f;
                ai.rubberBandDistance = 6f; // rubber-bands very aggressively
                break;
        }
    }
}
