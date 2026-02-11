using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Central game state manager for Turd Tunnel Rush.
/// Handles score, game over, restart, run stats, and persistent progress.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public bool isPlaying = false;
    public int score = 0;
    public float distanceTraveled = 0f;

    [Header("References")]
    public TurdController player;
    public GameUI gameUI;

    [Header("Scoring")]
    public int scorePerCoin = 10;
    public float scorePerMeter = 1f;

    [Header("Multiplier")]
    public float multiplierGrowthRate = 0.02f;
    public float multiplierMax = 5f;
    public float multiplierDecayOnHit = 0.5f;

    private bool _isGameOver = false;
    private float _gameOverTime;

    // Run stats (reset each run)
    private int _runCoins = 0;
    private int _runNearMisses = 0;
    private int _runBestCombo = 0;

    // Multiplier streak
    private float _multiplier = 1f;
    private float _multiplierTimer = 0f;
    private float _lastActionTime;

    // Distance milestones
    private int _nextMilestoneIdx = 0;
    private static readonly float[] MilestoneDistances = { 100f, 250f, 500f, 750f, 1000f, 1500f, 2000f };
    private static readonly string[] MilestoneNames = {
        "SEPTIC TANK!", "MAIN LINE!", "PUMP STATION!", "DEEP SEWERS!",
        "BROWN TOWN!", "THE ABYSS!", "LEGEND!" };

    // Freeze frame
    private float _freezeTimer = 0f;

    public int RunCoins => _runCoins;
    public float Multiplier => _multiplier;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (gameUI == null)
            StartGame();
    }

    public void StartGame()
    {
        // Check if flush sequence should play
        if (FlushSequence.Instance != null && FlushSequence.Instance.State == FlushSequence.FlushState.Idle)
        {
            FlushSequence.Instance.StartFlushSequence(() => ActuallyStartGame());
            return;
        }
        ActuallyStartGame();
    }

    void ActuallyStartGame()
    {
        isPlaying = true;
        _isGameOver = false;
        score = 0;
        distanceTraveled = 0f;
        _runCoins = 0;
        _runNearMisses = 0;
        _runBestCombo = 0;
        _multiplier = 1f;
        _multiplierTimer = 0f;
        _nextMilestoneIdx = 0;
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.ResetCombo();
        if (ProceduralAudio.Instance != null)
        {
            ProceduralAudio.Instance.PlayGameStart();
            ProceduralAudio.Instance.StartMusic();
        }
        if (gameUI != null)
            gameUI.ShowHUD();
    }

    void Update()
    {
        // Action input (keyboard space OR touch tap via TouchInput)
        bool actionPressed = false;
        if (TouchInput.Instance != null)
            actionPressed = TouchInput.Instance.ActionPressed;
        else if (Keyboard.current != null)
            actionPressed = Keyboard.current.spaceKey.wasPressedThisFrame;

        if (actionPressed)
        {
            if (!isPlaying && !_isGameOver)
            {
                StartGame();
                return;
            }
            if (_isGameOver && Time.time - _gameOverTime > 0.5f)
            {
                RestartGame();
                return;
            }
        }

        if (!isPlaying || player == null) return;

        // Freeze frame
        if (_freezeTimer > 0f)
        {
            _freezeTimer -= Time.unscaledDeltaTime;
            Time.timeScale = _freezeTimer > 0f ? 0.05f : 1f;
        }

        // Track distance along path
        distanceTraveled = player.DistanceTraveled;
        int distanceScore = Mathf.FloorToInt(distanceTraveled * scorePerMeter * _multiplier);

        // Multiplier growth (grows while doing well, no hits)
        _multiplierTimer += Time.deltaTime;
        if (_multiplierTimer > 5f)
        {
            _multiplier = Mathf.Min(_multiplier + multiplierGrowthRate * Time.deltaTime, multiplierMax);
        }

        // Distance milestones
        if (_nextMilestoneIdx < MilestoneDistances.Length &&
            distanceTraveled >= MilestoneDistances[_nextMilestoneIdx])
        {
            string name = MilestoneNames[_nextMilestoneIdx];
            int milestoneBonus = 500 * (_nextMilestoneIdx + 1);
            AddScore(milestoneBonus);

            if (ScorePopup.Instance != null && player != null)
                ScorePopup.Instance.ShowMilestone(player.transform.position + Vector3.up * 2f, name);
            if (ParticleManager.Instance != null && player != null)
                ParticleManager.Instance.PlayCelebration(player.transform.position);
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCelebration();
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.Shake(0.3f);
                PipeCamera.Instance.PunchFOV(6f);
            }
            HapticManager.HeavyTap();

            _nextMilestoneIdx++;
        }

        // Update atmospheric particles
        if (ParticleManager.Instance != null && player != null)
        {
            ParticleManager.Instance.UpdateDustMotes(player.transform.position);
            ParticleManager.Instance.UpdateSewerBubbles(
                player.transform.position + Vector3.down * 2.5f);
        }

        if (gameUI != null)
        {
            gameUI.UpdateScore(score + distanceScore);
            gameUI.UpdateDistance(distanceTraveled);
            gameUI.UpdateMultiplier(_multiplier);
        }
    }

    public void AddScore(int points)
    {
        score += Mathf.RoundToInt(points * _multiplier);
    }

    public void OnPlayerHit()
    {
        _multiplier = Mathf.Max(1f, _multiplier * multiplierDecayOnHit);
        _multiplierTimer = 0f;
    }

    public void TriggerFreezeFrame(float duration = 0.08f)
    {
        _freezeTimer = duration;
        Time.timeScale = 0.05f;
    }

    public void CollectCoin()
    {
        _runCoins++;
        AddScore(scorePerCoin);
    }

    public void RecordNearMiss()
    {
        _runNearMisses++;
    }

    public void RecordCombo(int comboCount)
    {
        if (comboCount > _runBestCombo)
            _runBestCombo = comboCount;
    }

    public void GameOver()
    {
        isPlaying = false;
        _isGameOver = true;
        _gameOverTime = Time.time;

        distanceTraveled = player.DistanceTraveled;
        int finalScore = score + Mathf.FloorToInt(distanceTraveled * scorePerMeter);

        // Record run in persistent data
        PlayerData.RecordRun(_runCoins, distanceTraveled, finalScore, _runNearMisses, _runBestCombo);

        // Check daily challenge
        if (ChallengeSystem.Instance != null)
            ChallengeSystem.Instance.CheckRun(_runCoins, distanceTraveled, _runNearMisses, _runBestCombo, finalScore);

        if (ProceduralAudio.Instance != null)
        {
            ProceduralAudio.Instance.StopMusic();
            ProceduralAudio.Instance.PlayGameOver();
        }

        if (gameUI != null)
            gameUI.ShowGameOver(finalScore, PlayerData.HighScore, _runCoins, distanceTraveled, _runNearMisses, _runBestCombo);

        if (player != null)
            player.enabled = false;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
