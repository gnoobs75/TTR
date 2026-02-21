using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Central controller for the Brown Town Grand Prix.
/// Manages race state, tracks all racers, calculates positions and time gaps.
/// Spawns 3D finish line gate and handles camera orbit on finish.
/// Race to the Brown Town Sewage Treatment Plant!
/// </summary>
public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance { get; private set; }

    public enum State { PreRace, Countdown, Racing, PlayerFinished, Finished }

    [Header("Race Settings")]
    public float raceDistance = 1000f; // meters to the Sewage Treatment Plant
    public float countdownDuration = 3f;

    [Header("References")]
    public TurdController playerController;
    public RacerAI[] aiRacers;
    public RaceLeaderboard leaderboard;
    public RaceFinish finishLine;
    public PipeGenerator pipeGen;

    // State
    private State _state = State.PreRace;
    private float _raceStartTime;
    private float _countdownTimer;
    private int _nextFinishPlace = 1;

    // Racer tracking
    private List<RacerEntry> _entries = new List<RacerEntry>();
    private float _leaderDistance;

    // Finish line 3D gate
    private GameObject _finishGate;
    private bool _gateSpawned;

    // Camera orbit on finish
    private bool _orbitingCamera;
    private float _orbitTimer;
    private float _orbitDuration = 4f;
    private Vector3 _orbitCenter;
    private float _orbitRadius = 5f;

    // Auto-finish timeout (don't wait forever for AI)
    private float _autoFinishTimer;
    private const float AUTO_FINISH_TIMEOUT = 15f;

    // Position change tracking
    private int _lastPlayerPosition = 0;

    public State RaceState => _state;
    public float LeaderDistance => _leaderDistance;
    public float RaceTime => _state >= State.Racing ? Time.time - _raceStartTime : 0f;
    public float RaceDistance => raceDistance;
    public TurdController PlayerController => playerController;
    public List<RacerEntry> Entries => _entries;
    public bool IsOrbiting => _orbitingCamera;

    public struct RacerEntry
    {
        public string name;
        public Color color;
        public float distance;
        public float speed;
        public int position;       // 1-based
        public float gapToLeader;  // seconds behind leader
        public bool isPlayer;
        public bool isFinished;
        public int finishPlace;    // 0 = not finished yet
        public float finishTime;
        public RacerAI ai;         // null for player
        public Transform transform; // for podium positioning
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        BuildEntries();
    }

    void BuildEntries()
    {
        _entries.Clear();

        // Player entry
        _entries.Add(new RacerEntry
        {
            name = "Mr. Corny",
            color = new Color(0.45f, 0.28f, 0.1f),
            isPlayer = true,
            ai = null,
            transform = playerController != null ? playerController.transform : null
        });

        // AI entries
        if (aiRacers != null)
        {
            foreach (var ai in aiRacers)
            {
                if (ai == null) continue;
                _entries.Add(new RacerEntry
                {
                    name = ai.racerName,
                    color = ai.racerColor,
                    isPlayer = false,
                    ai = ai,
                    transform = ai.transform
                });
            }
        }
    }

    void Update()
    {
        switch (_state)
        {
            case State.PreRace:
                if (GameManager.Instance != null && GameManager.Instance.isPlaying)
                    StartCountdown();
                break;

            case State.Countdown:
                UpdateCountdown();
                break;

            case State.Racing:
                UpdateRace();
                SpawnFinishGateAhead();
                break;

            case State.PlayerFinished:
                UpdateRace(); // keep tracking AI
                UpdateOrbit();
                UpdateAutoFinish();
                break;

            case State.Finished:
                UpdateOrbit();
                break;
        }
    }

    void StartCountdown()
    {
        _state = State.Countdown;
        _countdownTimer = countdownDuration;

        if (aiRacers != null)
        {
            for (int i = 0; i < aiRacers.Length; i++)
            {
                if (aiRacers[i] != null)
                    aiRacers[i].SetStartOffset(-3f - i * 2f);
            }
        }
    }

    void UpdateCountdown()
    {
        _countdownTimer -= Time.deltaTime;
        if (_countdownTimer <= 0f)
        {
            _state = State.Racing;
            _raceStartTime = Time.time;
        }
    }

    void UpdateRace()
    {
        float raceTime = Time.time - _raceStartTime;

        _leaderDistance = 0f;
        float leaderSpeed = 1f;

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];

            if (e.isPlayer && playerController != null)
            {
                e.distance = playerController.DistanceTraveled;
                e.speed = playerController.CurrentSpeed;
            }
            else if (e.ai != null)
            {
                e.distance = e.ai.DistanceTraveled;
                e.speed = e.ai.CurrentSpeed;
            }

            if (e.distance > _leaderDistance)
            {
                _leaderDistance = e.distance;
                leaderSpeed = Mathf.Max(e.speed, 1f);
            }

            _entries[i] = e;
        }

        _entries.Sort((a, b) => b.distance.CompareTo(a.distance));

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            e.position = i + 1;

            if (i == 0)
                e.gapToLeader = 0f;
            else
                e.gapToLeader = (_leaderDistance - e.distance) / leaderSpeed;

            // Check finish line crossing
            if (!e.isFinished && e.distance >= raceDistance)
            {
                e.isFinished = true;
                e.finishPlace = _nextFinishPlace++;
                e.finishTime = raceTime;

                if (e.ai != null)
                    e.ai.OnFinish(raceTime);

                if (e.isPlayer)
                    OnPlayerFinished(e.finishPlace, raceTime);
            }

            _entries[i] = e;
        }

        // Position change announcements
        foreach (var e in _entries)
        {
            if (e.isPlayer && !e.isFinished)
            {
                if (_lastPlayerPosition > 0 && e.position != _lastPlayerPosition)
                {
                    bool improved = e.position < _lastPlayerPosition;
                    string posStr = e.position + GetOrdinal(e.position);
                    if (improved)
                    {
                        // Player moved UP in position
                        if (ScorePopup.Instance != null && playerController != null)
                            ScorePopup.Instance.ShowMilestone(
                                playerController.transform.position + Vector3.up * 2f, posStr + "!");
                        if (PipeCamera.Instance != null)
                            PipeCamera.Instance.PunchFOV(3f);
                        HapticManager.MediumTap();
                    }
                    else
                    {
                        // Player dropped position
                        if (PipeCamera.Instance != null)
                            PipeCamera.Instance.Shake(0.15f);
                        HapticManager.LightTap();
                    }
                }
                _lastPlayerPosition = e.position;
                break;
            }
        }

        if (leaderboard != null)
            leaderboard.UpdatePositions(_entries);

        // Check if all racers finished
        bool allFinished = true;
        foreach (var e in _entries)
        {
            if (!e.isFinished) { allFinished = false; break; }
        }
        if (allFinished)
            OnRaceComplete();
    }

    void OnPlayerFinished(int place, float time)
    {
        _state = State.PlayerFinished;
        _autoFinishTimer = 0f;
        Debug.Log($"TTR Race: Player finished in {place}{GetOrdinal(place)} place! Time: {time:F1}s");

        // Stop player movement
        if (playerController != null)
            playerController.enabled = false;

        // Start camera orbit around player
        StartCameraOrbit();

        if (finishLine != null)
            finishLine.OnPlayerFinished(place, time);

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayCelebration();

        HapticManager.HeavyTap();
    }

    void OnRaceComplete()
    {
        _state = State.Finished;
        Debug.Log("TTR Race: All racers finished! Race complete.");

        if (finishLine != null)
            finishLine.ShowPodium(_entries);
    }

    // === 3D FINISH LINE GATE ===

    void SpawnFinishGateAhead()
    {
        if (_gateSpawned || pipeGen == null) return;

        float playerDist = playerController != null ? playerController.DistanceTraveled : 0f;

        // Spawn gate when player is within 100m of finish
        if (playerDist + 100f >= raceDistance)
        {
            _gateSpawned = true;
            CreateFinishGate();
        }
    }

    void CreateFinishGate()
    {
        Vector3 center, forward, right, up;
        pipeGen.GetPathFrame(raceDistance, out center, out forward, out right, out up);

        _finishGate = new GameObject("FinishGate");
        _finishGate.transform.position = center;
        _finishGate.transform.rotation = Quaternion.LookRotation(forward, up);

        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader shader = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        float radius = pipeGen.pipeRadius * 0.92f;

        // Checkered arch ring at finish
        int segments = 24;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 360f;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 dir = right * Mathf.Cos(rad) + up * Mathf.Sin(rad);
            Vector3 pos = center + dir * radius;

            // Alternating black/white checkered pattern
            bool isBlack = (i % 2 == 0);
            Color col = isBlack ? new Color(0.05f, 0.05f, 0.05f) : Color.white;

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Checker";
            block.transform.SetParent(_finishGate.transform);
            block.transform.position = pos;
            block.transform.rotation = Quaternion.LookRotation(forward, -dir);
            block.transform.localScale = new Vector3(0.8f, 0.4f, 0.3f);

            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", col);
            if (!isBlack)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white * 0.3f);
            }
            block.GetComponent<Renderer>().material = mat;
            Collider c = block.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        // "FINISH" banner across the top
        GameObject bannerObj = new GameObject("FinishBanner3D");
        bannerObj.transform.SetParent(_finishGate.transform);
        bannerObj.transform.position = center + up * (radius * 0.5f);
        bannerObj.transform.rotation = Quaternion.LookRotation(-forward, up);

        // Load font for TextMesh visibility
        Font bannerFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bannerFont == null) bannerFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        TextMesh tm = bannerObj.AddComponent<TextMesh>();
        tm.text = "FINISH";
        tm.fontSize = 80;
        tm.characterSize = 0.08f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.85f, 0.1f);
        tm.fontStyle = FontStyle.Bold;
        if (bannerFont != null) tm.font = bannerFont;
        // Ensure material is set for URP rendering
        MeshRenderer tmr = bannerObj.GetComponent<MeshRenderer>();
        if (tmr != null && bannerFont != null && bannerFont.material != null)
        {
            tmr.sharedMaterial = new Material(bannerFont.material);
            tmr.sharedMaterial.renderQueue = 3100;
        }

        // Second banner facing opposite direction
        GameObject banner2 = new GameObject("FinishBanner3D_Back");
        banner2.transform.SetParent(_finishGate.transform);
        banner2.transform.position = center + up * (radius * 0.5f);
        banner2.transform.rotation = Quaternion.LookRotation(forward, up);
        TextMesh tm2 = banner2.AddComponent<TextMesh>();
        tm2.text = "FINISH";
        tm2.fontSize = 80;
        tm2.characterSize = 0.08f;
        tm2.alignment = TextAlignment.Center;
        tm2.anchor = TextAnchor.MiddleCenter;
        tm2.color = new Color(1f, 0.85f, 0.1f);
        tm2.fontStyle = FontStyle.Bold;
        if (bannerFont != null) tm2.font = bannerFont;
        MeshRenderer tmr2 = banner2.GetComponent<MeshRenderer>();
        if (tmr2 != null && bannerFont != null && bannerFont.material != null)
        {
            tmr2.sharedMaterial = new Material(bannerFont.material);
            tmr2.sharedMaterial.renderQueue = 3100;
        }

        Debug.Log($"TTR: Spawned checkered finish gate at {raceDistance:F0}m");
    }

    // === CAMERA ORBIT ===

    void StartCameraOrbit()
    {
        if (playerController == null) return;
        _orbitingCamera = true;
        _orbitTimer = 0f;
        _orbitCenter = playerController.transform.position;
    }

    void UpdateOrbit()
    {
        if (!_orbitingCamera) return;

        _orbitTimer += Time.deltaTime;
        if (_orbitTimer > _orbitDuration + 2f) // extra 2s pause before podium
        {
            _orbitingCamera = false;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        // Orbit around the player's finish position
        float angle = (_orbitTimer / _orbitDuration) * 360f * Mathf.Deg2Rad;
        float height = 1.5f + Mathf.Sin(_orbitTimer * 0.5f) * 0.5f;
        Vector3 orbitPos = _orbitCenter + new Vector3(
            Mathf.Cos(angle) * _orbitRadius,
            height,
            Mathf.Sin(angle) * _orbitRadius
        );

        cam.transform.position = Vector3.Lerp(cam.transform.position, orbitPos, Time.deltaTime * 3f);
        cam.transform.LookAt(_orbitCenter + Vector3.up * 0.5f);

        // Disable PipeCamera during orbit
        PipeCamera pipeCam = cam.GetComponent<PipeCamera>();
        if (pipeCam != null && pipeCam.enabled)
            pipeCam.enabled = false;
    }

    void UpdateAutoFinish()
    {
        _autoFinishTimer += Time.deltaTime;
        if (_autoFinishTimer >= AUTO_FINISH_TIMEOUT)
        {
            // Force finish all remaining racers
            StartCoroutine(FinishRemainingAI());
            _state = State.Finished; // prevent re-entry
        }
    }

    /// <summary>Called when player crashes. They get last place among unfinished.</summary>
    public void OnPlayerCrashed()
    {
        if (_state != State.Racing) return;

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.isPlayer && !e.isFinished)
            {
                e.isFinished = true;
                e.finishPlace = 5; // last place (DNF)
                e.finishTime = Time.time - _raceStartTime;
                _entries[i] = e;
                break;
            }
        }

        StartCoroutine(FinishRemainingAI());
    }

    IEnumerator FinishRemainingAI()
    {
        yield return new WaitForSeconds(1f);

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (!e.isFinished)
            {
                e.isFinished = true;
                e.finishPlace = _nextFinishPlace++;
                e.finishTime = Time.time - _raceStartTime;
                if (e.ai != null)
                    e.ai.OnFinish(e.finishTime);
                _entries[i] = e;
                yield return new WaitForSeconds(0.3f);
            }
        }

        OnRaceComplete();
    }

    public int GetPlayerPosition()
    {
        foreach (var e in _entries)
            if (e.isPlayer) return e.position;
        return 5;
    }

    static string GetOrdinal(int n)
    {
        if (n == 1) return "st";
        if (n == 2) return "nd";
        if (n == 3) return "rd";
        return "th";
    }
}
