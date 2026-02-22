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
    private const float STUMBLE_DURATION = 1.8f;
    private float _stumbleInvincibleTimer; // post-stumble invincibility

    // Water/drop zone slowdown
    private float _waterSlowMult = 1f;

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

    // Near-miss detection (trigger combo when passing close to player)
    private bool _nearMissTriggered;
    private const float NEAR_MISS_DIST = 2.5f;

    // Zone affinity: per-zone speed multiplier (some racers excel in certain zones)
    // Index 0-4 maps to Porcelain, Grimy, Toxic, Rusty, Hellsewer
    [HideInInspector] public float[] zoneAffinities = { 1f, 1f, 1f, 1f, 1f };

    // Final stretch push
    private bool _finalStretchActive;
    private float _finalStretchMult = 1f;
    private const float FINISH_DISTANCE = 1000f;
    private const float FINAL_STRETCH_START = 850f; // last 150m
    private static bool _finalStretchAnnounced; // one-shot: first AI to sprint triggers player cue

    // Public accessors
    public float DistanceTraveled => _distanceAlongPath;
    public bool IsFinished => _finished;
    public float FinishTime => _finishTime;
    public float CurrentSpeed => _currentSpeed;

    void Start()
    {
        _finalStretchAnnounced = false; // reset per race
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

        // Stumble invincibility cooldown
        if (_stumbleInvincibleTimer > 0f)
            _stumbleInvincibleTimer -= dt;

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
                float obsDist = toObs.magnitude;
                Vector3 localObs = transform.InverseTransformDirection(toObs);
                dodgeSteer = localObs.x > 0 ? -dodgeSteerStrength : dodgeSteerStrength;

                // Deterministic stumble: close obstacle + not invincible = stumble
                if (!_stumbling && _stumbleInvincibleTimer <= 0f)
                {
                    if (obsDist < 1.5f || Random.value < stumbleChance)
                        TriggerStumble();
                }
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

        // Zone affinity: racers excel or struggle in different zones
        if (PipeZoneSystem.Instance != null)
        {
            int zi = PipeZoneSystem.Instance.CurrentZoneIndex;
            float blend = PipeZoneSystem.Instance.ZoneBlend;
            float affCurr = zoneAffinities[Mathf.Clamp(zi, 0, zoneAffinities.Length - 1)];
            float affNext = zoneAffinities[Mathf.Clamp(zi + 1, 0, zoneAffinities.Length - 1)];
            targetSpeed *= Mathf.Lerp(affCurr, affNext, blend);
        }

        // Water/drop zone slowdown: AI slows in water sections like the player does
        UpdateWaterSlow();
        targetSpeed *= _waterSlowMult;

        // Final stretch push: personality-driven sprint to the finish
        UpdateFinalStretch(dt);
        targetSpeed *= _finalStretchMult;

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
        // Water zone wobble (slight unsteadiness in water)
        if (_waterSlowMult < 1f)
        {
            float waterWobble = Mathf.Sin(Time.time * 8f + _currentAngle) * 0.15f;
            targetPos += right * waterWobble;
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

        // === NEAR-MISS DETECTION (combo event when racers pass close) ===
        if (rm != null && rm.PlayerController != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, rm.PlayerController.transform.position);
            if (distToPlayer < NEAR_MISS_DIST && !_nearMissTriggered)
            {
                _nearMissTriggered = true;
                if (ComboSystem.Instance != null)
                    ComboSystem.Instance.RegisterEvent(ComboSystem.EventType.NearMiss);
                if (ParticleManager.Instance != null)
                    ParticleManager.Instance.PlayNearMiss(transform.position);
                // Show popup with racer name
                if (ScorePopup.Instance != null)
                {
                    string[] closeCallWords = { "CLOSE CALL!", "WHOA!", "YIKES!", "WATCH IT!", "TOO CLOSE!" };
                    string word = closeCallWords[Random.Range(0, closeCallWords.Length)];
                    ScorePopup.Instance.ShowNearMiss(transform.position, 0);
                }
                if (ProceduralAudio.Instance != null)
                    ProceduralAudio.Instance.PlayNearMiss();
                HapticManager.LightTap();
            }
            else if (distToPlayer > NEAR_MISS_DIST * 2f)
            {
                _nearMissTriggered = false;
            }
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
        // Quick slowdown to 0.25x, gradual recovery (closer to player's stun experience)
        if (t < 0.2f)
            _stumbleSpeedMult = Mathf.Lerp(1f, 0.25f, t / 0.2f);
        else
            _stumbleSpeedMult = Mathf.Lerp(0.25f, 1f, (t - 0.2f) / 0.8f);

        if (_stumbleTimer >= STUMBLE_DURATION)
        {
            _stumbling = false;
            _stumbleSpeedMult = 1f;
            _stumbleInvincibleTimer = 1.5f; // post-stumble invincibility
        }
    }

    void UpdateWaterSlow()
    {
        // Detect water/drop zones by checking if the pipe goes steeply downward
        bool inWater = false;
        if (pipeGen != null)
        {
            Vector3 c, fwd, r, u;
            pipeGen.GetPathFrame(_distanceAlongPath, out c, out fwd, out r, out u);
            float downDot = Vector3.Dot(fwd, Vector3.down);
            inWater = downDot > 0.5f; // steep downward = drop zone / water
        }
        _waterSlowMult = inWater ? 0.75f : 1f;
    }

    void UpdateFinalStretch(float dt)
    {
        if (_distanceAlongPath < FINAL_STRETCH_START)
        {
            _finalStretchMult = 1f;
            return;
        }

        if (!_finalStretchActive)
        {
            _finalStretchActive = true;
            // Aggressive racers push harder in the final stretch
            // Consistent racers maintain a steady push; erratic ones are wilder
            float pushBase = 1.05f + aggression * 0.1f;
            float variance = (1f - consistency) * 0.08f;
            _finalStretchMult = pushBase + Random.Range(-variance, variance);

            // First AI to sprint: telegraph to the player that rivals are pushing
            if (!_finalStretchAnnounced)
            {
                _finalStretchAnnounced = true;
                if (ScreenEffects.Instance != null)
                    ScreenEffects.Instance.TriggerProximityWarning(); // red edge = danger
                if (PipeCamera.Instance != null)
                    PipeCamera.Instance.Shake(0.1f);
                if (CheerOverlay.Instance != null)
                    CheerOverlay.Instance.ShowCheer("RIVALS SPRINT!", new Color(1f, 0.4f, 0.2f), false);
                HapticManager.LightTap();
            }
        }

        // Ramp up as we approach the line
        float progress = (_distanceAlongPath - FINAL_STRETCH_START) / (FINISH_DISTANCE - FINAL_STRETCH_START);
        progress = Mathf.Clamp01(progress);
        _finalStretchMult = Mathf.Lerp(1f, _finalStretchMult, progress);
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
#if UNITY_EDITOR
        Debug.Log($"[AI] {gameObject.name} STUMBLE at dist={_distanceAlongPath:F0} speed={_currentSpeed:F1}");
#endif
        _stumbling = true;
        _stumbleTimer = 0f;
        _stumbleSpeedMult = 1f;

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayObstacleHit();
        // Visual feedback on AI stumble
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayHitExplosion(transform.position);
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

    // === PERSONALITY TAUNTS ===
    [HideInInspector] public string[] passQuips;    // said when passing the player
    [HideInInspector] public string[] passedQuips;  // said when player passes them

    /// <summary>Get a taunt when this racer passes the player.</summary>
    public string GetPassTaunt()
    {
        if (passQuips == null || passQuips.Length == 0) return "";
        return passQuips[Random.Range(0, passQuips.Length)];
    }

    /// <summary>Get a reaction when the player passes this racer.</summary>
    public string GetPassedReaction()
    {
        if (passedQuips == null || passedQuips.Length == 0) return "";
        return passedQuips[Random.Range(0, passedQuips.Length)];
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
                // Thrives in grimy/rusty zones, struggles in clean porcelain
                ai.zoneAffinities = new float[] { 0.94f, 1.06f, 0.98f, 1.08f, 1.02f };
                ai.passQuips = new[] { "Later, loser!", "Eat my skidmarks!", "Too slow, corn boy!", "Outta my way!" };
                ai.passedQuips = new[] { "Hey! No fair!", "I'll get you back!", "Lucky shot!", "That was a fluke!" };
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
                // Princess of the porcelain throne - excels in clean zones, hates filth
                ai.zoneAffinities = new float[] { 1.08f, 1.02f, 0.95f, 0.93f, 0.90f };
                ai.passQuips = new[] { "Excuse me, peasant!", "Make way for royalty!", "Toodles!", "A princess always leads!" };
                ai.passedQuips = new[] { "How DARE you!", "This is unbecoming!", "Hmph!", "I'll allow it... for now." };
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
                // Ancient sewer dweller - gets stronger deeper in, Hellsewer is home turf
                ai.zoneAffinities = new float[] { 0.92f, 0.96f, 1.02f, 1.06f, 1.12f };
                ai.passQuips = new[] { "Slow and steady...", "The Log abides.", "You cannot rush the Log.", "Ancient wisdom prevails." };
                ai.passedQuips = new[] { "No rush...", "I'll catch up.", "Patience...", "The race is long." };
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
                // Toxic little gremlin - loves the toxic zone, fragile in hellsewer
                ai.zoneAffinities = new float[] { 1.02f, 1.0f, 1.10f, 0.97f, 0.88f };
                ai.passQuips = new[] { "WHEEE!", "Zoom zoom!", "Can't catch me!", "Squirty speed!" };
                ai.passedQuips = new[] { "NOOOO!", "Wait up!", "Aw man!", "I'll be back!" };
                break;
        }
    }
}
