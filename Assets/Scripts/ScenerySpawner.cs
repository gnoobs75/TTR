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
    public float minSpacing = 5f;
    public float maxSpacing = 11f;
    public float pipeRadius = 3.5f;

    [Header("Scenery Prefabs")]
    public GameObject[] sceneryPrefabs;

    [Header("Pipe Character Prefabs (gross stuff)")]
    public GameObject[] grossPrefabs;

    [Header("Sign/Ad Prefabs (spawn more often for readability)")]
    public GameObject[] signPrefabs;

    [Header("Player Reference")]
    public Transform player;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private float _nextSpawnDist = 10f;
    private float _nextGrossDist = 5f;
    private float _nextSignDist = 15f;
    private List<SpawnedEntry> _spawnedEntries = new List<SpawnedEntry>();
    private float _cleanupDistance = 50f;

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
    }

    void Update()
    {
        if (player == null) return;

        float playerDist = _tc != null ? _tc.DistanceTraveled : 0f;

        // Spawn regular scenery (denser for immersion)
        while (_nextSpawnDist < playerDist + spawnDistance)
        {
            SpawnScenery(_nextSpawnDist);
            _nextSpawnDist += Random.Range(minSpacing, maxSpacing);
        }

        // Spawn pipe decor (stains, drips, cracks)
        while (_nextGrossDist < playerDist + spawnDistance)
        {
            SpawnGrossDecor(_nextGrossDist);
            _nextGrossDist += Random.Range(6f, 14f);
        }

        // Spawn signs/ads/graffiti separately at their own rate (walls only, readable)
        while (_nextSignDist < playerDist + spawnDistance)
        {
            SpawnSign(_nextSignDist);
            _nextSignDist += Random.Range(20f, 40f); // readable spacing
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
            if (distBehind > _cleanupDistance)
            {
                Destroy(_spawnedEntries[i].obj);
                _spawnedEntries.RemoveAt(i);
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
        _spawnedEntries.Add(new SpawnedEntry { obj = obj, spawnDist = dist });
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
        float spawnRadius = pipeRadius * Random.Range(0.65f, 0.82f);

        Vector3 pos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * spawnRadius;
        Vector3 inward = (center - pos).normalized;
        Quaternion rot = Quaternion.LookRotation(forward, inward);

        GameObject prefab = grossPrefabs[Random.Range(0, grossPrefabs.Length)];
        GameObject obj = Instantiate(prefab, pos, rot, transform);
        _spawnedEntries.Add(new SpawnedEntry { obj = obj, spawnDist = dist });
    }

    void SpawnSign(float dist)
    {
        if (signPrefabs == null || signPrefabs.Length == 0) return;
        if (_pipeGen == null) return;

        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Spray paint goes on walls and occasionally ceiling
        float angle;
        float r = Random.value;
        if (r < 0.4f)
            angle = Random.Range(160f, 210f); // right wall
        else if (r < 0.8f)
            angle = Random.Range(330f, 390f); // left wall
        else
            angle = Random.Range(50f, 130f);  // ceiling

        float rad = angle * Mathf.Deg2Rad;
        float spawnRadius = pipeRadius * 0.93f; // flush with pipe surface

        Vector3 pos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * spawnRadius;
        Vector3 inward = (center - pos).normalized;
        // Signs face inward so players inside the pipe can read them
        // Use -forward as up so text reads left-to-right consistently on all walls
        Quaternion rot = Quaternion.LookRotation(-inward, -forward);

        GameObject prefab = signPrefabs[Random.Range(0, signPrefabs.Length)];
        GameObject obj = Instantiate(prefab, pos, rot, transform);

        // Inject AI-generated graffiti into graffiti sign prefabs
        if (prefab.name.StartsWith("GraffitiSign") && AITextManager.Instance != null)
        {
            string aiGraffiti = AITextManager.Instance.GetGraffiti();
            if (!string.IsNullOrEmpty(aiGraffiti))
            {
                var tm = obj.GetComponentInChildren<TextMesh>();
                if (tm != null)
                    tm.text = aiGraffiti;

                var tmp = obj.GetComponentInChildren<TMPro.TextMeshPro>();
                if (tmp != null)
                    tmp.text = aiGraffiti;
            }
        }

        _spawnedEntries.Add(new SpawnedEntry { obj = obj, spawnDist = dist });
    }
}
