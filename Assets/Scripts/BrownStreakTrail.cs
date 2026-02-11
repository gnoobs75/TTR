using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Leaves nasty brown streak marks on the pipe walls when Mr. Corny rides
/// on the walls or ceiling. Streaks fade over time and get cleaned up behind the player.
/// </summary>
public class BrownStreakTrail : MonoBehaviour
{
    [Header("Streak Settings")]
    public float minAngleFromBottom = 40f; // degrees from 270 before streaks start
    public float streakInterval = 0.08f;   // seconds between streak spawns
    public float streakLifetime = 4f;
    public float streakWidth = 0.15f;
    public float streakLength = 0.6f;

    [Header("References")]
    public Transform player;

    private TurdController _tc;
    private PipeGenerator _pipeGen;
    private float _lastStreakTime;
    private List<StreakData> _streaks = new List<StreakData>();
    private Material _streakMat;

    struct StreakData
    {
        public GameObject obj;
        public float spawnTime;
        public Renderer renderer;
        public Color baseColor;
    }

    void Start()
    {
        if (player != null) _tc = player.GetComponent<TurdController>();
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        CreateStreakMaterial();
    }

    void CreateStreakMaterial()
    {
        // Use URP Particles/Unlit for transparent quads - Lit shader's transparency
        // requires complex pipeline setup and often renders as black squares on quads.
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Sprites/Default");

        _streakMat = new Material(shader);
        _streakMat.SetFloat("_Surface", 1); // Transparent
        _streakMat.SetFloat("_Blend", 0);   // Alpha
        _streakMat.SetFloat("_Cull", 0);    // No culling
        _streakMat.SetFloat("_ZWrite", 0);
        _streakMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _streakMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _streakMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _streakMat.EnableKeyword("_ALPHABLEND_ON");
        _streakMat.SetOverrideTag("RenderType", "Transparent");
        _streakMat.renderQueue = 3000;
    }

    void Update()
    {
        if (_tc == null || _pipeGen == null || player == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        float angleDelta = Mathf.Abs(Mathf.DeltaAngle(_tc.CurrentAngle, 270f));

        // Only streak when on walls or ceiling
        if (angleDelta > minAngleFromBottom && _tc.CurrentSpeed > 2f)
        {
            if (Time.time - _lastStreakTime > streakInterval)
            {
                _lastStreakTime = Time.time;
                SpawnStreak();
            }
        }

        // Fade and cleanup streaks
        for (int i = _streaks.Count - 1; i >= 0; i--)
        {
            if (_streaks[i].obj == null)
            {
                _streaks.RemoveAt(i);
                continue;
            }

            float age = Time.time - _streaks[i].spawnTime;

            // Fade out
            if (age > streakLifetime * 0.6f)
            {
                float fadeT = (age - streakLifetime * 0.6f) / (streakLifetime * 0.4f);
                Color c = _streaks[i].baseColor;
                c.a = Mathf.Lerp(c.a, 0f, fadeT);
                if (_streaks[i].renderer != null)
                    _streaks[i].renderer.material.SetColor("_BaseColor", c);
            }

            // Destroy when expired or far behind
            if (age > streakLifetime)
            {
                Destroy(_streaks[i].obj);
                _streaks.RemoveAt(i);
                continue;
            }

            // Also cleanup if way behind player
            float distBehind = (player.position - _streaks[i].obj.transform.position).magnitude;
            if (distBehind > 50f && Vector3.Dot(
                _streaks[i].obj.transform.position - player.position, player.forward) < 0)
            {
                Destroy(_streaks[i].obj);
                _streaks.RemoveAt(i);
            }
        }
    }

    void SpawnStreak()
    {
        Vector3 center, forward, right, up;
        _pipeGen.GetPathFrame(_tc.DistanceTraveled, out center, out forward, out right, out up);

        // Position on the pipe wall where the player currently is
        float rad = _tc.CurrentAngle * Mathf.Deg2Rad;
        Vector3 surfaceOffset = (right * Mathf.Cos(rad) + up * Mathf.Sin(rad));
        // Place just slightly inside the pipe wall
        Vector3 pos = center + surfaceOffset * (_pipeGen.pipeRadius - 0.02f);

        // Streak orientation: lies flat on the pipe wall, stretched along travel direction
        Vector3 inward = -surfaceOffset.normalized;
        Quaternion rot = Quaternion.LookRotation(forward, inward);

        // Create a flattened quad
        GameObject streak = GameObject.CreatePrimitive(PrimitiveType.Quad);
        streak.name = "BrownStreak";
        Destroy(streak.GetComponent<Collider>()); // no collision needed

        streak.transform.position = pos;
        streak.transform.rotation = rot;

        // Randomize size slightly
        float w = streakWidth * Random.Range(0.6f, 1.4f);
        float l = streakLength * Random.Range(0.7f, 1.3f);
        streak.transform.localScale = new Vector3(w, l, 1f);

        // Random brown shade - subtle so streaks blend with pipe wall
        float shade = Random.Range(0.15f, 0.35f);
        float alpha = Random.Range(0.12f, 0.25f);
        Color streakColor = new Color(shade, shade * 0.6f, shade * 0.2f, alpha);

        var rend = streak.GetComponent<Renderer>();
        rend.material = new Material(_streakMat);
        rend.material.SetColor("_BaseColor", streakColor);
        rend.material.SetColor("_Color", streakColor); // fallback property name
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _streaks.Add(new StreakData
        {
            obj = streak,
            spawnTime = Time.time,
            renderer = rend,
            baseColor = streakColor
        });
    }
}
