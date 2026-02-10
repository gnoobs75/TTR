using UnityEngine;

/// <summary>
/// Camera that follows the pipe path from the center, looking forward.
/// The pipe stays visually stable. Mr. Corny moves around inside it.
/// Includes camera juice: screen shake, FOV punch, lean into turns.
/// </summary>
public class PipeCamera : MonoBehaviour
{
    public static PipeCamera Instance { get; private set; }

    [Header("Target")]
    public Transform target;

    [Header("Follow Settings")]
    public float followDistance = 9f;
    public float cameraRadius = 0.4f;
    public float smoothSpeed = 8f;
    public float lookAhead = 7f;

    [Header("Pipe Awareness")]
    public float pipeRadius = 3.5f;

    [Header("FOV")]
    public float baseFOV = 68f;
    public float speedFOVBoost = 8f;

    [Header("Juice")]
    public float leanAmount = 2.5f;
    public float leanSmooth = 5f;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private Camera _cam;
    private Vector3 _velocity;

    // Juice state
    private float _shakeIntensity;
    private float _fovPunch;
    private float _currentLean;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        _cam = GetComponent<Camera>();
        if (target != null)
            _tc = target.GetComponent<TurdController>();
        if (_cam != null)
            _cam.fieldOfView = baseFOV;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (_pipeGen != null && _tc != null)
        {
            // Camera sits near the pipe CENTER, behind the player
            float camDist = Mathf.Max(0f, _tc.DistanceTraveled - followDistance);
            Vector3 camCenter, camFwd, camRight, camUp;
            _pipeGen.GetPathFrame(camDist, out camCenter, out camFwd, out camRight, out camUp);

            // Stay near pipe center - slight offset toward player so view isn't perfectly centered
            float playerAngle = _tc.CurrentAngle * Mathf.Deg2Rad;
            Vector3 towardPlayer = camRight * Mathf.Cos(playerAngle) + camUp * Mathf.Sin(playerAngle);
            Vector3 desiredPos = camCenter + towardPlayer * cameraRadius;

            // Look ahead along the pipe path center
            float lookDist = _tc.DistanceTraveled + lookAhead;
            Vector3 lookCenter, lookFwd;
            _pipeGen.GetPathInfo(lookDist, out lookCenter, out lookFwd);

            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocity, 1f / smoothSpeed);

            // Use pipe's local up as camera up - pipe stays visually stable
            Quaternion targetRot = Quaternion.LookRotation((lookCenter - transform.position).normalized, camUp);

            // Lean into turns based on angular velocity
            float leanTarget = -_tc.AngularVelocity * 0.012f;
            leanTarget = Mathf.Clamp(leanTarget, -leanAmount, leanAmount);
            _currentLean = Mathf.Lerp(_currentLean, leanTarget, Time.deltaTime * leanSmooth);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * smoothSpeed);
            transform.Rotate(Vector3.forward, _currentLean, Space.Self);

            // Speed-based FOV + punch
            if (_cam != null)
            {
                float speedNorm = _tc.DistanceTraveled > 1f ? Mathf.InverseLerp(6f, 14f, _tc.CurrentSpeed) : 0f;
                float targetFOV = baseFOV + speedNorm * speedFOVBoost + _fovPunch;
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, Time.deltaTime * 3f);
            }
        }
        else
        {
            // Fallback
            Vector3 desiredPos = target.position - target.forward * followDistance + Vector3.up * 2f;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocity, 1f / smoothSpeed);
            transform.LookAt(target.position + target.forward * lookAhead);
        }

        // Screen shake
        if (_shakeIntensity > 0.005f)
        {
            transform.position += Random.insideUnitSphere * _shakeIntensity;
            _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, Time.deltaTime * 8f);
        }
        else
        {
            _shakeIntensity = 0f;
        }

        // Decay FOV punch
        if (Mathf.Abs(_fovPunch) > 0.1f)
            _fovPunch = Mathf.Lerp(_fovPunch, 0f, Time.deltaTime * 4f);
        else
            _fovPunch = 0f;
    }

    /// <summary>Trigger screen shake (hit, near-miss, game over).</summary>
    public void Shake(float intensity)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
    }

    /// <summary>FOV punch (speed boost, combo milestone).</summary>
    public void PunchFOV(float amount)
    {
        _fovPunch += amount;
    }
}
