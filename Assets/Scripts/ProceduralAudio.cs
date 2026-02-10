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

    // Stomp sound
    private AudioClip _stomp;

    // Hit sounds (per obstacle type)
    private AudioClip _ratPounce;
    private AudioClip _hairWrap;
    private AudioClip _barrelSplash;
    private AudioClip _blobSquish;
    private AudioClip _mineExplosion;
    private AudioClip _roachScurry;

    // Music
    private AudioClip _bgmLoop;
    private bool _musicPlaying;

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

        // Music audio source
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;
        _musicSource.loop = true;

        GenerateAllClips();
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
        // Rat squeak: high-pitched chirp with vibrato
        _ratSqueak = GenerateClip("RatSqueak", 0.15f, (t, dur) =>
        {
            float freq = 1800f + Mathf.Sin(t * 80f) * 200f; // vibrato
            float env = Mathf.Sin(Mathf.PI * t / dur);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.35f;
        });

        // Barrel beep: two-tone warning alert
        _barrelBeep = GenerateClip("BarrelBeep", 0.3f, (t, dur) =>
        {
            float freq = t < dur * 0.5f ? 600f : 800f;
            float env = Mathf.Sin(Mathf.PI * (t % (dur * 0.5f)) / (dur * 0.5f));
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.35f;
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

        // Roach hiss: filtered noise burst
        _roachHiss = GenerateClip("RoachHiss", 0.25f, (t, dur) =>
        {
            float env = Mathf.Exp(-t * 8f);
            // Deterministic noise from sample index
            float noise = Mathf.Sin(t * 12345.6f) * Mathf.Cos(t * 54321.0f);
            float highFilter = Mathf.Sin(2f * Mathf.PI * 4000f * t) * 0.3f;
            return (noise * 0.6f + highFilter * 0.2f) * env * 0.3f;
        });

        // AI taunt: ascending celebratory chirp
        _aiTaunt = GenerateClip("AITaunt", 0.35f, (t, dur) =>
        {
            float freq = Mathf.Lerp(400f, 800f, t / dur);
            float env = Mathf.Sin(Mathf.PI * t / dur);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f
                       + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.15f;
            return tone * env * 0.4f;
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

        // Background music: simple bass-driven loop
        GenerateBGM();
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

    public void PlayCoinCollect() => PlaySFX(_coinCollect);
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

    // Hit sounds (per obstacle type)
    public void PlayRatPounce() => PlaySFX(_ratPounce);
    public void PlayHairWrap() => PlaySFX(_hairWrap);
    public void PlayBarrelSplash() => PlaySFX(_barrelSplash);
    public void PlayBlobSquish() => PlaySFX(_blobSquish);
    public void PlayMineExplosion() => PlaySFX(_mineExplosion);
    public void PlayRoachScurry() => PlaySFX(_roachScurry);

    public void StartMusic()
    {
        if (_musicSource == null || _bgmLoop == null || _musicPlaying) return;
        _musicSource.clip = _bgmLoop;
        _musicSource.volume = masterVolume * musicVolume;
        _musicSource.Play();
        _musicPlaying = true;
    }

    public void StopMusic()
    {
        if (_musicSource != null)
            _musicSource.Stop();
        _musicPlaying = false;
    }

    void Update()
    {
        if (_musicSource != null && _musicPlaying)
            _musicSource.volume = masterVolume * musicVolume;
    }
}
