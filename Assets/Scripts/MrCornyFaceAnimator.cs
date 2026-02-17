using UnityEngine;

/// <summary>
/// Animates Mr. Corny's face features based on game state.
/// Eyes: squint at speed, wide on hit, blink, googly tracking, HYPNOTIC SPIRALS at 5x multiplier.
/// Mouth: expressive jaw system with distinct gestures - smile, intense, angry, O-face, shocked.
/// Upper/lower jaw pivots open/close. Tongue shows when mouth opens. Cheeks puff on smile.
/// Mustache wiggles with speed.
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

    [Header("Mouth References")]
    public Transform mouthGroup;
    public Transform upperJaw;
    public Transform lowerJaw;
    public Transform tongue;
    public Transform cheekL;
    public Transform cheekR;

    [Header("Hypno Spiral References")]
    public Transform hypnoDiscL;
    public Transform hypnoDiscR;

    // Cached defaults
    private Vector3 _leftEyeBaseScale;
    private Vector3 _rightEyeBaseScale;
    private Vector3 _leftPupilBaseScale;
    private Vector3 _rightPupilBaseScale;
    private Vector3 _leftPupilBasePos;
    private Vector3 _rightPupilBasePos;
    private Quaternion _stacheLeftBaseRot;
    private Quaternion _stacheRightBaseRot;
    private Vector3 _mouthGroupBaseScale;
    private Vector3 _mouthGroupBasePos;
    private Quaternion _upperJawBaseRot;
    private Quaternion _lowerJawBaseRot;
    private Vector3 _tongueBaseScale;
    private Vector3 _cheekLBaseScale;
    private Vector3 _cheekRBaseScale;

    private TurdController _tc;
    private float _googlyPhaseL;
    private float _googlyPhaseR;
    private float _blinkTimer;
    private float _blinkDuration;
    private bool _isBlinking;
    private bool _hypnoActive;
    private float _hypnoTransition; // 0=normal, 1=full hypno

    void Start()
    {
        _tc = GetComponentInParent<TurdController>();
        if (_tc == null)
        {
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
        if (mouthGroup)
        {
            _mouthGroupBaseScale = mouthGroup.localScale;
            _mouthGroupBasePos = mouthGroup.localPosition;
        }
        if (upperJaw) _upperJawBaseRot = upperJaw.localRotation;
        if (lowerJaw) _lowerJawBaseRot = lowerJaw.localRotation;
        if (tongue) _tongueBaseScale = tongue.localScale;
        if (cheekL) _cheekLBaseScale = cheekL.localScale;
        if (cheekR) _cheekRBaseScale = cheekR.localScale;

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

        // Get multiplier for hypno check
        float multiplier = 1f;
        if (GameManager.Instance != null)
            multiplier = GameManager.Instance.Multiplier;

        // === BLINK TIMER ===
        _blinkTimer -= dt;
        if (_blinkTimer <= 0f && !_isBlinking && !stunned && !_hypnoActive)
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

        // === HYPNOTIC SPIRAL EYES (at 5x multiplier) ===
        bool shouldHypno = multiplier >= 4.8f; // slight lead-in
        _hypnoTransition = Mathf.MoveTowards(_hypnoTransition, shouldHypno ? 1f : 0f, dt * 3f);

        if (_hypnoTransition > 0.01f && !_hypnoActive)
        {
            _hypnoActive = true;
            if (hypnoDiscL) hypnoDiscL.gameObject.SetActive(true);
            if (hypnoDiscR) hypnoDiscR.gameObject.SetActive(true);
        }
        else if (_hypnoTransition <= 0.01f && _hypnoActive)
        {
            _hypnoActive = false;
            if (hypnoDiscL) hypnoDiscL.gameObject.SetActive(false);
            if (hypnoDiscR) hypnoDiscR.gameObject.SetActive(false);
        }

        // Spin the hypno discs
        if (_hypnoActive)
        {
            float spinSpeed = 360f * _hypnoTransition; // degrees per second
            if (hypnoDiscL)
                hypnoDiscL.Rotate(0, 0, spinSpeed * dt, Space.Self);
            if (hypnoDiscR)
                hypnoDiscR.Rotate(0, 0, -spinSpeed * dt, Space.Self); // opposite direction

            // Scale discs with transition (grow in smoothly)
            float discScale = _hypnoTransition;
            if (hypnoDiscL) hypnoDiscL.localScale = Vector3.one * discScale;
            if (hypnoDiscR) hypnoDiscR.localScale = Vector3.one * discScale;
        }

        // === EYE SCALE (squint/wide) ===
        float eyeYMult = 1f;
        if (_isBlinking)
        {
            eyeYMult = 0.15f;
        }
        else if (stunned)
        {
            eyeYMult = 1.4f; // wide-eyed shock
        }
        else if (_hypnoActive)
        {
            // Hypno: eyes go wide and slightly pulse
            float pulse = 1.2f + Mathf.Sin(Time.time * 6f) * 0.1f;
            eyeYMult = pulse;
        }
        else if (jumping)
        {
            eyeYMult = 1.25f;
        }
        else if (speedRatio > 0.7f)
        {
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

        // === PUPIL SIZE & VISIBILITY ===
        float pupilScale = 1f;
        float pupilAlpha = 1f;
        if (_hypnoActive)
        {
            // Hide pupils behind hypno disc (shrink to nothing)
            pupilScale = Mathf.Lerp(1f, 0.01f, _hypnoTransition);
        }
        else if (stunned)
        {
            pupilScale = 0.5f;
        }
        else if (jumping)
        {
            pupilScale = 1.15f;
        }

        if (leftPupil)
            leftPupil.localScale = Vector3.Lerp(leftPupil.localScale, _leftPupilBaseScale * pupilScale, dt * 10f);
        if (rightPupil)
            rightPupil.localScale = Vector3.Lerp(rightPupil.localScale, _rightPupilBaseScale * pupilScale, dt * 10f);

        // === PUPIL TRACKING (look toward steering direction) ===
        if (!_hypnoActive)
        {
            float steerLook = Mathf.Clamp(angVel * 0.02f, -0.15f, 0.15f);
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
        }

        // === MUSTACHE WIGGLE ===
        float stacheWiggle = Mathf.Sin(Time.time * 4f) * speedRatio * 5f;
        if (stunned) stacheWiggle = Mathf.Sin(Time.time * 12f) * 8f;
        if (_hypnoActive) stacheWiggle = Mathf.Sin(Time.time * 8f) * 10f; // frantic during hypno

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

        // === MOUTH EXPRESSION SYSTEM ===
        AnimateMouth(dt, speedRatio, stunned, jumping, multiplier);
    }

    void AnimateMouth(float dt, float speedRatio, bool stunned, bool jumping, float multiplier)
    {
        if (mouthGroup == null) return;

        // Target values for expression
        Vector3 mouthScale = _mouthGroupBaseScale;
        Vector3 mouthPos = _mouthGroupBasePos;
        float upperJawAngle = 0f;  // positive = open up
        float lowerJawAngle = 0f;  // positive = open down
        float tongueScale = 0f;    // 0 = hidden, 1 = visible
        float cheekPuff = 1f;      // 1 = normal, >1 = puffed
        float lerpSpeed = 10f;

        if (stunned)
        {
            // === SHOCKED O-FACE ===
            // Jaw drops wide open, mouth narrows into O, tongue visible
            mouthScale.x = _mouthGroupBaseScale.x * 0.7f;
            mouthScale.y = _mouthGroupBaseScale.y * 1.3f;
            upperJawAngle = 12f;
            lowerJawAngle = 18f;
            tongueScale = 0.8f;
            cheekPuff = 0.8f; // cheeks pull in
            lerpSpeed = 15f;
        }
        else if (_hypnoActive)
        {
            // === HYPNOTIZED DROOL ===
            // Slack jaw, mouth slightly open, tongue lolling
            float wobble = Mathf.Sin(Time.time * 3f) * 0.05f;
            mouthScale.x = _mouthGroupBaseScale.x * (1f + wobble);
            upperJawAngle = 5f;
            lowerJawAngle = 12f;
            tongueScale = 0.6f + Mathf.Sin(Time.time * 2f) * 0.2f;
            cheekPuff = 0.9f;
        }
        else if (jumping)
        {
            // === EXCITED OPEN GRIN ===
            // Wide open smile, teeth showing, cheeks puffed
            mouthScale.x = _mouthGroupBaseScale.x * 1.25f;
            upperJawAngle = 8f;
            lowerJawAngle = 10f;
            tongueScale = 0f;
            cheekPuff = 1.3f;
        }
        else if (speedRatio > 0.85f)
        {
            // === INTENSE SPEED FACE ===
            // Gritted teeth, jaw clenched, wide grimace
            float intensity = Mathf.InverseLerp(0.85f, 1f, speedRatio);
            mouthScale.x = _mouthGroupBaseScale.x * Mathf.Lerp(1.1f, 1.4f, intensity);
            mouthScale.y = _mouthGroupBaseScale.y * Mathf.Lerp(1f, 0.85f, intensity);
            upperJawAngle = -2f; // clenched = slightly closed
            lowerJawAngle = -2f;
            tongueScale = 0f;
            cheekPuff = 1f + intensity * 0.2f;
            // Jaw trembles at extreme speed
            if (intensity > 0.5f)
            {
                float tremble = Mathf.Sin(Time.time * 25f) * intensity * 2f;
                lowerJawAngle += tremble;
            }
        }
        else if (speedRatio > 0.5f)
        {
            // === DETERMINED / FOCUSED ===
            // Slight frown, jaw set, narrower mouth
            float det = Mathf.InverseLerp(0.5f, 0.85f, speedRatio);
            mouthScale.x = _mouthGroupBaseScale.x * Mathf.Lerp(1f, 1.1f, det);
            mouthScale.y = _mouthGroupBaseScale.y * Mathf.Lerp(1f, 0.9f, det);
            upperJawAngle = 0f;
            lowerJawAngle = Mathf.Lerp(0f, 3f, det);
            tongueScale = 0f;
            cheekPuff = 1f;
        }
        else if (_isBlinking)
        {
            // === BLINK: mouth purses ===
            mouthScale.x = _mouthGroupBaseScale.x * 0.8f;
            mouthScale.y = _mouthGroupBaseScale.y * 0.8f;
            upperJawAngle = -1f;
            lowerJawAngle = -1f;
            tongueScale = 0f;
            cheekPuff = 0.9f;
        }
        else
        {
            // === IDLE SMILE ===
            // Gentle smile with slight breathing animation
            float breathe = Mathf.Sin(Time.time * 1.5f);
            mouthScale.x = _mouthGroupBaseScale.x * (1.05f + breathe * 0.03f);
            mouthScale.y = _mouthGroupBaseScale.y * (0.95f - breathe * 0.02f);
            upperJawAngle = 2f + breathe * 0.5f;
            lowerJawAngle = 3f + breathe * 0.8f;
            tongueScale = 0f;
            cheekPuff = 1.1f + breathe * 0.05f;
        }

        // Apply mouth group scale and position
        mouthGroup.localScale = Vector3.Lerp(mouthGroup.localScale, mouthScale, dt * lerpSpeed);
        mouthGroup.localPosition = Vector3.Lerp(mouthGroup.localPosition, mouthPos, dt * lerpSpeed);

        // Apply jaw rotations (pivot around X axis = open/close)
        if (upperJaw)
        {
            Quaternion targetRot = _upperJawBaseRot * Quaternion.Euler(-upperJawAngle, 0, 0);
            upperJaw.localRotation = Quaternion.Slerp(upperJaw.localRotation, targetRot, dt * lerpSpeed);
        }
        if (lowerJaw)
        {
            Quaternion targetRot = _lowerJawBaseRot * Quaternion.Euler(lowerJawAngle, 0, 0);
            lowerJaw.localRotation = Quaternion.Slerp(lowerJaw.localRotation, targetRot, dt * lerpSpeed);
        }

        // Apply tongue visibility (scale from 0 to base)
        if (tongue)
        {
            Vector3 tScale = _tongueBaseScale * tongueScale;
            tongue.localScale = Vector3.Lerp(tongue.localScale, tScale, dt * 8f);
        }

        // Apply cheek puff
        if (cheekL)
            cheekL.localScale = Vector3.Lerp(cheekL.localScale, _cheekLBaseScale * cheekPuff, dt * 8f);
        if (cheekR)
            cheekR.localScale = Vector3.Lerp(cheekR.localScale, _cheekRBaseScale * cheekPuff, dt * 8f);
    }
}
