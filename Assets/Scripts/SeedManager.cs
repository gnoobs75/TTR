using UnityEngine;

/// <summary>
/// Deterministic RNG manager for seed-based challenge runs.
/// Provides per-system System.Random instances so pipe layout, obstacles,
/// coins, power-ups, and AI all use independent seeded streams.
/// </summary>
public class SeedManager : MonoBehaviour
{
    public static SeedManager Instance { get; private set; }

    public int CurrentSeed { get; private set; }
    public bool IsSeededRun { get; private set; }

    // Per-system RNG streams
    public System.Random PipeRNG { get; private set; }
    public System.Random PipeDetailRNG { get; private set; }
    public System.Random ObstacleRNG { get; private set; }
    public System.Random CoinRNG { get; private set; }
    public System.Random PowerUpRNG { get; private set; }
    public System.Random[] AIRNG { get; private set; }

    // XOR derivation constants for sub-seeds
    private const int PIPE_XOR       = 0x00000000; // pipe path = master seed
    private const int DETAIL_XOR     = 0x12345678;
    private const int OBSTACLE_XOR   = 0x67890ABC;
    private const int COIN_XOR       = 0x2468ACE0;
    private const int POWERUP_XOR    = 0x13579BDF;
    private const int AI_BASE_XOR    = 0x0A0B0C0D;

    private const int MAX_AI_RACERS = 5;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Initialize with a random seed immediately so streams are never null.
        // ActuallyStartGame() will re-initialize with the proper seed later.
        InitializeRandomSeed();
    }

    /// <summary>
    /// Initialize all RNG streams from a specific seed (for challenge runs).
    /// </summary>
    public void InitializeSeed(int seed)
    {
        CurrentSeed = seed;
        IsSeededRun = true;
        CreateStreams(seed);
    }

    /// <summary>
    /// Generate a random seed and initialize (for normal runs).
    /// </summary>
    public void InitializeRandomSeed()
    {
        // Use system random to generate a seed, not UnityEngine.Random
        CurrentSeed = new System.Random().Next(int.MinValue, int.MaxValue);
        IsSeededRun = false;
        CreateStreams(CurrentSeed);
    }

    private void CreateStreams(int seed)
    {
        PipeRNG       = new System.Random(seed ^ PIPE_XOR);
        PipeDetailRNG = new System.Random(seed ^ DETAIL_XOR);
        ObstacleRNG   = new System.Random(seed ^ OBSTACLE_XOR);
        CoinRNG       = new System.Random(seed ^ COIN_XOR);
        PowerUpRNG    = new System.Random(seed ^ POWERUP_XOR);

        AIRNG = new System.Random[MAX_AI_RACERS];
        for (int i = 0; i < MAX_AI_RACERS; i++)
            AIRNG[i] = new System.Random(seed ^ (AI_BASE_XOR + i));
    }

    // ──────────────────────────────────────────────
    // Static helpers to replace Random.Range / Random.value
    // ──────────────────────────────────────────────

    /// <summary>Float range [min, max) — replaces Random.Range(float, float)</summary>
    public static float Range(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    /// <summary>Int range [min, max) — replaces Random.Range(int, int)</summary>
    public static int Range(System.Random rng, int min, int max)
    {
        if (min >= max) return min;
        return min + rng.Next(max - min);
    }

    /// <summary>Float [0, 1) — replaces Random.value</summary>
    public static float Value(System.Random rng)
    {
        return (float)rng.NextDouble();
    }
}
