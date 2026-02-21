using UnityEngine;

/// <summary>
/// Pipe-center rail camera. Rides the center of the pipe like a rail,
/// a fixed distance behind the turd, looking forward down the tunnel.
/// </summary>
public class PipeCamera : MonoBehaviour
{
    public static PipeCamera Instance { get; private set; }

    [Header("Target")]
    public Transform target;

    [Header("Follow Settings")]
    [Tooltip("How far behind the turd (along the pipe path) the camera sits")]
    public float followDistance = 4.5f;
    [Tooltip("How far ahead of the turd the camera looks")]
    public float lookAhead = 7f;
    public float positionSmooth = 12f;
    public float rotationSmooth = 10f;

    [Header("Pipe Awareness")]
    public float pipeRadius = 3.5f;
    [Tooltip("How far from the player toward pipe center (0=at player, 1=at center)")]
    [Range(0f, 1f)]
    public float centerPull = 0.45f;

    [Header("FOV")]
    public float baseFOV = 68f;
    public float speedFOVBoost = 8f;

    // kept for API compatibility with SceneDiagnostics
    [HideInInspector] public float heightAbovePlayer = 1.2f;
    [HideInInspector] public float playerBias = 0f;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private Camera _cam;
    private Vector3 _velocity;
    private bool _initialized = false;
    private float _debugTimer = 0f;

    // Juice
    private float _shakeIntensity;
    private float _fovPunch;
    private float _smoothCamBlend; // smoothed fork blend for camera (avoids jerky transitions)

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

            // === CAMERA POSITION: Behind the turd, elevated toward center ===
            float camDist = Mathf.Max(0f, playerDist - followDistance);
            Vector3 camCenter, camFwd, camRight, camUp;
            Vector3 playerCenter, playerFwd, playerRight, playerUp;

            // Branch-aware path following: use the same fork/branch as the player
            PipeFork fork = _tc.CurrentFork;
            int branch = _tc.ForkBranch;

            if (fork != null && branch >= 0)
            {
                // Get main path frame first
                _pipeGen.GetPathFrame(camDist, out camCenter, out camFwd, out camRight, out camUp);
                Vector3 bCamC, bCamF, bCamR, bCamU;
                if (fork.GetBranchFrame(branch, camDist, out bCamC, out bCamF, out bCamR, out bCamU))
                {
                    float targetBlend = fork.GetBranchBlend(camDist);
                    _smoothCamBlend = Mathf.Lerp(_smoothCamBlend, targetBlend, Time.deltaTime * 4f);
                    // Blend position into the branch
                    camCenter = Vector3.Lerp(camCenter, bCamC, _smoothCamBlend);
                    // Smoothly blend FORWARD direction toward branch so camera looks
                    // where the player is actually going (not back along main path)
                    camFwd = Vector3.Slerp(camFwd, bCamF, _smoothCamBlend).normalized;
                }

                // Player position: blend center and forward for look target
                _pipeGen.GetPathFrame(playerDist, out playerCenter, out playerFwd, out playerRight, out playerUp);
                Vector3 bPC, bPF, bPR, bPU;
                if (fork.GetBranchFrame(branch, playerDist, out bPC, out bPF, out bPR, out bPU))
                {
                    float pBlend = fork.GetBranchBlend(playerDist);
                    playerCenter = Vector3.Lerp(playerCenter, bPC, pBlend);
                    playerFwd = Vector3.Slerp(playerFwd, bPF, pBlend).normalized;
                }
            }
            else
            {
                _smoothCamBlend = 0f; // reset when not in fork
                _pipeGen.GetPathFrame(camDist, out camCenter, out camFwd, out camRight, out camUp);
                _pipeGen.GetPathFrame(playerDist, out playerCenter, out playerFwd, out playerRight, out playerUp);
            }

            // Player's offset from pipe center (their position on the pipe wall)
            Vector3 playerOffset = target.position - playerCenter;

            // Camera: behind player on the path, at the player's angular position
            // but pulled toward pipe center by centerPull (0.45 = slightly above/behind)
            Vector3 desiredPos = camCenter + playerOffset * (1f - centerPull);

            // === LOOK TARGET: Ahead of the turd, at the turd's level ===
            Vector3 lookTarget = target.position + camFwd * lookAhead;

            // === WORLD UP ===
            Vector3 upDir = Vector3.up;
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

            // Smooth position along the rail
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPos, ref _velocity, 1f / positionSmooth);

            // Smooth rotation to look forward
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

            // Debug: log positions periodically so we can verify
            _debugTimer += Time.deltaTime;
            if (_debugTimer > 3f)
            {
                _debugTimer = 0f;
                Debug.Log($"[PipeCamera] playerDist={playerDist:F1} camDist={camDist:F1} " +
                    $"camPos={transform.position} playerPos={target.position} " +
                    $"camZ={transform.position.z:F1} playerZ={target.position.z:F1} " +
                    $"delta={target.position.z - transform.position.z:F1}(+ahead/-behind)");
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
