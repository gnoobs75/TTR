using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Central controller for the Brown Town Grand Prix.
/// Manages race state, tracks all racers, calculates positions and time gaps.
/// Race to the Brown Town Sewage Treatment Plant!
/// </summary>
public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance { get; private set; }

    public enum State { PreRace, Countdown, Racing, Finished }

    [Header("Race Settings")]
    public float raceDistance = 1000f; // meters to the Sewage Treatment Plant
    public float countdownDuration = 3f;

    [Header("References")]
    public TurdController playerController;
    public RacerAI[] aiRacers;
    public RaceLeaderboard leaderboard;
    public RaceFinish finishLine;

    // State
    private State _state = State.PreRace;
    private float _raceStartTime;
    private float _countdownTimer;
    private int _nextFinishPlace = 1;

    // Racer tracking
    private List<RacerEntry> _entries = new List<RacerEntry>();
    private float _leaderDistance;

    public State RaceState => _state;
    public float LeaderDistance => _leaderDistance;
    public float RaceTime => _state == State.Racing ? Time.time - _raceStartTime : 0f;
    public float RaceDistance => raceDistance;
    public TurdController PlayerController => playerController;
    public List<RacerEntry> Entries => _entries;

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
            ai = null
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
                    ai = ai
                });
            }
        }
    }

    void Update()
    {
        switch (_state)
        {
            case State.PreRace:
                // Wait for game to start
                if (GameManager.Instance != null && GameManager.Instance.isPlaying)
                    StartCountdown();
                break;

            case State.Countdown:
                UpdateCountdown();
                break;

            case State.Racing:
                UpdateRace();
                break;

            case State.Finished:
                break;
        }
    }

    void StartCountdown()
    {
        _state = State.Countdown;
        _countdownTimer = countdownDuration;

        // Stagger AI starting positions (slightly behind player)
        if (aiRacers != null)
        {
            for (int i = 0; i < aiRacers.Length; i++)
            {
                if (aiRacers[i] != null)
                    aiRacers[i].SetStartOffset(-3f - i * 2f); // 3-9m behind
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

        // Update distances and find leader
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

        // Sort by distance (descending) and assign positions
        _entries.Sort((a, b) => b.distance.CompareTo(a.distance));

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            e.position = i + 1;

            // Calculate time gap to leader (distance gap / leader speed)
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

                // If player finished
                if (e.isPlayer)
                    OnPlayerFinished(e.finishPlace, raceTime);
            }

            _entries[i] = e;
        }

        // Update leaderboard UI
        if (leaderboard != null)
            leaderboard.UpdatePositions(_entries);

        // Check if all racers finished
        bool allFinished = true;
        foreach (var e in _entries)
        {
            if (!e.isFinished)
            {
                allFinished = false;
                break;
            }
        }

        if (allFinished)
            OnRaceComplete();
    }

    void OnPlayerFinished(int place, float time)
    {
        Debug.Log($"TTR Race: Player finished in {place}{GetOrdinal(place)} place! Time: {time:F1}s");

        if (finishLine != null)
            finishLine.OnPlayerFinished(place, time);
    }

    void OnRaceComplete()
    {
        _state = State.Finished;
        Debug.Log("TTR Race: All racers finished! Race complete.");

        if (finishLine != null)
            finishLine.ShowPodium(_entries);
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

        // Force-finish remaining AI racers quickly
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
