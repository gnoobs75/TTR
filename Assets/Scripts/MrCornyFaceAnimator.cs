using UnityEngine;

/// <summary>
/// Animates Mr. Corny's face features based on game state.
/// Eyes squint at speed, go wide on hit, pupils track steering, mustache wiggles.
/// Attached at scene setup; references wired by AddMrCornyFace().
/// </summary>
public class MrCornyFaceAnimator : MonoBehaviour
{
    [Header("Eye References")]
    public Transform leftEye;
    public Transform rightEye;
    public Transform leftPupil;
    public Transform rightPupil;

    [Header("Mustache References")]
    public Transform stacheLeft;
    public Transform stacheRight;
    public Transform stacheBridge;

    // Cached defaults
    private Vector3 _leftEyeBaseScale;
    private Vector3 _rightEyeBaseScale;
    private Vector3 _leftPupilBaseScale;
    private Vector3 _rightPupilBaseScale;
    private Vector3 _leftPupilBasePos;
    private Vector3 _rightPupilBasePos;
    private Quaternion _stacheLeftBaseRot;
    private Quaternion _stacheRightBaseRot;

    private TurdController _tc;
    private float _googlyPhaseL;
    private float _googlyPhaseR;
    private float _blinkTimer;
    private float _blinkDuration;
    private bool _isBlinking;

    void Start()
    {
        _tc = GetComponentInParent<TurdController>();
        if (_tc == null)
        {
            // Walk up to root to find it
            Transform t = transform.parent;
            while (t != null)
            {
                _tc = t.GetComponent<TurdController>();
                if (_tc != null) break;
                t = t.parent;
            }
        }

        // Cache base transforms
        if (leftEye) _leftEyeBaseScale = leftEye.localScale;
        if (rightEye) _rightEyeBaseScale = rightEye.localScale;
        if (leftPupil)
        {
            _leftPupilBaseScale = leftPupil.localScale;
            _leftPupilBasePos = leftPupil.localPosition;
        }
        if (rightPupil)
        {
            _rightPupilBaseScale = rightPupil.localScale;
            _rightPupilBasePos = rightPupil.localPosition;
        }
        if (stacheLeft) _stacheLeftBaseRot = stacheLeft.localRotation;
        if (stacheRight) _stacheRightBaseRot = stacheRight.localRotation;

        // Randomize googly eye phases so they don't sync
        _googlyPhaseL = Random.Range(0f, Mathf.PI * 2f);
        _googlyPhaseR = Random.Range(0f, Mathf.PI * 2f);
        _blinkTimer = Random.Range(2f, 5f);
    }

    void Update()
    {
        if (_tc == null) return;

        float dt = Time.deltaTime;
        float speedRatio = _tc.CurrentSpeed / Mathf.Max(_tc.maxSpeed, 1f);
        bool stunned = _tc.IsStunned;
        bool jumping = _tc.IsJumping;
        float angVel = _tc.AngularVelocity;

        // === BLINK TIMER ===
        _blinkTimer -= dt;
        if (_blinkTimer <= 0f && !_isBlinking && !stunned)
        {
            _isBlinking = true;
            _blinkDuration = 0.12f;
        }
        if (_isBlinking)
        {
            _blinkDuration -= dt;
            if (_blinkDuration <= 0f)
            {
                _isBlinking = false;
                _blinkTimer = Random.Range(2.5f, 6f);
            }
        }

        // === EYE SCALE (squint/wide) ===
        float eyeYMult = 1f;
        if (_isBlinking)
        {
            eyeYMult = 0.15f; // nearly closed
        }
        else if (stunned)
        {
            // Wide-eyed shock
            eyeYMult = 1.4f;
        }
        else if (jumping)
        {
            // Excited big eyes during jumps
            eyeYMult = 1.25f;
        }
        else if (speedRatio > 0.7f)
        {
            // Squinting at high speed
            float squintAmount = Mathf.InverseLerp(0.7f, 1f, speedRatio);
            eyeYMult = Mathf.Lerp(1f, 0.55f, squintAmount);
        }

        if (leftEye)
        {
            Vector3 s = _leftEyeBaseScale;
            s.y *= eyeYMult;
            leftEye.localScale = Vector3.Lerp(leftEye.localScale, s, dt * 12f);
        }
        if (rightEye)
        {
            Vector3 s = _rightEyeBaseScale;
            s.y *= eyeYMult;
            rightEye.localScale = Vector3.Lerp(rightEye.localScale, s, dt * 12f);
        }

        // === PUPIL SIZE (scared shrink on stun) ===
        float pupilScale = 1f;
        if (stunned)
            pupilScale = 0.5f; // tiny scared pupils
        else if (jumping)
            pupilScale = 1.15f; // slightly dilated excitement

        if (leftPupil)
            leftPupil.localScale = Vector3.Lerp(leftPupil.localScale, _leftPupilBaseScale * pupilScale, dt * 10f);
        if (rightPupil)
            rightPupil.localScale = Vector3.Lerp(rightPupil.localScale, _rightPupilBaseScale * pupilScale, dt * 10f);

        // === PUPIL TRACKING (look toward steering direction) ===
        float steerLook = Mathf.Clamp(angVel * 0.02f, -0.15f, 0.15f);
        // Subtle idle googly wiggle
        float googlyL = Mathf.Sin(Time.time * 1.8f + _googlyPhaseL) * 0.03f;
        float googlyR = Mathf.Sin(Time.time * 2.1f + _googlyPhaseR) * 0.03f;

        if (leftPupil)
        {
            Vector3 targetPos = _leftPupilBasePos;
            targetPos.x += steerLook + googlyL;
            targetPos.y += googlyL * 0.5f;
            leftPupil.localPosition = Vector3.Lerp(leftPupil.localPosition, targetPos, dt * 8f);
        }
        if (rightPupil)
        {
            Vector3 targetPos = _rightPupilBasePos;
            targetPos.x += steerLook + googlyR;
            targetPos.y += googlyR * 0.5f;
            rightPupil.localPosition = Vector3.Lerp(rightPupil.localPosition, targetPos, dt * 8f);
        }

        // === MUSTACHE WIGGLE ===
        float stacheWiggle = Mathf.Sin(Time.time * 4f) * speedRatio * 5f;
        if (stunned) stacheWiggle = Mathf.Sin(Time.time * 12f) * 8f; // frantic wiggle on stun

        if (stacheLeft)
        {
            Quaternion target = _stacheLeftBaseRot * Quaternion.Euler(0, 0, stacheWiggle);
            stacheLeft.localRotation = Quaternion.Slerp(stacheLeft.localRotation, target, dt * 10f);
        }
        if (stacheRight)
        {
            Quaternion target = _stacheRightBaseRot * Quaternion.Euler(0, 0, -stacheWiggle);
            stacheRight.localRotation = Quaternion.Slerp(stacheRight.localRotation, target, dt * 10f);
        }
    }
}
