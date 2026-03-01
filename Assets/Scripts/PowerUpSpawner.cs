using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns speed boost pads, jump ramps, and special power-ups along the pipe path.
/// Power-ups can appear on floor, walls, and ceiling - encouraging exploration.
/// Special power-ups (Shield, Magnet, Slow-Mo) are rarer and appear after 100m.
/// </summary>
public class PowerUpSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float spawnDistance = 100f;
    public float minSpacing = 30f;
    public float maxSpacing = 60f;
    public float pipeRadius = 3.5f;

    [Header("Prefabs")]
    public GameObject speedBoostPrefab;
    public GameObject jumpRampPrefab;
    public GameObject bonusCoinPrefab;
    public GameObject shieldPrefab;
    public GameObject magnetPrefab;
    public GameObject slowMoPrefab;

    [Header("Special Power-Up Settings")]
    public float specialMinDistance = 100f;
    public float specialChance = 0.15f; // 15% chance per spawn point (after min distance)
    public float specialMinSpacing = 80f; // minimum distance between specials

    [Header("Player Reference")]
    public Transform player;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private System.Random _puRng;
    private float _nextSpawnDist = 40f;
    private List<SpawnedEntry> _spawnedEntries = new List<SpawnedEntry>();
    private int _typeIndex = 0;
    private float _lastSpecialDist = -200f;

    private struct SpawnedEntry
    {
        public GameObject obj;
        public float spawnDist;
    }

    void Start()
    {
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (player != null)
            _tc = player.GetComponent<TurdController>();
        if (SeedManager.Instance != null)
            _puRng = SeedManager.Instance.PowerUpRNG;
    }

    void Update()
    {
        if (player == null || _pipeGen == null) return;

        float playerDist = _tc != null ? _tc.DistanceTraveled : 0f;

        while (_nextSpawnDist < playerDist + spawnDistance)
        {
            SpawnAtDistance(_nextSpawnDist);

            // Dense boost spacing in speed corridors (12-15m vs normal 30-60m)
            if (ObstacleSpawner.IsSpeedCorridor(_nextSpawnDist))
                _nextSpawnDist += SeedManager.Range(_puRng, 12f, 15f);
            else
                _nextSpawnDist += SeedManager.Range(_puRng, minSpacing, maxSpacing);
        }

        // Cleanup behind using pipe distance (world-space direction fails in curves)
        for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
        {
            if (_spawnedEntries[i].obj == null)
            {
                _spawnedEntries.RemoveAt(i);
                continue;
            }

            float distBehind = playerDist - _spawnedEntries[i].spawnDist;
            if (distBehind > 60f)
            {
                Destroy(_spawnedEntries[i].obj);
                _spawnedEntries.RemoveAt(i);
            }
        }
    }

    void SpawnAtDistance(float dist)
    {
        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Lane zone: stretch spawn positions + bias speed boosts to risky (right) side
        float laneWidth = _pipeGen.GetLaneWidthAt(dist);
        PipeLaneZone laneZone = _pipeGen.GetLaneZoneAtDistance(dist);
        bool inLaneZone = laneZone != null;

        // Power-ups can appear ANYWHERE on the pipe - floor, walls, ceiling
        // In speed corridors: mostly floor for easy chain collection
        // In lane zones: speed boosts biased to right (risky) side
        float angleDeg;
        bool inCorridor = ObstacleSpawner.IsSpeedCorridor(dist);
        float zonePick = SeedManager.Value(_puRng);

        if (inLaneZone)
        {
            // Lane zone: 60% right side (risky = speed boosts), 25% floor center, 15% left
            if (zonePick < 0.60f)
                angleDeg = SeedManager.Range(_puRng, 300f, 370f); // right side (risky)
            else if (zonePick < 0.85f)
                angleDeg = 270f + SeedManager.Range(_puRng, -20f, 20f); // floor center
            else
                angleDeg = SeedManager.Range(_puRng, 190f, 240f); // left side (safe)
        }
        else if (inCorridor)
        {
            // Speed corridors: 80% floor, 10% left, 10% right (easy to chain)
            if (zonePick < 0.8f)
                angleDeg = 270f + SeedManager.Range(_puRng, -20f, 20f);
            else if (zonePick < 0.9f)
                angleDeg = SeedManager.Range(_puRng, 200f, 240f);
            else
                angleDeg = SeedManager.Range(_puRng, 300f, 340f);
        }
        else if (zonePick < 0.4f)
        {
            // Floor (easiest to hit)
            angleDeg = 270f + SeedManager.Range(_puRng, -25f, 25f);
        }
        else if (zonePick < 0.65f)
        {
            // Left wall
            angleDeg = SeedManager.Range(_puRng, 170f, 220f);
        }
        else if (zonePick < 0.9f)
        {
            // Right wall
            angleDeg = SeedManager.Range(_puRng, 320f, 370f);
        }
        else
        {
            // Ceiling (hardest to reach, high reward)
            angleDeg = SeedManager.Range(_puRng, 60f, 120f);
        }

        float angle = angleDeg * Mathf.Deg2Rad;
        float spawnRadius = pipeRadius * 0.82f;
        // Apply lane zone horizontal stretch
        Vector3 pos = center + (right * Mathf.Cos(angle) * laneWidth + up * Mathf.Sin(angle)) * spawnRadius;

        // Check for special power-up spawn (Shield, Magnet, Slow-Mo)
        bool spawnedSpecial = false;
        if (!inCorridor && dist >= specialMinDistance &&
            dist - _lastSpecialDist >= specialMinSpacing &&
            SeedManager.Value(_puRng) < specialChance)
        {
            GameObject specialPrefab = PickSpecialPrefab();
            if (specialPrefab != null)
            {
                // Specials always on floor for visibility
                float floorAngle = (270f + SeedManager.Range(_puRng, -15f, 15f)) * Mathf.Deg2Rad;
                Vector3 specialPos = center + (right * Mathf.Cos(floorAngle) + up * Mathf.Sin(floorAngle)) * spawnRadius;
                Vector3 inward = (center - specialPos).normalized;
                Quaternion rot = Quaternion.LookRotation(forward, inward);
                GameObject obj = Instantiate(specialPrefab, specialPos, rot, transform);
                _spawnedEntries.Add(new SpawnedEntry { obj = obj, spawnDist = dist });
                _lastSpecialDist = dist;
                spawnedSpecial = true;
#if UNITY_EDITOR
                Debug.Log($"[SPAWN] Special power-up {specialPrefab.name} at dist={dist:F0}");
#endif
            }
        }

        // Normal spawn: speed boost or jump ramp (skip if we just placed a special)
        if (!spawnedSpecial)
        {
            GameObject prefab;
            if (inCorridor)
            {
                prefab = speedBoostPrefab;
            }
            else
            {
                _typeIndex = (_typeIndex + 1) % 3;
                prefab = _typeIndex < 2 ? speedBoostPrefab : jumpRampPrefab;
            }
            if (prefab == null) return;

            Vector3 inward = (center - pos).normalized;
            Quaternion rot = Quaternion.LookRotation(forward, inward);
            GameObject obj = Instantiate(prefab, pos, rot, transform);
            _spawnedEntries.Add(new SpawnedEntry { obj = obj, spawnDist = dist });

            // Spawn a bonus Fartcoin after every jump ramp - only reachable by jumping
            if (prefab == jumpRampPrefab && bonusCoinPrefab != null)
            {
                float bonusDist = dist + 6f;
                Vector3 bCenter, bFwd, bRight, bUp;
                _pipeGen.GetPathFrame(bonusDist, out bCenter, out bFwd, out bRight, out bUp);

                Vector3 coinPos = bCenter - bUp * (pipeRadius * 0.1f);
                Quaternion coinRot = Quaternion.LookRotation(bFwd, bUp);
                GameObject coin = Instantiate(bonusCoinPrefab, coinPos, coinRot, transform);
                _spawnedEntries.Add(new SpawnedEntry { obj = coin, spawnDist = bonusDist });
            }
        }
    }

    GameObject PickSpecialPrefab()
    {
        // Equal weight: 1/3 each
        float pick = SeedManager.Value(_puRng);
        if (pick < 0.33f && shieldPrefab != null) return shieldPrefab;
        if (pick < 0.66f && magnetPrefab != null) return magnetPrefab;
        if (slowMoPrefab != null) return slowMoPrefab;
        // Fallback if some prefabs are null
        if (shieldPrefab != null) return shieldPrefab;
        if (magnetPrefab != null) return magnetPrefab;
        return null;
    }
}
