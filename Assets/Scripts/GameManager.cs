using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Central game state manager for Turd Tunnel Rush.
/// Handles score, game over, restart, run stats, and persistent progress.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public bool isPlaying = false;
    public bool isTourMode = false;
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
    private float _gameStartTime;
    private bool _restarting = false;

    // Last obstacle that hit the player (for AI death quips)
    [HideInInspector] public string lastHitCreature;

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
    private static readonly float[] MilestoneDistances = { 100f, 250f, 500f, 750f, 1000f, 1500f, 2000f, 2500f };
    private static readonly string[] MilestoneNames = {
        "SEPTIC TANK!", "MAIN LINE!", "PUMP STATION!", "DEEP SEWERS!",
        "BROWN TOWN!", "THE ABYSS!", "LEGEND!", "ABSOLUTE UNIT!" };

    // Freeze frame
    private float _freezeTimer = 0f;

    // Fork approach warning (disabled - replaced by lane zones)
    // private PipeFork _warnedFork;

    // Near-miss streak (consecutive dodges without getting hit)
    private int _nearMissStreak;
    public int NearMissStreak => _nearMissStreak;

    // Speed milestones (one-shot announcements when hitting speed thresholds)
    private int _speedMilestoneReached = -1;
    private static readonly float[] SpeedThresholds = { 8f, 10f, 13f, 16f, 20f };
    private static readonly string[] SpeedNames = {
        "WARMING UP!", "TURBO TURD!", "LUDICROUS SPEED!",
        "GONE PLAID!", "WARP FLUSH!"
    };
    private static readonly Color[] SpeedColors = {
        new Color(0.3f, 0.9f, 1f),   // cyan
        new Color(1f, 0.6f, 0f),     // orange
        new Color(1f, 0.2f, 0.8f),   // hot pink
        new Color(0.9f, 0.1f, 0.1f), // red
        new Color(1f, 1f, 0.2f)      // golden
    };

    public int RunCoins => _runCoins;
    public int RunNearMisses => _runNearMisses;
    public int RunBestCombo => _runBestCombo;
    public float Multiplier => _multiplier;

    // Race stats
    private int _runHitsTaken;
    private int _runBoostsUsed;
    private float _runMaxSpeed;
    private int _runStomps;
    public int RunHitsTaken => _runHitsTaken;
    public int RunBoostsUsed => _runBoostsUsed;
    public float RunMaxSpeed => _runMaxSpeed;
    public int RunStomps => _runStomps;
    public void RecordHit() { _runHitsTaken++; }
    public void RecordBoost() { _runBoostsUsed++; }
    public void RecordStomp() { _runStomps++; }
    public void TrackMaxSpeed(float speed) { if (speed > _runMaxSpeed) _runMaxSpeed = speed; }

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
        // Race mode: skip flush sequence — race has its own intro with camera orbit
        if (RaceManager.Instance != null)
        {
            ActuallyStartGame();
            return;
        }

        // Check if flush sequence should play
        if (FlushSequence.Instance != null && FlushSequence.Instance.State == FlushSequence.FlushState.Idle)
        {
            FlushSequence.Instance.StartFlushSequence(() => ActuallyStartGame());
            return;
        }
        ActuallyStartGame();
    }

    public void StartSewerTour()
    {
        isTourMode = true;
        isPlaying = true;
        _isGameOver = false;
        score = 0;
        distanceTraveled = 0f;

        // Hide start panel
        if (gameUI != null)
        {
            gameUI.ShowHUD();
            // Hide scoring HUD elements in tour mode
            if (gameUI.scoreText != null) gameUI.scoreText.gameObject.SetActive(false);
            if (gameUI.multiplierText != null) gameUI.multiplierText.gameObject.SetActive(false);
            if (gameUI.coinCountText != null) gameUI.coinCountText.gameObject.SetActive(false);
        }

        // Hide overlays that don't apply
        if (ComboSystem.Instance != null) ComboSystem.Instance.enabled = false;
        if (CheerOverlay.Instance != null) CheerOverlay.Instance.gameObject.SetActive(false);

        // Start the tour
        if (SewerTour.Instance != null)
            SewerTour.Instance.StartTour();
    }

    void ActuallyStartGame()
    {
#if UNITY_EDITOR
        Debug.Log("[GAME] === GAME STARTED ===");
#endif
        // Initialize seed: word seed > challenge code > random
        if (SeedManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(SeedChallenge.SeedWord))
                SeedManager.Instance.InitializeSeed(SeedChallenge.WordToSeed(SeedChallenge.SeedWord));
            else if (SeedChallenge.ActiveChallenge.HasValue)
                SeedManager.Instance.InitializeSeed(SeedChallenge.ActiveChallenge.Value.seed);
            else
                SeedManager.Instance.InitializeRandomSeed();
        }

        // Kill any lingering music (splash screen) to prevent overlap
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (src != null && src.clip != null && src.clip.length > 10f && src.isPlaying)
                src.Stop();
        }

        isPlaying = true;
        _isGameOver = false;
        _gameStartTime = Time.time;
        score = 0;
        distanceTraveled = 0f;
        _runCoins = 0;
        _runNearMisses = 0;
        _runBestCombo = 0;
        _runHitsTaken = 0;
        _runBoostsUsed = 0;
        _runMaxSpeed = 0f;
        _runStomps = 0;
        _multiplier = 1f;
        _multiplierTimer = 0f;
        _nextMilestoneIdx = 0;
        _speedMilestoneReached = -1;
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.ResetCombo();
        if (ProceduralAudio.Instance != null)
        {
            ProceduralAudio.Instance.PlayGameStart();
            ProceduralAudio.Instance.StartMusic();
        }
        if (gameUI != null)
            gameUI.ShowHUD();

        if (TutorialOverlay.Instance != null)
            TutorialOverlay.Instance.ResetForNewRun();
        if (AnalyticsManager.Instance != null)
            AnalyticsManager.Instance.LogRunStart();

        // Ghost racer: start playback of best run + begin recording this run
        if (GhostRecorder.Instance != null)
        {
            GhostRecorder.Instance.StartPlayback();
            if (player != null)
                GhostRecorder.Instance.StartRecording(player.transform);
        }

        // Show pause button during gameplay
        if (PauseMenu.Instance != null)
            PauseMenu.Instance.ShowPauseButton();

        // Show music button during gameplay
        if (MusicPanel.Instance != null)
            MusicPanel.Instance.ShowMusicButton();

        // Start signature poop effects
        if (ParticleManager.Instance != null && player != null)
        {
            ParticleManager.Instance.StartStinkCloud(player.transform);
            ParticleManager.Instance.StartSewerFlies(player.transform);
            ParticleManager.Instance.StartZoneTrail(player.transform);
        }
    }

    void Update()
    {
        // Block input while paused or music panel open
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) return;
        if (MusicPanel.Instance != null && MusicPanel.Instance.IsOpen) return;

        // SPACE key shortcut for start/restart (touch is handled by UI buttons only
        // to prevent accidental starts when touching sliders, gallery, etc.)
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (spacePressed)
        {
            // Don't restart during race finish (podium showing)
            bool raceFinished = RaceManager.Instance != null &&
                (RaceManager.Instance.RaceState == RaceManager.State.PlayerFinished ||
                 RaceManager.Instance.RaceState == RaceManager.State.Finished);

            if (!isPlaying && !_isGameOver && !raceFinished)
            {
                StartGame();
                return;
            }
            if (_isGameOver && !_restarting && Time.time - _gameOverTime > 0.5f)
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

        // Track distance along path + max speed
        distanceTraveled = player.DistanceTraveled;
        if (player.CurrentSpeed > _runMaxSpeed)
            _runMaxSpeed = player.CurrentSpeed;
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
#if UNITY_EDITOR
            Debug.Log($"[MILESTONE] {name} at {distanceTraveled:F0}m (+{milestoneBonus} pts) score={score} mult={_multiplier:F1}x");
#endif

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
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerMilestoneFlash();
            HapticManager.HeavyTap();

            _nextMilestoneIdx++;
        }

        // Lane zone approach - no warning, just let the pipe widen naturally

        // Update atmospheric particles
        if (ParticleManager.Instance != null && player != null)
        {
            ParticleManager.Instance.UpdateDustMotes(player.transform.position);
            ParticleManager.Instance.UpdateSewerBubbles(
                player.transform.position + Vector3.down * 2.5f);
            ParticleManager.Instance.UpdateStinkIntensity(player.CurrentSpeed);
        }

        // Speed milestones (one-shot announcement per threshold)
        if (player != null)
        {
            float speed = player.CurrentSpeed;
            for (int i = SpeedThresholds.Length - 1; i >= 0; i--)
            {
                if (speed >= SpeedThresholds[i] && i > _speedMilestoneReached)
                {
                    _speedMilestoneReached = i;
                    if (CheerOverlay.Instance != null)
                        CheerOverlay.Instance.ShowCheer(SpeedNames[i], SpeedColors[i], i >= 3);
                    if (PipeCamera.Instance != null)
                        PipeCamera.Instance.PunchFOV(3f + i * 2f);
                    if (ScreenEffects.Instance != null)
                        ScreenEffects.Instance.FlashSpeedStreaks(0.6f + i * 0.2f);
                    // Bonus score for reaching high speed tiers
                    if (i >= 2)
                    {
                        int speedBonus = 25 * (i + 1);
                        AddScore(speedBonus);
                        if (ScorePopup.Instance != null)
                            ScorePopup.Instance.Show($"+{speedBonus}", player.transform.position + Vector3.up * 1.5f, ScorePopup.PopupType.Coin);
                    }

                    // Escalating haptics + effects for higher tiers
                    if (i >= 4)
                    {
                        HapticManager.HeavyTap();
                        if (ScreenEffects.Instance != null)
                            ScreenEffects.Instance.TriggerMilestoneFlash();
                        if (ProceduralAudio.Instance != null)
                            ProceduralAudio.Instance.PlayCelebration();
                    }
                    else if (i >= 2)
                    {
                        HapticManager.MediumTap();
                        if (ScreenEffects.Instance != null)
                            ScreenEffects.Instance.TriggerPowerUpFlash();
                    }
                    else
                    {
                        HapticManager.LightTap();
                    }
                    break;
                }
            }
        }

        if (gameUI != null)
        {
            gameUI.UpdateScore(score + distanceScore);
            gameUI.UpdateDistance(distanceTraveled);
            gameUI.UpdateMultiplier(_multiplier);
            gameUI.UpdateCoinCount(_runCoins);
            if (player != null) gameUI.UpdateSpeed(player.CurrentSpeed);
        }
    }

    public void AddScore(int points)
    {
        score += Mathf.RoundToInt(points * _multiplier);
    }

    public void OnPlayerHit()
    {
        _runHitsTaken++;
        float oldMult = _multiplier;
        _multiplier = Mathf.Max(1f, _multiplier * multiplierDecayOnHit);
        _multiplierTimer = 0f;
        _nearMissStreak = 0; // reset dodge streak on hit
#if UNITY_EDITOR
        Debug.Log($"[GAME] OnPlayerHit multiplier {oldMult:F2}x → {_multiplier:F2}x");
#endif
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

    // Near-miss streak milestones
    private static readonly int[] DodgeMilestones = { 3, 5, 10, 20 };
    private static readonly string[] DodgeNames = { "SLIPPERY!", "UNTOUCHABLE!", "GHOST MODE!", "PHANTOM FLUSH!" };
    private static readonly Color[] DodgeColors = {
        new Color(0.3f, 0.9f, 1f),     // cyan
        new Color(0.4f, 0.6f, 1f),     // blue
        new Color(0.7f, 0.3f, 1f),     // purple
        new Color(1f, 0.85f, 0.2f)     // gold
    };

    public void RecordNearMiss()
    {
        _runNearMisses++;
        _nearMissStreak++;

        // Dodge streak milestones
        for (int i = DodgeMilestones.Length - 1; i >= 0; i--)
        {
            if (_nearMissStreak == DodgeMilestones[i])
            {
                int bonus = 50 * (i + 1);
                AddScore(bonus);
                if (CheerOverlay.Instance != null)
                    CheerOverlay.Instance.ShowCheer(DodgeNames[i], DodgeColors[i], i >= 2);
                if (ScorePopup.Instance != null && player != null)
                    ScorePopup.Instance.Show($"+{bonus}", player.transform.position + Vector3.up * 1.5f, ScorePopup.PopupType.Coin);
                if (PipeCamera.Instance != null)
                    PipeCamera.Instance.PunchFOV(2f + i * 1.5f);
                if (i >= 1 && ProceduralAudio.Instance != null)
                    ProceduralAudio.Instance.PlayComboUp();
                HapticManager.LightTap();
                break;
            }
        }
    }

    public void RecordCombo(int comboCount)
    {
        if (comboCount > _runBestCombo)
            _runBestCombo = comboCount;
    }

    public void GameOver()
    {
        if (isTourMode) return; // Tour mode: no game over
#if UNITY_EDITOR
        Debug.Log($"[GAME] === GAME OVER === score={score} dist={distanceTraveled:F0} coins={_runCoins} nearMisses={_runNearMisses} bestCombo={_runBestCombo} multiplier={_multiplier:F1}x");
#endif
        isPlaying = false;
        _isGameOver = true;
        _gameOverTime = Time.time;

        // Dramatic death freeze
        TriggerFreezeFrame(0.12f);

        // Death explosion effects at player position
        if (player != null)
        {
            Vector3 deathPos = player.transform.position;
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.Shake(0.6f);
                PipeCamera.Instance.PunchFOV(-8f);
                PipeCamera.Instance.Recoil(0.5f);
            }
            if (ScreenEffects.Instance != null)
            {
                ScreenEffects.Instance.TriggerHitFlash(new Color(0.6f, 0.3f, 0.05f));
                ScreenEffects.Instance.TriggerSplatter(new Color(0.4f, 0.25f, 0.1f));
                ScreenEffects.Instance.TriggerDesaturation(0.8f); // gray wash on death
            }
            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayDeathExplosion(deathPos);

            // Poop crew reacts in horror
            if (CheerOverlay.Instance != null)
            {
                string[] deathWords = { "R.I.P.", "WIPEOUT!", "GAME OVER!", "NOOO!" };
                string word = AITextManager.Instance != null
                    ? AITextManager.Instance.GetBark("hit")
                    : deathWords[Random.Range(0, deathWords.Length)];
                CheerOverlay.Instance.ShowCheer(word, new Color(0.6f, 0.2f, 0.1f), true);
            }

            HapticManager.HeavyTap();
        }

        distanceTraveled = player.DistanceTraveled;
        int finalScore = score + Mathf.FloorToInt(distanceTraveled * scorePerMeter);

        // Record run in persistent data (mode-specific)
        if (RaceManager.Instance != null && RaceManager.Instance.RaceState != RaceManager.State.PreRace)
        {
            float raceTime = RaceManager.Instance.RaceTime;
            int finishPlace = RaceManager.Instance.GetPlayerFinishPlace();
            PlayerData.RecordRaceRun(_runCoins, distanceTraveled, finalScore, _runNearMisses, _runBestCombo, raceTime, finishPlace);
        }
        else
        {
            PlayerData.RecordEndlessRun(_runCoins, distanceTraveled, finalScore, _runNearMisses, _runBestCombo);
        }

        // Capture challenge result for seed-based challenges
        if (SeedManager.Instance != null)
        {
            bool isRace = RaceManager.Instance != null && RaceManager.Instance.RaceState != RaceManager.State.PreRace;
            SeedChallenge.PlayerResult = new SeedChallenge.ChallengeData
            {
                seed = SeedManager.Instance.CurrentSeed,
                isRace = isRace,
                place = isRace ? RaceManager.Instance.GetPlayerFinishPlace() : 0,
                score = finalScore,
                time = isRace ? RaceManager.Instance.RaceTime : Time.time - _gameStartTime,
                maxSpeed = _runMaxSpeed,
                hits = _runHitsTaken,
                boosts = _runBoostsUsed,
                nearMisses = _runNearMisses,
                bestCombo = _runBestCombo,
                stomps = _runStomps,
                coins = _runCoins,
                distance = distanceTraveled
            };
        }

        // Ghost racer: stop recording, save if this was a high score
        bool isNewHighScore = finalScore > PlayerData.HighScore;
        if (GhostRecorder.Instance != null)
        {
            GhostRecorder.Instance.StopRecording(isNewHighScore);
            GhostRecorder.Instance.StopPlayback();
        }

        // Check daily challenge
        if (ChallengeSystem.Instance != null)
            ChallengeSystem.Instance.CheckRun(_runCoins, distanceTraveled, _runNearMisses, _runBestCombo, finalScore);

        if (ProceduralAudio.Instance != null)
        {
            ProceduralAudio.Instance.StopMusic();
            ProceduralAudio.Instance.PlayGameOver();
        }

        // Hide pause button on game over
        if (PauseMenu.Instance != null)
            PauseMenu.Instance.HidePauseButton();

        // Hide music button on game over
        if (MusicPanel.Instance != null)
            MusicPanel.Instance.HideMusicButton();

        // Delay game-over screen slightly so death effects breathe
        int _finalScore = finalScore;
        StartCoroutine(DelayedGameOver(_finalScore, _runCoins, distanceTraveled, _runNearMisses, _runBestCombo));

        // Game Center: report scores and check zone achievements
        if (GameCenterManager.Instance != null)
        {
            GameCenterManager.Instance.ReportScore(finalScore, distanceTraveled);
            GameCenterManager.Instance.CheckZoneAchievements(distanceTraveled);
            if (_runBestCombo >= 20)
                GameCenterManager.Instance.ReportComboKing();
            if (PlayerData.TotalRuns == 1)
                GameCenterManager.Instance.ReportFirstFlush();
        }

        // Cloud save
        if (CloudSaveManager.Instance != null)
            CloudSaveManager.Instance.SyncToCloud();

        // Analytics
        if (AnalyticsManager.Instance != null)
            AnalyticsManager.Instance.LogRunEnd(finalScore, distanceTraveled, _runCoins, _runNearMisses, _runBestCombo);

        // Rate app prompt (after a good run)
        if (RateAppPrompt.Instance != null)
            RateAppPrompt.Instance.OnRunEnd(finalScore, distanceTraveled);

        // Tutorial: mark done after first game over
        if (TutorialOverlay.Instance != null)
            TutorialOverlay.Instance.CompleteTutorial();

        // Notify race system of player crash
        if (RaceManager.Instance != null)
            RaceManager.Instance.OnPlayerCrashed();

        if (player != null)
        {
            player.enabled = false;

            // Slow-motion death: brief time dilation for dramatic effect
            Time.timeScale = 0.3f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            // Camera: zoom out for death overview
            if (PipeCamera.Instance != null)
                PipeCamera.Instance.DeathZoomOut(3f, 0.8f);

            // Start death tumble animation on the model
            StartCoroutine(DeathTumble(player.transform));
        }
    }

    IEnumerator DelayedGameOver(int finalScore, int coins, float distance, int nearMisses, int bestCombo)
    {
        // Brief pause so camera shake and death effects play out (realtime, unaffected by slow-mo)
        yield return new WaitForSecondsRealtime(0.35f);

        // Restore normal time
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // If this was a challenge run, show comparison instead of normal game over
        if (SeedChallenge.ActiveChallenge.HasValue && SeedChallenge.PlayerResult.HasValue
            && SeedChallengeUI.Instance != null)
        {
            SeedChallengeUI.Instance.ShowResults(
                SeedChallenge.PlayerResult.Value,
                SeedChallenge.ActiveChallenge.Value);
            SeedChallenge.ActiveChallenge = null;
        }
        else if (gameUI != null)
        {
            gameUI.ShowGameOver(finalScore, PlayerData.HighScore, coins, distance, nearMisses, bestCombo);
        }
    }

    IEnumerator DeathTumble(Transform playerTransform)
    {
        if (playerTransform == null) yield break;
        Vector3 startScale = playerTransform.localScale;
        Quaternion startRot = playerTransform.rotation;
        float elapsed = 0f;
        float duration = 0.8f;
        // Random tumble axis for variety
        Vector3 tumbleAxis = new Vector3(
            Random.Range(-1f, 1f), Random.Range(0.5f, 1f), Random.Range(-1f, 1f)).normalized;

        while (elapsed < duration && playerTransform != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            // Tumble rotation (accelerating spin)
            float angle = t * t * 360f;
            playerTransform.rotation = startRot * Quaternion.AngleAxis(angle, tumbleAxis);
            // Shrink toward end (starts at 60% through)
            float shrink = t > 0.6f ? Mathf.Lerp(1f, 0.1f, (t - 0.6f) / 0.4f) : 1f;
            playerTransform.localScale = startScale * shrink;
            yield return null;
        }
        // Hide the model at the end
        if (playerTransform != null)
            playerTransform.localScale = Vector3.zero;
    }

    public void RestartGame()
    {
        if (_restarting) return;
        _restarting = true;
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        StartCoroutine(RestartWithFade());
    }

    IEnumerator RestartWithFade()
    {
        // Create full-screen black overlay on the highest-order canvas
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 100)
            { canvas = c; break; }
        }

        Image fadeOverlay = null;
        if (canvas != null)
        {
            GameObject fadeObj = new GameObject("RestartFade");
            fadeObj.transform.SetParent(canvas.transform, false);
            RectTransform rt = fadeObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            fadeOverlay = fadeObj.AddComponent<Image>();
            fadeOverlay.color = new Color(0.02f, 0.03f, 0.01f, 0f);
            fadeOverlay.raycastTarget = true; // block input during fade
        }

        // Fade to black over 0.35s
        float elapsed = 0f;
        float duration = 0.35f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (fadeOverlay != null)
                fadeOverlay.color = new Color(0.02f, 0.03f, 0.01f, t);
            yield return null;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
