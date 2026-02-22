using UnityEngine;
using UnityEngine.UI;

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
    private float _breathePhase;   // subtle organic camera breathing
    private float _recoilAmount;   // backward kick on collision
    private float _tensionBlend;   // 0-1 tension when stunned (tighter camera)
    private Image _fadeOverlay;    // black overlay for fade-in from black
    private float _fadeAlpha = 1f; // starts fully black

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

        // Create fade-from-black overlay
        CreateFadeOverlay();
    }

    void CreateFadeOverlay()
    {
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 100)
            { canvas = c; break; }
        }
        if (canvas == null) return;

        GameObject fadeObj = new GameObject("CameraFade");
        fadeObj.transform.SetParent(canvas.transform, false);
        RectTransform rt = fadeObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _fadeOverlay = fadeObj.AddComponent<Image>();
        _fadeOverlay.color = new Color(0, 0, 0, 1f);
        _fadeOverlay.raycastTarget = false;
        _fadeAlpha = 1f;
    }

    void LateUpdate()
    {
        // Fade-in from black on game start
        if (_fadeAlpha > 0f && _fadeOverlay != null)
        {
            _fadeAlpha -= Time.deltaTime * 1.5f; // ~0.7s fade
            if (_fadeAlpha <= 0f)
            {
                _fadeAlpha = 0f;
                Destroy(_fadeOverlay.gameObject);
                _fadeOverlay = null;
            }
            else
            {
                _fadeOverlay.color = new Color(0, 0, 0, _fadeAlpha);
            }
        }

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

            // === FORK-AWARE PATH FOLLOWING ===
            // Branch paths are just laterally offset from the main pipe — they follow
            // the same general direction. We use branch POSITIONS for camera placement
            // but MAIN PATH forward/up for orientation. Branch forward vectors are
            // unreliable (up to 90° off) because pipe curvature rotates the lateral
            // offset between samples, corrupting the position-delta tangent.
            PipeFork fork = _tc.CurrentFork;
            int branch = _tc.ForkBranch;
            bool inFork = fork != null && branch >= 0;

            // Always get main path frames — these have reliable forward vectors
            _pipeGen.GetPathFrame(camDist, out camCenter, out camFwd, out camRight, out camUp);
            _pipeGen.GetPathFrame(playerDist, out playerCenter, out playerFwd, out playerRight, out playerUp);

            if (inFork)
            {
                // Use the SAME blend as TurdController so camera and player agree
                // on where "pipe center" is. Without this, playerOffset is wrong
                // because the player is at a blended position but camera would
                // subtract the full branch center.
                Vector3 bC, bF, bR, bU;
                if (fork.GetBranchFrame(branch, camDist, out bC, out bF, out bR, out bU))
                {
                    float camBlend = fork.GetBranchBlend(camDist);
                    camCenter = Vector3.Lerp(camCenter, bC, camBlend);
                }

                Vector3 bPC, bPF, bPR, bPU;
                if (fork.GetBranchFrame(branch, playerDist, out bPC, out bPF, out bPR, out bPU))
                {
                    float pBlend = fork.GetBranchBlend(playerDist);
                    playerCenter = Vector3.Lerp(playerCenter, bPC, pBlend);
                }
            }

#if UNITY_EDITOR
            if (inFork)
            {
                float dbgBlend = fork.GetBranchBlend(playerDist);
                Vector3 td = (playerCenter - camCenter).normalized;
                Debug.Log($"[CAM] pDist={playerDist:F1} blend={dbgBlend:F2} tunnel=({td.x:F2},{td.y:F2},{td.z:F2}) mainFwd=({playerFwd.x:F2},{playerFwd.y:F2},{playerFwd.z:F2})");
            }
#endif

            // Player's offset from pipe center (their position on the pipe wall)
            Vector3 playerOffset = target.position - playerCenter;

            // Camera: behind player on the path, at the player's angular position
            // but pulled toward pipe center by centerPull (0.45 = slightly above/behind)
            Vector3 desiredPos = camCenter + playerOffset * (1f - effectivePull);

            // === LOOK TARGET: Ahead of the turd along the ACTUAL tunnel direction ===
            // Use the vector between camera center and player center as the tunnel
            // direction. This naturally follows the branch curve because both centers
            // are blended into the branch. Main path forward points straight through
            // the Y-junction center — wrong when the player is on a branch.
            float dynLookAhead = lookAhead + Mathf.InverseLerp(6f, 14f, _tc.CurrentSpeed) * 4f;
            Vector3 tunnelDir = (playerCenter - camCenter);
            if (tunnelDir.sqrMagnitude > 0.01f)
                tunnelDir.Normalize();
            else
                tunnelDir = playerFwd; // fallback if centers overlap
            Vector3 lookTarget = target.position + tunnelDir * dynLookAhead;

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

    /// <summary>Gradually zoom out for death cam. Adds to followDistance over time.</summary>
    public void DeathZoomOut(float extraDistance = 3f, float duration = 0.8f)
    {
        StartCoroutine(DeathZoomCoroutine(extraDistance, duration));
    }

    System.Collections.IEnumerator DeathZoomCoroutine(float extraDist, float dur)
    {
        float startFollow = followDistance;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            // Ease-out curve for smooth deceleration feel
            float eased = 1f - (1f - t) * (1f - t);
            followDistance = startFollow + extraDist * eased;
            yield return null;
        }
        followDistance = startFollow + extraDist;
    }
}
