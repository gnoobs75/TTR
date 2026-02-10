using UnityEngine;

/// <summary>
/// Mario Kart-style chase camera for pipe surfing.
/// Key insight: camera angle LAGS behind player angle so steering
/// shows the turd visually moving left/right across the screen.
/// Without lag, camera follows player exactly = turd appears stationary = "spinning in place".
/// </summary>
public class PipeCamera : MonoBehaviour
{
    public static PipeCamera Instance { get; private set; }

    [Header("Target")]
    public Transform target;

    [Header("Follow Settings")]
    public float followDistance = 4f;
    public float heightAbovePlayer = 1.2f;
    public float lookAhead = 5f;
    public float positionSmooth = 8f;
    public float rotationSmooth = 8f;

    [Header("Pipe Awareness")]
    public float pipeRadius = 3.5f;

    [Header("Angle Tracking")]
    [Tooltip("How fast camera angle follows player angle (lower = more visible steering)")]
    public float angleLag = 3f;

    [Header("FOV")]
    public float baseFOV = 68f;
    public float speedFOVBoost = 8f;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private Camera _cam;
    private Vector3 _velocity;
    private float _smoothAngle = 270f; // starts at bottom
    private bool _initialized = false;

    // Juice
    private float _shakeIntensity;
    private float _fovPunch;

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
        if (target == null || _tc == null) return;

        if (_pipeGen != null)
        {
            float playerDist = _tc.DistanceTraveled;
            float playerAngle = _tc.CurrentAngle;

            // === CAMERA ANGLE: Lag behind player for visible steering ===
            // This is the key to "Mario Kart feel" - you see the turd move left/right
            _smoothAngle = Mathf.LerpAngle(_smoothAngle, playerAngle, Time.deltaTime * angleLag);

            // === CAMERA POSITION: Behind player on the pipe path ===
            float camDist = Mathf.Max(0f, playerDist - followDistance);
            Vector3 camCenter, camFwd, camRight, camUp;
            _pipeGen.GetPathFrame(camDist, out camCenter, out camFwd, out camRight, out camUp);

            // Camera sits at the LAGGED angle (not the player's exact angle)
            float camAngleRad = _smoothAngle * Mathf.Deg2Rad;
            Vector3 camSurfaceDir = camRight * Mathf.Cos(camAngleRad) + camUp * Mathf.Sin(camAngleRad);
            float camRadius = pipeRadius - heightAbovePlayer;
            Vector3 desiredPos = camCenter + camSurfaceDir * camRadius;

            // === LOOK TARGET: The player's actual position (ahead on pipe) ===
            // Look at where the player IS, not where the camera is - creates natural offset
            Vector3 lookTarget = target.position + camFwd * lookAhead * 0.3f;

            // === OUTWARD DIRECTION for camera "up" ===
            Vector3 outward = (desiredPos - camCenter).normalized;
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.up;

            // === APPLY ===
            if (!_initialized)
            {
                // First frame: snap instantly (no weird transition from identity)
                transform.position = desiredPos;
                _smoothAngle = playerAngle;

                Vector3 initLook = (lookTarget - desiredPos);
                if (initLook.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(initLook.normalized, outward);

                _velocity = Vector3.zero;
                _initialized = true;
                return;
            }

            // Smooth position
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPos, ref _velocity, 1f / positionSmooth);

            // Smooth rotation
            Vector3 lookDir = (lookTarget - transform.position);
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, outward);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, Time.deltaTime * rotationSmooth);
            }

            // Speed-based FOV
            if (_cam != null)
            {
                float speedNorm = playerDist > 1f
                    ? Mathf.InverseLerp(6f, 14f, _tc.CurrentSpeed) : 0f;
                float targetFOV = baseFOV + speedNorm * speedFOVBoost + _fovPunch;
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, Time.deltaTime * 5f);
            }
        }
        else
        {
            // Fallback: simple chase cam
            Vector3 desiredPos = target.position - target.forward * followDistance + Vector3.up * 1.5f;
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPos, ref _velocity, 1f / positionSmooth);
            transform.LookAt(target.position + target.forward * lookAhead);
        }

        // Screen shake
        if (_shakeIntensity > 0.005f)
        {
            transform.position += Random.insideUnitSphere * _shakeIntensity;
            _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, Time.deltaTime * 12f);
        }
        else _shakeIntensity = 0f;

        // FOV punch decay
        if (Mathf.Abs(_fovPunch) > 0.1f)
            _fovPunch = Mathf.Lerp(_fovPunch, 0f, Time.deltaTime * 8f);
        else _fovPunch = 0f;
    }

    public void Shake(float intensity)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
    }

    public void PunchFOV(float amount)
    {
        _fovPunch = Mathf.Clamp(_fovPunch + amount, -10f, 15f);
    }
}
