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
    private float _nextSpawnDist = 40f;
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private int _typeIndex = 0;
    private float _lastSpecialDist = -200f;

    void Start()
    {
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (player != null)
            _tc = player.GetComponent<TurdController>();
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
                _nextSpawnDist += Random.Range(12f, 15f);
            else
                _nextSpawnDist += Random.Range(minSpacing, maxSpacing);
        }

        // Cleanup behind
        for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (_spawnedObjects[i] == null)
            {
                _spawnedObjects.RemoveAt(i);
                continue;
            }

            Vector3 toObj = _spawnedObjects[i].transform.position - player.position;
            if (toObj.magnitude > 60f && Vector3.Dot(toObj, player.forward) < 0)
            {
                Destroy(_spawnedObjects[i]);
                _spawnedObjects.RemoveAt(i);
            }
        }
    }

    void SpawnAtDistance(float dist)
    {
        // Skip fork zones: power-ups on the main path center would float between branches
        if (_pipeGen.GetForkAtDistance(dist) != null) return;

        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Power-ups can appear ANYWHERE on the pipe - floor, walls, ceiling
        // In speed corridors: mostly floor for easy chain collection
        // This rewards players who explore the full circumference
        float angleDeg;
        bool inCorridor = ObstacleSpawner.IsSpeedCorridor(dist);
        float zonePick = Random.value;
        if (inCorridor)
        {
            // Speed corridors: 80% floor, 10% left, 10% right (easy to chain)
            if (zonePick < 0.8f)
                angleDeg = 270f + Random.Range(-20f, 20f);
            else if (zonePick < 0.9f)
                angleDeg = Random.Range(200f, 240f);
            else
                angleDeg = Random.Range(300f, 340f);
        }
        else if (zonePick < 0.4f)
        {
            // Floor (easiest to hit)
            angleDeg = 270f + Random.Range(-25f, 25f);
        }
        else if (zonePick < 0.65f)
        {
            // Left wall
            angleDeg = Random.Range(170f, 220f);
        }
        else if (zonePick < 0.9f)
        {
            // Right wall
            angleDeg = Random.Range(320f, 370f);
        }
        else
        {
            // Ceiling (hardest to reach, high reward)
            angleDeg = Random.Range(60f, 120f);
        }

        float angle = angleDeg * Mathf.Deg2Rad;
        float spawnRadius = pipeRadius * 0.82f;
        Vector3 pos = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * spawnRadius;

        // Check for special power-up spawn (Shield, Magnet, Slow-Mo)
        bool spawnedSpecial = false;
        if (!inCorridor && dist >= specialMinDistance &&
            dist - _lastSpecialDist >= specialMinSpacing &&
            Random.value < specialChance)
        {
            GameObject specialPrefab = PickSpecialPrefab();
            if (specialPrefab != null)
            {
                // Specials always on floor for visibility
                float floorAngle = (270f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                Vector3 specialPos = center + (right * Mathf.Cos(floorAngle) + up * Mathf.Sin(floorAngle)) * spawnRadius;
                Vector3 inward = (center - specialPos).normalized;
                Quaternion rot = Quaternion.LookRotation(forward, inward);
                GameObject obj = Instantiate(specialPrefab, specialPos, rot, transform);
                _spawnedObjects.Add(obj);
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
            _spawnedObjects.Add(obj);

            // Spawn a bonus Fartcoin after every jump ramp - only reachable by jumping
            if (prefab == jumpRampPrefab && bonusCoinPrefab != null)
            {
                float bonusDist = dist + 6f;
                Vector3 bCenter, bFwd, bRight, bUp;
                _pipeGen.GetPathFrame(bonusDist, out bCenter, out bFwd, out bRight, out bUp);

                Vector3 coinPos = bCenter - bUp * (pipeRadius * 0.1f);
                Quaternion coinRot = Quaternion.LookRotation(bFwd, bUp);
                GameObject coin = Instantiate(bonusCoinPrefab, coinPos, coinRot, transform);
                _spawnedObjects.Add(coin);
            }
        }
    }

    GameObject PickSpecialPrefab()
    {
        // Equal weight: 1/3 each
        float pick = Random.value;
        if (pick < 0.33f && shieldPrefab != null) return shieldPrefab;
        if (pick < 0.66f && magnetPrefab != null) return magnetPrefab;
        if (slowMoPrefab != null) return slowMoPrefab;
        // Fallback if some prefabs are null
        if (shieldPrefab != null) return shieldPrefab;
        if (magnetPrefab != null) return magnetPrefab;
        return null;
    }
}
