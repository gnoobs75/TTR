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
    public float speedFOVBoost = 12f;

    // kept for API compatibility with SceneDiagnostics
    [HideInInspector] public float heightAbovePlayer = 1.2f;
    [HideInInspector] public float playerBias = 0f;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private Camera _cam;
    private Vector3 _velocity;
    private bool _initialized = false;

    // Juice
    private float _shakeIntensity;
    private float _fovPunch;
    private float _smoothCamBlend; // smoothed fork blend for camera (avoids jerky transitions)
    private float _steerTilt;      // smoothed camera roll tilt from steering
    private float _breathePhase;   // subtle organic camera breathing
    private float _recoilAmount;   // backward kick on collision
    private float _lastSpeed;      // for acceleration lean
    private float _tensionBlend;   // 0-1 tension when stunned (tighter camera)

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

            // === TENSION: Tighter camera when stunned for claustrophobic feel ===
            float tensionTarget = 0f;
            if (_tc.CurrentHitState == TurdController.HitState.Stunned)
                tensionTarget = 1f;
            else if (_tc.CurrentHitState == TurdController.HitState.Recovering)
                tensionTarget = 0.4f;
            _tensionBlend = Mathf.Lerp(_tensionBlend, tensionTarget, Time.deltaTime * (tensionTarget > _tensionBlend ? 12f : 3f));

            float effectiveFollow = Mathf.Lerp(followDistance, followDistance * 0.6f, _tensionBlend);
            float effectivePull = Mathf.Lerp(centerPull, centerPull + 0.2f, _tensionBlend);

            // === CAMERA POSITION: Behind the turd, elevated toward center ===
            float camDist = Mathf.Max(0f, playerDist - effectiveFollow);
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
            Vector3 desiredPos = camCenter + playerOffset * (1f - effectivePull);

            // === LOOK TARGET: Ahead of the turd, at the turd's level ===
            // Dynamic look-ahead: further ahead at higher speeds
            float dynLookAhead = lookAhead + Mathf.InverseLerp(6f, 14f, _tc.CurrentSpeed) * 4f;
            Vector3 lookTarget = target.position + camFwd * dynLookAhead;

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

            // Steer tilt: bank camera when player turns for dynamic feel
            if (_tc != null)
            {
                float targetTilt = -_tc.AngularVelocity * 0.06f;
                targetTilt = Mathf.Clamp(targetTilt, -12f, 12f);
                _steerTilt = Mathf.Lerp(_steerTilt, targetTilt, Time.deltaTime * 6f);
                if (Mathf.Abs(_steerTilt) > 0.1f)
                    transform.rotation *= Quaternion.Euler(0, 0, _steerTilt);
            }

            // Acceleration lean: dip forward when speeding up, lean back when slowing
            if (_tc != null)
            {
                float accel = (_tc.CurrentSpeed - _lastSpeed) / Mathf.Max(Time.deltaTime, 0.001f);
                _lastSpeed = _tc.CurrentSpeed;
                float leanAngle = Mathf.Clamp(accel * 0.15f, -3f, 3f);
                transform.rotation *= Quaternion.Euler(leanAngle, 0, 0);
            }

            // Hit recoil: pushes camera backward briefly
            if (_recoilAmount > 0.01f)
            {
                transform.position -= transform.forward * _recoilAmount;
                _recoilAmount = Mathf.Lerp(_recoilAmount, 0f, Time.deltaTime * 10f);
            }

            // Speed-based FOV
            if (_cam != null)
            {
                float speedNorm = playerDist > 1f
                    ? Mathf.InverseLerp(6f, 14f, _tc.CurrentSpeed) : 0f;
                float tensionFOV = _tensionBlend * -4f; // narrow FOV when stunned
                float targetFOV = baseFOV + speedNorm * speedFOVBoost + _fovPunch + tensionFOV;
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, Time.deltaTime * 5f);
            }

            // Zone-aware camera breathing: each zone has a unique feel
            _breathePhase += Time.deltaTime;
            float speedT = Mathf.Clamp01((_tc.CurrentSpeed - 4f) / 10f);

            // Per-zone breathing personality
            float zoneSpeedMult = 1f;    // breathing frequency multiplier
            float zoneAmpMult = 1f;      // breathing amplitude multiplier
            float zoneSecondary = 0.5f;  // secondary axis ratio (side wobble)
            float zoneTertiary = 0f;     // third axis (forward lurch) ratio

            if (PipeZoneSystem.Instance != null)
            {
                int zi = PipeZoneSystem.Instance.CurrentZoneIndex;
                float zb = PipeZoneSystem.Instance.ZoneBlend;
                // Zone breathing profiles: [speedMult, ampMult, secondary, tertiary]
                // Porcelain: calm and gentle
                // Grimy: slightly rougher, more side wobble
                // Toxic: slow and uneasy, forward lurching
                // Rusty: industrial rumble, fast and tight
                // Hellsewer: claustrophobic, rapid shallow breathing
                float[][] profiles = {
                    new[] { 1.0f, 1.0f, 0.4f, 0.0f },  // Porcelain: calm
                    new[] { 1.15f, 1.1f, 0.6f, 0.1f },  // Grimy: rough
                    new[] { 0.7f, 1.4f, 0.5f, 0.25f },  // Toxic: slow uneasy
                    new[] { 1.6f, 0.8f, 0.7f, 0.15f },  // Rusty: industrial
                    new[] { 2.0f, 1.3f, 0.8f, 0.3f },   // Hellsewer: tense
                };
                int next = Mathf.Min(zi + 1, profiles.Length - 1);
                zoneSpeedMult = Mathf.Lerp(profiles[zi][0], profiles[next][0], zb);
                zoneAmpMult = Mathf.Lerp(profiles[zi][1], profiles[next][1], zb);
                zoneSecondary = Mathf.Lerp(profiles[zi][2], profiles[next][2], zb);
                zoneTertiary = Mathf.Lerp(profiles[zi][3], profiles[next][3], zb);
            }

            float breatheSpeed = Mathf.Lerp(1.8f, 0.8f, speedT) * zoneSpeedMult;
            float breatheAmp = Mathf.Lerp(0.02f, 0.005f, speedT) * zoneAmpMult;
            Vector3 breatheOffset = camUp * Mathf.Sin(_breathePhase * breatheSpeed) * breatheAmp
                                  + camRight * Mathf.Sin(_breathePhase * breatheSpeed * 0.7f) * breatheAmp * zoneSecondary
                                  + camFwd * Mathf.Sin(_breathePhase * breatheSpeed * 0.4f) * breatheAmp * zoneTertiary;
            transform.position += breatheOffset;
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

    /// <summary>Brief backward kick on collision. Pairs well with Shake().</summary>
    public void Recoil(float amount = 0.3f)
    {
        _recoilAmount = Mathf.Max(_recoilAmount, amount);
    }
}
