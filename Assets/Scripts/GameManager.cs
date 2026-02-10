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

    private bool _isGameOver = false;
    private float _gameOverTime;

    // Run stats (reset each run)
    private int _runCoins = 0;
    private int _runNearMisses = 0;
    private int _runBestCombo = 0;

    public int RunCoins => _runCoins;

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
        isPlaying = true;
        _isGameOver = false;
        score = 0;
        distanceTraveled = 0f;
        _runCoins = 0;
        _runNearMisses = 0;
        _runBestCombo = 0;
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

        // Track distance along path
        distanceTraveled = player.DistanceTraveled;
        int distanceScore = Mathf.FloorToInt(distanceTraveled * scorePerMeter);

        if (gameUI != null)
        {
            gameUI.UpdateScore(score + distanceScore);
            gameUI.UpdateDistance(distanceTraveled);
        }
    }

    public void AddScore(int points)
    {
        score += points;
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
