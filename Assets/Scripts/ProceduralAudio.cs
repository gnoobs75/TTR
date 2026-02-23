using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates and plays all game sound effects procedurally.
/// No audio files needed - everything synthesized from sine waves, noise, and frequency sweeps.
/// </summary>
public class ProceduralAudio : MonoBehaviour
{
    public static ProceduralAudio Instance { get; private set; }

    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.4f;

    private AudioSource _sfxSource;
    private AudioSource _coinSource; // dedicated source for pitch-shifted coin streaks
    private int _coinStreak;
    private float _lastCoinTime;
    private AudioSource _musicSource;

    // Cached clips
    private AudioClip _coinCollect;
    private AudioClip _nearMiss;
    private AudioClip _speedBoost;
    private AudioClip _gameOver;
    private AudioClip _comboUp;
    private AudioClip _gameStart;
    private AudioClip _obstacleHit;
    private AudioClip _jumpLaunch;

    // Creature sounds
    private AudioClip _ratSqueak;
    private AudioClip _barrelBeep;
    private AudioClip _blobGroan;
    private AudioClip _roachHiss;
    private AudioClip _aiTaunt;
    private AudioClip _frogCroak;
    private AudioClip _jellyZap;
    private AudioClip _spiderHiss;

    // Stomp sound
    private AudioClip _stomp;

    // Trick sound
    private AudioClip _trickComplete;

    // Hit sounds (per obstacle type)
    private AudioClip _ratPounce;
    private AudioClip _hairWrap;
    private AudioClip _barrelSplash;
    private AudioClip _blobSquish;
    private AudioClip _mineExplosion;
    private AudioClip _roachScurry;

    // Water sounds
    private AudioClip _waterGush;
    private AudioClip _waterfallSplash;
    private AudioClip _waterDrip;
    private AudioClip _waterSplosh;  // comic water entry
    private AudioClip _waterPloop;   // comic water exit

    // Proximity warning
    private AudioClip _dangerPing;
    private AudioSource _dangerSource; // dedicated source for pitch-shifting danger pings

    // Drift grinding
    private AudioClip _driftGrind;
    private AudioSource _driftSource; // dedicated looping source for drift grinding
    private float _driftVolTarget;

    // New feature sounds
    private AudioClip _zoneTransition;
    private AudioClip _flushSound;
    private AudioClip _countdownTick;
    private AudioClip _celebration;
    private AudioClip _bubblePop;
    private AudioClip _coinMagnet;
    private AudioClip _forkWarning;
    private AudioClip _comboBreak;
    private AudioClip _uiClick;
    private AudioClip _victoryFanfare;
    private AudioClip _sadTrombone;
    private AudioClip _zoneApproachBuild;

    // Real audio file clips
    private AudioClip _toiletFlush;
    private AudioClip[] _fartClips;

    // Music
    private AudioClip _bgmLoop;
    private bool _musicPlaying;
    private float _targetPitch = 1f;
    private float _currentPitch = 1f;
    private float _targetMusicVol = 1f;
    private float _currentMusicVol = 1f;

    // Dynamic music state
    private float _stunDipTimer;       // drops pitch/vol briefly on stun
    private float _rivalProximityPulse; // bass pulse when rival is close
    private float _zoneTransitionSweep; // pitch wobble on zone change
    private bool _finalStretchAnnounced; // one-shot "FINAL STRETCH!" at 200m
    private bool _photoFinishAnnounced;  // one-shot at 50m
    private float _heartbeatPhase;       // pulsing bass in final stretch

    // Speed wind loop (airflow sound that scales with player speed)
    private AudioClip _windLoop;
    private AudioSource _windSource;
    private float _windVolTarget;

    // Zone ambient audio (environmental layers per zone)
    private AudioSource _ambientSource;
    private AudioClip[] _zoneAmbientClips; // one loop per zone
    private int _currentAmbientZone = -1;
    private float _ambientCrossfade; // 0-1 crossfade progress
    private AudioSource _ambientSource2; // second source for crossfading
    private bool _ambientSwapping; // which source is active

    // Water drain
    private AudioClip _drainGurgle;

    // Tour mode audio
    private AudioClip _tourAmbient;
    private AudioClip _tourEntryChime;
    private AudioClip _tourExitStinger;
    private AudioSource _tourSource;

    // Music fade
    private float _musicFadeMul = 1f;
    private float _musicFadeTarget = 1f;
    private float _musicFadeSpeed = 2f;

    const int SAMPLE_RATE = 44100;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // SFX audio source
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f; // 2D

        // Coin audio source (separate so pitch doesn't affect other SFX)
        _coinSource = gameObject.AddComponent<AudioSource>();
        _coinSource.playOnAwake = false;
        _coinSource.spatialBlend = 0f;

        // Music audio source
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;
        _musicSource.loop = true;

        // Danger ping source (separate so pitch shifts don't affect SFX)
        _dangerSource = gameObject.AddComponent<AudioSource>();
        _dangerSource.playOnAwake = false;
        _dangerSource.spatialBlend = 0f;

        // Drift grinding source (looping, volume-controlled)
        _driftSource = gameObject.AddComponent<AudioSource>();
        _driftSource.playOnAwake = false;
        _driftSource.spatialBlend = 0f;
        _driftSource.loop = true;
        _driftSource.volume = 0f;

        // Wind loop source (continuous airflow that scales with speed)
        _windSource = gameObject.AddComponent<AudioSource>();
        _windSource.playOnAwake = false;
        _windSource.spatialBlend = 0f;
        _windSource.loop = true;
        _windSource.volume = 0f;

        // Ambient audio sources (two for crossfading between zones)
        _ambientSource = gameObject.AddComponent<AudioSource>();
        _ambientSource.playOnAwake = false;
        _ambientSource.spatialBlend = 0f;
        _ambientSource.loop = true;
        _ambientSource.volume = 0f;

        _ambientSource2 = gameObject.AddComponent<AudioSource>();
        _ambientSource2.playOnAwake = false;
        _ambientSource2.spatialBlend = 0f;
        _ambientSource2.loop = true;
        _ambientSource2.volume = 0f;

        // Tour ambient source
        _tourSource = gameObject.AddComponent<AudioSource>();
        _tourSource.playOnAwake = false;
        _tourSource.spatialBlend = 0f;
        _tourSource.loop = true;
        _tourSource.volume = 0f;

        GenerateAllClips();
        GenerateWindLoop();
        GenerateZoneAmbientClips();
        LoadAudioFiles();
    }

    void GenerateAllClips()
    {
        // Coin collect: quick ascending two-tone ping (Mario-ish)
        _coinCollect = GenerateClip("CoinCollect", 0.12f, (t, dur) =>
        {
            float freq = t < dur * 0.4f ? 880f : 1320f; // A5 -> E6
            float env = 1f - (t / dur);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
        });

        // Near miss: quick whoosh (filtered noise sweep)
        _nearMiss = GenerateClip("NearMiss", 0.2f, (t, dur) =>
        {
            float env = Mathf.Sin(Mathf.PI * t / dur); // bell curve
            float noise = (Random.value * 2f - 1f);
            float freq = Mathf.Lerp(200f, 800f, t / dur);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
            return (noise * 0.3f + tone * 0.4f) * env * 0.4f;
        });

        // Speed boost: ascending frequency sweep
        _speedBoost = GenerateClip("SpeedBoost", 0.35f, (t, dur) =>
        {
            float freq = Mathf.Lerp(300f, 1200f, t / dur);
            float env = 1f - Mathf.Pow(t / dur, 2f);
            float saw = (t * freq % 1f) * 2f - 1f;
            return (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + saw * 0.2f) * env * 0.4f;
        });

        // Game over: descending sad trombone-ish
        _gameOver = GenerateClip("GameOver", 0.6f, (t, dur) =>
        {
            float freq = Mathf.Lerp(440f, 110f, t / dur);
            float env = 1f - (t / dur) * 0.7f;
            float vibrato = Mathf.Sin(t * 8f) * 15f;
            return Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * t) * env * 0.4f;
        });

        // Combo up: quick arpeggio (3 ascending notes)
        _comboUp = GenerateClip("ComboUp", 0.18f, (t, dur) =>
        {
            float third = dur / 3f;
            float freq;
            if (t < third) freq = 523f;       // C5
            else if (t < third * 2) freq = 659f; // E5
            else freq = 784f;                    // G5
            float env = 1f - (t / dur);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
        });

        // Game start: dramatic rising chord
        _gameStart = GenerateClip("GameStart", 0.4f, (t, dur) =>
        {
            float env = Mathf.Sin(Mathf.PI * t / dur);
            float f1 = 261f; // C4
            float f2 = 329f; // E4
            float f3 = 392f; // G4
            float mix = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.3f
                      + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.3f
                      + Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.3f;
            return mix * env * 0.4f;
        });

        // Obstacle hit: crunchy impact noise
        _obstacleHit = GenerateClip("ObstacleHit", 0.15f, (t, dur) =>
        {
            float env = Mathf.Exp(-t * 30f);
            float noise = Random.value * 2f - 1f;
            float lowFreq = Mathf.Sin(2f * Mathf.PI * 80f * t);
            return (noise * 0.5f + lowFreq * 0.5f) * env * 0.6f;
        });

        // Jump launch: boing spring sound
        _jumpLaunch = GenerateClip("JumpLaunch", 0.25f, (t, dur) =>
        {
            float freq = Mathf.Lerp(200f, 600f, t / dur);
            float env = 1f - (t / dur);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
        });

        // Creature sounds
        // Rat squeak: softer chittery squeak (lower freq, less harsh)
        _ratSqueak = GenerateClip("RatSqueak", 0.12f, (t, dur) =>
        {
            float freq = 900f + Mathf.Sin(t * 50f) * 100f; // gentler vibrato
            float env = Mathf.Sin(Mathf.PI * t / dur) * Mathf.Exp(-t * 12f);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.2f;
        });

        // Barrel beep: low muffled thud-pulse (NOT a harsh two-tone whine)
        _barrelBeep = GenerateClip("BarrelBeep", 0.2f, (t, dur) =>
        {
            float freq = 140f + Mathf.Sin(t * 15f) * 30f; // low rumble
            float env = Mathf.Exp(-t * 10f);
            float noise = Mathf.Sin(t * 2345f) * 0.1f * env;
            return (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f + noise) * env * 0.25f;
        });

        // Blob groan: descending wet moan
        _blobGroan = GenerateClip("BlobGroan", 0.4f, (t, dur) =>
        {
            float freq = Mathf.Lerp(180f, 80f, t / dur);
            float env = 1f - (t / dur) * 0.8f;
            float harmonics = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f
                            + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.2f
                            + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.1f;
            return harmonics * env * 0.4f;
        });

        // Roach hiss: soft skittery rustle (lower, less harsh)
        _roachHiss = GenerateClip("RoachHiss", 0.2f, (t, dur) =>
        {
            float env = Mathf.Exp(-t * 10f);
            float noise = Mathf.Sin(t * 6789f) * Mathf.Cos(t * 3456f);
            float low = Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.15f;
            return (noise * 0.3f + low) * env * 0.2f;
        });

        // Frog croak: deep vibrating throat pouch sound
        _frogCroak = GenerateClip("FrogCroak", 0.35f, (t, dur) =>
        {
            float freq = 120f + Mathf.Sin(t * 15f) * 30f; // warbling
            float env = Mathf.Sin(Mathf.PI * t / dur) * Mathf.Exp(-t * 3f);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f
                       + Mathf.Sin(2f * Mathf.PI * freq * 2.02f * t) * 0.25f
                       + Mathf.Sin(2f * Mathf.PI * freq * 3.1f * t) * 0.1f;
            return tone * env * 0.4f;
        });

        // Jellyfish zap: electric sting with buzzy crackle
        _jellyZap = GenerateClip("JellyZap", 0.2f, (t, dur) =>
        {
            float freq = 800f + Mathf.Sin(t * 120f) * 400f;
            float env = Mathf.Exp(-t * 15f);
            float buzz = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            float crackle = Mathf.Sin(t * 12345f) * Mathf.Cos(t * 8765f) * 0.3f;
            return (buzz + crackle) * env * 0.35f;
        });

        // Spider hiss: high-pitched breathy hiss with clicking
        _spiderHiss = GenerateClip("SpiderHiss", 0.25f, (t, dur) =>
        {
            float env = Mathf.Exp(-t * 8f);
            float noise = Mathf.Sin(t * 8888f) * Mathf.Cos(t * 5555f) * 0.4f;
            float click = (t < 0.03f) ? Mathf.Sin(2f * Mathf.PI * 2000f * t) * 0.5f : 0f;
            float hiss = Mathf.Sin(2f * Mathf.PI * 3500f * t) * 0.15f;
            return (noise + click + hiss) * env * 0.25f;
        });

        // AI taunt: soft mocking chuckle (lower, warmer)
        _aiTaunt = GenerateClip("AITaunt", 0.3f, (t, dur) =>
        {
            float freq = Mathf.Lerp(200f, 350f, t / dur);
            float env = Mathf.Sin(Mathf.PI * t / dur) * Mathf.Exp(-t * 4f);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f
                       + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.1f;
            return tone * env * 0.3f;
        });

        // Stomp: satisfying squelch + spring bounce
        _stomp = GenerateClip("Stomp", 0.25f, (t, dur) =>
        {
            float squelch = 0f;
            if (t < 0.08f) // initial splat
            {
                float env = Mathf.Exp(-t * 40f);
                squelch = (Mathf.Sin(t * 4567f) * 0.4f + Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.6f) * env;
            }
            // Spring bounce (ascending boing)
            if (t > 0.05f)
            {
                float t2 = t - 0.05f;
                float freq = Mathf.Lerp(200f, 800f, Mathf.Min(t2 / 0.15f, 1f));
                float env = Mathf.Sin(Mathf.PI * t2 / (dur - 0.05f));
                squelch += Mathf.Sin(2f * Mathf.PI * freq * t2) * env * 0.4f;
            }
            return squelch * 0.55f;
        });

        // Trick complete: triumphant ascending chord stab
        _trickComplete = GenerateClip("TrickComplete", 0.25f, (t, dur) =>
        {
            float prog = t / dur;
            float f1 = 523f; // C5
            float f2 = 659f; // E5
            float f3 = 784f; // G5
            float f4 = 1047f; // C6
            float env = Mathf.Sin(Mathf.PI * prog) * (1f - prog * 0.3f);
            float mix = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.25f
                      + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.25f
                      + Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.25f
                      + Mathf.Sin(2f * Mathf.PI * f4 * t) * (prog * 0.3f); // octave fades in
            return mix * env * 0.45f;
        });

        // Hit sounds (per obstacle type)

        // Rat pounce: screech up + thud
        _ratPounce = GenerateClip("RatPounce", 0.3f, (t, dur) =>
        {
            float freq = Mathf.Lerp(800f, 2200f, Mathf.Min(t / 0.1f, 1f));
            if (t > 0.15f) freq = 80f; // thud
            float env = t < 0.15f ? 1f : Mathf.Exp(-(t - 0.15f) * 15f);
            float noise = Mathf.Sin(t * 7654f) * 0.15f;
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + noise) * env * 0.45f;
        });

        // Hair wrap: muffled wet squelch
        _hairWrap = GenerateClip("HairWrap", 0.4f, (t, dur) =>
        {
            float freq = 100f + Mathf.Sin(t * 12f) * 40f; // wobbly
            float env = Mathf.Sin(Mathf.PI * t / dur) * 0.8f;
            float noise = Mathf.Sin(t * 3456f) * Mathf.Cos(t * 8765f) * 0.3f;
            return (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + noise) * env * 0.4f;
        });

        // Barrel splash: sharp hiss + sizzle
        _barrelSplash = GenerateClip("BarrelSplash", 0.35f, (t, dur) =>
        {
            float noise = Mathf.Sin(t * 9876f) * Mathf.Cos(t * 5432f);
            float sizzle = Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.3f;
            float env = Mathf.Exp(-t * 6f);
            return (noise * 0.5f + sizzle) * env * 0.5f;
        });

        // Blob squish: deep wet impact
        _blobSquish = GenerateClip("BlobSquish", 0.35f, (t, dur) =>
        {
            float freq = Mathf.Lerp(120f, 40f, t / dur);
            float env = Mathf.Exp(-t * 8f);
            float harmonics = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f
                            + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.2f;
            float noise = Mathf.Sin(t * 2345f) * 0.2f * env;
            return (harmonics + noise) * env * 0.5f;
        });

        // Mine explosion: sharp metallic blast
        _mineExplosion = GenerateClip("MineExplosion", 0.3f, (t, dur) =>
        {
            float freq = Mathf.Lerp(1000f, 100f, t / dur);
            float env = Mathf.Exp(-t * 10f);
            float noise = (Mathf.Sin(t * 54321f) * Mathf.Cos(t * 12345f)) * 0.6f;
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            return (noise + tone) * env * 0.55f;
        });

        // Roach scurry: rapid chittering clicks
        _roachScurry = GenerateClip("RoachScurry", 0.4f, (t, dur) =>
        {
            float clickRate = 15f; // clicks per second
            float clickPhase = (t * clickRate) % 1f;
            float click = clickPhase < 0.1f ? 1f : 0f;
            float freq = 2000f + Mathf.Sin(t * 30f) * 500f;
            float env = (1f - t / dur) * click;
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.35f;
        });

        // Water sounds

        // Water gush: soft whooshy flow burst (no high freqs)
        _waterGush = GenerateClip("WaterGush", 0.6f, (t, dur) =>
        {
            float env = Mathf.Sin(Mathf.PI * t / dur) * 0.7f;
            float noise = Mathf.Sin(t * 4321f) * Mathf.Cos(t * 2345f) * 0.25f;
            float lowFreq = Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.25f;
            float rumble = Mathf.Sin(2f * Mathf.PI * (50f + Mathf.Sin(t * 2f) * 15f) * t) * 0.2f;
            return (noise + lowFreq + rumble) * env * 0.35f;
        });

        // Waterfall splash: wet impact when player walks through
        _waterfallSplash = GenerateClip("WaterfallSplash", 0.35f, (t, dur) =>
        {
            float env = Mathf.Exp(-t * 7f);
            float noise = (Mathf.Sin(t * 3456f) * Mathf.Cos(t * 7890f)) * 0.5f;
            float splat = Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.3f;
            float drip = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(600f, 200f, t / dur) * t) * 0.2f;
            return (noise + splat + drip) * env * 0.5f;
        });

        // Water drip: plip-plop droplet
        _waterDrip = GenerateClip("WaterDrip", 0.2f, (t, dur) =>
        {
            float env = Mathf.Exp(-t * 25f);
            float freq = Mathf.Lerp(800f, 300f, t / dur);
            float plop = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
            float splash = Mathf.Sin(t * 5432f) * env * 0.08f;
            return (plop * 0.5f + splash) * 0.45f;
        });

        // Water splosh: comical fat body hitting water (bassy thwap + splash)
        _waterSplosh = GenerateClip("WaterSplosh", 0.45f, (t, dur) =>
        {
            float env = t < 0.03f ? t / 0.03f : Mathf.Exp(-t * 8f);
            // Big bassy impact
            float bass = Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.6f * Mathf.Exp(-t * 12f);
            // Water splash noise burst
            float splash = Mathf.Sin(t * 7654f) * Mathf.Cos(t * 3210f) * 0.4f * Mathf.Exp(-t * 6f);
            // Descending "splooosh" tone
            float sploshFreq = Mathf.Lerp(500f, 120f, t / dur);
            float splosh = Mathf.Sin(2f * Mathf.PI * sploshFreq * t) * 0.3f * env;
            // Bubbles after impact
            float bubbles = Mathf.Sin(2f * Mathf.PI * 350f * t) * Mathf.Max(0, Mathf.Sin(t * 25f)) * 0.15f * Mathf.Max(0, t - 0.1f) * 4f;
            return (bass + splash + splosh + bubbles) * 0.6f;
        });

        // Water ploop: comical popping out of water (rising pitch pop + drip)
        _waterPloop = GenerateClip("WaterPloop", 0.35f, (t, dur) =>
        {
            float env = t < 0.02f ? t / 0.02f : Mathf.Exp(-t * 10f);
            // Rising "ploop!" pop
            float freq = Mathf.Lerp(200f, 800f, Mathf.Min(t * 8f, 1f));
            float pop = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f * env;
            // Quick suction release
            float suction = Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.3f * Mathf.Exp(-t * 20f);
            // Tiny water drops falling back
            float drips = Mathf.Sin(2f * Mathf.PI * (600f + Mathf.Sin(t * 30f) * 200f) * t)
                * 0.15f * Mathf.Max(0, t - 0.15f) * 3f * Mathf.Exp(-(t - 0.15f) * 8f);
            return (pop + suction + drips) * 0.55f;
        });

        // Zone transition: deep rumbling shift with rising tone
        _zoneTransition = GenerateClip("ZoneTransition", 0.8f, (t, dur) =>
        {
            float freq = Mathf.Lerp(80f, 300f, t / dur);
            float env = Mathf.Sin(Mathf.PI * t / dur);
            float rumble = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;
            float sweep = Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.2f;
            float noise = Mathf.Sin(t * 3456f) * 0.05f * env;
            return (rumble + sweep + noise) * env * 0.35f;
        });

        // Flush: whooshing water swirl descending
        _flushSound = GenerateClip("Flush", 1.5f, (t, dur) =>
        {
            float freq = Mathf.Lerp(600f, 80f, t / dur);
            float env = t < 0.2f ? t / 0.2f : Mathf.Exp(-(t - 0.2f) * 2f);
            float swirl = Mathf.Sin(2f * Mathf.PI * freq * t + Mathf.Sin(t * 8f) * 3f);
            float noise = (Mathf.Sin(t * 8765f) * Mathf.Cos(t * 5432f)) * 0.4f;
            float bubbles = Mathf.Sin(2f * Mathf.PI * 200f * t) * Mathf.Max(0, Mathf.Sin(t * 20f)) * 0.2f;
            return (swirl * 0.3f + noise * env + bubbles) * env * 0.5f;
        });

        // Countdown tick: sharp percussive tick
        _countdownTick = GenerateClip("CountdownTick", 0.15f, (t, dur) =>
        {
            float freq = 800f;
            float env = Mathf.Exp(-t * 30f);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
        });

        // Celebration: ascending fanfare chord
        _celebration = GenerateClip("Celebration", 0.5f, (t, dur) =>
        {
            float prog = t / dur;
            float f1 = 523f + prog * 100f; // Rising C5
            float f2 = 659f + prog * 100f; // Rising E5
            float f3 = 784f + prog * 100f; // Rising G5
            float f4 = 1047f + prog * 100f; // Rising C6
            float env = Mathf.Sin(Mathf.PI * prog);
            float mix = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.25f
                      + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.25f
                      + Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.2f
                      + Mathf.Sin(2f * Mathf.PI * f4 * t) * 0.15f;
            return mix * env * 0.45f;
        });

        // Bubble pop: quick high-pitched blip
        _bubblePop = GenerateClip("BubblePop", 0.08f, (t, dur) =>
        {
            float freq = Mathf.Lerp(1200f, 600f, t / dur);
            float env = Mathf.Exp(-t * 50f);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.3f;
        });

        // Coin magnet: shimmer with ascending ping
        _coinMagnet = GenerateClip("CoinMagnet", 0.2f, (t, dur) =>
        {
            float freq = Mathf.Lerp(1000f, 2000f, t / dur);
            float env = Mathf.Sin(Mathf.PI * t / dur);
            float shimmer = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f
                          + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.15f;
            return shimmer * env * 0.3f;
        });

        // Fork warning: urgent two-tone klaxon
        _forkWarning = GenerateClip("ForkWarning", 0.5f, (t, dur) =>
        {
            float freq = (t % 0.15f < 0.075f) ? 600f : 450f; // alternating tones
            float env = Mathf.Sin(Mathf.PI * t / dur);
            float signal = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f
                         + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.15f;
            return signal * env * 0.5f;
        });

        // Combo break: 3 quick descending notes (sad trombone mini)
        _comboBreak = GenerateClip("ComboBreak", 0.25f, (t, dur) =>
        {
            float third = dur / 3f;
            float freq;
            if (t < third) freq = 523f;          // C5 (starts high)
            else if (t < third * 2) freq = 392f; // G4 (drops)
            else freq = 294f;                     // D4 (drops more)
            float env = 1f - (t / dur);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.25f;
        });

        // UI click: short crisp pop
        _uiClick = GenerateClip("UIClick", 0.06f, (t, dur) =>
        {
            float env = 1f - (t / dur);
            return Mathf.Sin(2f * Mathf.PI * 1200f * t) * env * env * 0.3f;
        });

        // Victory fanfare: triumphant ascending chord progression (C-E-G-C with trumpety timbre)
        _victoryFanfare = GenerateClip("VictoryFanfare", 1.2f, (t, dur) =>
        {
            float prog = t / dur;
            // 4 ascending stabs: C5→E5→G5→C6 with overlap
            float note1T = Mathf.Clamp01(prog / 0.35f);   // C5: 0.0-0.35
            float note2T = Mathf.Clamp01((prog - 0.2f) / 0.35f);  // E5: 0.2-0.55
            float note3T = Mathf.Clamp01((prog - 0.4f) / 0.35f);  // G5: 0.4-0.75
            float note4T = Mathf.Clamp01((prog - 0.55f) / 0.45f); // C6: 0.55-1.0

            float env1 = Mathf.Sin(Mathf.PI * note1T) * (prog < 0.35f ? 1f : 0f);
            float env2 = Mathf.Sin(Mathf.PI * note2T) * (prog >= 0.2f && prog < 0.55f ? 1f : 0f);
            float env3 = Mathf.Sin(Mathf.PI * note3T) * (prog >= 0.4f && prog < 0.75f ? 1f : 0f);
            float env4 = Mathf.Sin(Mathf.PI * note4T) * (prog >= 0.55f ? 1f : 0f);

            // Trumpety timbre: fundamental + 2nd + 3rd harmonics
            float Trumpet(float freq, float time)
            {
                return Mathf.Sin(2f * Mathf.PI * freq * time) * 0.5f
                     + Mathf.Sin(2f * Mathf.PI * freq * 2f * time) * 0.3f
                     + Mathf.Sin(2f * Mathf.PI * freq * 3f * time) * 0.15f;
            }

            float mix = Trumpet(523f, t) * env1
                      + Trumpet(659f, t) * env2
                      + Trumpet(784f, t) * env3
                      + Trumpet(1047f, t) * env4;

            // Final note sustains with slight vibrato
            if (prog > 0.7f)
            {
                float vibrato = 1f + Mathf.Sin(t * 30f) * 0.008f;
                mix += Trumpet(1047f * vibrato, t) * env4 * 0.3f;
            }

            return mix * 0.35f;
        });

        // Sad trombone: classic "wah wah wah wahhhhh" descending
        _sadTrombone = GenerateClip("SadTrombone", 1.5f, (t, dur) =>
        {
            float prog = t / dur;
            // 4 descending notes: Bb4→A4→Ab4→long G4
            float freq;
            float noteEnv;
            if (prog < 0.2f)
            {
                freq = 466f; // Bb4
                noteEnv = Mathf.Sin(Mathf.PI * prog / 0.2f);
            }
            else if (prog < 0.4f)
            {
                freq = 440f; // A4
                noteEnv = Mathf.Sin(Mathf.PI * (prog - 0.2f) / 0.2f);
            }
            else if (prog < 0.6f)
            {
                freq = 415f; // Ab4
                noteEnv = Mathf.Sin(Mathf.PI * (prog - 0.4f) / 0.2f);
            }
            else
            {
                // Final long descending note with vibrato
                float slideT = (prog - 0.6f) / 0.4f;
                freq = Mathf.Lerp(392f, 350f, slideT); // G4 sliding down
                noteEnv = 1f - slideT * 0.7f; // slow fade
                // Wobble/vibrato gets wider as note dies
                freq += Mathf.Sin(t * 12f) * (3f + slideT * 8f);
            }

            // Trombone timbre: warm fundamental + soft harmonics
            float signal = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f
                         + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.25f
                         + Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.08f;

            return signal * noteEnv * 0.35f;
        });

        // Danger ping: short urgent beep that rises in pitch (sonar-like)
        _dangerPing = GenerateClip("DangerPing", 0.08f, (t, dur) =>
        {
            float prog = t / dur;
            float freq = Mathf.Lerp(600f, 900f, prog); // rising chirp
            float env = Mathf.Sin(Mathf.PI * prog);     // smooth bell envelope
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
        });

        // Drift grind: loopable friction/scraping noise (filtered noise + resonance)
        _driftGrind = GenerateClip("DriftGrind", 0.5f, (t, dur) =>
        {
            float noise = Random.Range(-1f, 1f);
            // Band-pass around grinding frequency for metallic texture
            float grindFreq = 220f + Mathf.Sin(t * 30f) * 80f;
            float resonance = Mathf.Sin(2f * Mathf.PI * grindFreq * t) * 0.3f;
            // Mix noise + resonance for gritty friction sound
            float signal = noise * 0.2f + resonance;
            // Seamless loop (fade edges slightly)
            float loopEnv = Mathf.Sin(Mathf.PI * (t / dur));
            return signal * loopEnv * 0.3f;
        });

        // Tour entry chime: gentle exploratory bell (discovery feel)
        _tourEntryChime = GenerateClip("TourEntry", 0.6f, (t, dur) =>
        {
            float prog = t / dur;
            float f1 = 392f; // G4
            float f2 = 523f; // C5
            float f3 = 659f; // E5
            float env = Mathf.Sin(Mathf.PI * prog) * Mathf.Exp(-prog * 2f);
            float mix = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.3f
                      + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.25f * Mathf.Clamp01((prog - 0.1f) * 5f)
                      + Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.2f * Mathf.Clamp01((prog - 0.2f) * 3f);
            return mix * env * 0.5f;
        });

        // Tour exit stinger: brief descending "done" sound
        _tourExitStinger = GenerateClip("TourExit", 0.4f, (t, dur) =>
        {
            float prog = t / dur;
            float freq = Mathf.Lerp(659f, 392f, prog); // E5 -> G4
            float env = (1f - prog) * Mathf.Exp(-prog * 3f);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
        });

        // Zone approach build: low foreboding rumble that rises in tension
        _zoneApproachBuild = GenerateClip("ZoneApproach", 1.5f, (t, dur) =>
        {
            float prog = t / dur;
            // Rising low drone
            float freq = Mathf.Lerp(40f, 120f, prog * prog);
            float env = prog * Mathf.Sin(Mathf.PI * prog); // builds then fades
            float drone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;
            float sub = Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.2f;
            // Rising noise wash
            float noise = (Random.value * 2f - 1f) * prog * 0.15f;
            // Dissonant overtone
            float dissonance = Mathf.Sin(2f * Mathf.PI * (freq * 1.06f) * t) * 0.1f * prog;
            return (drone + sub + noise + dissonance) * env * 0.5f;
        });

        // Drain gurgle: bubbly water suction sound
        _drainGurgle = GenerateClip("DrainGurgle", 0.8f, (t, dur) =>
        {
            float prog = t / dur;
            float env = Mathf.Sin(Mathf.PI * prog);
            // Descending bubbly tone
            float freq = Mathf.Lerp(300f, 80f, prog);
            float gurgle = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;
            // Bubble modulation
            float bubbles = Mathf.Sin(2f * Mathf.PI * 18f * t) * 0.5f + 0.5f;
            gurgle *= bubbles;
            // Water noise
            float noise = (Random.value * 2f - 1f) * 0.1f * env;
            return (gurgle + noise) * env * 0.5f;
        });

        // Tour ambient: calm exploratory loop (warm pads, soft drips)
        _tourAmbient = GenerateClip("TourAmbient", 4f, (t, dur) =>
        {
            float val = 0f;
            // Gentle pad (warm minor chord)
            val += Mathf.Sin(2f * Mathf.PI * 196f * t) * 0.06f; // G3
            val += Mathf.Sin(2f * Mathf.PI * 233f * t) * 0.04f; // Bb3
            val += Mathf.Sin(2f * Mathf.PI * 294f * t) * 0.03f; // D4
            // Slow LFO modulation
            float lfo = Mathf.Sin(t * 0.5f * Mathf.PI * 2f) * 0.5f + 0.5f;
            val *= 0.7f + lfo * 0.3f;
            // Occasional drip
            float drip = Mathf.Sin(t * 1.3f * Mathf.PI * 2f);
            if (drip > 0.97f) val += 0.15f * Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-(t % 0.769f) * 20f);
            // Soft noise
            val += (Random.value * 2f - 1f) * 0.006f;
            return val * 0.5f;
        });

        // Background music: simple bass-driven loop
        GenerateBGM();
    }

    void LoadAudioFiles()
    {
        // Load real sound files from Assets/sounds/ (Resources path: "sounds/filename" without extension)
        // Files must be in Assets/Resources/sounds/ OR we load via path
        // Since they're in Assets/sounds/, we load them as Resources if moved, or use direct path
        _toiletFlush = Resources.Load<AudioClip>("sounds/toilet1");
        if (_toiletFlush == null)
            Debug.LogWarning("[ProceduralAudio] Could not load sounds/toilet1 - make sure Assets/sounds/ is renamed to Assets/Resources/sounds/");

        _fartClips = new AudioClip[5];
        for (int i = 0; i < 5; i++)
        {
            _fartClips[i] = Resources.Load<AudioClip>($"sounds/fart{i + 1}");
            if (_fartClips[i] == null)
                Debug.LogWarning($"[ProceduralAudio] Could not load sounds/fart{i + 1}");
        }
    }

    delegate float SynthFunc(float t, float duration);

    AudioClip GenerateClip(string name, float duration, SynthFunc synth)
    {
        int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
        float[] data = new float[sampleCount];

        // Use a fixed seed per-clip for consistent noise
        Random.State prevState = Random.state;
        Random.InitState(name.GetHashCode());

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            data[i] = Mathf.Clamp(synth(t, duration), -1f, 1f);
        }

        Random.state = prevState;

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        return clip;
    }

    void GenerateBGM()
    {
        float duration = 8f; // 8-second loop
        int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
        float[] data = new float[sampleCount];
        float bpm = 140f;
        float beatLen = 60f / bpm;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float beat = t / beatLen;
            float beatFrac = beat % 1f;

            // Bass: simple pattern (root, fifth, octave)
            float[] bassPattern = { 55f, 55f, 82.5f, 55f, 73.4f, 55f, 82.5f, 110f };
            int bassNote = (int)(beat % bassPattern.Length);
            float bassFreq = bassPattern[bassNote];
            float bassEnv = Mathf.Exp(-beatFrac * 4f);
            float bass = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * bassEnv * 0.3f;

            // Kick on beats 0, 2, 4, 6
            float kick = 0f;
            if ((int)beat % 2 == 0)
            {
                float kickEnv = Mathf.Exp(-beatFrac * 12f);
                float kickFreq = Mathf.Lerp(150f, 40f, beatFrac);
                kick = Mathf.Sin(2f * Mathf.PI * kickFreq * t) * kickEnv * 0.35f;
            }

            // Hi-hat on eighth notes
            float hihat = 0f;
            float eighthFrac = (beat * 2f) % 1f;
            if (eighthFrac < 0.3f)
            {
                float hhEnv = Mathf.Exp(-eighthFrac * 20f);
                // Use deterministic noise based on sample position
                float hhNoise = Mathf.Sin(i * 127.1f) * Mathf.Cos(i * 311.7f);
                hihat = hhNoise * hhEnv * 0.1f;
            }

            // Synth pad (minor chord, subtle)
            float pad = 0f;
            float padEnv = 0.08f;
            pad += Mathf.Sin(2f * Mathf.PI * 220f * t) * padEnv;
            pad += Mathf.Sin(2f * Mathf.PI * 261.6f * t) * padEnv * 0.7f;
            pad += Mathf.Sin(2f * Mathf.PI * 329.6f * t) * padEnv * 0.5f;

            data[i] = Mathf.Clamp(bass + kick + hihat + pad, -1f, 1f);
        }

        _bgmLoop = AudioClip.Create("BGMLoop", sampleCount, 1, SAMPLE_RATE, false);
        _bgmLoop.SetData(data, 0);
    }

    void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, masterVolume * sfxVolume * volumeScale);
    }

    // === PUBLIC API ===

    public void PlayCoinCollect()
    {
        // Streak tracking: coins collected within 0.8s of each other raise pitch
        float now = Time.time;
        if (now - _lastCoinTime < 0.8f)
            _coinStreak = Mathf.Min(_coinStreak + 1, 12);
        else
            _coinStreak = 0;
        _lastCoinTime = now;

        // Rising pitch: base 1.0 → up to 1.8 over 12-streak (musical scale feel)
        float pitch = 1f + _coinStreak * 0.065f;

        AudioClip clip = _coinCollect;
        if (_fartClips != null && _fartClips.Length > 0)
        {
            AudioClip fart = _fartClips[Random.Range(0, _fartClips.Length)];
            if (fart != null) clip = fart;
        }

        if (clip != null && _coinSource != null)
        {
            _coinSource.pitch = pitch;
            _coinSource.PlayOneShot(clip, masterVolume * sfxVolume);
        }
    }

    public void PlayToiletFlush()
    {
        if (_toiletFlush != null)
            PlaySFX(_toiletFlush);
        else
            PlaySFX(_flushSound); // fall back to procedural flush
    }
    /// <summary>Play danger proximity ping. Pitch 1.0=far, up to 1.8=very close.</summary>
    public void PlayDangerPing(float pitch = 1f)
    {
        if (_dangerPing == null || _dangerSource == null) return;
        _dangerSource.pitch = Mathf.Clamp(pitch, 0.8f, 2f);
        _dangerSource.PlayOneShot(_dangerPing, masterVolume * sfxVolume * 0.5f);
    }

    /// <summary>Update drift grinding volume and pitch. Intensity 0=silent, 1=full grind.</summary>
    public void UpdateDriftGrind(float intensity)
    {
        if (_driftSource == null || _driftGrind == null) return;
        _driftVolTarget = intensity;
        if (intensity > 0.01f && !_driftSource.isPlaying)
        {
            _driftSource.clip = _driftGrind;
            _driftSource.Play();
        }
        // Pitch rises with intensity for urgency
        _driftSource.pitch = Mathf.Lerp(0.8f, 1.4f, intensity);
    }

    public void PlayNearMiss() => PlaySFX(_nearMiss);
    public void PlaySpeedBoost() => PlaySFX(_speedBoost);
    public void PlayGameOver() => PlaySFX(_gameOver);
    public void PlayComboUp() => PlaySFX(_comboUp, 0.8f);
    public void PlayGameStart() => PlaySFX(_gameStart);
    public void PlayObstacleHit() => PlaySFX(_obstacleHit);
    public void PlayJumpLaunch() => PlaySFX(_jumpLaunch);

    // Creature sounds
    public void PlayRatSqueak() => PlaySFX(_ratSqueak);
    public void PlayBarrelBeep() => PlaySFX(_barrelBeep);
    public void PlayBlobGroan() => PlaySFX(_blobGroan);
    public void PlayRoachHiss() => PlaySFX(_roachHiss);
    public void PlayAITaunt() => PlaySFX(_aiTaunt);

    public void PlayStomp() => PlaySFX(_stomp);
    public void PlayTrickComplete() => PlaySFX(_trickComplete);

    // Hit sounds (per obstacle type)
    public void PlayRatPounce() => PlaySFX(_ratPounce);
    public void PlayHairWrap() => PlaySFX(_hairWrap);
    public void PlayBarrelSplash() => PlaySFX(_barrelSplash);
    public void PlayBlobSquish() => PlaySFX(_blobSquish);
    public void PlayMineExplosion() => PlaySFX(_mineExplosion);
    public void PlayRoachScurry() => PlaySFX(_roachScurry);
    public void PlayFrogCroak() => PlaySFX(_frogCroak);
    public void PlayJellyZap() => PlaySFX(_jellyZap);
    public void PlaySpiderHiss() => PlaySFX(_spiderHiss);

    // Water sounds
    public void PlayWaterGush() => PlaySFX(_waterGush, 0.6f);
    public void PlayWaterfallSplash() => PlaySFX(_waterfallSplash, 0.5f);
    public void PlayWaterDrip() => PlaySFX(_waterDrip, 0.35f);
    public void PlayWaterSplosh() => PlaySFX(_waterSplosh, 0.7f);
    public void PlayWaterPloop() => PlaySFX(_waterPloop, 0.6f);

    // New feature sounds
    public void PlayZoneTransition() => PlaySFX(_zoneTransition, 0.7f);
    public void PlayFlush() => PlaySFX(_flushSound);
    public void PlayCountdownTick() => PlaySFX(_countdownTick);
    public void PlayCelebration() => PlaySFX(_celebration);
    public void PlayBubblePop() => PlaySFX(_bubblePop, 0.4f);
    public void PlayCoinMagnet() => PlaySFX(_coinMagnet, 0.5f);
    public void PlayForkWarning() => PlaySFX(_forkWarning, 0.8f);
    public void PlayComboBreak() => PlaySFX(_comboBreak, 0.5f);
    public void PlayUIClick() => PlaySFX(_uiClick, 0.6f);
    public void PlayVictoryFanfare() => PlaySFX(_victoryFanfare, 0.7f);
    public void PlaySadTrombone() => PlaySFX(_sadTrombone, 0.6f);

    // Zone approach build
    public void PlayZoneApproachBuild() => PlaySFX(_zoneApproachBuild, 0.6f);

    // Water drain gurgle
    public void PlayDrainGurgle() => PlaySFX(_drainGurgle, 0.7f);

    // Tour audio
    public void PlayTourEntry() => PlaySFX(_tourEntryChime, 0.7f);
    public void PlayTourExit() => PlaySFX(_tourExitStinger, 0.7f);

    /// <summary>Start tour ambient loop.</summary>
    public void StartTourAmbient()
    {
        if (_tourSource == null || _tourAmbient == null) return;
        _tourSource.clip = _tourAmbient;
        _tourSource.volume = masterVolume * sfxVolume * 0.3f;
        _tourSource.Play();
    }

    /// <summary>Stop tour ambient loop.</summary>
    public void StopTourAmbient()
    {
        if (_tourSource != null) _tourSource.Stop();
    }

    /// <summary>Fade out music over duration seconds.</summary>
    public void FadeOutMusic(float duration)
    {
        _musicFadeTarget = 0f;
        _musicFadeSpeed = duration > 0.01f ? 1f / duration : 100f;
    }

    /// <summary>Fade in music over duration seconds.</summary>
    public void FadeInMusic(float duration)
    {
        if (_musicSource != null && !_musicSource.isPlaying && _bgmLoop != null)
        {
            _musicSource.clip = _bgmLoop;
            _musicSource.Play();
            _musicPlaying = true;
        }
        _musicFadeMul = 0f;
        _musicFadeTarget = 1f;
        _musicFadeSpeed = duration > 0.01f ? 1f / duration : 100f;
    }

    public void StartMusic()
    {
        // Procedural BGM disabled — ogg tracks from Resources/music/ used instead
        // (RaceManager plays race music, GameUI plays splash music)
        _musicPlaying = true; // flag so other systems don't try to restart
        _finalStretchAnnounced = false;
        _photoFinishAnnounced = false;
        _heartbeatPhase = 0f;

        // Start zone ambient (defaults to zone 0 / Porcelain)
        if (_zoneAmbientClips != null && _zoneAmbientClips.Length > 0)
        {
            _ambientSource.clip = _zoneAmbientClips[0];
            _ambientSource.volume = masterVolume * sfxVolume * 0.35f;
            _ambientSource.Play();
            _currentAmbientZone = 0;
            _ambientCrossfade = 1f;
        }
    }

    public void StopMusic()
    {
        if (_musicSource != null)
            _musicSource.Stop();
        _musicPlaying = false;
    }

    /// <summary>
    /// Updates BGM pitch based on player speed. Call externally or auto-polls TurdController.
    /// Speed 6 m/s (base) = pitch 0.92, speed 14 m/s (max) = pitch 1.18.
    /// </summary>
    public void SetSpeedIntensity(float speed, float minSpeed = 6f, float maxSpeed = 14f)
    {
        float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        _targetPitch = Mathf.Lerp(0.92f, 1.18f, t);
        // Volume slightly louder when going fast (builds excitement)
        _targetMusicVol = Mathf.Lerp(0.85f, 1.1f, t);
    }

    /// <summary>Briefly dip music pitch/volume on player stun.</summary>
    public void TriggerStunDip() { _stunDipTimer = 0.8f; }

    /// <summary>Brief pitch wobble on zone transition.</summary>
    public void TriggerZoneSweep() { _zoneTransitionSweep = 0.5f; }

    void GenerateWindLoop()
    {
        // Loopable tunnel wind: band-pass filtered noise simulating air rushing through a pipe
        // Uses a longer clip (1s) for smoother looping
        float dur = 1.0f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * dur);
        float[] data = new float[samples];

        // Pre-generate white noise, then apply simple low-pass + high-pass for band-pass effect
        System.Random rng = new System.Random(777); // deterministic for consistent sound
        float prevLow = 0f;
        float prevHigh = 0f;
        float prevOut = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // Low-pass at ~800Hz (smooths out harshness)
            float lpAlpha = 0.11f; // cutoff ~800Hz at 44100
            prevLow += lpAlpha * (noise - prevLow);

            // High-pass at ~200Hz (removes low rumble, keeps airy quality)
            float hpAlpha = 0.97f;
            prevHigh = hpAlpha * (prevHigh + prevLow - prevOut);
            prevOut = prevLow;

            float signal = prevHigh;

            // Add subtle tonal resonance (pipe harmonic at ~350Hz)
            signal += Mathf.Sin(2f * Mathf.PI * 350f * t) * 0.08f;
            // Second harmonic for richer pipe sound
            signal += Mathf.Sin(2f * Mathf.PI * 700f * t + Mathf.Sin(t * 5f)) * 0.04f;

            // Smooth loop envelope (gentle fade at edges to prevent click)
            float loopEnv = 1f;
            float fadeZone = 0.02f; // 20ms fade
            if (t < fadeZone) loopEnv = t / fadeZone;
            else if (t > dur - fadeZone) loopEnv = (dur - t) / fadeZone;

            data[i] = signal * loopEnv * 0.25f;
        }

        _windLoop = AudioClip.Create("WindLoop", samples, 1, SAMPLE_RATE, false);
        _windLoop.SetData(data, 0);
    }

    /// <summary>Update wind loop volume/pitch based on player speed. Call from game loop.</summary>
    public void UpdateWindLoop(float speed)
    {
        if (_windSource == null || _windLoop == null) return;

        // Wind fades in starting at 4 SMPH, full intensity at 16+
        float intensity = Mathf.Clamp01((speed - 4f) / 12f);
        _windVolTarget = intensity;

        // Start playing when needed
        if (intensity > 0.01f && !_windSource.isPlaying)
        {
            _windSource.clip = _windLoop;
            _windSource.Play();
        }

        // Pitch rises with speed (deeper at slow, higher/whistlier at fast)
        _windSource.pitch = Mathf.Lerp(0.7f, 1.5f, intensity);
    }

    void GenerateZoneAmbientClips()
    {
        _zoneAmbientClips = new AudioClip[5];
        float loopDur = 4f; // 4 second loops

        // Zone 0: Porcelain - clean drips, gentle echo
        _zoneAmbientClips[0] = GenerateClip("Amb_Porcelain", loopDur, (t, dur) =>
        {
            float val = 0f;
            // Slow drip pattern (irregular timing)
            float drip1 = Mathf.Sin(t * 1.7f * Mathf.PI * 2f);
            float drip2 = Mathf.Sin(t * 2.3f * Mathf.PI * 2f);
            if (drip1 > 0.97f) val += (1f - Mathf.Abs(drip1 - 0.985f) / 0.015f) * 0.3f *
                Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-(t % 0.588f) * 25f);
            if (drip2 > 0.96f) val += 0.2f * Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-(t % 0.435f) * 20f);
            // Low reverberant hum
            val += Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.02f;
            // Very faint white noise (echo feel)
            val += (Random.value * 2f - 1f) * 0.008f;
            return val * 0.4f;
        });

        // Zone 1: Grimy - thicker drips, pipe groans, distant rumble
        _zoneAmbientClips[1] = GenerateClip("Amb_Grimy", loopDur, (t, dur) =>
        {
            float val = 0f;
            // Heavier drips
            float drip = Mathf.Sin(t * 3.1f * Mathf.PI * 2f);
            if (drip > 0.95f) val += 0.25f * Mathf.Sin(2f * Mathf.PI * 1400f * t) * Mathf.Exp(-(t % 0.322f) * 15f);
            // Pipe groan (low frequency sweep)
            float groan = Mathf.Sin(t * 0.4f * Mathf.PI * 2f);
            if (groan > 0.8f) val += groan * 0.06f * Mathf.Sin(2f * Mathf.PI * (45f + groan * 15f) * t);
            // Distant rumble
            val += Mathf.Sin(2f * Mathf.PI * 35f * t + Mathf.Sin(t * 0.7f) * 2f) * 0.03f;
            val += (Random.value * 2f - 1f) * 0.015f;
            return val * 0.4f;
        });

        // Zone 2: Toxic - bubbling, chemical hiss, acidic fizz
        _zoneAmbientClips[2] = GenerateClip("Amb_Toxic", loopDur, (t, dur) =>
        {
            float val = 0f;
            // Bubbling (random bursts at varying rates)
            float bubble1 = Mathf.Sin(t * 5.7f * Mathf.PI * 2f);
            float bubble2 = Mathf.Sin(t * 7.3f * Mathf.PI * 2f);
            if (bubble1 > 0.92f) val += 0.15f * Mathf.Sin(2f * Mathf.PI * (600f + bubble1 * 400f) * t) * (1f - bubble1);
            if (bubble2 > 0.94f) val += 0.12f * Mathf.Sin(2f * Mathf.PI * (800f + bubble2 * 300f) * t) * (1f - bubble2);
            // Chemical hiss (filtered noise)
            float hissEnv = Mathf.Sin(t * 0.8f * Mathf.PI * 2f) * 0.5f + 0.5f;
            val += (Random.value * 2f - 1f) * 0.025f * hissEnv;
            // Low toxic hum
            val += Mathf.Sin(2f * Mathf.PI * 80f * t + Mathf.Sin(t * 1.3f) * 3f) * 0.04f;
            return val * 0.4f;
        });

        // Zone 3: Rusty - metallic clanks, industrial hum, stress groans
        _zoneAmbientClips[3] = GenerateClip("Amb_Rusty", loopDur, (t, dur) =>
        {
            float val = 0f;
            // Metallic clanks (sharp transients)
            float clank1 = Mathf.Sin(t * 1.9f * Mathf.PI * 2f);
            if (clank1 > 0.98f)
            {
                float clankT = t % 0.526f;
                val += 0.3f * Mathf.Sin(2f * Mathf.PI * 3200f * clankT) * Mathf.Exp(-clankT * 40f);
                val += 0.15f * Mathf.Sin(2f * Mathf.PI * 4800f * clankT) * Mathf.Exp(-clankT * 50f);
            }
            // Industrial hum (power transformer)
            val += Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.04f;
            val += Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.02f; // harmonics
            // Stress groan (slow metallic bend)
            float groanCycle = Mathf.Sin(t * 0.3f * Mathf.PI * 2f);
            if (groanCycle > 0.7f) val += groanCycle * 0.05f * Mathf.Sin(2f * Mathf.PI * (50f + groanCycle * 30f) * t);
            val += (Random.value * 2f - 1f) * 0.012f;
            return val * 0.4f;
        });

        // Zone 4: Hellsewer - ominous drones, deep rumble, distant screech
        _zoneAmbientClips[4] = GenerateClip("Amb_Hellsewer", loopDur, (t, dur) =>
        {
            float val = 0f;
            // Deep ominous drone (multiple detuned oscillators)
            val += Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.05f;
            val += Mathf.Sin(2f * Mathf.PI * 41.5f * t) * 0.04f; // slight detune = beating
            val += Mathf.Sin(2f * Mathf.PI * 80f * t + Mathf.Sin(t * 0.5f) * 4f) * 0.03f;
            // Rumbling (modulated low noise)
            float rumbleMod = Mathf.Sin(t * 0.6f * Mathf.PI * 2f) * 0.5f + 0.5f;
            val += (Random.value * 2f - 1f) * 0.035f * rumbleMod;
            // Distant screech/wail (rare, high-pitched)
            float screech = Mathf.Sin(t * 0.15f * Mathf.PI * 2f);
            if (screech > 0.95f)
            {
                float screechFreq = 2000f + screech * 1500f;
                val += 0.08f * Mathf.Sin(2f * Mathf.PI * screechFreq * t) * (screech - 0.95f) * 20f;
            }
            // Heartbeat-like pulse
            float hb = Mathf.Sin(t * 1.2f * Mathf.PI * 2f);
            if (hb > 0.9f) val += 0.06f * Mathf.Sin(2f * Mathf.PI * 30f * t) * (hb - 0.9f) * 10f;
            return val * 0.45f;
        });
    }

    /// <summary>Update zone ambient audio. Call when zone changes or blends.</summary>
    public void UpdateZoneAmbient(int zoneIndex)
    {
        if (_zoneAmbientClips == null || zoneIndex < 0 || zoneIndex >= _zoneAmbientClips.Length) return;
        if (zoneIndex == _currentAmbientZone) return;

        _currentAmbientZone = zoneIndex;
        _ambientCrossfade = 0f;

        // Crossfade: new zone on source2, old continues on source1 (then swap)
        AudioSource incoming = _ambientSwapping ? _ambientSource : _ambientSource2;
        incoming.clip = _zoneAmbientClips[zoneIndex];
        incoming.volume = 0f;
        incoming.Play();
        _ambientSwapping = !_ambientSwapping;
    }

    void Update()
    {
        // Smooth drift grind volume ramping
        if (_driftSource != null)
        {
            float targetVol = _driftVolTarget * masterVolume * sfxVolume * 0.35f;
            _driftSource.volume = Mathf.Lerp(_driftSource.volume, targetVol, Time.deltaTime * 8f);
            if (_driftSource.volume < 0.005f && _driftSource.isPlaying && _driftVolTarget <= 0f)
                _driftSource.Stop();
        }

        // Wind loop: smooth volume ramp with speed
        if (_windSource != null)
        {
            float targetVol = _windVolTarget * masterVolume * sfxVolume * 0.28f;
            _windSource.volume = Mathf.Lerp(_windSource.volume, targetVol, Time.deltaTime * 4f);
            if (_windSource.volume < 0.003f && _windSource.isPlaying && _windVolTarget <= 0f)
                _windSource.Stop();
        }

        if (_musicSource != null && _musicPlaying)
        {
            // Auto-poll player speed if available
            if (RaceManager.Instance != null && RaceManager.Instance.PlayerController != null
                && RaceManager.Instance.RaceState == RaceManager.State.Racing)
            {
                float playerSpeed = RaceManager.Instance.PlayerController.CurrentSpeed;
                float playerDist = RaceManager.Instance.PlayerController.DistanceTraveled;
                float raceDist = RaceManager.Instance.RaceDistance;

                SetSpeedIntensity(playerSpeed);

                // Final stretch tension: builds over last 300m with escalating intensity
                float distToFinish = raceDist - playerDist;
                if (distToFinish < 300f && distToFinish > 0f)
                {
                    float finaleT = 1f - (distToFinish / 300f); // 0 at 300m, 1 at finish

                    // Exponential ramp: gentle early, aggressive in final 100m
                    float ramp = finaleT * finaleT; // quadratic curve
                    _targetPitch += ramp * 0.15f;   // up to +15% pitch at finish
                    _targetMusicVol += ramp * 0.15f; // up to +15% volume

                    // Heartbeat bass pulse in final 150m (gets faster as you approach)
                    if (distToFinish < 150f)
                    {
                        float heartT = 1f - (distToFinish / 150f);
                        float heartRate = Mathf.Lerp(1.5f, 4f, heartT); // 1.5 to 4 Hz
                        _heartbeatPhase += Time.deltaTime * heartRate * Mathf.PI * 2f;
                        float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_heartbeatPhase)), 4f);
                        _targetMusicVol += pulse * heartT * 0.12f;
                    }

                    // "FINAL STRETCH!" announcement at 200m
                    if (!_finalStretchAnnounced && distToFinish < 200f)
                    {
                        _finalStretchAnnounced = true;
                        if (CheerOverlay.Instance != null)
                            CheerOverlay.Instance.ShowCheer("FINAL STRETCH!", new Color(1f, 0.6f, 0.1f), true);
                        if (PipeCamera.Instance != null)
                            PipeCamera.Instance.PunchFOV(4f);
                        HapticManager.MediumTap();
                    }

                    // "PHOTO FINISH!" at 50m if position is close
                    if (!_photoFinishAnnounced && distToFinish < 50f)
                    {
                        _photoFinishAnnounced = true;
                        int pos = RaceManager.Instance.GetPlayerPosition();
                        if (pos <= 3) // only if player is competitive
                        {
                            if (CheerOverlay.Instance != null)
                                CheerOverlay.Instance.ShowCheer("PHOTO FINISH!", new Color(1f, 0.2f, 0.2f), true);
                            if (PipeCamera.Instance != null)
                            {
                                PipeCamera.Instance.Shake(0.15f);
                                PipeCamera.Instance.PunchFOV(6f);
                            }
                            HapticManager.HeavyTap();
                        }
                    }
                }
            }

            // Stun dip: drop pitch and vol when stunned, recover gradually
            float pitchMod = 0f;
            float volMod = 0f;
            if (_stunDipTimer > 0f)
            {
                _stunDipTimer -= Time.deltaTime;
                float stunT = _stunDipTimer / 0.8f;
                pitchMod -= stunT * 0.15f; // drop pitch up to 15%
                volMod -= stunT * 0.25f;   // drop volume 25%
            }

            // Zone transition sweep: brief pitch wobble
            if (_zoneTransitionSweep > 0f)
            {
                _zoneTransitionSweep -= Time.deltaTime;
                float sweepT = _zoneTransitionSweep / 0.5f;
                pitchMod += Mathf.Sin(sweepT * Mathf.PI * 3f) * 0.06f * sweepT;
            }

            // Rival proximity: subtle volume pulse when someone is close behind
            if (RaceManager.Instance != null)
            {
                float closestBehind = float.MaxValue;
                float pDist = RaceManager.Instance.PlayerController != null
                    ? RaceManager.Instance.PlayerController.DistanceTraveled : 0f;
                foreach (var ai in Object.FindObjectsByType<RacerAI>(FindObjectsSortMode.None))
                {
                    if (ai.IsFinished) continue;
                    float gap = pDist - ai.DistanceTraveled;
                    if (gap > 0f && gap < closestBehind) closestBehind = gap;
                }
                // Pulse intensity based on how close rival is (within 8m)
                float rivalT = closestBehind < 8f ? 1f - (closestBehind / 8f) : 0f;
                _rivalProximityPulse = Mathf.Lerp(_rivalProximityPulse, rivalT, Time.deltaTime * 4f);
                if (_rivalProximityPulse > 0.05f)
                    volMod += Mathf.Sin(Time.time * 4f) * _rivalProximityPulse * 0.08f;
            }

            // Smooth pitch interpolation (avoids jarring jumps)
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch + pitchMod, Time.deltaTime * 3f);
            _currentMusicVol = Mathf.Lerp(_currentMusicVol, _targetMusicVol + volMod, Time.deltaTime * 3f);

            _musicSource.pitch = _currentPitch;
            _musicSource.volume = masterVolume * musicVolume * Mathf.Max(0.1f, _currentMusicVol) * _musicFadeMul;
        }

        // Music fade multiplier (for tour mode transitions)
        _musicFadeMul = Mathf.MoveTowards(_musicFadeMul, _musicFadeTarget, Time.deltaTime * _musicFadeSpeed);

        // Zone ambient crossfade
        if (_ambientSource != null && _ambientSource2 != null && _currentAmbientZone >= 0)
        {
            _ambientCrossfade = Mathf.MoveTowards(_ambientCrossfade, 1f, Time.deltaTime * 0.8f); // ~1.2s crossfade
            float ambVol = masterVolume * sfxVolume * 0.35f;
            // _ambientSwapping toggles which source is "incoming"
            // After swap: source is outgoing, source2 is incoming (or vice versa)
            if (_ambientSwapping)
            {
                _ambientSource.volume = ambVol * _ambientCrossfade;
                _ambientSource2.volume = ambVol * (1f - _ambientCrossfade);
                if (_ambientCrossfade >= 1f && _ambientSource2.isPlaying)
                    _ambientSource2.Stop();
            }
            else
            {
                _ambientSource2.volume = ambVol * _ambientCrossfade;
                _ambientSource.volume = ambVol * (1f - _ambientCrossfade);
                if (_ambientCrossfade >= 1f && _ambientSource.isPlaying && _ambientSource.clip != _ambientSource2.clip)
                    _ambientSource.Stop();
            }

            // Poll zone changes automatically
            if (PipeZoneSystem.Instance != null)
                UpdateZoneAmbient(PipeZoneSystem.Instance.CurrentZoneIndex);
        }
    }
}
