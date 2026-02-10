using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AI controller for "Smooth Snake" - the rival racer poop character.
/// Follows the pipe path with random steering, rubber-banding, obstacle dodging,
/// eye tracking, slither animation, and taunts when ahead.
/// </summary>
public class SmoothSnakeAI : MonoBehaviour
{
    [Header("Movement")]
    public float baseSpeed = 7f;
    public float maxSpeed = 13f;

    [Header("Steering")]
    public float steerSpeed = 2f;
    public float steerChangeInterval = 1.5f;

    [Header("Rubber Banding")]
    public float catchUpMultiplier = 1.3f;
    public float slowDownMultiplier = 0.8f;
    public float rubberBandDistance = 15f;

    [Header("Pipe")]
    public float pipeRadius = 3f;
    public PipeGenerator pipeGen;

    [Header("Obstacle Dodge")]
    public float dodgeLookAhead = 6f;
    public float dodgeSteerStrength = 3f;

    [Header("Taunt")]
    public float tauntLeadDistance = 8f;
    public float tauntCooldownMin = 5f;
    public float tauntCooldownMax = 10f;

    private float _distanceAlongPath = 0f;
    private float _currentAngle = 250f;
    private float _currentSpeed;
    private float _targetSteer;
    private float _steerInput;
    private float _nextSteerChange;
    private float _nextTauntTime;
    private TurdController _playerController;

    // Slither
    private TurdSlither _slither;

    // Eye tracking
    private List<Transform> _pupils = new List<Transform>();
    private List<Transform> _eyes = new List<Transform>();

    public float DistanceTraveled => _distanceAlongPath;

    void Start()
    {
        _currentSpeed = baseSpeed;
        if (pipeGen == null)
            pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        _playerController = Object.FindFirstObjectByType<TurdController>();
        _slither = GetComponent<TurdSlither>();
        _nextTauntTime = Time.time + Random.Range(tauntCooldownMin, tauntCooldownMax);

        // Find eye parts
        CreatureAnimUtils.FindChildrenRecursive(transform, "pupil", _pupils);
        CreatureAnimUtils.FindChildrenRecursive(transform, "eye", _eyes);
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;
        if (pipeGen == null) return;

        // === STEERING: Random + Obstacle Dodge ===
        if (Time.time > _nextSteerChange)
        {
            _targetSteer = Random.Range(-1f, 1f);
            _nextSteerChange = Time.time + Random.Range(0.8f, steerChangeInterval * 2f);
        }

        // Obstacle dodge: raycast forward along path
        float dodgeSteer = 0f;
        if (_playerController != null)
        {
            // Check for obstacles ahead by sampling nearby obstacles
            Collider[] nearby = Physics.OverlapSphere(transform.position + transform.forward * dodgeLookAhead, 2f);
            foreach (var col in nearby)
            {
                if (col.CompareTag("Obstacle") && col.transform != transform)
                {
                    // Steer away from obstacle
                    Vector3 toObs = col.transform.position - transform.position;
                    Vector3 localObs = transform.InverseTransformDirection(toObs);
                    dodgeSteer = localObs.x > 0 ? -dodgeSteerStrength : dodgeSteerStrength;
                    break;
                }
            }
        }

        float combinedSteer = _targetSteer + dodgeSteer;
        _steerInput = Mathf.Lerp(_steerInput, combinedSteer, Time.deltaTime * 3f);

        // === RUBBER-BAND SPEED ===
        float targetSpeed = baseSpeed + (_distanceAlongPath * 0.01f);
        if (_playerController != null)
        {
            float playerDist = _playerController.DistanceTraveled;
            float diff = playerDist - _distanceAlongPath;

            if (diff > rubberBandDistance)
                targetSpeed *= catchUpMultiplier;
            else if (diff < -rubberBandDistance)
                targetSpeed *= slowDownMultiplier;
        }
        targetSpeed = Mathf.Clamp(targetSpeed, baseSpeed * 0.5f, maxSpeed);
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * 2f);

        // Advance
        _distanceAlongPath += _currentSpeed * Time.deltaTime;

        // Steer
        _currentAngle += _steerInput * steerSpeed * Time.deltaTime * 40f;

        // === POSITION ON PIPE ===
        Vector3 center, forward, right, up;
        pipeGen.GetPathFrame(_distanceAlongPath, out center, out forward, out right, out up);

        float rad = _currentAngle * Mathf.Deg2Rad;
        Vector3 offset = (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * pipeRadius;
        Vector3 targetPos = center + offset;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 6f);

        // Face forward with surface roll
        float surfaceAngle = _currentAngle - 90f;
        Quaternion pathRot = Quaternion.LookRotation(forward, up);
        Quaternion surfaceRoll = Quaternion.Euler(0, 0, surfaceAngle);
        transform.rotation = Quaternion.Slerp(transform.rotation, pathRot * surfaceRoll, Time.deltaTime * 5f);

        // === SLITHER ANIMATION ===
        if (_slither != null)
        {
            _slither.currentSpeed = _currentSpeed / baseSpeed;
            _slither.turnInput = _steerInput;
        }

        // === EYE TRACKING ===
        if (_playerController != null)
        {
            Vector3 playerPos = _playerController.transform.position;
            for (int i = 0; i < _pupils.Count; i++)
            {
                Transform eye = (i < _eyes.Count) ? _eyes[i] : _pupils[i].parent;
                CreatureAnimUtils.TrackEyeTarget(_pupils[i], eye, playerPos, 0.06f);
            }
        }

        // === TAUNT ===
        if (_playerController != null && Time.time > _nextTauntTime)
        {
            float lead = _distanceAlongPath - _playerController.DistanceTraveled;
            if (lead > tauntLeadDistance)
            {
                if (ProceduralAudio.Instance != null)
                    ProceduralAudio.Instance.PlayAITaunt();
                _nextTauntTime = Time.time + Random.Range(tauntCooldownMin, tauntCooldownMax);
            }
        }
    }
}
