using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns sewer squirt creatures in the water at the bottom of the pipe.
/// They pop up, look around, and duck back down when the player approaches.
/// </summary>
public class WaterCreatureSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float spawnDistance = 80f;
    public float minSpacing = 12f;
    public float maxSpacing = 25f;
    public float pipeRadius = 3.5f;

    [Header("Prefab")]
    public GameObject squirtPrefab;

    [Header("Player")]
    public Transform player;

    private PipeGenerator _pipeGen;
    private TurdController _tc;
    private float _nextSpawnDist = 20f;
    private List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (player != null) _tc = player.GetComponent<TurdController>();
    }

    void Update()
    {
        if (player == null || _pipeGen == null || squirtPrefab == null) return;
        float playerDist = _tc != null ? _tc.DistanceTraveled : 0f;

        // Spawn ahead
        while (_nextSpawnDist < playerDist + spawnDistance)
        {
            SpawnSquirt(_nextSpawnDist);
            _nextSpawnDist += Random.Range(minSpacing, maxSpacing);
        }

        // Cleanup behind
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] == null) { _spawned.RemoveAt(i); continue; }
            Vector3 toObj = _spawned[i].transform.position - player.position;
            if (toObj.magnitude > 60f && Vector3.Dot(toObj, player.forward) < 0)
            {
                Destroy(_spawned[i]);
                _spawned.RemoveAt(i);
            }
        }
    }

    void SpawnSquirt(float dist)
    {
        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        // Position at water level (bottom of pipe)
        float waterHeight = -pipeRadius * 0.82f;
        // Random left-right offset within water surface
        float sideOffset = Random.Range(-pipeRadius * 0.6f, pipeRadius * 0.6f);

        Vector3 pos = center + up * waterHeight + right * sideOffset;
        Quaternion rot = Quaternion.LookRotation(forward, -up); // face forward, "up" toward pipe center

        // Spawn a cluster of 1-3 squirts
        int count = Random.Range(1, 4);
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = right * Random.Range(-0.3f, 0.3f) + forward * Random.Range(-0.2f, 0.2f);
            float scale = Random.Range(0.7f, 1.3f);
            GameObject obj = Instantiate(squirtPrefab, pos + offset, rot, transform);
            obj.transform.localScale *= scale;
            _spawned.Add(obj);
        }
    }
}
