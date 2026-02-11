using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns decorative scenery props on pipe walls, ceiling, and floor edges.
/// Includes gross sewer character: slime drips, worms, graffiti, mold, bubbles.
/// Props are non-interactive visual dressing positioned to make the sewer feel alive.
/// </summary>
public class ScenerySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float spawnDistance = 100f;
    public float minSpacing = 6f;
    public float maxSpacing = 14f;
    public float pipeRadius = 3.5f;

    [Header("Scenery Prefabs")]
    public GameObject[] sceneryPrefabs;

    [Header("Pipe Character Prefabs (gross stuff)")]
    public GameObject[] grossPrefabs;

    [Header("Player Reference")]
    public Transform player;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private float _nextSpawnDist = 10f;
    private float _nextGrossDist = 5f;
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private float _cleanupDistance = 50f;

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

        // Spawn regular scenery
        while (_nextSpawnDist < playerDist + spawnDistance)
        {
            SpawnScenery(_nextSpawnDist);
            _nextSpawnDist += Random.Range(minSpacing, maxSpacing);
        }

        // Spawn subtle pipe decor (sparse - just occasional stains/drips)
        while (_nextGrossDist < playerDist + spawnDistance)
        {
            SpawnGrossDecor(_nextGrossDist);
            _nextGrossDist += Random.Range(8f, 18f); // sparse so it doesn't clutter
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

    void SpawnScenery(float dist)
    {
        if (sceneryPrefabs == null || sceneryPrefabs.Length == 0) return;
        if (_pipeGen == null) return;

        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Scenery on upper walls and ceiling
        float angle;
        int zone = Random.Range(0, 3);
        switch (zone)
        {
            case 0: angle = Random.Range(330f, 410f); break; // Left wall
            case 1: angle = Random.Range(130f, 210f); break; // Right wall
            default: angle = Random.Range(50f, 130f); break; // Ceiling
        }

        float rad = angle * Mathf.Deg2Rad;
        float spawnRadius = pipeRadius * Random.Range(0.65f, 0.82f);
        Vector3 pos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * spawnRadius;

        Vector3 inward = (center - pos).normalized;
        Quaternion rot = Quaternion.LookRotation(forward, inward);

        GameObject prefab = sceneryPrefabs[Random.Range(0, sceneryPrefabs.Length)];
        GameObject obj = Instantiate(prefab, pos, rot, transform);
        _spawnedObjects.Add(obj);
    }

    void SpawnGrossDecor(float dist)
    {
        if (grossPrefabs == null || grossPrefabs.Length == 0) return;
        if (_pipeGen == null) return;

        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Gross decor goes EVERYWHERE - full 360°
        float angle = Random.Range(0f, 360f);
        float rad = angle * Mathf.Deg2Rad;
        float spawnRadius = pipeRadius * Random.Range(0.65f, 0.82f); // inside pipe, not clipping through wall

        Vector3 pos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * spawnRadius;
        Vector3 inward = (center - pos).normalized;
        Quaternion rot = Quaternion.LookRotation(forward, inward);

        GameObject prefab = grossPrefabs[Random.Range(0, grossPrefabs.Length)];
        GameObject obj = Instantiate(prefab, pos, rot, transform);
        _spawnedObjects.Add(obj);
    }
}
