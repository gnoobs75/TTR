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
    public float minSpacing = 16f;
    public float maxSpacing = 30f;
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
    public float obstacleChance = 0.4f;
    public float difficultyRamp = 0.01f;

    [Header("Player Reference")]
    public Transform player;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private float _nextSpawnDist = 25f;
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private float _cleanupDistance = 50f;
    private int _obstacleIndex = 0;

    // Special event tracking
    private float _nextBigAirDist = 300f;
    private float _nextDropDist = 200f;
    private float _nextGrateDist = 80f;

    void Start()
    {
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (player != null)
            _tc = player.GetComponent<TurdController>();
    }

    void Update()
    {
        if (player == null) return;

        float playerDist = _tc != null ? _tc.DistanceTraveled : 0f;

        // Spawn ahead
        while (_nextSpawnDist < playerDist + spawnDistance)
        {
            SpawnAtDistance(_nextSpawnDist);
            _nextSpawnDist += Random.Range(minSpacing, maxSpacing);
        }

        // Special events: big air ramps (every 300-500m)
        if (bigAirRampPrefab != null && _nextBigAirDist < playerDist + spawnDistance)
        {
            SpawnBigAirRamp(_nextBigAirDist);
            _nextBigAirDist += Random.Range(300f, 500f);
        }

        // Special events: vertical drops (every 400-600m, starts after 200m)
        if (dropZonePrefab != null && _nextDropDist < playerDist + spawnDistance)
        {
            SpawnDropZone(_nextDropDist);
            _nextDropDist += Random.Range(400f, 600f);
        }

        // Grate obstacles (every 60-120m, starts after 80m)
        if (gratePrefab != null && _nextGrateDist < playerDist + spawnDistance)
        {
            SpawnGrate(_nextGrateDist);
            _nextGrateDist += Random.Range(60f, 120f);
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
            if (toObj.magnitude > _cleanupDistance && Vector3.Dot(toObj, player.forward) < 0)
            {
                Destroy(_spawnedObjects[i]);
                _spawnedObjects.RemoveAt(i);
            }
        }
    }

    void SpawnAtDistance(float dist)
    {
        float currentChance = Mathf.Min(obstacleChance + dist * difficultyRamp * 0.001f, 0.7f);

        if (_pipeGen != null)
        {
            Vector3 center, forward, right, up;
            _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

            if (Random.value < currentChance && obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            {
                SpawnObstacle(dist, center, forward, right, up);
            }
            else if (coinPrefab != null)
            {
                SpawnCoinTrailAlongPath(dist);
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

        // Cycle through obstacle types to guarantee variety
        _obstacleIndex = (_obstacleIndex + 1) % obstaclePrefabs.Length;
        GameObject prefab = obstaclePrefabs[_obstacleIndex];

        // Mines always float in the water at the pipe bottom
        bool isMine = prefab.GetComponent<SewerMineBehavior>() != null;
        if (isMine)
        {
            angleDeg = 270f + Random.Range(-20f, 20f); // water level at bottom
        }

        float angle = angleDeg * Mathf.Deg2Rad;
        // Mines float near the water surface (closer to pipe wall = lower in water)
        float spawnRadius = isMine
            ? pipeRadius * 0.82f  // at water surface level
            : pipeRadius * Random.Range(0.55f, 0.75f);

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

        // Pass actual spawn distance to rat for accurate orbit
        SewerRatBehavior rat = obj.GetComponent<SewerRatBehavior>();
        if (rat != null)
            rat.spawnDistToCenter = spawnRadius;
    }

    void SpawnCoinTrailAlongPath(float startDist)
    {
        if (coinPrefab == null || _pipeGen == null) return;

        // Coins form a clear line along the player's path
        // Placed at player running height, near the bottom of the pipe
        // Slight left/right offset for variety
        float sideOffset = Random.Range(-1.2f, 1.2f);

        int count = Random.Range(4, 8);
        for (int i = 0; i < count; i++)
        {
            float coinDist = startDist + i * 3f;
            Vector3 center, forward, right, up;
            _pipeGen.GetPathFrame(coinDist, out center, out forward, out right, out up);

            Vector3 pos = center - up * (pipeRadius * 0.72f) + right * sideOffset;
            Quaternion rot = Quaternion.LookRotation(forward, up);
            GameObject coin = Instantiate(coinPrefab, pos, rot, transform);
            _spawnedObjects.Add(coin);
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
