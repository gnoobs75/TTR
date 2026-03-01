using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates endless curved sewer pipe segments using a path system.
/// The path gently curves left/right, increasing with distance.
/// Other scripts query GetPathInfo/GetPathFrame for positioning.
/// </summary>
public class PipeGenerator : MonoBehaviour
{
    [Header("Pipe Settings")]
    public float pipeRadius = 3.5f;
    public int circumSegments = 24;
    public int visiblePipes = 8;

    [Header("Path Settings")]
    public float nodeSpacing = 2f;
    public int nodesPerSegment = 10;
    [Tooltip("Max yaw change per node in degrees (higher = tighter curves)")]
    public float maxCurveRate = 9f;
    [Tooltip("Max pitch change per node in degrees (up/down undulations)")]
    public float maxPitchRate = 6f;
    [Tooltip("Max pitch angle in degrees (prevents going too vertical)")]
    public float maxPitchAngle = 70f;

    [Header("Materials")]
    public Material pipeMaterial;
    public Material pipeRingMaterial;

    [Header("Player Reference")]
    public Transform player;

    // Path data
    private List<Vector3> _positions = new List<Vector3>();
    private List<Vector3> _forwards = new List<Vector3>();
    private float _currentYaw = 0f;
    private float _currentPitch = 0f;

    // Mesh segments
    struct Seg { public GameObject obj; public int startNode; public float dist; }
    private Queue<Seg> _segs = new Queue<Seg>();
    private int _nextSegStart = 0;
    private TurdController _tc;
    private Material _defaultMat;
    private Material _waterMat;
    private Material _detailMat; // rust/slime overlay

    // Lane zones (replaces fork system: pipe widens into pill shape)
    [Header("Lane Zone Settings")]
    public float firstLaneDistance = 500f;
    public float secondLaneDistance = 1000f;
    public float thirdLaneDistance = 1500f;
    private List<PipeLaneZone> _laneZones = new List<PipeLaneZone>();
    private bool _laneZonesCreated;

    public List<PipeLaneZone> LaneZones => _laneZones;

    // Keep fork list empty for backward compat (spawners check it)
    private List<PipeFork> _forks = new List<PipeFork>();
    public List<PipeFork> Forks => _forks;

    public float SegmentLength => nodesPerSegment * nodeSpacing;

    // Structural geometry materials (shared across segments for batching)
    private Material _bracketMat;
    private Material _brickMat;
    private Material _grateMat;
    private Material _conduitMat;
    private Material _chainMat;
    private Material _rivetMat;
    private Shader _toonShader; // cached shader for fork geometry

    void Awake()
    {
        // Use toon shader if available, fall back to URP Lit
        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader urpLit = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        _toonShader = urpLit; // cache for fork geometry

        _defaultMat = new Material(urpLit);
        _defaultMat.SetColor("_BaseColor", new Color(0.32f, 0.27f, 0.2f));
        _defaultMat.SetFloat("_Metallic", 0.05f);
        _defaultMat.SetFloat("_Smoothness", 0.3f);
        _defaultMat.EnableKeyword("_EMISSION");
        _defaultMat.SetColor("_EmissionColor", new Color(0.04f, 0.035f, 0.025f));
        if (_defaultMat.HasProperty("_ShadowColor"))
            _defaultMat.SetColor("_ShadowColor", new Color(0.12f, 0.1f, 0.07f));

        // Sewage water - toxic green sludge
        _waterMat = new Material(urpLit);
        _waterMat.SetColor("_BaseColor", new Color(0.22f, 0.38f, 0.1f));
        _waterMat.SetFloat("_Metallic", 0.6f);
        _waterMat.SetFloat("_Smoothness", 0.95f);
        _waterMat.EnableKeyword("_EMISSION");
        _waterMat.SetColor("_EmissionColor", new Color(0.1f, 0.18f, 0.05f));
        _waterMat.SetFloat("_Cull", 0);
        if (_waterMat.HasProperty("_ShadowColor"))
            _waterMat.SetColor("_ShadowColor", new Color(0.08f, 0.15f, 0.03f));
        if (_waterMat.HasProperty("_SpecularSize"))
            _waterMat.SetFloat("_SpecularSize", 0.3f); // big shiny water highlight band

        // Structural geometry shared materials
        _bracketMat = MakeToonMat(urpLit, "Bracket", new Color(0.35f, 0.32f, 0.28f), 0.5f, 0.4f);
        _brickMat = MakeToonMat(urpLit, "Brick", new Color(0.55f, 0.35f, 0.22f), 0.05f, 0.2f);
        _grateMat = MakeToonMat(urpLit, "Grate", new Color(0.3f, 0.3f, 0.28f), 0.6f, 0.35f);
        _conduitMat = MakeToonMat(urpLit, "Conduit", new Color(0.4f, 0.38f, 0.32f), 0.4f, 0.45f);
        _chainMat = MakeToonMat(urpLit, "Chain", new Color(0.28f, 0.26f, 0.22f), 0.55f, 0.3f);
        _rivetMat = MakeToonMat(urpLit, "Rivet", new Color(0.25f, 0.24f, 0.2f), 0.7f, 0.5f);

        // Seed initial path
        _positions.Add(Vector3.zero);
        _forwards.Add(Vector3.forward);

        int needed = (visiblePipes + 3) * nodesPerSegment + 1;
        for (int i = 0; i < needed; i++)
            AddPathNode();
    }

    void Start()
    {
        if (player != null)
            _tc = player.GetComponent<TurdController>();

        // Create lane zones (pipe widens into pill shape at these distances)
        CreateLaneZones();

        for (int i = 0; i < visiblePipes + 2; i++)
            SpawnSegment();
    }

    void CreateLaneZones()
    {
        if (_laneZonesCreated) return;
        _laneZonesCreated = true;

        // Zone 1: gentle introduction (500m) — moderate widening
        _laneZones.Add(new PipeLaneZone(firstLaneDistance, 35f, 50f, 35f, 1.8f));
        // Zone 2: wider (1000m) — full pill shape
        _laneZones.Add(new PipeLaneZone(secondLaneDistance, 30f, 65f, 30f, 2.0f));
        // Zone 3: intense (1500m) — widest, longest hold
        _laneZones.Add(new PipeLaneZone(thirdLaneDistance, 25f, 80f, 25f, 2.2f));

        Debug.Log($"TTR: Created {_laneZones.Count} lane zones at {firstLaneDistance}/{secondLaneDistance}/{thirdLaneDistance}m");
    }

    void Update()
    {
        if (_tc == null) return;
        float dist = _tc.DistanceTraveled;

        // Spawn ahead
        while (_nextSegStart * nodeSpacing < dist + SegmentLength * visiblePipes)
        {
            int nodesNeeded = _nextSegStart + nodesPerSegment + 1;
            while (_positions.Count < nodesNeeded)
                AddPathNode();
            SpawnSegment();
        }

        // Recycle behind
        while (_segs.Count > 0)
        {
            Seg s = _segs.Peek();
            float endDist = (s.startNode + nodesPerSegment) * nodeSpacing;
            if (endDist < dist - SegmentLength * 2)
            {
                _segs.Dequeue();
                Destroy(s.obj);
            }
            else break;
        }

        // Zone-based material updates
        UpdateZoneMaterials();
    }

    void UpdateZoneMaterials()
    {
        if (PipeZoneSystem.Instance == null) return;
        var zone = PipeZoneSystem.Instance;

        // Update pipe material with zone colors and texture properties
        Material pipeMat = pipeMaterial != null ? pipeMaterial : _defaultMat;
        pipeMat.SetColor("_BaseColor", zone.CurrentPipeColor);
        pipeMat.SetColor("_EmissionColor", zone.CurrentPipeEmission * zone.CurrentEmissionBoost);
        if (pipeMat.HasProperty("_BumpScale"))
            pipeMat.SetFloat("_BumpScale", zone.CurrentBumpScale);
        // Update toon shadow color to match zone
        if (pipeMat.HasProperty("_ShadowColor"))
        {
            Color pc = zone.CurrentPipeColor;
            float ph, ps, pv;
            Color.RGBToHSV(pc, out ph, out ps, out pv);
            pipeMat.SetColor("_ShadowColor", Color.HSVToRGB(ph, Mathf.Min(ps * 1.2f, 1f), pv * 0.3f));
        }

        // Update water material with stronger emission for zone visibility
        _waterMat.SetColor("_BaseColor", zone.CurrentWaterColor);
        _waterMat.SetColor("_EmissionColor", zone.CurrentWaterEmission * zone.CurrentEmissionBoost);
        if (_waterMat.HasProperty("_ShadowColor"))
        {
            Color wc = zone.CurrentWaterColor;
            float wh, ws, wv;
            Color.RGBToHSV(wc, out wh, out ws, out wv);
            _waterMat.SetColor("_ShadowColor", Color.HSVToRGB(wh, Mathf.Min(ws * 1.3f, 1f), wv * 0.25f));
        }

        // Update pipe ring material to match zone theme
        if (pipeRingMaterial != null)
        {
            Color ringTint = Color.Lerp(zone.CurrentPipeColor, new Color(0.5f, 0.4f, 0.3f), 0.4f);
            pipeRingMaterial.SetColor("_BaseColor", ringTint);
            if (pipeRingMaterial.HasProperty("_ShadowColor"))
            {
                float rh, rs, rv;
                Color.RGBToHSV(ringTint, out rh, out rs, out rv);
                pipeRingMaterial.SetColor("_ShadowColor", Color.HSVToRGB(rh, rs, rv * 0.3f));
            }
        }
    }

    void AddPathNode()
    {
        Vector3 prev = _positions[_positions.Count - 1];
        float dist = _positions.Count * nodeSpacing;

        // No curves for first 40m, then ramp up quickly
        float maxTurn = 0f;
        float maxPitch = 0f;
        if (dist > 40f)
        {
            float ramp = Mathf.Clamp01((dist - 40f) / 150f);
            maxTurn = maxCurveRate * ramp;
            maxPitch = maxPitchRate * ramp;
        }

        // Horizontal curves (yaw) - big sweeping side-to-side
        var pRng = SeedManager.Instance.PipeRNG;
        _currentYaw += SeedManager.Range(pRng, -maxTurn, maxTurn);
        _currentYaw *= 0.96f; // dampen to prevent tight spiraling

        // Vertical undulations (pitch) - hills and valleys
        _currentPitch += SeedManager.Range(pRng, -maxPitch, maxPitch);
        _currentPitch *= 0.93f; // dampen to prevent going vertical
        _currentPitch = Mathf.Clamp(_currentPitch, -maxPitchAngle, maxPitchAngle);

        // Occasional dramatic sweep: ~5% chance per node after 80m
        if (dist > 80f && SeedManager.Value(pRng) < 0.05f)
        {
            float sweepStrength = SeedManager.Range(pRng, 0.5f, 1f);
            if (SeedManager.Value(pRng) < 0.5f)
                _currentYaw += maxTurn * 2.5f * sweepStrength * (SeedManager.Value(pRng) < 0.5f ? 1f : -1f);
            else
                _currentPitch += maxPitch * 2f * sweepStrength * (SeedManager.Value(pRng) < 0.5f ? 1f : -1f);
            _currentPitch = Mathf.Clamp(_currentPitch, -maxPitchAngle, maxPitchAngle);
        }

        // Build forward direction from yaw and pitch
        float yawRad = _currentYaw * Mathf.Deg2Rad;
        float pitchRad = _currentPitch * Mathf.Deg2Rad;
        Vector3 fwd = new Vector3(
            Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            Mathf.Sin(pitchRad),
            Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        ).normalized;

        _positions.Add(prev + fwd * nodeSpacing);
        _forwards.Add(fwd);
    }

    void SpawnSegment()
    {
        int start = _nextSegStart;
        int end = start + nodesPerSegment;
        float segDist = start * nodeSpacing;
        GameObject obj = BuildMesh(start, end);
        obj.transform.parent = transform;

        // Add sewage water plane at the bottom
        GameObject water = BuildWaterPlane(start, end);
        water.transform.parent = obj.transform;

        // Pipe ring cylinders removed - they rendered as solid discs blocking the view.
        // Segment joints are visually marked by pipe detail patches instead.

        // Add procedural pipe wall detail (rust, slime, cracks)
        AddPipeDetail(obj, start, end, segDist);

        // Structural geometry disabled - crosshatch shader + pipe detail provides enough visual density
        // AddStructuralGeometry(obj, start, end, segDist);

        _segs.Enqueue(new Seg { obj = obj, startNode = start, dist = segDist });
        _nextSegStart = end;
    }

    /// <summary>
    /// Adds a visible metal ring band at the pipe segment joint for visual variety.
    /// Uses a cylinder primitive scaled to wrap the inside of the pipe.
    /// </summary>
    void AddPipeRing(GameObject parent, int nodeIdx)
    {
        if (nodeIdx >= _positions.Count) return;
        Material ringMat = pipeRingMaterial;
        if (ringMat == null) return;

        Vector3 center = _positions[nodeIdx];
        Vector3 fwd = _forwards[nodeIdx];

        // Create a thin cylinder at the joint
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "PipeRing";
        ring.transform.SetParent(parent.transform);
        ring.transform.position = center;
        // Orient cylinder along the pipe direction
        ring.transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(90, 0, 0);
        // Scale: diameter = pipe diameter * 1.01 (slightly inside), height = thin band
        float diam = pipeRadius * 2.02f;
        ring.transform.localScale = new Vector3(diam, 0.15f, diam);

        ring.GetComponent<Renderer>().material = ringMat;
        Collider c = ring.GetComponent<Collider>();
        if (c != null) Destroy(c);
    }

    /// <summary>
    /// Adds rust streaks, slime patches, and grime to pipe segment walls.
    /// Uses deterministic random based on distance so detail is consistent.
    /// </summary>
    void AddPipeDetail(GameObject parent, int startNode, int endNode, float segDist)
    {
        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader urpLit = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        // Deterministic per-segment RNG (derived from PipeDetailRNG seed + segment distance)
        var dRng = new System.Random(SeedManager.Instance.CurrentSeed ^ 0x12345678 ^ Mathf.FloorToInt(segDist * 7.3f));

        int detailCount = SeedManager.Range(dRng, 2, 6); // 2-5 details per segment

        // More details further into the run
        if (segDist > 200f) detailCount += 1;
        if (segDist > 500f) detailCount += 2;

        for (int d = 0; d < detailCount; d++)
        {
            int nodeIdx = startNode + SeedManager.Range(dRng, 1, endNode - startNode - 1);
            if (nodeIdx >= _positions.Count) continue;

            Vector3 center = _positions[nodeIdx];
            Vector3 fwd = _forwards[nodeIdx];
            Vector3 refUp = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(fwd, refUp).normalized;
            Vector3 up = Vector3.Cross(right, fwd).normalized;

            float angle = SeedManager.Range(dRng, 0f, 360f) * Mathf.Deg2Rad;
            float radius = pipeRadius * 0.90f; // safely inside pipe wall
            float detailWidth = GetLaneWidthAt(nodeIdx * nodeSpacing);
            Vector3 pos = center + (right * Mathf.Cos(angle) * detailWidth + up * Mathf.Sin(angle)) * radius;
            Vector3 inward = (center - pos).normalized;

            int detailType = SeedManager.Range(dRng, 0, 5);
            Color detailColor;
            Color detailEmission = Color.black;
            float detailSmooth = 0.2f;
            float detailMetal = 0.05f;

            switch (detailType)
            {
                case 0: // Rust streak
                    detailColor = new Color(
                        SeedManager.Range(dRng, 0.4f, 0.6f),
                        SeedManager.Range(dRng, 0.18f, 0.3f),
                        SeedManager.Range(dRng, 0.05f, 0.12f));
                    detailSmooth = 0.15f;
                    detailMetal = 0.3f;
                    break;
                case 1: // Slime patch
                    detailColor = new Color(
                        SeedManager.Range(dRng, 0.1f, 0.2f),
                        SeedManager.Range(dRng, 0.4f, 0.6f),
                        SeedManager.Range(dRng, 0.05f, 0.15f));
                    detailEmission = detailColor * 0.3f;
                    detailSmooth = 0.85f;
                    break;
                case 2: // Dark grime
                    detailColor = new Color(
                        SeedManager.Range(dRng, 0.08f, 0.15f),
                        SeedManager.Range(dRng, 0.06f, 0.12f),
                        SeedManager.Range(dRng, 0.04f, 0.08f));
                    detailSmooth = 0.1f;
                    break;
                case 3: // Moss/mold
                    detailColor = new Color(
                        SeedManager.Range(dRng, 0.15f, 0.25f),
                        SeedManager.Range(dRng, 0.3f, 0.4f),
                        SeedManager.Range(dRng, 0.08f, 0.15f));
                    detailEmission = detailColor * 0.15f;
                    detailSmooth = 0.4f;
                    break;
                default: // Mineral deposit / calcium buildup
                    detailColor = new Color(
                        SeedManager.Range(dRng, 0.6f, 0.75f),
                        SeedManager.Range(dRng, 0.55f, 0.7f),
                        SeedManager.Range(dRng, 0.45f, 0.55f));
                    detailSmooth = 0.6f;
                    detailMetal = 0.15f;
                    break;
            }

            Material mat = new Material(urpLit);
            mat.SetColor("_BaseColor", detailColor);
            mat.SetFloat("_Metallic", detailMetal);
            mat.SetFloat("_Smoothness", detailSmooth);
            if (detailEmission != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", detailEmission);
            }
            // Toon shadow color
            if (mat.HasProperty("_ShadowColor"))
            {
                float dh, ds, dv;
                Color.RGBToHSV(detailColor, out dh, out ds, out dv);
                mat.SetColor("_ShadowColor", Color.HSVToRGB(dh, Mathf.Min(ds * 1.2f, 1f), dv * 0.3f));
            }

            // Create detail geometry - subtle flat patches hugging the pipe wall
            GameObject detail = GameObject.CreatePrimitive(PrimitiveType.Quad);
            detail.name = "PipeDetail";
            detail.transform.SetParent(parent.transform);
            detail.transform.position = pos;

            // Face outward from pipe wall (quad faces -Z, so inward becomes forward)
            Quaternion surfaceRot = Quaternion.LookRotation(-inward, fwd);
            detail.transform.rotation = surfaceRot;

            // Small flat patches that hug the wall surface
            float sizeX = SeedManager.Range(dRng, 0.3f, 0.9f);
            float sizeY = SeedManager.Range(dRng, 0.2f, 0.7f);
            detail.transform.localScale = new Vector3(sizeX, sizeY, 1f);

            detail.GetComponent<Renderer>().material = mat;
            Collider c = detail.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }
    }

    GameObject BuildMesh(int startNode, int endNode)
    {
        GameObject go = new GameObject("SewerPipe");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        MeshCollider mc = go.AddComponent<MeshCollider>();

        int ringCount = endNode - startNode + 1;
        int vpr = circumSegments + 1;
        int totalVerts = vpr * ringCount;

        Vector3[] verts = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];
        int[] tris = new int[circumSegments * (ringCount - 1) * 6];

        for (int r = 0; r < ringCount; r++)
        {
            int nodeIdx = startNode + r;
            Vector3 center = _positions[nodeIdx];
            Vector3 fwd = _forwards[nodeIdx];
            Vector3 refUp = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(fwd, refUp).normalized;
            Vector3 up = Vector3.Cross(right, fwd).normalized;

            // Support ribs at joints and periodically
            float radius = pipeRadius;
            if (r == 0 || r == ringCount - 1)
                radius *= 1.06f;
            else if (r % 4 == 0)
                radius *= 1.03f;

            // Lane zone: stretch horizontally for pill/oval shape
            float widthMult = GetLaneWidthAt(nodeIdx * nodeSpacing);

            for (int s = 0; s <= circumSegments; s++)
            {
                float angle = (float)s / circumSegments * Mathf.PI * 2f;
                int idx = r * vpr + s;
                // Pill shape: stretch the right (horizontal) axis, keep up (vertical) axis normal
                verts[idx] = center + (right * Mathf.Cos(angle) * widthMult + up * Mathf.Sin(angle)) * radius;
                uvs[idx] = new Vector2((float)s / circumSegments, (float)r / (ringCount - 1) * 2f);
            }
        }

        int tri = 0;
        for (int r = 0; r < ringCount - 1; r++)
        {
            for (int s = 0; s < circumSegments; s++)
            {
                int cur = r * vpr + s;
                int nxt = cur + vpr;

                tris[tri++] = cur;
                tris[tri++] = cur + 1;
                tris[tri++] = nxt;

                tris[tri++] = nxt;
                tris[tri++] = cur + 1;
                tris[tri++] = nxt + 1;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "PipeMesh";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        // Flip normals inward (player is inside)
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
            normals[i] = -normals[i];
        mesh.normals = normals;

        mf.mesh = mesh;
        mc.sharedMesh = mesh;
        mr.material = pipeMaterial != null ? pipeMaterial : _defaultMat;

        return go;
    }

    /// <summary>
    /// Builds a flat water surface at the bottom of the pipe segment.
    /// </summary>
    GameObject BuildWaterPlane(int startNode, int endNode)
    {
        GameObject go = new GameObject("SewerWater");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();

        int ringCount = endNode - startNode + 1;
        // Two verts per ring (left and right edges of water surface)
        Vector3[] verts = new Vector3[ringCount * 2];
        Vector2[] uvs = new Vector2[ringCount * 2];
        int[] tris = new int[(ringCount - 1) * 6];

        float waterWidth = pipeRadius * 0.9f; // water covers most of the pipe bottom
        float waterHeight = -pipeRadius * 0.82f; // at player level - Mr. Corny slides through sewage

        for (int r = 0; r < ringCount; r++)
        {
            int nodeIdx = startNode + r;
            Vector3 center = _positions[nodeIdx];
            Vector3 fwd = _forwards[nodeIdx];
            Vector3 refUp = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(fwd, refUp).normalized;
            Vector3 up = Vector3.Cross(right, fwd).normalized;

            // Lane zone: water plane stretches horizontally too
            float widthMult = GetLaneWidthAt(nodeIdx * nodeSpacing);

            Vector3 waterCenter = center + up * waterHeight;
            verts[r * 2] = waterCenter - right * waterWidth * widthMult;
            verts[r * 2 + 1] = waterCenter + right * waterWidth * widthMult;
            uvs[r * 2] = new Vector2(0, (float)r / (ringCount - 1));
            uvs[r * 2 + 1] = new Vector2(1, (float)r / (ringCount - 1));
        }

        int tri = 0;
        for (int r = 0; r < ringCount - 1; r++)
        {
            int bl = r * 2;
            int br = bl + 1;
            int tl = bl + 2;
            int tr = bl + 3;

            // Winding order: normals face UPWARD toward camera (not downward)
            tris[tri++] = bl; tris[tri++] = br; tris[tri++] = tl;
            tris[tri++] = br; tris[tri++] = tr; tris[tri++] = tl;
        }

        Mesh mesh = new Mesh();
        mesh.name = "WaterMesh";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        mf.mesh = mesh;
        mr.material = _waterMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return go;
    }

    /// <summary>
    /// Get center position and forward direction at a distance along the path.
    /// </summary>
    public void GetPathInfo(float distance, out Vector3 position, out Vector3 forward)
    {
        if (_positions.Count < 2)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            return;
        }

        float t = distance / nodeSpacing;
        int idx = Mathf.FloorToInt(t);
        idx = Mathf.Clamp(idx, 0, _positions.Count - 2);
        float frac = t - idx;

        position = Vector3.Lerp(_positions[idx], _positions[idx + 1], frac);
        forward = Vector3.Slerp(_forwards[idx], _forwards[idx + 1], frac).normalized;
    }

    /// <summary>
    /// Get full coordinate frame (position, forward, right, up) at a distance.
    /// </summary>
    public void GetPathFrame(float distance, out Vector3 position, out Vector3 forward,
        out Vector3 right, out Vector3 up)
    {
        GetPathInfo(distance, out position, out forward);
        // Use world up as reference, but handle steep angles gracefully
        Vector3 refUp = Vector3.up;
        // If forward is nearly vertical, use world forward as fallback
        if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f)
            refUp = Vector3.forward;
        right = Vector3.Cross(forward, refUp).normalized;
        up = Vector3.Cross(right, forward).normalized;
    }

    // ===== TOON MATERIAL HELPER =====
    static Material MakeToonMat(Shader shader, string name, Color color, float metallic, float smoothness)
    {
        Material mat = new Material(shader);
        mat.name = name;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_ShadowColor"))
        {
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);
            mat.SetColor("_ShadowColor", Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.3f));
        }
        return mat;
    }

    // ===== STRUCTURAL SEWER GEOMETRY =====
    /// <summary>
    /// Adds dense structural sewer elements to pipe segments for comic-book visual density.
    /// Includes brick arch ribs, support brackets, cross-braces, conduits, grates, chains,
    /// stalactites, and rivet rows. Density increases with distance / zone progression.
    /// </summary>
    void AddStructuralGeometry(GameObject parent, int startNode, int endNode, float segDist)
    {
        // Deterministic per-segment RNG for structural geometry
        var sRng = new System.Random(SeedManager.Instance.CurrentSeed ^ 0x12345678 ^ Mathf.FloorToInt(segDist * 13.7f));

        // Density ramps up: Porcelain = sparse, Hellsewer = packed
        float densityMult = 1f;
        if (segDist > 800f) densityMult = 2.5f;
        else if (segDist > 500f) densityMult = 2.0f;
        else if (segDist > 250f) densityMult = 1.5f;
        else if (segDist > 80f) densityMult = 1.2f;

        int nodeCount = endNode - startNode;
        float segLen = nodeCount * nodeSpacing;

        // 1. BRICK ARCH RIBS - rows of flattened bricks forming arches
        float archSpacing = Mathf.Max(3f, 6f / densityMult);
        for (float d = 0; d < segLen; d += archSpacing)
        {
            int nodeIdx = startNode + Mathf.FloorToInt(d / nodeSpacing);
            if (nodeIdx >= _positions.Count) break;
            AddBrickArchRib(parent, nodeIdx);
        }

        // 2. SUPPORT BRACKETS - L-shaped metal on walls
        int bracketCount = Mathf.FloorToInt(2 * densityMult);
        for (int i = 0; i < bracketCount; i++)
        {
            int nodeIdx = startNode + SeedManager.Range(sRng, 2, nodeCount - 2);
            if (nodeIdx >= _positions.Count) continue;
            float angle = SeedManager.Range(sRng, 0f, 360f);
            AddSupportBracket(parent, nodeIdx, angle);
        }

        // 3. CROSS-BRACES - diagonal struts
        if (densityMult > 1.0f)
        {
            int braceCount = Mathf.FloorToInt(1 * densityMult);
            for (int i = 0; i < braceCount; i++)
            {
                int nodeIdx = startNode + SeedManager.Range(sRng, 3, nodeCount - 3);
                if (nodeIdx >= _positions.Count) continue;
                AddCrossBrace(parent, nodeIdx, sRng);
            }
        }

        // 4. PIPE CONDUITS - secondary pipes running along walls/ceiling
        if (SeedManager.Value(sRng) < 0.4f * densityMult)
        {
            float angle = SeedManager.Range(sRng, 100f, 260f); // upper half
            AddPipeConduit(parent, startNode, endNode, angle);
        }

        // 5. GRATE PANELS - thin grids on walls
        int grateCount = Mathf.FloorToInt(1 * densityMult);
        for (int i = 0; i < grateCount; i++)
        {
            int nodeIdx = startNode + SeedManager.Range(sRng, 2, nodeCount - 2);
            if (nodeIdx >= _positions.Count) continue;
            float angle = SeedManager.Range(sRng, 0f, 360f);
            AddGratePanel(parent, nodeIdx, angle);
        }

        // 6. CHAIN HANGERS - from ceiling
        if (densityMult > 1.2f && SeedManager.Value(sRng) < 0.5f)
        {
            int nodeIdx = startNode + SeedManager.Range(sRng, 3, nodeCount - 3);
            if (nodeIdx < _positions.Count)
                AddChainHanger(parent, nodeIdx, sRng);
        }

        // 7. RIVET ROWS - along structural seams
        if (densityMult > 1.0f)
        {
            int rivetSets = Mathf.FloorToInt(2 * densityMult);
            for (int i = 0; i < rivetSets; i++)
            {
                int nodeIdx = startNode + SeedManager.Range(sRng, 1, nodeCount - 1);
                if (nodeIdx >= _positions.Count) continue;
                AddRivetRow(parent, nodeIdx, sRng);
            }
        }
    }

    void GetNodeFrame(int nodeIdx, out Vector3 center, out Vector3 fwd, out Vector3 right, out Vector3 up)
    {
        center = _positions[nodeIdx];
        fwd = _forwards[nodeIdx];
        Vector3 refUp = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        right = Vector3.Cross(fwd, refUp).normalized;
        up = Vector3.Cross(right, fwd).normalized;
    }

    Vector3 GetWallPos(Vector3 center, Vector3 right, Vector3 up, float angleDeg, float radiusFrac = 0.92f)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * pipeRadius * radiusFrac;
    }

    // Brick arch: ring of flattened cubes forming a brick arch rib
    void AddBrickArchRib(GameObject parent, int nodeIdx)
    {
        GetNodeFrame(nodeIdx, out Vector3 center, out Vector3 fwd, out Vector3 right, out Vector3 up);

        int brickCount = 16; // bricks around circumference
        for (int i = 0; i < brickCount; i++)
        {
            float angle = (float)i / brickCount * 360f;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 dir = right * Mathf.Cos(rad) + up * Mathf.Sin(rad);
            Vector3 pos = center + dir * pipeRadius * 0.88f;
            Vector3 inward = -dir;

            GameObject brick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brick.name = "BrickArch";
            brick.transform.SetParent(parent.transform);
            brick.transform.position = pos;
            brick.transform.rotation = Quaternion.LookRotation(fwd, inward);
            // Each brick: wide along circumference, thin depth, medium height
            brick.transform.localScale = new Vector3(
                pipeRadius * 0.42f, // width (circumference)
                0.15f,              // depth into wall
                0.35f               // height along pipe
            );
            brick.GetComponent<Renderer>().sharedMaterial = _brickMat;
            Collider c = brick.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
    }

    // L-shaped support bracket bolted to wall
    void AddSupportBracket(GameObject parent, int nodeIdx, float angleDeg)
    {
        GetNodeFrame(nodeIdx, out Vector3 center, out Vector3 fwd, out Vector3 right, out Vector3 up);
        Vector3 wallPos = GetWallPos(center, right, up, angleDeg, 0.88f);
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 outDir = (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)).normalized;
        Vector3 inward = -outDir;

        // Vertical arm
        GameObject arm1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm1.name = "Bracket_V";
        arm1.transform.SetParent(parent.transform);
        arm1.transform.position = wallPos - outDir * 0.15f;
        arm1.transform.rotation = Quaternion.LookRotation(fwd, inward);
        arm1.transform.localScale = new Vector3(0.08f, 0.6f, 0.08f);
        arm1.GetComponent<Renderer>().sharedMaterial = _bracketMat;
        Collider c1 = arm1.GetComponent<Collider>();
        if (c1 != null) Destroy(c1);

        // Horizontal arm (shelf)
        GameObject arm2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm2.name = "Bracket_H";
        arm2.transform.SetParent(parent.transform);
        arm2.transform.position = wallPos - outDir * 0.4f;
        arm2.transform.rotation = Quaternion.LookRotation(fwd, inward);
        arm2.transform.localScale = new Vector3(0.08f, 0.08f, 0.5f);
        arm2.GetComponent<Renderer>().sharedMaterial = _bracketMat;
        Collider c2 = arm2.GetComponent<Collider>();
        if (c2 != null) Destroy(c2);

        // Diagonal brace
        GameObject diag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        diag.name = "Bracket_D";
        diag.transform.SetParent(parent.transform);
        Vector3 diagPos = wallPos - outDir * 0.28f;
        diag.transform.position = diagPos;
        Quaternion baseRot = Quaternion.LookRotation(fwd, inward);
        diag.transform.rotation = baseRot * Quaternion.Euler(0, 0, 45);
        diag.transform.localScale = new Vector3(0.05f, 0.55f, 0.05f);
        diag.GetComponent<Renderer>().sharedMaterial = _bracketMat;
        Collider c3 = diag.GetComponent<Collider>();
        if (c3 != null) Destroy(c3);
    }

    // Diagonal cross-brace spanning across pipe
    void AddCrossBrace(GameObject parent, int nodeIdx, System.Random rng = null)
    {
        GetNodeFrame(nodeIdx, out Vector3 center, out Vector3 fwd, out Vector3 right, out Vector3 up);

        float angle1 = rng != null ? SeedManager.Range(rng, 20f, 70f) : Random.Range(20f, 70f);
        float angle2 = angle1 + 180f;
        Vector3 p1 = GetWallPos(center, right, up, angle1, 0.85f);
        Vector3 p2 = GetWallPos(center, right, up, angle2, 0.85f);

        Vector3 mid = (p1 + p2) * 0.5f;
        Vector3 dir = (p2 - p1).normalized;
        float len = (p2 - p1).magnitude;

        GameObject brace = GameObject.CreatePrimitive(PrimitiveType.Cube);
        brace.name = "CrossBrace";
        brace.transform.SetParent(parent.transform);
        brace.transform.position = mid;
        brace.transform.rotation = Quaternion.LookRotation(fwd, dir);
        brace.transform.localScale = new Vector3(0.06f, len, 0.06f);
        brace.GetComponent<Renderer>().sharedMaterial = _bracketMat;
        Collider c = brace.GetComponent<Collider>();
        if (c != null) Destroy(c);
    }

    // Secondary pipe conduit running along wall
    void AddPipeConduit(GameObject parent, int startNode, int endNode, float angleDeg)
    {
        GetNodeFrame(startNode, out Vector3 startCenter, out Vector3 startFwd, out Vector3 sR, out Vector3 sU);
        GetNodeFrame(endNode, out Vector3 endCenter, out Vector3 endFwd, out Vector3 eR, out Vector3 eU);

        // Place cylinder segments along the conduit path
        int steps = 3;
        for (int i = 0; i < steps; i++)
        {
            float t0 = (float)i / steps;
            float t1 = (float)(i + 1) / steps;
            int n0 = startNode + Mathf.FloorToInt(t0 * (endNode - startNode));
            int n1 = startNode + Mathf.FloorToInt(t1 * (endNode - startNode));
            if (n0 >= _positions.Count || n1 >= _positions.Count) break;

            GetNodeFrame(n0, out Vector3 c0, out Vector3 f0, out Vector3 r0, out Vector3 u0);
            GetNodeFrame(n1, out Vector3 c1, out Vector3 f1, out Vector3 r1, out Vector3 u1);

            Vector3 p0 = GetWallPos(c0, r0, u0, angleDeg, 0.82f);
            Vector3 p1 = GetWallPos(c1, r1, u1, angleDeg, 0.82f);

            Vector3 mid = (p0 + p1) * 0.5f;
            Vector3 dir = (p1 - p0).normalized;
            float len = (p1 - p0).magnitude;

            GameObject cond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cond.name = "Conduit";
            cond.transform.SetParent(parent.transform);
            cond.transform.position = mid;
            cond.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
            cond.transform.localScale = new Vector3(0.15f, len * 0.5f, 0.15f);
            cond.GetComponent<Renderer>().sharedMaterial = _conduitMat;
            Collider c = cond.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        // Mounting brackets for conduit
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int nIdx = startNode + Mathf.FloorToInt(t * (endNode - startNode));
            if (nIdx >= _positions.Count) break;
            GetNodeFrame(nIdx, out Vector3 cn, out Vector3 fn, out Vector3 rn, out Vector3 un);
            Vector3 mountPos = GetWallPos(cn, rn, un, angleDeg, 0.86f);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 outDir = (rn * Mathf.Cos(rad) + un * Mathf.Sin(rad)).normalized;

            GameObject mount = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mount.name = "ConduitMount";
            mount.transform.SetParent(parent.transform);
            mount.transform.position = mountPos;
            mount.transform.rotation = Quaternion.LookRotation(fn, -outDir);
            mount.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);
            mount.GetComponent<Renderer>().sharedMaterial = _bracketMat;
            Collider mc = mount.GetComponent<Collider>();
            if (mc != null) Destroy(mc);
        }
    }

    // Grid grate panel on wall
    void AddGratePanel(GameObject parent, int nodeIdx, float angleDeg)
    {
        GetNodeFrame(nodeIdx, out Vector3 center, out Vector3 fwd, out Vector3 right, out Vector3 up);
        Vector3 wallPos = GetWallPos(center, right, up, angleDeg, 0.87f);
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 outDir = (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)).normalized;

        // Frame
        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "GrateFrame";
        frame.transform.SetParent(parent.transform);
        frame.transform.position = wallPos;
        frame.transform.rotation = Quaternion.LookRotation(fwd, -outDir);
        frame.transform.localScale = new Vector3(0.8f, 0.04f, 0.8f);
        frame.GetComponent<Renderer>().sharedMaterial = _grateMat;
        Collider fc = frame.GetComponent<Collider>();
        if (fc != null) Destroy(fc);

        // Horizontal bars
        for (int i = 0; i < 4; i++)
        {
            float offset = (i - 1.5f) * 0.18f;
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "GrateBar";
            bar.transform.SetParent(parent.transform);
            bar.transform.position = wallPos + fwd * offset;
            bar.transform.rotation = Quaternion.LookRotation(fwd, -outDir);
            bar.transform.localScale = new Vector3(0.7f, 0.06f, 0.03f);
            bar.GetComponent<Renderer>().sharedMaterial = _grateMat;
            Collider bc = bar.GetComponent<Collider>();
            if (bc != null) Destroy(bc);
        }

        // Vertical bars
        for (int i = 0; i < 4; i++)
        {
            float offset = (i - 1.5f) * 0.18f;
            Vector3 tangent = Vector3.Cross(outDir, fwd).normalized;
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "GrateBarV";
            bar.transform.SetParent(parent.transform);
            bar.transform.position = wallPos + tangent * offset;
            bar.transform.rotation = Quaternion.LookRotation(fwd, -outDir);
            bar.transform.localScale = new Vector3(0.03f, 0.06f, 0.7f);
            bar.GetComponent<Renderer>().sharedMaterial = _grateMat;
            Collider bc = bar.GetComponent<Collider>();
            if (bc != null) Destroy(bc);
        }
    }

    // Chain hanging from ceiling
    void AddChainHanger(GameObject parent, int nodeIdx, System.Random rng = null)
    {
        GetNodeFrame(nodeIdx, out Vector3 center, out Vector3 fwd, out Vector3 right, out Vector3 up);
        Vector3 ceilingPos = center + up * pipeRadius * 0.85f;

        int links = rng != null ? SeedManager.Range(rng, 3, 7) : Random.Range(3, 7);
        for (int i = 0; i < links; i++)
        {
            GameObject link = GameObject.CreatePrimitive(PrimitiveType.Cube);
            link.name = "ChainLink";
            link.transform.SetParent(parent.transform);
            link.transform.position = ceilingPos - up * (i * 0.2f);
            link.transform.rotation = Quaternion.LookRotation(fwd, up);
            // Alternate link orientation for chain look
            if (i % 2 == 1)
                link.transform.rotation *= Quaternion.Euler(0, 90, 0);
            link.transform.localScale = new Vector3(0.08f, 0.06f, 0.15f);
            link.GetComponent<Renderer>().sharedMaterial = _chainMat;
            Collider c = link.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
    }

    // Row of rivets along a circumferential line
    void AddRivetRow(GameObject parent, int nodeIdx, System.Random rng = null)
    {
        GetNodeFrame(nodeIdx, out Vector3 center, out Vector3 fwd, out Vector3 right, out Vector3 up);

        int rivetCount = 12;
        float startAngle = rng != null ? SeedManager.Range(rng, 0f, 360f) : Random.Range(0f, 360f);
        float arcSpan = rng != null ? SeedManager.Range(rng, 60f, 180f) : Random.Range(60f, 180f);

        for (int i = 0; i < rivetCount; i++)
        {
            float angle = startAngle + (float)i / rivetCount * arcSpan;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 dir = right * Mathf.Cos(rad) + up * Mathf.Sin(rad);
            Vector3 pos = center + dir * pipeRadius * 0.89f;

            GameObject rivet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rivet.name = "Rivet";
            rivet.transform.SetParent(parent.transform);
            rivet.transform.position = pos;
            rivet.transform.localScale = Vector3.one * 0.06f;
            rivet.GetComponent<Renderer>().sharedMaterial = _rivetMat;
            Collider c = rivet.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
    }

    // ===== LANE ZONE SYSTEM (replaces forks) =====

    /// <summary>
    /// Get the horizontal width multiplier at a given distance along the pipe.
    /// Returns 1.0 for normal circular pipe, >1.0 in lane zones (pill shape).
    /// </summary>
    public float GetLaneWidthAt(float distance)
    {
        for (int i = 0; i < _laneZones.Count; i++)
        {
            if (_laneZones[i].Contains(distance))
                return _laneZones[i].GetWidthMultiplier(distance);
        }
        return 1f;
    }

    /// <summary>
    /// Find the active lane zone (if any) at a given distance.
    /// </summary>
    public PipeLaneZone GetLaneZoneAtDistance(float distance)
    {
        for (int i = 0; i < _laneZones.Count; i++)
        {
            if (_laneZones[i].Contains(distance))
                return _laneZones[i];
        }
        return null;
    }

    // === BACKWARD COMPAT: Fork queries return null/empty ===

    /// <summary>Fork system replaced by lane zones. Always returns null.</summary>
    public PipeFork GetForkAtDistance(float distance)
    {
        return null;
    }

    /// <summary>Fork system replaced by lane zones. Falls through to main path.</summary>
    public void GetPathFrameForBranch(float distance, int branchIdx, PipeFork fork,
        out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        GetPathFrame(distance, out position, out forward, out right, out up);
    }
}
