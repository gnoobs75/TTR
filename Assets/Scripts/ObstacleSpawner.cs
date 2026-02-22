using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns obstacles and collectibles along the pipe path.
/// Uses PipeGenerator path system for positioning in curved pipes.
/// Obstacles spawn on floor, walls, AND ceiling to prevent cheese strategies.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float spawnDistance = 80f;
    public float minSpacing = 24f;
    public float maxSpacing = 48f;
    public float pipeRadius = 3.5f;

    [Header("Obstacle Prefabs")]
    public GameObject[] obstaclePrefabs;

    [Header("Collectible Prefabs")]
    public GameObject coinPrefab;

    [Header("Special Prefabs")]
    public GameObject gratePrefab;
    public GameObject bigAirRampPrefab;
    public GameObject dropZonePrefab;

    [Header("Difficulty")]
    [Range(0f, 1f)]
    public float obstacleChance = 0.3f;
    public float difficultyRamp = 0.01f;

    [Header("Player Reference")]
    public Transform player;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private float _nextSpawnDist = 25f;
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private float _cleanupDistance = 50f;
    private int _obstacleIndex = 0;

    // Zone-themed obstacle pools (built once at Start from obstaclePrefabs)
    // Porcelain: PoopBlob, ToxicBarrel, Duck (easy intro)
    // Grimy: HairWad (the clog)
    // Toxic: ToxicFrog, SewerJellyfish, SewerMine (toxic hazards)
    // Rusty: SewerRat, Cockroach, SewerSpider (pest infestation)
    // Hellsewer: ALL (everything at once)
    private List<GameObject>[] _zonePools;
    private int[] _zonePoolIndex; // per-zone cycling index

    // Special event tracking
    private float _nextBigAirDist = 300f;
    private float _nextDropDist = 200f;
    private float _nextGrateDist = 80f;

    // Speed corridors: obstacle-free stretches packed with boosts and coins
    private static readonly Vector2[] SpeedCorridors = {
        new Vector2(280f, 360f),    // Late Porcelain → Grimy transition
        new Vector2(770f, 850f),    // Mid Toxic, before Rusty
        new Vector2(1350f, 1430f),  // Late Rusty, approaching Hellsewer
    };
    private bool[] _corridorAnnounced;

    /// <summary>Returns true if distance is inside a speed corridor (no obstacles).</summary>
    public static bool IsSpeedCorridor(float dist)
    {
        for (int i = 0; i < SpeedCorridors.Length; i++)
        {
            if (dist >= SpeedCorridors[i].x && dist <= SpeedCorridors[i].y)
                return true;
        }
        return false;
    }

    /// <summary>Returns the end distance of the corridor containing dist, or dist if not in one.</summary>
    public static float GetCorridorEnd(float dist)
    {
        for (int i = 0; i < SpeedCorridors.Length; i++)
        {
            if (dist >= SpeedCorridors[i].x && dist <= SpeedCorridors[i].y)
                return SpeedCorridors[i].y;
        }
        return dist;
    }

    /// <summary>Returns a spacing multiplier based on proximity to corridors.
    /// Spacing opens up approaching corridors, tightens after.</summary>
    public static float GetCorridorSpacingMultiplier(float dist)
    {
        for (int i = 0; i < SpeedCorridors.Length; i++)
        {
            float start = SpeedCorridors[i].x;
            float end = SpeedCorridors[i].y;
            // 20m approach: gradually open up
            if (dist >= start - 20f && dist < start)
            {
                float t = (dist - (start - 20f)) / 20f;
                return Mathf.Lerp(1f, 1.5f, t);
            }
            // 20m after: gradually tighten back
            if (dist > end && dist <= end + 20f)
            {
                float t = (dist - end) / 20f;
                return Mathf.Lerp(1.5f, 0.8f, t);
            }
        }
        return 1f;
    }

    void Start()
    {
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (player != null)
            _tc = player.GetComponent<TurdController>();
        _corridorAnnounced = new bool[SpeedCorridors.Length];
        BuildZonePools();
    }

    void BuildZonePools()
    {
        _zonePools = new List<GameObject>[5];
        _zonePoolIndex = new int[5];
        for (int i = 0; i < 5; i++)
        {
            _zonePools[i] = new List<GameObject>();
            _zonePoolIndex[i] = 0;
        }

        if (obstaclePrefabs == null) return;

        foreach (var prefab in obstaclePrefabs)
        {
            if (prefab == null) continue;

            // Classify by behavior component or name
            if (prefab.GetComponent<PoopBlobBehavior>() != null ||
                prefab.GetComponent<ToxicBarrelBehavior>() != null ||
                prefab.name.IndexOf("Duck", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _zonePools[0].Add(prefab); // Porcelain
            }
            else if (prefab.GetComponent<HairWadBehavior>() != null)
            {
                _zonePools[1].Add(prefab); // Grimy
            }
            else if (prefab.GetComponent<ToxicFrogBehavior>() != null ||
                     prefab.GetComponent<SewerJellyfishBehavior>() != null ||
                     prefab.GetComponent<SewerMineBehavior>() != null)
            {
                _zonePools[2].Add(prefab); // Toxic
            }
            else if (prefab.GetComponent<SewerRatBehavior>() != null ||
                     prefab.GetComponent<CockroachBehavior>() != null ||
                     prefab.GetComponent<SewerSpiderBehavior>() != null)
            {
                _zonePools[3].Add(prefab); // Rusty
            }
            else
            {
                // Unclassified: add to Porcelain as fallback
                _zonePools[0].Add(prefab);
            }
        }

        // Hellsewer (zone 4) gets ALL obstacle types
        _zonePools[4].AddRange(obstaclePrefabs);
    }

    GameObject GetZoneObstacle(float dist)
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return null;

        // Determine zone from distance (matches PipeZoneSystem boundaries)
        int zoneIdx;
        if (dist < 155f) zoneIdx = 0;       // Porcelain
        else if (dist < 510f) zoneIdx = 1;   // Grimy
        else if (dist < 1020f) zoneIdx = 2;  // Toxic
        else if (dist < 1600f) zoneIdx = 3;  // Rusty
        else zoneIdx = 4;                     // Hellsewer

        // 15% chance of a "wanderer" from the full pool for variety
        if (Random.value < 0.15f)
        {
            _obstacleIndex = (_obstacleIndex + 1) % obstaclePrefabs.Length;
            return obstaclePrefabs[_obstacleIndex];
        }

        // Use zone pool; fall back to full pool if zone pool is empty
        var pool = _zonePools[zoneIdx];
        if (pool == null || pool.Count == 0)
        {
            _obstacleIndex = (_obstacleIndex + 1) % obstaclePrefabs.Length;
            return obstaclePrefabs[_obstacleIndex];
        }

        // Cycle through zone pool sequentially for fairness
        int idx = _zonePoolIndex[zoneIdx];
        _zonePoolIndex[zoneIdx] = (idx + 1) % pool.Count;
        return pool[idx];
    }

    void Update()
    {
        if (player == null) return;

        float playerDist = _tc != null ? _tc.DistanceTraveled : 0f;

        // Spawn ahead
        while (_nextSpawnDist < playerDist + spawnDistance)
        {
            SpawnAtDistance(_nextSpawnDist);
            // Spacing opens up near corridors, tightens after
            float spacingMult = GetCorridorSpacingMultiplier(_nextSpawnDist);
            _nextSpawnDist += Random.Range(minSpacing, maxSpacing) * spacingMult;
        }

        // Special events: big air ramps (every 300-500m) — skip in corridors
        if (bigAirRampPrefab != null && _nextBigAirDist < playerDist + spawnDistance)
        {
            if (IsSpeedCorridor(_nextBigAirDist))
                _nextBigAirDist = GetCorridorEnd(_nextBigAirDist) + 20f; // push past corridor
            else
            {
                SpawnBigAirRamp(_nextBigAirDist);
                _nextBigAirDist += Random.Range(300f, 500f);
            }
        }

        // Special events: vertical drops (every 400-600m, starts after 200m) — skip in corridors
        if (dropZonePrefab != null && _nextDropDist < playerDist + spawnDistance)
        {
            if (IsSpeedCorridor(_nextDropDist))
                _nextDropDist = GetCorridorEnd(_nextDropDist) + 20f;
            else
            {
                SpawnDropZone(_nextDropDist);
                _nextDropDist += Random.Range(400f, 600f);
            }
        }

        // Grate obstacles (every 60-120m, starts after 80m) — skip in corridors
        if (gratePrefab != null && _nextGrateDist < playerDist + spawnDistance)
        {
            if (IsSpeedCorridor(_nextGrateDist))
                _nextGrateDist = GetCorridorEnd(_nextGrateDist) + 10f;
            else
            {
                SpawnGrate(_nextGrateDist);
                _nextGrateDist += Random.Range(60f, 120f);
            }
        }

        // Speed corridor entry announcements
        CheckCorridorEntry(playerDist);

        // Cleanup behind
        for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (_spawnedObjects[i] == null)
            {
                _spawnedObjects.RemoveAt(i);
                continue;
            }

            Vector3 toObj = _spawnedObjects[i].transform.position - player.position;
            if (toObj.magnitude > _cleanupDistance && Vector3.Dot(toObj, player.forward) < 0)
            {
                Destroy(_spawnedObjects[i]);
                _spawnedObjects.RemoveAt(i);
            }
        }
    }

    void SpawnAtDistance(float dist)
    {
        // Speed corridors: no obstacles, always spawn dense coin trails
        if (IsSpeedCorridor(dist))
        {
            if (coinPrefab != null)
            {
                // Always Spiral or FullLoop patterns in corridors for max collection
                if (Random.value < 0.5f)
                    SpawnCoinsSpiral(dist);
                else
                    SpawnCoinsFullLoop(dist);
            }
            return;
        }

        float currentChance = Mathf.Min(obstacleChance + dist * difficultyRamp * 0.001f, 0.7f);

        // Corridor proximity spacing modifier
        float spacingMult = GetCorridorSpacingMultiplier(dist);
        if (spacingMult > 1f)
            currentChance *= (1f / spacingMult); // reduce obstacle chance near corridors

        // Fork density modifier: if in a fork zone, adjust spawn rates
        float coinMult = 1f;
        float obstacleMult = 1f;
        if (_tc != null && _tc.CurrentFork != null)
        {
            coinMult = _tc.CurrentFork.GetCoinMultiplier();
            obstacleMult = _tc.CurrentFork.GetObstacleMultiplier();
            currentChance *= obstacleMult;
        }

        if (_pipeGen != null)
        {
            Vector3 center, forward, right, up;

            // Branch-aware: spawn on the player's current branch path
            PipeFork fork = _tc != null ? _tc.CurrentFork : null;
            int branch = _tc != null ? _tc.ForkBranch : -1;

            if (fork != null && branch >= 0)
            {
                Vector3 mainC, mainF, mainR, mainU;
                _pipeGen.GetPathFrame(dist, out mainC, out mainF, out mainR, out mainU);

                Vector3 bC, bF, bR, bU;
                if (fork.GetBranchFrame(branch, dist, out bC, out bF, out bR, out bU))
                {
                    float blend = fork.GetBranchBlend(dist);
                    center = Vector3.Lerp(mainC, bC, blend);
                    forward = Vector3.Slerp(mainF, bF, blend).normalized;
                    right = Vector3.Slerp(mainR, bR, blend).normalized;
                    up = Vector3.Slerp(mainU, bU, blend).normalized;
                }
                else
                {
                    center = mainC; forward = mainF; right = mainR; up = mainU;
                }
            }
            else
            {
                _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);
            }

            if (Random.value < currentChance && obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            {
                SpawnObstacle(dist, center, forward, right, up);
            }
            else if (coinPrefab != null)
            {
                // In risky fork: spawn extra coin trails
                SpawnCoinTrailAlongPath(dist);
                if (coinMult > 1.5f && Random.value < 0.4f)
                    SpawnCoinTrailAlongPath(dist + 5f); // bonus trail
            }
        }
    }

    static readonly string[] CorridorSubtitles = {
        "OPEN ROAD!", "PEDAL TO THE METAL!", "FULL FLUSH AHEAD!"
    };

    void CheckCorridorEntry(float playerDist)
    {
        if (_corridorAnnounced == null) return;
        for (int i = 0; i < SpeedCorridors.Length; i++)
        {
            if (_corridorAnnounced[i]) continue;
            float start = SpeedCorridors[i].x;
            if (playerDist >= start && playerDist <= start + 5f)
            {
                _corridorAnnounced[i] = true;
#if UNITY_EDITOR
                Debug.Log($"[CORRIDOR] Entering speed corridor {i} at dist={playerDist:F0}");
#endif
                // "SPEED ZONE!" popup
                if (ScorePopup.Instance != null && _tc != null)
                    ScorePopup.Instance.ShowMilestone(
                        _tc.transform.position + Vector3.up * 2.5f,
                        "SPEED ZONE!\n" + CorridorSubtitles[i % CorridorSubtitles.Length]);

                // Cheer overlay
                if (CheerOverlay.Instance != null)
                    CheerOverlay.Instance.ShowCheer("ZOOM!", new Color(0.1f, 0.9f, 1f), false);

                // Camera: wide open FOV punch
                if (PipeCamera.Instance != null)
                    PipeCamera.Instance.PunchFOV(4f);

                // Green flash for speed zone entry
                if (ScreenEffects.Instance != null)
                    ScreenEffects.Instance.TriggerPowerUpFlash();

                // Audio cue
                if (ProceduralAudio.Instance != null)
                    ProceduralAudio.Instance.PlayCelebration();

                HapticManager.MediumTap();
            }
        }
    }

    void SpawnObstacle(float dist, Vector3 center, Vector3 forward, Vector3 right, Vector3 up)
    {
        // Pick placement zone - obstacles can appear anywhere around the pipe
        // 70% floor, 15% walls, 15% ceiling - forces player to navigate
        float zonePick = Random.value;
        float angleDeg;

        if (zonePick < 0.70f)
        {
            // Floor (where player usually is) - spread across bottom
            angleDeg = 270f + Random.Range(-45f, 45f);
        }
        else if (zonePick < 0.85f)
        {
            // Walls - left or right side
            if (Random.value < 0.5f)
                angleDeg = Random.Range(160f, 220f); // left wall
            else
                angleDeg = Random.Range(340f, 400f); // right wall (wraps)
        }
        else
        {
            // Ceiling - top of pipe
            angleDeg = Random.Range(60f, 120f);
        }

        // Zone-themed obstacle selection
        GameObject prefab = GetZoneObstacle(dist);
        if (prefab == null) return;

        // Mines always float in the water at the pipe bottom
        bool isMine = prefab.GetComponent<SewerMineBehavior>() != null;
        if (isMine)
        {
            angleDeg = 270f + Random.Range(-20f, 20f); // water level at bottom
        }

        float angle = angleDeg * Mathf.Deg2Rad;
        // Use smaller radius when in a fork branch
        PipeFork fork = _tc != null ? _tc.CurrentFork : null;
        int branch = _tc != null ? _tc.ForkBranch : -1;
        float effectivePipeRadius = pipeRadius;
        if (fork != null && branch >= 0)
            effectivePipeRadius = Mathf.Lerp(pipeRadius, fork.branchPipeRadius,
                fork.GetBranchBlend(dist));

        // Mines float near the water surface (closer to pipe wall = lower in water)
        float spawnRadius = isMine
            ? effectivePipeRadius * 0.82f
            : effectivePipeRadius * Random.Range(0.55f, 0.75f);

        Vector3 pos = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * spawnRadius;

        // Orient obstacle: face forward along pipe, "up" points inward toward pipe center
        // This makes obstacles sit naturally on any surface (floor, walls, ceiling)
        // For mines: orient upright so fuse points up (toward pipe center)
        Vector3 inward = (center - pos).normalized;
        Quaternion rot;
        if (isMine)
            rot = Quaternion.LookRotation(forward, inward);
        else
            rot = Quaternion.LookRotation(forward, inward);
        GameObject obj = Instantiate(prefab, pos, rot, transform);
        _spawnedObjects.Add(obj);
#if UNITY_EDITOR
        Debug.Log($"[SPAWN] {prefab.name} at dist={dist:F0} angle={angleDeg:F0}° radius={spawnRadius:F1} fork={fork != null}");
#endif

        // Pass actual spawn distance to rat for accurate orbit
        SewerRatBehavior rat = obj.GetComponent<SewerRatBehavior>();
        if (rat != null)
            rat.spawnDistToCenter = spawnRadius;
    }

    // Pattern types for coin trails - spiral around the pipe, not just bottom!
    enum CoinPattern { Straight, Spiral, HalfLoop, FullLoop, SCurve, WallRun, CeilingArc }

    void SpawnCoinTrailAlongPath(float startDist)
    {
        if (coinPrefab == null || _pipeGen == null) return;

        // Pick pattern - cycle through them with some randomness
        // Early game (< 60m) only straight/wall runs, later game gets loops and spirals
        CoinPattern pattern;
        if (startDist < 60f)
        {
            pattern = Random.value < 0.6f ? CoinPattern.Straight : CoinPattern.WallRun;
        }
        else
        {
            // Weighted random from all patterns, favoring spirals and loops
            float roll = Random.value;
            if (roll < 0.12f)
                pattern = CoinPattern.Straight;
            else if (roll < 0.30f)
                pattern = CoinPattern.Spiral;
            else if (roll < 0.45f)
                pattern = CoinPattern.HalfLoop;
            else if (roll < 0.60f)
                pattern = CoinPattern.FullLoop;
            else if (roll < 0.75f)
                pattern = CoinPattern.SCurve;
            else if (roll < 0.88f)
                pattern = CoinPattern.WallRun;
            else
                pattern = CoinPattern.CeilingArc;
        }

        switch (pattern)
        {
            case CoinPattern.Straight:
                SpawnCoinsStraight(startDist);
                break;
            case CoinPattern.Spiral:
                SpawnCoinsSpiral(startDist);
                break;
            case CoinPattern.HalfLoop:
                SpawnCoinsHalfLoop(startDist);
                break;
            case CoinPattern.FullLoop:
                SpawnCoinsFullLoop(startDist);
                break;
            case CoinPattern.SCurve:
                SpawnCoinsSCurve(startDist);
                break;
            case CoinPattern.WallRun:
                SpawnCoinsWallRun(startDist);
                break;
            case CoinPattern.CeilingArc:
                SpawnCoinsCeilingArc(startDist);
                break;
        }
    }

    void SpawnCoinAtAngle(float dist, float angleDeg)
    {
        Vector3 center, forward, right, up;

        // Branch-aware coin placement
        PipeFork fork = _tc != null ? _tc.CurrentFork : null;
        int branch = _tc != null ? _tc.ForkBranch : -1;
        float effectiveRadius = pipeRadius;

        if (fork != null && branch >= 0)
        {
            Vector3 mainC, mainF, mainR, mainU;
            _pipeGen.GetPathFrame(dist, out mainC, out mainF, out mainR, out mainU);

            Vector3 bC, bF, bR, bU;
            if (fork.GetBranchFrame(branch, dist, out bC, out bF, out bR, out bU))
            {
                float blend = fork.GetBranchBlend(dist);
                center = Vector3.Lerp(mainC, bC, blend);
                forward = Vector3.Slerp(mainF, bF, blend).normalized;
                right = Vector3.Slerp(mainR, bR, blend).normalized;
                up = Vector3.Slerp(mainU, bU, blend).normalized;
                effectiveRadius = Mathf.Lerp(pipeRadius, fork.branchPipeRadius, blend);
            }
            else
            {
                _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);
            }
        }
        else
        {
            _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);
        }

        float rad = angleDeg * Mathf.Deg2Rad;
        float spawnRadius = effectiveRadius * 0.92f;
        Vector3 pos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * spawnRadius;

        // Orient coin: inward is "up" for the coin so it sits on the pipe surface
        Vector3 inward = (center - pos).normalized;
        Quaternion rot = Quaternion.LookRotation(forward, inward);
        GameObject coin = Instantiate(coinPrefab, pos, rot, transform);
        _spawnedObjects.Add(coin);
    }

    // Original straight line trail at a random angle (not just bottom)
    void SpawnCoinsStraight(float startDist)
    {
        // Can be at any angle now, not just bottom
        float angleDeg = Random.value < 0.5f
            ? 270f + Random.Range(-40f, 40f)  // bottom half
            : Random.Range(0f, 360f);          // anywhere

        int count = Random.Range(4, 8);
        for (int i = 0; i < count; i++)
            SpawnCoinAtAngle(startDist + i * 3f, angleDeg);
    }

    // Spiral: coins go around the pipe like a corkscrew
    void SpawnCoinsSpiral(float startDist)
    {
        float startAngle = Random.Range(0f, 360f);
        // How much of the pipe circumference to cover (180-540 degrees)
        float totalSweep = Random.Range(180f, 540f);
        // Randomize direction (clockwise vs counterclockwise)
        float dir = Random.value < 0.5f ? 1f : -1f;

        int count = Random.Range(8, 14);
        float spacing = 4f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = startAngle + totalSweep * t * dir;
            SpawnCoinAtAngle(startDist + i * spacing, angle);
        }
    }

    // Half loop: coins arc from bottom, up one wall, to the top (or reverse)
    void SpawnCoinsHalfLoop(float startDist)
    {
        // Start at bottom (270°), sweep 180° to top (90°)
        // Or start on a wall and sweep half around
        float startAngle = Random.value < 0.5f ? 270f : 90f;
        float endAngle = startAngle + (Random.value < 0.5f ? 180f : -180f);

        int count = Random.Range(6, 10);
        float spacing = 3.5f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            SpawnCoinAtAngle(startDist + i * spacing, angle);
        }
    }

    // Full loop: coins go all the way around the pipe (360°)
    void SpawnCoinsFullLoop(float startDist)
    {
        float startAngle = Random.Range(0f, 360f);
        float dir = Random.value < 0.5f ? 1f : -1f;

        int count = Random.Range(10, 16);
        float spacing = 3.5f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = startAngle + 360f * t * dir;
            SpawnCoinAtAngle(startDist + i * spacing, angle);
        }
    }

    // S-curve: coins weave side-to-side across the pipe
    void SpawnCoinsSCurve(float startDist)
    {
        float centerAngle = Random.Range(0f, 360f);
        float amplitude = Random.Range(60f, 120f); // degrees of swing

        int count = Random.Range(8, 12);
        float spacing = 3.5f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float wave = Mathf.Sin(t * Mathf.PI * 2f) * amplitude;
            SpawnCoinAtAngle(startDist + i * spacing, centerAngle + wave);
        }
    }

    // Wall run: straight line up the left or right wall
    void SpawnCoinsWallRun(float startDist)
    {
        // Left wall (~180°) or right wall (~0°/360°)
        float angleDeg = Random.value < 0.5f
            ? Random.Range(160f, 200f)
            : Random.Range(340f, 380f);

        int count = Random.Range(5, 8);
        for (int i = 0; i < count; i++)
            SpawnCoinAtAngle(startDist + i * 4f, angleDeg);
    }

    // Ceiling arc: coins across the top of the pipe
    void SpawnCoinsCeilingArc(float startDist)
    {
        // Arc from one upper side to the other across the ceiling
        float startAngle = Random.Range(130f, 160f);
        float endAngle = Random.Range(20f, 50f);

        int count = Random.Range(6, 10);
        float spacing = 3.5f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            SpawnCoinAtAngle(startDist + i * spacing, angle);
        }
    }

    void SpawnBigAirRamp(float dist)
    {
        if (_pipeGen == null) return;
        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Place ramp on the pipe floor (bottom)
        Vector3 pos = center - up * (pipeRadius * 0.65f);
        Vector3 inward = (center - pos).normalized;
        Quaternion rot = Quaternion.LookRotation(forward, inward);
        GameObject obj = Instantiate(bigAirRampPrefab, pos, rot, transform);
        _spawnedObjects.Add(obj);
    }

    void SpawnDropZone(float dist)
    {
        if (_pipeGen == null) return;
        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Place drop trigger at pipe center
        Quaternion rot = Quaternion.LookRotation(forward, up);
        GameObject obj = Instantiate(dropZonePrefab, center, rot, transform);
        _spawnedObjects.Add(obj);
    }

    void SpawnGrate(float dist)
    {
        if (_pipeGen == null) return;
        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Pick which half to block (left, right, top, bottom)
        GrateBehavior.BlockSide side = (GrateBehavior.BlockSide)Random.Range(0, 4);
        Vector3 offset = Vector3.zero;
        float halfRadius = pipeRadius * 0.5f;

        switch (side)
        {
            case GrateBehavior.BlockSide.Left:
                offset = -right * halfRadius;
                break;
            case GrateBehavior.BlockSide.Right:
                offset = right * halfRadius;
                break;
            case GrateBehavior.BlockSide.Top:
                offset = up * halfRadius;
                break;
            case GrateBehavior.BlockSide.Bottom:
                offset = -up * halfRadius;
                break;
        }

        Vector3 pos = center + offset;
        Quaternion rot = Quaternion.LookRotation(forward, up);
        GameObject obj = Instantiate(gratePrefab, pos, rot, transform);

        GrateBehavior grate = obj.GetComponent<GrateBehavior>();
        if (grate != null)
            grate.blockSide = side;

        _spawnedObjects.Add(obj);
    }
}
