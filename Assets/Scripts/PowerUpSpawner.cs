using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns speed boost pads and jump ramps along the pipe path.
/// Power-ups can appear on floor, walls, and ceiling - encouraging exploration.
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

    [Header("Player Reference")]
    public Transform player;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private float _nextSpawnDist = 40f;
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private int _typeIndex = 0;

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
        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Power-ups can appear ANYWHERE on the pipe - floor, walls, ceiling
        // This rewards players who explore the full circumference
        float angleDeg;
        float zonePick = Random.value;
        if (zonePick < 0.4f)
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

        // Alternate between speed boost and jump ramp (2:1 ratio)
        _typeIndex = (_typeIndex + 1) % 3;
        GameObject prefab = _typeIndex < 2 ? speedBoostPrefab : jumpRampPrefab;
        if (prefab == null) return;

        // Orient: face forward, "up" points inward so it sits on any surface
        Vector3 inward = (center - pos).normalized;
        Quaternion rot = Quaternion.LookRotation(forward, inward);
        GameObject obj = Instantiate(prefab, pos, rot, transform);
        _spawnedObjects.Add(obj);

        // Spawn a bonus Fartcoin after every jump ramp - only reachable by jumping
        if (prefab == jumpRampPrefab && bonusCoinPrefab != null)
        {
            // Place the coin at the apex of the jump arc: ~6m ahead, ~3m above the surface
            float bonusDist = dist + 6f;
            Vector3 bCenter, bFwd, bRight, bUp;
            _pipeGen.GetPathFrame(bonusDist, out bCenter, out bFwd, out bRight, out bUp);

            // Position coin near pipe center (high up from surface = only reachable mid-air)
            Vector3 coinPos = bCenter - bUp * (pipeRadius * 0.1f);
            Quaternion coinRot = Quaternion.LookRotation(bFwd, bUp);
            GameObject coin = Instantiate(bonusCoinPrefab, coinPos, coinRot, transform);
            _spawnedObjects.Add(coin);
        }
    }
}
