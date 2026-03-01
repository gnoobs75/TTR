using UnityEngine;
using System;

/// <summary>
/// Challenge code encoding/decoding and stat comparison for seed-based runs.
/// Binary-packed → Base64 with TTR- prefix. No server needed — code contains all data.
/// </summary>
public static class SeedChallenge
{
    /// <summary>All stats from a challenge run.</summary>
    public struct ChallengeData
    {
        public int seed;
        public bool isRace;
        public int place;       // 0-5 (0 = 1st)
        public int score;
        public float time;
        public float maxSpeed;
        public int hits;
        public int boosts;
        public int nearMisses;
        public int bestCombo;
        public int stomps;
        public int coins;
        public float distance;
    }

    /// <summary>Per-stat comparison result.</summary>
    public enum StatResult { Win, Lose, Tie }

    /// <summary>Full comparison between player and challenger.</summary>
    public struct ComparisonResult
    {
        public StatResult place;
        public StatResult time;
        public StatResult score;
        public StatResult maxSpeed;
        public StatResult hits;     // lower = better
        public StatResult boosts;
        public StatResult nearMisses;
        public StatResult bestCombo;
        public StatResult stomps;
        public StatResult coins;
        public StatResult distance;
        public int playerWins;
        public int challengerWins;
    }

    /// <summary>The active challenge being played (null if normal run).</summary>
    public static ChallengeData? ActiveChallenge;

    /// <summary>Player's result after completing a challenge run.</summary>
    public static ChallengeData? PlayerResult;

    /// <summary>The seed word for the current seeded run (null if random/normal run).</summary>
    public static string SeedWord;

    /// <summary>
    /// Convert any text into a deterministic seed integer.
    /// Same word always produces the same seed, case-insensitive.
    /// Uses djb2 hash (deterministic across all .NET runtimes, unlike string.GetHashCode).
    /// </summary>
    public static int WordToSeed(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;
        string normalized = word.Trim().ToLowerInvariant();
        int hash = 5381;
        for (int i = 0; i < normalized.Length; i++)
            hash = ((hash << 5) + hash) + normalized[i];
        return hash;
    }

    // ──────────────────────────────────────────────
    // ENCODING: ChallengeData → 32-char code
    // Layout (19 bytes):
    //   [0-3]   seed (int32)
    //   [4]     flags: bit0=isRace, bits1-3=place(0-5)
    //   [5-7]   score (uint24, max ~16M)
    //   [8-9]   time (uint16, tenths of second, max 6553.5s)
    //   [10-11] maxSpeed (uint16, hundredths, max 655.35)
    //   [12]    hits (byte)
    //   [13]    boosts (byte)
    //   [14]    nearMisses (byte, capped at 255)
    //   [15]    bestCombo (byte, capped at 255)
    //   [16]    stomps (byte, capped at 255)
    //   [17]    coins (byte, capped at 255)
    //   [18]    checksum (XOR of bytes 0-17)
    // ──────────────────────────────────────────────

    private const int DATA_SIZE = 19;
    private const string PREFIX = "TTR-";

    /// <summary>Encode challenge data into a shareable code string.</summary>
    public static string Encode(ChallengeData data)
    {
        byte[] buf = new byte[DATA_SIZE];

        // Seed (little-endian)
        buf[0] = (byte)(data.seed & 0xFF);
        buf[1] = (byte)((data.seed >> 8) & 0xFF);
        buf[2] = (byte)((data.seed >> 16) & 0xFF);
        buf[3] = (byte)((data.seed >> 24) & 0xFF);

        // Flags
        byte flags = 0;
        if (data.isRace) flags |= 0x01;
        flags |= (byte)((Mathf.Clamp(data.place, 0, 5) & 0x07) << 1);
        buf[4] = flags;

        // Score (24-bit)
        int score = Mathf.Clamp(data.score, 0, 0xFFFFFF);
        buf[5] = (byte)(score & 0xFF);
        buf[6] = (byte)((score >> 8) & 0xFF);
        buf[7] = (byte)((score >> 16) & 0xFF);

        // Time (tenths of second, 16-bit)
        ushort timeTenths = (ushort)Mathf.Clamp(data.time * 10f, 0, 65535);
        buf[8] = (byte)(timeTenths & 0xFF);
        buf[9] = (byte)((timeTenths >> 8) & 0xFF);

        // MaxSpeed (hundredths, 16-bit)
        ushort speedHundredths = (ushort)Mathf.Clamp(data.maxSpeed * 100f, 0, 65535);
        buf[10] = (byte)(speedHundredths & 0xFF);
        buf[11] = (byte)((speedHundredths >> 8) & 0xFF);

        // Single-byte stats
        buf[12] = (byte)Mathf.Clamp(data.hits, 0, 255);
        buf[13] = (byte)Mathf.Clamp(data.boosts, 0, 255);
        buf[14] = (byte)Mathf.Clamp(data.nearMisses, 0, 255);
        buf[15] = (byte)Mathf.Clamp(data.bestCombo, 0, 255);
        buf[16] = (byte)Mathf.Clamp(data.stomps, 0, 255);
        buf[17] = (byte)Mathf.Clamp(data.coins, 0, 255);

        // Checksum
        byte checksum = 0;
        for (int i = 0; i < DATA_SIZE - 1; i++)
            checksum ^= buf[i];
        buf[18] = checksum;

        string b64 = Convert.ToBase64String(buf).TrimEnd('=');
        return PREFIX + FormatDashes(b64);
    }

    /// <summary>Decode a challenge code string into ChallengeData. Returns false on invalid.</summary>
    public static bool Decode(string code, out ChallengeData data)
    {
        data = default;
        if (string.IsNullOrEmpty(code)) return false;

        // Strip prefix and dashes
        string clean = code.Trim().ToUpperInvariant();
        if (clean.StartsWith(PREFIX))
            clean = clean.Substring(PREFIX.Length);
        clean = clean.Replace("-", "");

        // Pad base64
        while (clean.Length % 4 != 0)
            clean += "=";

        byte[] buf;
        try { buf = Convert.FromBase64String(clean); }
        catch { return false; }

        if (buf.Length != DATA_SIZE) return false;

        // Verify checksum
        byte checksum = 0;
        for (int i = 0; i < DATA_SIZE - 1; i++)
            checksum ^= buf[i];
        if (checksum != buf[18]) return false;

        // Decode
        data.seed = buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
        data.isRace = (buf[4] & 0x01) != 0;
        data.place = (buf[4] >> 1) & 0x07;
        data.score = buf[5] | (buf[6] << 8) | (buf[7] << 16);
        data.time = (buf[8] | (buf[9] << 8)) / 10f;
        data.maxSpeed = (buf[10] | (buf[11] << 8)) / 100f;
        data.hits = buf[12];
        data.boosts = buf[13];
        data.nearMisses = buf[14];
        data.bestCombo = buf[15];
        data.stomps = buf[16];
        data.coins = buf[17];

        return true;
    }

    /// <summary>Compare player result to challenger. Higher-is-better for most stats, lower for hits.</summary>
    public static ComparisonResult Compare(ChallengeData player, ChallengeData challenger)
    {
        var r = new ComparisonResult();

        // Place: lower is better (0 = 1st)
        r.place = CompLower(player.place, challenger.place);
        // Time: lower is better
        r.time = CompLower(player.time, challenger.time);
        // Score: higher is better
        r.score = CompHigher(player.score, challenger.score);
        // MaxSpeed: higher is better
        r.maxSpeed = CompHigher(player.maxSpeed, challenger.maxSpeed);
        // Hits: lower is better
        r.hits = CompLower(player.hits, challenger.hits);
        // Boosts: higher is better
        r.boosts = CompHigher(player.boosts, challenger.boosts);
        // Near misses: higher is better
        r.nearMisses = CompHigher(player.nearMisses, challenger.nearMisses);
        // Best combo: higher is better
        r.bestCombo = CompHigher(player.bestCombo, challenger.bestCombo);
        // Stomps: higher is better
        r.stomps = CompHigher(player.stomps, challenger.stomps);
        // Coins: higher is better
        r.coins = CompHigher(player.coins, challenger.coins);
        // Distance: higher is better (endless mode)
        r.distance = CompHigher(player.distance, challenger.distance);

        // Count wins
        r.playerWins = 0; r.challengerWins = 0;
        CountWin(r.place, ref r.playerWins, ref r.challengerWins);
        CountWin(r.time, ref r.playerWins, ref r.challengerWins);
        CountWin(r.score, ref r.playerWins, ref r.challengerWins);
        CountWin(r.maxSpeed, ref r.playerWins, ref r.challengerWins);
        CountWin(r.hits, ref r.playerWins, ref r.challengerWins);
        CountWin(r.boosts, ref r.playerWins, ref r.challengerWins);
        CountWin(r.nearMisses, ref r.playerWins, ref r.challengerWins);
        CountWin(r.bestCombo, ref r.playerWins, ref r.challengerWins);
        CountWin(r.stomps, ref r.playerWins, ref r.challengerWins);
        CountWin(r.coins, ref r.playerWins, ref r.challengerWins);

        return r;
    }

    /// <summary>Build shareable clipboard text with seed word and stats.</summary>
    public static string BuildShareText(ChallengeData data, string seedWord)
    {
        string placeStr = data.isRace ? $"#{data.place + 1} PLACE" : $"{data.distance:F0}m";

        return $"TURD TUNNEL RUSH\n" +
               $"Race seed: {seedWord}\n" +
               $"{placeStr} | {data.time:F1}s\n" +
               $"{data.coins} Coins | {data.maxSpeed:F1} SMPH\n" +
               $"Score: {data.score:N0}\n" +
               $"Can you beat me?";
    }

    // ──────── Helpers ────────

    static string FormatDashes(string s)
    {
        // Insert dashes every 6 chars for readability
        string result = "";
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && i % 6 == 0) result += "-";
            result += s[i];
        }
        return result;
    }

    static StatResult CompHigher(float a, float b)
    {
        if (a > b + 0.001f) return StatResult.Win;
        if (b > a + 0.001f) return StatResult.Lose;
        return StatResult.Tie;
    }

    static StatResult CompLower(float a, float b)
    {
        if (a < b - 0.001f) return StatResult.Win;
        if (b < a - 0.001f) return StatResult.Lose;
        return StatResult.Tie;
    }

    static void CountWin(StatResult r, ref int playerWins, ref int challengerWins)
    {
        if (r == StatResult.Win) playerWins++;
        else if (r == StatResult.Lose) challengerWins++;
    }
}
