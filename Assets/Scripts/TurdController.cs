using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Controls Mr. Corny's movement through the sewer pipe.
/// Kart racer physics: momentum, drift, responsive but weighty.
/// Mario Kart-style hit system: obstacles slow/stun, not kill.
/// </summary>
public class TurdController : MonoBehaviour
{
    public enum HitState { Normal, Stunned, Recovering, Invincible }

    [Header("Movement")]
    public float forwardSpeed = 6f;
    public float maxSpeed = 14f;
    public float acceleration = 0.5f;

    [Header("Steering")]
    public float steerSpeed = 5f;
    public float maxSteerAngle = 60f;
    [Tooltip("Use tilt controls on mobile, arrow keys on desktop")]
    public bool useTiltControls = true;

    [Header("Kart Physics")]
    public float pipeRadius = 3f;
    public float gravity = 9.8f;
    public float surfaceStickForce = 6f;
    [Tooltip("Angular momentum - how much the turd keeps sliding after letting go")]
    public float angularDrag = 3.5f;
    [Tooltip("Drift factor - higher = more slide when reversing direction")]
    public float driftFactor = 0.6f;

    [Header("Hit Recovery")]
    public float stunDuration = 1.5f;
    public float stunSpeedMult = 0.3f;
    public float recoveryDuration = 0.5f;
    public float invincibilityDuration = 2f;
    public float flashSpeed = 10f;

    [Header("Jump")]
    public float jumpArcHeight = 2.5f;
    public float jumpArcDuration = 0.9f;

    [Header("Tricks")]
    public float trickRotSpeed = 540f;
    public int trickScoreBonus = 200;
    public float trickSpeedBoostMult = 1.2f;
    public float trickSpeedBoostDur = 2f;

    [Header("References")]
    public TurdSlither slither;
    public PipeGenerator pipeGen;

    private float _currentAngle = 270f;  // 270 = bottom of pipe
    private float _currentSpeed;
    private float _steerInput;
    private float _angularVelocity = 0f;
    private float _distanceAlongPath = 0f;

    // Hit state
    private HitState _hitState = HitState.Normal;
    private Renderer[] _renderers;
    private Coroutine _stunCoroutine;
    private float _hitPhaseTimer;    // tracks time within current hit phase
    private float _hitPhaseDuration; // total duration of current hit phase

    // Jump state
    private bool _isJumping = false;
    private float _jumpTimer = 0f;
    private float _jumpDuration = 0f;
    private float _jumpHeight = 0f;

    // Stomp combo
    private int _stompCombo = 0;
    private float _stompComboTimer = 0f;
    private const float STOMP_COMBO_TIMEOUT = 2f;

    // Trick state
    private int _trickDirection = 0;   // 1=front flip, -1=backflip, 0=none
    private float _trickAngle = 0f;    // accumulated rotation degrees
    private int _tricksCompleted = 0;  // full 360s completed this jump

    // Water tracking
    private bool _wasInWater = false;

    // Coin magnet
    private float _coinMagnetTimer = 0f;
    private const float COIN_MAGNET_RADIUS = 6f;

    // Vertical drop state
    private bool _isDropping = false;
    private float _dropTimer = 0f;
    private float _dropDuration = 12f;
    private float _dropSpeed = 18f;
    private float _dropMoveRadius = 2.5f;
    private float _dropMoveSpeed = 8f;
    private float _dropExitBoostMult = 1.4f;
    private float _dropExitBoostDur = 3f;
    private Vector2 _dropOffset = Vector2.zero; // 2D offset from pipe center

    // Fork tracking
    private PipeFork _currentFork;
    private int _forkBranch = -1;

    public float DistanceTraveled => _distanceAlongPath;
    public float CurrentSpeed => _currentSpeed;
    public float CurrentAngle => _currentAngle;
    public float AngularVelocity => _angularVelocity;
    public HitState CurrentHitState => _hitState;
    public float HitPhaseProgress => _hitPhaseDuration > 0f ? Mathf.Clamp01(_hitPhaseTimer / _hitPhaseDuration) : 0f;
    public int ForkBranch => _forkBranch;
    public PipeFork CurrentFork => _currentFork;
    public bool IsInvincible => _hitState == HitState.Invincible || _hitState == HitState.Stunned;
    public bool IsStunned => _hitState == HitState.Stunned;
    public bool IsJumping => _isJumping;
    public bool IsDropping => _isDropping;
    public bool IsMagnetActive => _coinMagnetTimer > 0f;

    void Start()
    {
        _currentSpeed = forwardSpeed;
        if (slither == null)
            slither = GetComponent<TurdSlither>();
        if (pipeGen == null)
            pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        _renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        // === VERTICAL DROP MODE (2D freefall) ===
        if (_isDropping)
        {
            UpdateDropState();
            return;
        }

        // === STEERING INPUT ===
        float rawInput = 0f;
        if (_hitState != HitState.Stunned)
        {
            if (TouchInput.Instance != null)
            {
                rawInput = TouchInput.Instance.SteerInput;
            }
            else if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
                    rawInput += 1f;
                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
                    rawInput -= 1f;
            }
        }
        // Fast input response - minimal latency on initial press
        _steerInput = Mathf.Lerp(_steerInput, rawInput, Time.deltaTime * 18f);

        // === TRICK INPUT (during jumps only) ===
        if (_isJumping && _hitState != HitState.Stunned)
        {
            if (_trickDirection == 0)
            {
                // Touch: swipe up = front flip, swipe down = backflip
                if (TouchInput.Instance != null)
                {
                    if (TouchInput.Instance.SwipeUp) _trickDirection = 1;
                    else if (TouchInput.Instance.SwipeDown) _trickDirection = -1;
                }
                // Keyboard fallback
                if (_trickDirection == 0 && Keyboard.current != null)
                {
                    if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
                        _trickDirection = 1;  // front flip
                    else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
                        _trickDirection = -1; // backflip
                }
            }

            if (_trickDirection != 0)
            {
                _trickAngle += trickRotSpeed * Time.deltaTime;
                if (_trickAngle >= 360f * (_tricksCompleted + 1))
                {
                    _tricksCompleted++;
                    if (ProceduralAudio.Instance != null)
                        ProceduralAudio.Instance.PlayTrickComplete();
                    if (PipeCamera.Instance != null)
                        PipeCamera.Instance.PunchFOV(4f);
                    HapticManager.MediumTap();
                }
            }
        }

        // === SPEED CONTROL (when not jumping: up/down arrows control speed) ===
        bool manualSpeedControl = false;
        if (!_isJumping && _hitState != HitState.Stunned && Keyboard.current != null)
        {
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
            {
                // Brake: slow down to near crawl (20% of base speed)
                float brakeTarget = forwardSpeed * 0.2f;
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, brakeTarget, acceleration * 6f * Time.deltaTime);
                manualSpeedControl = true;
            }
            else if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
            {
                // Boost back up: accelerate faster than normal
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed, acceleration * 3f * Time.deltaTime);
                manualSpeedControl = true;
            }
        }

        // === FORWARD SPEED (normal gradual acceleration when no manual speed input) ===
        if (!manualSpeedControl)
            _currentSpeed = Mathf.Min(_currentSpeed + acceleration * Time.deltaTime, maxSpeed);

        // Slope speed effect: slower uphill, faster downhill
        if (pipeGen != null)
        {
            Vector3 slopePos, slopeFwd;
            pipeGen.GetPathInfo(_distanceAlongPath, out slopePos, out slopeFwd);
            float slope = Vector3.Dot(slopeFwd, Vector3.up);
            _currentSpeed -= slope * gravity * 0.4f * Time.deltaTime;
            _currentSpeed = Mathf.Clamp(_currentSpeed, forwardSpeed * 0.4f, maxSpeed * 1.5f);
        }

        _distanceAlongPath += _currentSpeed * Time.deltaTime;

        // === KART PHYSICS: MOMENTUM + DRIFT ===
        float targetAngVel = _steerInput * steerSpeed * 55f;

        bool drifting = (_angularVelocity > 35f && _steerInput < -0.1f) ||
                        (_angularVelocity < -35f && _steerInput > 0.1f);

        float steerAccel = drifting ? angularDrag * driftFactor : angularDrag * 4.5f;
        _angularVelocity = Mathf.Lerp(_angularVelocity, targetAngVel, Time.deltaTime * steerAccel);

        if (Mathf.Abs(_steerInput) < 0.05f)
            _angularVelocity *= (1f - angularDrag * Time.deltaTime);

        _currentAngle += _angularVelocity * Time.deltaTime;

        // === GRAVITY: pull toward pipe bottom (270°) ===
        float angleDelta = Mathf.DeltaAngle(_currentAngle, 270f);
        float gravityStrength = gravity * 0.015f;
        float distFromBottom = Mathf.Abs(angleDelta) / 180f;
        gravityStrength *= (1f + distFromBottom * 1.5f);
        float gravityPull = angleDelta * gravityStrength * Time.deltaTime;
        _angularVelocity += gravityPull;

        // Dampen angular velocity near pipe bottom to prevent overshoot
        if (Mathf.Abs(angleDelta) < 10f && Mathf.Abs(_steerInput) < 0.05f)
            _angularVelocity *= (1f - 6f * Time.deltaTime);

        // === FORK CHECK (visual only - tracks branch for spawn density) ===
        if (pipeGen != null)
        {
            PipeFork fork = pipeGen.GetForkAtDistance(_distanceAlongPath);
            if (fork != null && _currentFork != fork)
            {
                // Entering a new fork - assign branch based on which side of pipe we're on
                _currentFork = fork;
                fork.AssignPlayer(_currentAngle);
                _forkBranch = fork.PlayerBranch;
            }
            else if (fork == null && _currentFork != null)
            {
                // Exited fork zone
                _currentFork.ResetPlayerBranch();
                _currentFork = null;
                _forkBranch = -1;
            }
        }

        // === POSITION AND ORIENT ===
        if (pipeGen != null)
        {
            Vector3 center, forward, right, up;

            // Always use main path orientation (forward/right/up) for stable camera.
            // Only blend the CENTER position laterally into the chosen branch.
            pipeGen.GetPathFrame(_distanceAlongPath, out center, out forward, out right, out up);

            if (_currentFork != null && _forkBranch >= 0)
            {
                Vector3 branchCenter, branchFwd, branchRight, branchUp;
                if (_currentFork.GetBranchFrame(_forkBranch, _distanceAlongPath,
                    out branchCenter, out branchFwd, out branchRight, out branchUp))
                {
                    float blend = _currentFork.GetBranchBlend(_distanceAlongPath);
                    center = Vector3.Lerp(center, branchCenter, blend);
                }
            }

            // Use smaller radius when in a branch tube
            float activeRadius = (_currentFork != null && _forkBranch >= 0)
                ? Mathf.Lerp(pipeRadius, _currentFork.branchPipeRadius,
                    _currentFork.GetBranchBlend(_distanceAlongPath))
                : pipeRadius;

            float rad = _currentAngle * Mathf.Deg2Rad;
            Vector3 offset = (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * activeRadius;
            Vector3 targetPos = center + offset;

            // Jump: smooth parabolic arc above the pipe surface
            if (_isJumping)
            {
                _jumpTimer += Time.deltaTime;
                float jumpT = Mathf.Clamp01(_jumpTimer / _jumpDuration);

                // Parabolic arc: 4h * t * (1-t) peaks at h at t=0.5
                float arcOffset = 4f * _jumpHeight * jumpT * (1f - jumpT);

                // Move TOWARD pipe center (jump into the air inside the pipe)
                Vector3 towardCenter = (center - targetPos).normalized;
                targetPos += towardCenter * arcOffset;

                // Squash/stretch
                float squash;
                if (jumpT < 0.15f) // launch stretch
                    squash = 1f + (jumpT / 0.15f) * 0.3f;
                else if (jumpT > 0.85f) // landing squash
                    squash = 1f + ((1f - jumpT) / 0.15f) * -0.2f;
                else
                    squash = 1f;

                Vector3 baseScale = Vector3.one;
                if (slither != null) baseScale = slither.transform.localScale;
                // Apply squash: stretch along forward, compress perpendicular
                transform.localScale = new Vector3(
                    1f / Mathf.Sqrt(squash),
                    1f / Mathf.Sqrt(squash),
                    squash
                );

                if (jumpT >= 1f)
                {
                    _isJumping = false;
                    transform.localScale = Vector3.one;

                    // Trick landing rewards
                    if (_tricksCompleted > 0)
                    {
                        int bonus = trickScoreBonus * _tricksCompleted;
                        if (GameManager.Instance != null)
                            GameManager.Instance.AddScore(bonus);
                        ApplySpeedBoost(trickSpeedBoostMult, trickSpeedBoostDur);
                        if (PipeCamera.Instance != null)
                        {
                            PipeCamera.Instance.Shake(0.3f);
                            PipeCamera.Instance.PunchFOV(5f);
                        }
                        if (ComboSystem.Instance != null)
                            ComboSystem.Instance.RegisterEvent(ComboSystem.EventType.NearMiss);

                        // Score popup
                        string trickName = _trickDirection > 0 ? "FLIP" : "BACKFLIP";
                        if (_tricksCompleted > 1) trickName = $"{_tricksCompleted}x " + trickName;
                        if (ScorePopup.Instance != null)
                            ScorePopup.Instance.ShowTrick(transform.position, trickName, bonus);

                        // Poop crew goes nuts for tricks
                        if (CheerOverlay.Instance != null)
                        {
                            string cheerWord = _tricksCompleted switch
                            {
                                1 => "SICK FLIP!",
                                2 => "DOUBLE FLIP!",
                                _ => "TRICKSTER!"
                            };
                            Color cheerCol = _tricksCompleted >= 2
                                ? new Color(1f, 0.3f, 0.8f)  // hot pink for multi
                                : new Color(0.3f, 1f, 0.5f);  // green for single
                            CheerOverlay.Instance.ShowCheer(cheerWord, cheerCol, _tricksCompleted >= 2);
                        }

                        // Screen effects escalate with trick count
                        if (ScreenEffects.Instance != null)
                        {
                            if (_tricksCompleted >= 2)
                                ScreenEffects.Instance.TriggerMilestoneFlash();
                            else
                                ScreenEffects.Instance.TriggerPowerUpFlash();
                        }

                        // Celebration particles
                        if (ParticleManager.Instance != null)
                            ParticleManager.Instance.PlayCelebration(transform.position);

                        // Freeze frame for juice (longer for multi-tricks)
                        if (GameManager.Instance != null)
                            GameManager.Instance.TriggerFreezeFrame(_tricksCompleted >= 2 ? 0.1f : 0.06f);

                        HapticManager.HeavyTap();
                    }
                    else
                    {
                        // Normal landing impact - weighty thud
                        if (PipeCamera.Instance != null)
                        {
                            PipeCamera.Instance.Shake(0.15f);
                            PipeCamera.Instance.PunchFOV(-2f); // brief squish on impact
                        }
                        HapticManager.MediumTap();
                    }

                    // Landing dust burst at feet
                    if (ParticleManager.Instance != null)
                        ParticleManager.Instance.PlayLandingDust(transform.position);

                    if (ProceduralAudio.Instance != null)
                        ProceduralAudio.Instance.PlayObstacleHit();

                    // Reset trick state
                    _trickDirection = 0;
                    _trickAngle = 0f;
                    _tricksCompleted = 0;
                }
            }

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);

            // Face path direction, roll to match surface
            float surfaceAngle = _currentAngle - 90f + 180f;
            Quaternion pathRot = Quaternion.LookRotation(forward, up);
            Quaternion surfaceRoll = Quaternion.Euler(0, 0, surfaceAngle);
            Quaternion targetRot = pathRot * surfaceRoll;

            // Apply trick flip rotation during jumps
            if (_isJumping && _trickDirection != 0)
            {
                Quaternion trickRot = Quaternion.Euler(_trickAngle * _trickDirection, 0, 0);
                targetRot = targetRot * trickRot;
            }

            float rotSmooth = (_isJumping && _trickDirection != 0) ? 20f : 8f;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotSmooth);
        }
        else
        {
            transform.Translate(Vector3.forward * _currentSpeed * Time.deltaTime, Space.Self);
            float rad = _currentAngle * Mathf.Deg2Rad;
            Vector3 pos = transform.position;
            pos.x = Mathf.Lerp(pos.x, Mathf.Cos(rad) * pipeRadius, Time.deltaTime * surfaceStickForce);
            pos.y = Mathf.Lerp(pos.y, Mathf.Sin(rad) * pipeRadius, Time.deltaTime * surfaceStickForce);
            transform.position = pos;

            Quaternion targetRot = Quaternion.Euler(0, 0, _currentAngle - 90f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
        }

        // Feed slither animation
        if (slither != null)
        {
            slither.currentSpeed = _currentSpeed / forwardSpeed;
            slither.turnInput = _steerInput;
        }

        // === STOMP COMBO TIMER ===
        if (_stompCombo > 0)
        {
            _stompComboTimer -= Time.deltaTime;
            if (_stompComboTimer <= 0f)
                _stompCombo = 0;
        }

        // === WATER INTERACTION ===
        float angleDeltaFromBottom = Mathf.Abs(Mathf.DeltaAngle(_currentAngle, 270f));
        bool inWater = angleDeltaFromBottom < 25f && !_isJumping;
        if (inWater && !_wasInWater)
        {
            // Entering water - comic SPLOSH!
            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayWaterSplash(transform.position);
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayWaterSplosh();
            HapticManager.MediumTap();
            ParticleManager.Instance?.StartWakeSpray(transform);
        }
        else if (!inWater && _wasInWater)
        {
            // Leaving water - comic PLOOP!
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayWaterPloop();
            ParticleManager.Instance?.StopWakeSpray();
        }
        _wasInWater = inWater;

        // === COIN MAGNET ===
        if (_coinMagnetTimer > 0f)
        {
            _coinMagnetTimer -= Time.deltaTime;
            AttractNearbyCoins();
            if (_coinMagnetTimer <= 0f)
                ParticleManager.Instance?.StopCoinMagnet();
        }

        // === INVINCIBILITY FLASH ===
        if (_hitState == HitState.Invincible && _renderers != null)
        {
            float t = Time.time;
            float remaining = _hitPhaseDuration > 0f ? 1f - (_hitPhaseTimer / _hitPhaseDuration) : 1f;

            // Multi-frequency flash: primary + secondary beat for organic feel
            float freq = flashSpeed;
            // Speed up flashing as invincibility expires (warning)
            if (remaining < 0.35f)
                freq *= Mathf.Lerp(3f, 1f, remaining / 0.35f);

            float wave = Mathf.Sin(t * freq * Mathf.PI)
                       + Mathf.Sin(t * freq * 0.7f * Mathf.PI) * 0.3f;

            // Random glitch frames (5% chance per frame, only in middle of duration)
            bool glitch = remaining > 0.2f && remaining < 0.8f && Random.value < 0.05f;
            bool visible = glitch ? !(_renderers[0] != null && _renderers[0].enabled) : wave > 0f;

            foreach (var r in _renderers)
            {
                if (r != null)
                    r.enabled = visible;
            }
        }
        else if (_renderers != null)
        {
            // Make sure all renderers are visible when not invincible
            foreach (var r in _renderers)
            {
                if (r != null && !r.enabled)
                    r.enabled = true;
            }
        }
    }

    // === HIT SYSTEM ===

    /// <summary>
    /// Called when player hits an obstacle. Slows the player and starts recovery.
    /// </summary>
    public void TakeHit(ObstacleBehavior obstacle)
    {
        if (_hitState != HitState.Normal) return;

        // Underwater hit: push player to a random direction, brief stagger
        if (_isDropping)
        {
            // Knockback: push offset in a random direction
            Vector2 knockDir = Random.insideUnitCircle.normalized;
            _dropOffset += knockDir * 1.2f;
            if (_dropOffset.magnitude > _dropMoveRadius)
                _dropOffset = _dropOffset.normalized * _dropMoveRadius;

            if (PipeCamera.Instance != null)
                PipeCamera.Instance.Shake(0.3f);
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayObstacleHit();
            HapticManager.MediumTap();

            if (ComboSystem.Instance != null)
                ComboSystem.Instance.ResetCombo();
            return;
        }

        // Reset combo
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.ResetCombo();

        // Hitstop freeze frame for that crunchy impact feel
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerFreezeFrame(0.07f);

        // Camera juice
        if (PipeCamera.Instance != null)
        {
            PipeCamera.Instance.Shake(0.4f);
            PipeCamera.Instance.PunchFOV(-4f); // negative = zoom in for impact
        }

        // Audio + haptics
        if (ProceduralAudio.Instance != null)
        {
            ProceduralAudio.Instance.PlayObstacleHit();
            ProceduralAudio.Instance.TriggerStunDip(); // music drops on stun
        }
        HapticManager.HeavyTap();

        // Screen flash overlay (obstacle-type-specific color)
        if (ScreenEffects.Instance != null)
        {
            if (obstacle != null)
            {
                ScreenEffects.Instance.TriggerHitFlash(obstacle.HitFlashColor);
                // Messy creatures splatter the screen
                if (obstacle.SplatterOnHit)
                    ScreenEffects.Instance.TriggerSplatter(obstacle.HitFlashColor);
            }
            else
                ScreenEffects.Instance.TriggerHitFlash();
        }

        // Camera recoil (backward kick on impact)
        if (PipeCamera.Instance != null)
            PipeCamera.Instance.Recoil(0.35f);

        // Reset multiplier
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerHit();

        // Scatter skiing poop buddies on hit!
        if (PoopBuddyChain.Instance != null && PoopBuddyChain.Instance.BuddyCount > 0)
            PoopBuddyChain.Instance.ScatterAll();

        _stunCoroutine = StartCoroutine(StunCoroutine());
    }

    IEnumerator StunCoroutine()
    {
        // === STUN PHASE ===
        _hitState = HitState.Stunned;
        _hitPhaseDuration = stunDuration;
        _hitPhaseTimer = 0f;
        float originalMax = maxSpeed;

        // Immediate slowdown
        maxSpeed *= stunSpeedMult;
        _currentSpeed *= stunSpeedMult;
        _angularVelocity *= 0.2f; // kill momentum

        while (_hitPhaseTimer < stunDuration)
        {
            _hitPhaseTimer += Time.deltaTime;
            yield return null;
        }

        // === RECOVERY PHASE ===
        _hitState = HitState.Recovering;
        _hitPhaseDuration = recoveryDuration;
        _hitPhaseTimer = 0f;
        maxSpeed = originalMax;

        float recoveryStartSpeed = _currentSpeed;
        while (_hitPhaseTimer < recoveryDuration)
        {
            // Ease-out quadratic: fast initial snap, smooth settle
            float t = _hitPhaseTimer / recoveryDuration;
            float easeT = 1f - (1f - t) * (1f - t); // quadratic ease-out
            _currentSpeed = Mathf.Lerp(recoveryStartSpeed, forwardSpeed, easeT);
            _hitPhaseTimer += Time.deltaTime;
            yield return null;
        }

        // === INVINCIBILITY PHASE ===
        _hitState = HitState.Invincible;
        _hitPhaseDuration = invincibilityDuration;
        _hitPhaseTimer = 0f;

        // Golden shimmer during i-frames so player knows they're protected
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.SetInvincShimmer(1f);

        while (_hitPhaseTimer < invincibilityDuration)
        {
            _hitPhaseTimer += Time.deltaTime;
            yield return null;
        }

        // Ensure all renderers visible
        if (_renderers != null)
        {
            foreach (var r in _renderers)
                if (r != null) r.enabled = true;
        }

        // Flash to indicate invincibility ending + clear shimmer
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.TriggerInvincibilityFlash();

        _hitState = HitState.Normal;
        _hitPhaseDuration = 0f;
        _hitPhaseTimer = 0f;
    }

    // === STOMP SYSTEM ===

    /// <summary>
    /// Called when player stomps an obstacle while jumping. Bounces back up for combo chains.
    /// </summary>
    public void StompBounce()
    {
        _stompCombo++;
        _stompComboTimer = STOMP_COMBO_TIMEOUT;

        // Score bonus: increases with combo
        int stompScore = 50 * _stompCombo;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(stompScore);

        // Score popup
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowStomp(transform.position, _stompCombo, stompScore);

        // Freeze frame on combo stomps
        if (_stompCombo >= 2 && GameManager.Instance != null)
            GameManager.Instance.TriggerFreezeFrame(0.05f);

        // Register as combo event
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.RegisterEvent(ComboSystem.EventType.NearMiss); // reuse near-miss for combo

        // Bounce! Relaunch into another jump immediately
        _trickDirection = 0;
        _trickAngle = 0f;
        _tricksCompleted = 0;
        _isJumping = true;
        _jumpTimer = 0f;
        _jumpDuration = jumpArcDuration * 0.8f; // slightly shorter bounces
        _jumpHeight = jumpArcHeight * (0.8f + _stompCombo * 0.1f); // higher with combo
        _jumpHeight = Mathf.Min(_jumpHeight, jumpArcHeight * 2f); // cap it

        // Satisfying stomp juice
        if (PipeCamera.Instance != null)
        {
            PipeCamera.Instance.Shake(0.15f + _stompCombo * 0.05f);
            PipeCamera.Instance.PunchFOV(2f + _stompCombo * 0.5f);
        }
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayStomp();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayStompSquash(transform.position);
        HapticManager.MediumTap();
    }

    // === JUMP (Mario Kart style) ===

    public void LaunchJump(float height, float duration)
    {
        if (_isJumping) return;
        _stompCombo = 0; // reset stomp combo on fresh jump
        _trickDirection = 0;
        _trickAngle = 0f;
        _tricksCompleted = 0;
        _isJumping = true;
        _jumpTimer = 0f;
        _jumpDuration = duration > 0 ? Mathf.Max(duration, jumpArcDuration) : jumpArcDuration;
        _jumpHeight = height > 0 ? Mathf.Max(height, jumpArcHeight) : jumpArcHeight;

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayJumpLaunch();

        if (PipeCamera.Instance != null)
            PipeCamera.Instance.PunchFOV(3f);

        HapticManager.LightTap();
    }

    public void ActivateCoinMagnet(float duration)
    {
        _coinMagnetTimer = duration;
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.StartCoinMagnet(transform);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayCoinMagnet();
    }

    void AttractNearbyCoins()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, COIN_MAGNET_RADIUS);
        foreach (var col in nearby)
        {
            if (col == null) continue;
            Collectible coin = col.GetComponent<Collectible>();
            if (coin != null)
            {
                // Pull coin toward player
                Vector3 dir = (transform.position - coin.transform.position).normalized;
                coin.transform.position += dir * 12f * Time.deltaTime;
            }
        }
    }

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        StartCoroutine(SpeedBoostCoroutine(multiplier, duration));
    }

    private IEnumerator SpeedBoostCoroutine(float multiplier, float duration)
    {
        float originalMax = maxSpeed;
        maxSpeed *= multiplier;
        _currentSpeed *= multiplier;
        _boostTimeRemaining = duration;
        _boostDuration = duration;

        if (PipeCamera.Instance != null)
            PipeCamera.Instance.PunchFOV(6f);
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.StartBoostTrail(transform);
            if (Camera.main != null)
                ParticleManager.Instance.StartSpeedLines(Camera.main.transform);
        }
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlaySpeedBoost();
        if (ScreenEffects.Instance != null)
        {
            ScreenEffects.Instance.TriggerPowerUpFlash();
            ScreenEffects.Instance.FlashSpeedStreaks(1.5f);
        }
        HapticManager.MediumTap();

        // Tick down boost with visual feedback
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _boostTimeRemaining = duration - elapsed;

            // Intensify exhaust trail color as boost winds down (cyan -> orange -> red)
            float t = elapsed / duration;
            if (ParticleManager.Instance != null)
            {
                Color exhaust = Color.Lerp(
                    new Color(0f, 0.9f, 1f),   // cyan at start
                    new Color(1f, 0.4f, 0.1f),  // fiery orange at end
                    t);
                ParticleManager.Instance.SetBoostTrailColor(exhaust);
            }

            // Speed streaks stay strong during boost
            if (ScreenEffects.Instance != null && t < 0.8f)
                ScreenEffects.Instance.FlashSpeedStreaks(0.8f);

            // Boost winding down warning: pulses in last 20%
            if (t >= 0.8f)
            {
                float windDown = (t - 0.8f) / 0.2f; // 0→1 over final 20%
                // Pulsing orange edge flash that accelerates
                float pulseRate = Mathf.Lerp(3f, 8f, windDown);
                float pulse = Mathf.Sin(Time.time * pulseRate * Mathf.PI);
                if (pulse > 0.7f && ScreenEffects.Instance != null)
                    ScreenEffects.Instance.TriggerProximityWarning(); // orange edge pulse

                // One-shot warning at exactly 80% elapsed
                if (!_boostWarningFired)
                {
                    _boostWarningFired = true;
                    HapticManager.LightTap();
                    if (ProceduralAudio.Instance != null)
                        ProceduralAudio.Instance.PlayComboBreak(); // descending tone = "losing power"
                }

                // Stutter the speed lines to suggest fading power
                if (ScreenEffects.Instance != null && pulse > 0f)
                    ScreenEffects.Instance.FlashSpeedStreaks(0.3f * (1f - windDown));
            }

            yield return null;
        }

        _boostWarningFired = false;
        _boostTimeRemaining = 0f;
        maxSpeed = originalMax;
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.StopBoostTrail();
            ParticleManager.Instance.StopSpeedLines();
        }

        // Final haptic + subtle FOV dip when boost ends
        HapticManager.MediumTap();
        if (PipeCamera.Instance != null)
            PipeCamera.Instance.PunchFOV(-3f);
    }

    // Boost state for HUD display
    private float _boostTimeRemaining;
    private float _boostDuration;
    private bool _boostWarningFired;
    public float BoostTimeRemaining => _boostTimeRemaining;
    public float BoostDuration => _boostDuration;
    public bool IsBoosting => _boostTimeRemaining > 0f;

    // === VERTICAL DROP ===

    /// <summary>Enter freefall drop mode. Called by VerticalDrop trigger.</summary>
    public void EnterDrop(float duration, float speed, float moveRadius, float moveSpeed,
        float exitBoostMult, float exitBoostDur)
    {
        if (_isDropping || _isJumping) return;
        _isDropping = true;
        _dropTimer = 0f;
        _dropDuration = duration;
        _dropSpeed = speed;
        _dropMoveRadius = moveRadius;
        _dropMoveSpeed = moveSpeed;
        _dropExitBoostMult = exitBoostMult;
        _dropExitBoostDur = exitBoostDur;
        _dropOffset = Vector2.zero;

        // Kill angular velocity during drop
        _angularVelocity = 0f;

        // Underwater screen tint
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.SetUnderwater(true);

        // Underwater bubble and debris particles
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.StartUnderwaterEffects(transform);
    }

    void UpdateDropState()
    {
        float dt = Time.deltaTime;
        _dropTimer += dt;

        float progress = Mathf.Clamp01(_dropTimer / _dropDuration);

        // 2D movement input (touch + keyboard for pipe cross-section)
        float inputX = 0f, inputY = 0f;

        if (TouchInput.Instance != null)
        {
            inputX = TouchInput.Instance.SteerInput;
            inputY = TouchInput.Instance.VerticalInput; // touch Y for swimming up/down
        }

        // Keyboard input (fallback for X, additive for Y)
        if (Keyboard.current != null)
        {
            if (TouchInput.Instance == null)
            {
                if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
                    inputX -= 1f;
                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
                    inputX += 1f;
            }
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
                inputY += 1f;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
                inputY -= 1f;
        }

        // === UNDERWATER PHYSICS: floaty, inertial movement ===
        // Movement feels like swimming - slow response, drifty
        float floatyLerp = 7f; // responsive but still floaty
        Vector2 inputDir = new Vector2(inputX, inputY);
        Vector2 targetOffset = _dropOffset + inputDir * _dropMoveSpeed * dt;
        if (targetOffset.magnitude > _dropMoveRadius)
            targetOffset = targetOffset.normalized * _dropMoveRadius;
        _dropOffset = Vector2.Lerp(_dropOffset, targetOffset, dt * floatyLerp);

        // Drift slowly back to center when no input (gentle current)
        if (inputDir.magnitude < 0.1f)
            _dropOffset = Vector2.Lerp(_dropOffset, Vector2.zero, dt * 0.15f);

        // Forward speed: gentle swim, then PLUNGE acceleration in the final 20%
        float currentSwimSpeed = _dropSpeed;
        bool plunging = progress > 0.8f;

        if (plunging)
        {
            // Dramatic acceleration! The flush is coming!
            float plungeT = (progress - 0.8f) / 0.2f; // 0→1 over last 20%
            currentSwimSpeed = Mathf.Lerp(_dropSpeed, _dropSpeed * 4f, plungeT * plungeT);

            // Camera effects during plunge
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.PunchFOV(plungeT * 0.5f); // gradual FOV increase
                if (plungeT > 0.5f)
                    PipeCamera.Instance.Shake(plungeT * 0.15f);
            }
        }

        _distanceAlongPath += currentSwimSpeed * dt;

        // Position: pipe center + 2D offset
        if (pipeGen != null)
        {
            Vector3 center, forward, right, up;
            pipeGen.GetPathFrame(_distanceAlongPath, out center, out forward, out right, out up);

            Vector3 targetPos = center + right * _dropOffset.x + up * _dropOffset.y;
            transform.position = Vector3.Lerp(transform.position, targetPos, dt * 8f);

            // Face forward with gentle swimming tilt (not wild spinning)
            Quaternion pathRot = Quaternion.LookRotation(forward, up);
            float tiltX = -inputY * 12f;
            float tiltZ = inputX * 18f;

            // Gentle idle bob when not moving (floating underwater feel)
            if (inputDir.magnitude < 0.1f && !plunging)
            {
                tiltX += Mathf.Sin(Time.time * 1.2f) * 3f;
                tiltZ += Mathf.Sin(Time.time * 0.8f + 1f) * 2f;
            }

            Quaternion tilt = Quaternion.Euler(tiltX, 0, tiltZ);
            Quaternion targetRot = pathRot * tilt;
            float rotSmooth = plunging ? 12f : 4f; // snappier during plunge
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * rotSmooth);
        }

        // Feed slither animation (gentle undulation underwater)
        if (slither != null)
        {
            slither.currentSpeed = currentSwimSpeed / forwardSpeed;
            slither.turnInput = inputX * 0.6f; // gentler slither underwater
        }

        // Check drop end
        if (_dropTimer >= _dropDuration)
            ExitDrop();
    }

    void ExitDrop()
    {
        _isDropping = false;
        _currentAngle = 270f; // snap back to pipe bottom
        _currentSpeed = forwardSpeed; // reset speed before boost

        // Clear underwater tint and particles
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.SetUnderwater(false);
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.StopUnderwaterEffects();

        // === DRAMATIC PLUNGE FLUSH ===
        // The water rushes forward and carries you at super speed!
        ApplySpeedBoost(_dropExitBoostMult, _dropExitBoostDur);

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowMilestone(transform.position + Vector3.up * 2f, "FLUSH!!!");

        if (PipeCamera.Instance != null)
        {
            PipeCamera.Instance.Shake(0.6f);
            PipeCamera.Instance.PunchFOV(12f); // massive FOV punch
        }

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlaySpeedBoost();

        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayWaterSplash(transform.position);
            if (Camera.main != null)
                ParticleManager.Instance.StartSpeedLines(Camera.main.transform);
        }

        HapticManager.HeavyTap();
    }
}
