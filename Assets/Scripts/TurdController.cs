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
    public float jumpArcHeight = 1.2f;
    public float jumpArcDuration = 0.6f;

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

    // Jump state
    private bool _isJumping = false;
    private float _jumpTimer = 0f;
    private float _jumpDuration = 0f;
    private float _jumpHeight = 0f;

    // Stomp combo
    private int _stompCombo = 0;
    private float _stompComboTimer = 0f;
    private const float STOMP_COMBO_TIMEOUT = 2f;

    public float DistanceTraveled => _distanceAlongPath;
    public float CurrentSpeed => _currentSpeed;
    public float CurrentAngle => _currentAngle;
    public float AngularVelocity => _angularVelocity;
    public HitState CurrentHitState => _hitState;
    public bool IsInvincible => _hitState == HitState.Invincible || _hitState == HitState.Stunned;
    public bool IsStunned => _hitState == HitState.Stunned;
    public bool IsJumping => _isJumping;

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

        // === FORWARD SPEED ===
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

        // === POSITION AND ORIENT ===
        if (pipeGen != null)
        {
            Vector3 center, forward, right, up;
            pipeGen.GetPathFrame(_distanceAlongPath, out center, out forward, out right, out up);

            float rad = _currentAngle * Mathf.Deg2Rad;
            Vector3 offset = (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * pipeRadius;
            Vector3 targetPos = center + offset;

            // Jump: smooth parabolic arc above the pipe surface
            if (_isJumping)
            {
                _jumpTimer += Time.deltaTime;
                float jumpT = Mathf.Clamp01(_jumpTimer / _jumpDuration);

                // Parabolic arc: 4h * t * (1-t) peaks at h at t=0.5
                float arcOffset = 4f * _jumpHeight * jumpT * (1f - jumpT);

                // Move away from pipe center (inward = toward center, so we go outward)
                Vector3 surfaceNormal = (targetPos - center).normalized;
                targetPos += surfaceNormal * arcOffset;

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

                    // Landing impact
                    if (PipeCamera.Instance != null)
                        PipeCamera.Instance.Shake(0.2f);
                    if (ProceduralAudio.Instance != null)
                        ProceduralAudio.Instance.PlayObstacleHit();
                    HapticManager.LightTap();
                }
            }

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);

            // Face path direction, roll to match surface
            float surfaceAngle = _currentAngle - 90f;
            Quaternion pathRot = Quaternion.LookRotation(forward, up);
            Quaternion surfaceRoll = Quaternion.Euler(0, 0, surfaceAngle);
            Quaternion targetRot = pathRot * surfaceRoll;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
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
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
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

        // === INVINCIBILITY FLASH ===
        if (_hitState == HitState.Invincible && _renderers != null)
        {
            bool visible = Mathf.Sin(Time.time * flashSpeed * Mathf.PI) > 0f;
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

        // Reset combo
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.ResetCombo();

        // Camera juice
        if (PipeCamera.Instance != null)
        {
            PipeCamera.Instance.Shake(0.4f);
            PipeCamera.Instance.PunchFOV(-4f); // negative = zoom in for impact
        }

        // Audio + haptics
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayObstacleHit();
        HapticManager.HeavyTap();

        _stunCoroutine = StartCoroutine(StunCoroutine());
    }

    IEnumerator StunCoroutine()
    {
        // === STUN PHASE ===
        _hitState = HitState.Stunned;
        float originalMax = maxSpeed;

        // Immediate slowdown
        maxSpeed *= stunSpeedMult;
        _currentSpeed *= stunSpeedMult;
        _angularVelocity *= 0.2f; // kill momentum

        yield return new WaitForSeconds(stunDuration);

        // === RECOVERY PHASE ===
        _hitState = HitState.Recovering;
        maxSpeed = originalMax;

        float elapsed = 0f;
        while (elapsed < recoveryDuration)
        {
            // Gradually ramp speed back up
            _currentSpeed = Mathf.Lerp(_currentSpeed, forwardSpeed, Time.deltaTime * 3f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // === INVINCIBILITY PHASE ===
        _hitState = HitState.Invincible;
        yield return new WaitForSeconds(invincibilityDuration);

        // Ensure all renderers visible
        if (_renderers != null)
        {
            foreach (var r in _renderers)
                if (r != null) r.enabled = true;
        }

        _hitState = HitState.Normal;
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

        // Register as combo event
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.RegisterEvent(ComboSystem.EventType.NearMiss); // reuse near-miss for combo

        // Bounce! Relaunch into another jump immediately
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
        HapticManager.MediumTap();
    }

    // === JUMP (Mario Kart style) ===

    public void LaunchJump(float force, float duration)
    {
        if (_isJumping) return;
        _stompCombo = 0; // reset stomp combo on fresh jump
        _isJumping = true;
        _jumpTimer = 0f;
        _jumpDuration = jumpArcDuration;
        _jumpHeight = jumpArcHeight;

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayJumpLaunch();

        if (PipeCamera.Instance != null)
            PipeCamera.Instance.PunchFOV(3f);

        HapticManager.LightTap();
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

        if (PipeCamera.Instance != null)
            PipeCamera.Instance.PunchFOV(6f);
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.StartBoostTrail(transform);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlaySpeedBoost();
        HapticManager.MediumTap();

        yield return new WaitForSeconds(duration);

        maxSpeed = originalMax;
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.StopBoostTrail();
    }
}
