using UnityEngine;

/// <summary>
/// Pipe-center chase camera. Stays at the pipe center looking forward.
/// The pipe appears stationary. The turd visually moves around the pipe
/// cross-section when steering. Uses world up so nothing flips.
/// </summary>
public class PipeCamera : MonoBehaviour
{
    public static PipeCamera Instance { get; private set; }

    [Header("Target")]
    public Transform target;

    [Header("Follow Settings")]
    public float followDistance = 4f;
    public float lookAhead = 5f;
    public float positionSmooth = 10f;
    public float rotationSmooth = 8f;

    [Header("Pipe Awareness")]
    public float pipeRadius = 3.5f;
    [Tooltip("0 = camera at pipe center, 1 = camera at player. 0.3 is good.")]
    [Range(0f, 0.8f)]
    public float playerBias = 0.3f;

    [Header("FOV")]
    public float baseFOV = 68f;
    public float speedFOVBoost = 8f;

    // kept for API compatibility with SceneDiagnostics
    [HideInInspector] public float heightAbovePlayer = 1.2f;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private Camera _cam;
    private Vector3 _velocity;
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

            // === CAMERA POSITION: At pipe center, behind player ===
            float camDist = Mathf.Max(0f, playerDist - followDistance);
            Vector3 camCenter, camFwd, camRight, camUp;
            _pipeGen.GetPathFrame(camDist, out camCenter, out camFwd, out camRight, out camUp);

            // Bias camera slightly toward the player so they're prominent in frame
            // 0 = exact pipe center (pipe perfectly still, player small)
            // 0.3 = slightly toward player (pipe mostly still, player nicely framed)
            Vector3 toPlayer = target.position - camCenter;
            Vector3 desiredPos = camCenter + toPlayer * playerBias;

            // === LOOK TARGET: Pipe center ahead, biased toward player ===
            float lookDist = playerDist + lookAhead;
            Vector3 lookCenter, lookFwd, lookRight, lookUp;
            _pipeGen.GetPathFrame(lookDist, out lookCenter, out lookFwd, out lookRight, out lookUp);

            // Look mostly forward along pipe, with a pull toward where the player is
            Vector3 lookTarget = Vector3.Lerp(lookCenter, target.position + camFwd * lookAhead, 0.3f);

            // === WORLD UP: Pipe stays visually stable, never flips ===
            Vector3 upDir = Vector3.up;
            // Fallback if pipe goes nearly vertical
            if (Mathf.Abs(Vector3.Dot(camFwd, Vector3.up)) > 0.9f)
                upDir = -camFwd.z > 0 ? Vector3.forward : Vector3.back;

            // === APPLY ===
            if (!_initialized)
            {
                transform.position = desiredPos;
                Vector3 initDir = (lookTarget - desiredPos);
                if (initDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(initDir.normalized, upDir);
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
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, upDir);
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
