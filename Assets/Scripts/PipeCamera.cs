using UnityEngine;

/// <summary>
/// Over-the-shoulder camera that rides just behind/above Mr. Corny,
/// looking forward so you can see his face. Follows the player's position
/// on the pipe surface, not the pipe center.
/// Includes camera juice: screen shake, FOV punch, lean into turns.
/// </summary>
public class PipeCamera : MonoBehaviour
{
    public static PipeCamera Instance { get; private set; }

    [Header("Target")]
    public Transform target;

    [Header("Follow Settings")]
    [Tooltip("How far behind the player (along pipe path)")]
    public float followDistance = 3.5f;
    [Tooltip("How far above/outward from the player (toward pipe center)")]
    public float heightAbovePlayer = 0.8f;
    [Tooltip("How far ahead to look (along pipe path)")]
    public float lookAhead = 4f;
    public float positionSmooth = 12f;
    public float rotationSmooth = 10f;

    [Header("Pipe Awareness")]
    public float pipeRadius = 3.5f;

    [Header("FOV")]
    public float baseFOV = 65f;
    public float speedFOVBoost = 6f;

    [Header("Juice")]
    public float leanAmount = 2f;
    public float leanSmooth = 6f;

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
            // === CAMERA POSITION: Behind and slightly above player ===

            // Get the player's current pipe frame
            float playerDist = _tc.DistanceTraveled;
            Vector3 playerCenter, playerFwd, playerRight, playerUp;
            _pipeGen.GetPathFrame(playerDist, out playerCenter, out playerFwd, out playerRight, out playerUp);

            // Get the camera's pipe frame (behind the player)
            float camDist = Mathf.Max(0f, playerDist - followDistance);
            Vector3 camCenter, camFwd, camRight, camUp;
            _pipeGen.GetPathFrame(camDist, out camCenter, out camFwd, out camRight, out camUp);

            // Player's position on the pipe surface
            float playerAngle = _tc.CurrentAngle * Mathf.Deg2Rad;
            Vector3 playerSurfaceDir = camRight * Mathf.Cos(playerAngle) + camUp * Mathf.Sin(playerAngle);

            // Camera sits behind player, at the same angular position but pushed
            // slightly toward pipe center (above/behind the turd)
            float camRadius = pipeRadius - heightAbovePlayer;
            Vector3 desiredPos = camCenter + playerSurfaceDir * camRadius;

            // === LOOK TARGET: Ahead of the player on the pipe surface ===
            float lookDist = playerDist + lookAhead;
            Vector3 lookCenter, lookFwd, lookRight, lookUp;
            _pipeGen.GetPathFrame(lookDist, out lookCenter, out lookFwd, out lookRight, out lookUp);

            // Look at a point on the same angular position but ahead
            Vector3 lookSurfaceDir = lookRight * Mathf.Cos(playerAngle) + lookUp * Mathf.Sin(playerAngle);
            Vector3 lookTarget = lookCenter + lookSurfaceDir * (pipeRadius * 0.5f);

            // Smooth position
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPos, ref _velocity, 1f / positionSmooth);

            // Rotation: look at the target, use the pipe's local "outward" as up
            // This keeps the pipe visually stable - the turd moves, pipe stays put
            Vector3 lookDir = (lookTarget - transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                // Use the direction from pipe center to camera as "up" reference
                // This means the camera rolls with the player around the pipe
                Vector3 camOutward = (transform.position - camCenter).normalized;
                Quaternion targetRot = Quaternion.LookRotation(lookDir, camOutward);

                // Lean into turns
                float leanTarget = -_tc.AngularVelocity * 0.01f;
                leanTarget = Mathf.Clamp(leanTarget, -leanAmount, leanAmount);
                _currentLean = Mathf.Lerp(_currentLean, leanTarget, Time.deltaTime * leanSmooth);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, Time.deltaTime * rotationSmooth);
                transform.Rotate(Vector3.forward, _currentLean, Space.Self);
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
            // Fallback: behind and above target
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
        else
        {
            _shakeIntensity = 0f;
        }

        // FOV punch decay - faster decay so it doesn't linger after jumps
        if (Mathf.Abs(_fovPunch) > 0.1f)
            _fovPunch = Mathf.Lerp(_fovPunch, 0f, Time.deltaTime * 8f);
        else
            _fovPunch = 0f;
    }

    /// <summary>Trigger screen shake (hit, near-miss, game over).</summary>
    public void Shake(float intensity)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
    }

    /// <summary>FOV punch (speed boost, combo milestone). Capped to prevent runaway.</summary>
    public void PunchFOV(float amount)
    {
        _fovPunch = Mathf.Clamp(_fovPunch + amount, -10f, 15f);
    }
}
