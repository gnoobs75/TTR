using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages a chain of poop buddies that ski behind Mr. Corny when picked up from the water.
/// Each buddy follows the one in front with a smooth delay, creating a water-ski conga line.
/// Purely cosmetic - no gameplay impact, just adorable/disgusting.
/// </summary>
public class PoopBuddyChain : MonoBehaviour
{
    public static PoopBuddyChain Instance { get; private set; }

    [Header("Chain Settings")]
    public int maxBuddies = 6;
    public float followDistance = 0.6f;  // gap between each buddy
    public float followSmooth = 8f;     // how quickly buddies catch up
    public float wobbleAmount = 10f;    // side-to-side wobble degrees
    public float wobbleSpeed = 4f;

    [Header("Ski Wake")]
    public bool enableSkiWake = true;

    private TurdController _tc;
    private PipeGenerator _pipeGen;

    struct ChainBuddy
    {
        public GameObject obj;
        public Vector3 targetPos;
        public float wobblePhase;
        public ParticleSystem skiWake;  // tiny spray behind each skier
    }
    private List<ChainBuddy> _chain = new List<ChainBuddy>();

    // Position history for smooth following
    private List<Vector3> _positionHistory = new List<Vector3>();
    private float _historyInterval = 0.02f;
    private float _historyTimer;

    // Materials
    private Material _poopMat;
    private Material _eyeWhiteMat;
    private Material _pupilMat;
    private Material _skiMat;

    public int BuddyCount => _chain.Count;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        _tc = Object.FindFirstObjectByType<TurdController>();
        _pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        CreateMaterials();
    }

    void CreateMaterials()
    {
        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader lit = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");

        _poopMat = new Material(lit);
        _poopMat.SetColor("_BaseColor", new Color(0.4f, 0.25f, 0.1f));
        _poopMat.SetFloat("_Smoothness", 0.65f);

        _eyeWhiteMat = new Material(lit);
        _eyeWhiteMat.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.92f));
        _eyeWhiteMat.SetFloat("_Smoothness", 0.8f);

        _pupilMat = new Material(lit);
        _pupilMat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
        _pupilMat.SetFloat("_Smoothness", 0.9f);

        // Ski material - tiny flat planks under the buddies
        _skiMat = new Material(lit);
        _skiMat.SetColor("_BaseColor", new Color(0.55f, 0.35f, 0.15f));
        _skiMat.SetFloat("_Smoothness", 0.4f);
    }

    void Update()
    {
        if (_tc == null || _chain.Count == 0) return;
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying) return;

        // Record player position history
        _historyTimer -= Time.deltaTime;
        if (_historyTimer <= 0f)
        {
            _historyTimer = _historyInterval;
            _positionHistory.Insert(0, _tc.transform.position);

            // Keep enough history for the full chain
            int needed = Mathf.CeilToInt(maxBuddies * followDistance / (_tc.CurrentSpeed * _historyInterval + 0.01f)) + 20;
            while (_positionHistory.Count > needed)
                _positionHistory.RemoveAt(_positionHistory.Count - 1);
        }

        // Update each buddy in the chain
        for (int i = 0; i < _chain.Count; i++)
        {
            var buddy = _chain[i];
            if (buddy.obj == null) { _chain.RemoveAt(i); i--; continue; }

            // Target position: follow the path at a delay
            float targetDist = followDistance * (i + 1);
            Vector3 targetPos = GetHistoryPosition(targetDist);

            // Smooth follow
            buddy.obj.transform.position = Vector3.Lerp(
                buddy.obj.transform.position, targetPos, Time.deltaTime * followSmooth);

            // Face forward along the chain
            Vector3 lookTarget;
            if (i == 0)
                lookTarget = _tc.transform.position;
            else
                lookTarget = _chain[i - 1].obj.transform.position;

            Vector3 lookDir = (lookTarget - buddy.obj.transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                // Add wobble - they're skiing, so they sway!
                float wobble = Mathf.Sin(Time.time * wobbleSpeed + buddy.wobblePhase) * wobbleAmount;
                targetRot *= Quaternion.Euler(0, 0, wobble);
                buddy.obj.transform.rotation = Quaternion.Slerp(
                    buddy.obj.transform.rotation, targetRot, Time.deltaTime * 10f);
            }

            // Ski wake particles
            if (buddy.skiWake != null)
            {
                bool inWater = _tc != null &&
                    Mathf.Abs(Mathf.DeltaAngle(_tc.CurrentAngle, 270f)) < 30f;
                if (inWater && _tc.CurrentSpeed > 3f)
                {
                    if (!buddy.skiWake.isPlaying) buddy.skiWake.Play();
                }
                else
                {
                    if (buddy.skiWake.isPlaying) buddy.skiWake.Stop();
                }
            }

            _chain[i] = buddy;
        }
    }

    Vector3 GetHistoryPosition(float distance)
    {
        if (_positionHistory.Count < 2) return _tc != null ? _tc.transform.position : Vector3.zero;

        float accDist = 0f;
        for (int i = 1; i < _positionHistory.Count; i++)
        {
            float segDist = Vector3.Distance(_positionHistory[i - 1], _positionHistory[i]);
            accDist += segDist;
            if (accDist >= distance)
            {
                float overshoot = accDist - distance;
                float t = overshoot / (segDist + 0.001f);
                return Vector3.Lerp(_positionHistory[i], _positionHistory[i - 1], t);
            }
        }

        return _positionHistory[_positionHistory.Count - 1];
    }

    /// <summary>
    /// Add a poop buddy to the ski chain. Called when player picks one up from the water.
    /// </summary>
    public bool AddBuddy(GameObject existingPoop = null)
    {
        if (_chain.Count >= maxBuddies) return false;

        // Destroy the water version and create a fresh skiing version
        if (existingPoop != null)
            Destroy(existingPoop);

        GameObject buddy = CreateSkiingBuddy();
        buddy.transform.SetParent(transform);

        // Place behind the last buddy (or behind player)
        Vector3 spawnPos;
        if (_chain.Count > 0 && _chain[_chain.Count - 1].obj != null)
            spawnPos = _chain[_chain.Count - 1].obj.transform.position - Vector3.forward * followDistance;
        else
            spawnPos = _tc != null ? _tc.transform.position - _tc.transform.forward * followDistance : Vector3.zero;
        buddy.transform.position = spawnPos;

        ParticleSystem wake = null;
        if (enableSkiWake)
            wake = CreateSkiWakePS(buddy.transform);

        _chain.Add(new ChainBuddy
        {
            obj = buddy,
            wobblePhase = Random.Range(0f, Mathf.PI * 2f),
            skiWake = wake
        });

        // Celebration feedback
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayCoinCollect(); // reuse the happy sound
        if (PipeCamera.Instance != null)
            PipeCamera.Instance.PunchFOV(2f);
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowMilestone(
                _tc != null ? _tc.transform.position + Vector3.up * 1.5f : Vector3.zero,
                $"BUDDY x{_chain.Count}!");
        HapticManager.MediumTap();

        return true;
    }

    /// <summary>
    /// Lose all buddies (e.g. on hit). They scatter dramatically.
    /// </summary>
    public void ScatterAll()
    {
        foreach (var buddy in _chain)
        {
            if (buddy.obj == null) continue;
            if (buddy.skiWake != null) buddy.skiWake.Stop();

            // Fling them outward comically
            var rb = buddy.obj.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = 0.5f;
            Vector3 flingDir = (Random.onUnitSphere + Vector3.up).normalized;
            rb.AddForce(flingDir * Random.Range(3f, 8f), ForceMode.Impulse);
            rb.AddTorque(Random.onUnitSphere * 10f, ForceMode.Impulse);

            // Self-destruct after a moment
            Destroy(buddy.obj, 2f);
        }
        _chain.Clear();
    }

    GameObject CreateSkiingBuddy()
    {
        var root = new GameObject("SkiBuddy");
        float scale = 0.1f;

        // Poop body
        var bottom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bottom.name = "Bottom";
        bottom.transform.SetParent(root.transform);
        bottom.transform.localPosition = Vector3.zero;
        bottom.transform.localScale = new Vector3(scale, scale * 0.55f, scale);
        bottom.GetComponent<Renderer>().material = _poopMat;
        Object.Destroy(bottom.GetComponent<Collider>());

        var middle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        middle.name = "Middle";
        middle.transform.SetParent(root.transform);
        middle.transform.localPosition = new Vector3(0, scale * 0.35f, 0);
        middle.transform.localScale = new Vector3(scale * 0.75f, scale * 0.45f, scale * 0.75f);
        middle.GetComponent<Renderer>().material = _poopMat;
        Object.Destroy(middle.GetComponent<Collider>());

        var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        top.name = "Top";
        top.transform.SetParent(root.transform);
        top.transform.localPosition = new Vector3(0, scale * 0.6f, 0);
        top.transform.localScale = new Vector3(scale * 0.5f, scale * 0.35f, scale * 0.5f);
        top.GetComponent<Renderer>().material = _poopMat;
        Object.Destroy(top.GetComponent<Collider>());

        // Excited eyes (wide open, looking forward)
        float eyeSize = scale * 0.25f;
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = new GameObject(side < 0 ? "LeftEye" : "RightEye");
            eye.transform.SetParent(root.transform);
            eye.transform.localPosition = new Vector3(
                side * scale * 0.2f, scale * 0.48f, scale * 0.35f);

            var white = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            white.transform.SetParent(eye.transform);
            white.transform.localPosition = Vector3.zero;
            white.transform.localScale = Vector3.one * eyeSize * 1.2f; // big excited eyes!
            white.GetComponent<Renderer>().material = _eyeWhiteMat;
            Object.Destroy(white.GetComponent<Collider>());

            var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.transform.SetParent(eye.transform);
            pupil.transform.localPosition = new Vector3(0, 0, eyeSize * 0.35f);
            pupil.transform.localScale = Vector3.one * eyeSize * 0.5f;
            pupil.GetComponent<Renderer>().material = _pupilMat;
            Object.Destroy(pupil.GetComponent<Collider>());
        }

        // Big happy smile
        var mouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mouth.name = "Mouth";
        mouth.transform.SetParent(root.transform);
        mouth.transform.localPosition = new Vector3(0, scale * 0.3f, scale * 0.42f);
        mouth.transform.localScale = new Vector3(scale * 0.28f, scale * 0.04f, scale * 0.04f);
        // Curve the mouth upward slightly
        mouth.transform.localRotation = Quaternion.Euler(0, 0, 0);
        mouth.GetComponent<Renderer>().material = _pupilMat;
        Object.Destroy(mouth.GetComponent<Collider>());

        // SKIS! Two little planks under the body
        for (int side = -1; side <= 1; side += 2)
        {
            var ski = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ski.name = side < 0 ? "LeftSki" : "RightSki";
            ski.transform.SetParent(root.transform);
            ski.transform.localPosition = new Vector3(
                side * scale * 0.25f, -scale * 0.25f, 0);
            ski.transform.localScale = new Vector3(
                scale * 0.08f,  // thin
                scale * 0.02f,  // flat
                scale * 0.8f);  // long
            // Tip the front up slightly
            ski.transform.localRotation = Quaternion.Euler(-5f, 0, 0);
            ski.GetComponent<Renderer>().material = _skiMat;
            Object.Destroy(ski.GetComponent<Collider>());
        }

        // Rope handle (tiny cylinder connecting to the buddy in front)
        var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rope.name = "Rope";
        rope.transform.SetParent(root.transform);
        rope.transform.localPosition = new Vector3(0, scale * 0.15f, scale * 0.5f);
        rope.transform.localScale = new Vector3(0.003f, scale * 0.3f, 0.003f);
        rope.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");
        Material ropeMat = new Material(urpLit);
        ropeMat.SetColor("_BaseColor", new Color(0.3f, 0.25f, 0.15f));
        rope.GetComponent<Renderer>().material = ropeMat;
        Object.Destroy(rope.GetComponent<Collider>());

        return root;
    }

    ParticleSystem CreateSkiWakePS(Transform parent)
    {
        var go = new GameObject("SkiWake");
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.down * 0.03f + Vector3.back * 0.03f;
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 30;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.25f, 0.35f, 0.12f, 0.5f),
            new Color(0.18f, 0.28f, 0.08f, 0.3f));
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.2f;

        var emission = ps.emission;
        emission.rateOverTime = 12;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.02f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.2f, 0.3f, 0.08f, 0.4f));
        mat.SetColor("_Color", new Color(0.2f, 0.3f, 0.08f, 0.4f));
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_ZWrite", 0);
        mat.SetFloat("_Cull", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        renderer.material = mat;

        return ps;
    }
}
