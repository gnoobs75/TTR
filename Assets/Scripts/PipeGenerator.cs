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
    public float maxCurveRate = 6f;
    [Tooltip("Max pitch change per node in degrees (up/down undulations)")]
    public float maxPitchRate = 4f;
    [Tooltip("Max pitch angle in degrees (prevents going too vertical)")]
    public float maxPitchAngle = 60f;

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

    public float SegmentLength => nodesPerSegment * nodeSpacing;

    void Awake()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");
        _defaultMat = new Material(urpLit);
        // Sewer concrete - brighter, warmer, cartoon-visible
        _defaultMat.SetColor("_BaseColor", new Color(0.32f, 0.27f, 0.2f));
        _defaultMat.SetFloat("_Metallic", 0.05f);
        _defaultMat.SetFloat("_Smoothness", 0.3f);
        // Subtle self-illumination so pipe walls are never black
        _defaultMat.EnableKeyword("_EMISSION");
        _defaultMat.SetColor("_EmissionColor", new Color(0.04f, 0.035f, 0.025f));

        // Sewage water - bright toxic green sludge with strong glow
        _waterMat = new Material(urpLit);
        _waterMat.SetColor("_BaseColor", new Color(0.22f, 0.38f, 0.1f));
        _waterMat.SetFloat("_Metallic", 0.6f);
        _waterMat.SetFloat("_Smoothness", 0.95f);
        _waterMat.EnableKeyword("_EMISSION");
        _waterMat.SetColor("_EmissionColor", new Color(0.1f, 0.18f, 0.05f));
        _waterMat.SetFloat("_Cull", 0); // Render both faces for visibility
        // Specular highlights make water look wet and alive
        _waterMat.SetFloat("_SpecularHighlights", 1f);
        _waterMat.SetFloat("_EnvironmentReflections", 1f);

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

        for (int i = 0; i < visiblePipes + 2; i++)
            SpawnSegment();
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

        // Update water material with stronger emission for zone visibility
        _waterMat.SetColor("_BaseColor", zone.CurrentWaterColor);
        _waterMat.SetColor("_EmissionColor", zone.CurrentWaterEmission * zone.CurrentEmissionBoost);

        // Update pipe ring material to match zone theme
        if (pipeRingMaterial != null)
        {
            Color ringTint = Color.Lerp(zone.CurrentPipeColor, new Color(0.5f, 0.4f, 0.3f), 0.4f);
            pipeRingMaterial.SetColor("_BaseColor", ringTint);
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
        _currentYaw += Random.Range(-maxTurn, maxTurn);
        _currentYaw *= 0.96f; // dampen to prevent tight spiraling

        // Vertical undulations (pitch) - hills and valleys
        _currentPitch += Random.Range(-maxPitch, maxPitch);
        _currentPitch *= 0.93f; // dampen to prevent going vertical
        _currentPitch = Mathf.Clamp(_currentPitch, -maxPitchAngle, maxPitchAngle);

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
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        // Deterministic seed per segment
        Random.State prev = Random.state;
        Random.InitState(Mathf.FloorToInt(segDist * 7.3f));

        int detailCount = Random.Range(2, 6); // 2-5 details per segment

        // More details further into the run
        if (segDist > 200f) detailCount += 1;
        if (segDist > 500f) detailCount += 2;

        for (int d = 0; d < detailCount; d++)
        {
            int nodeIdx = startNode + Random.Range(1, endNode - startNode - 1);
            if (nodeIdx >= _positions.Count) continue;

            Vector3 center = _positions[nodeIdx];
            Vector3 fwd = _forwards[nodeIdx];
            Vector3 refUp = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(fwd, refUp).normalized;
            Vector3 up = Vector3.Cross(right, fwd).normalized;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = pipeRadius * 0.90f; // safely inside pipe wall
            Vector3 pos = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            Vector3 inward = (center - pos).normalized;

            int detailType = Random.Range(0, 5);
            Color detailColor;
            Color detailEmission = Color.black;
            float detailSmooth = 0.2f;
            float detailMetal = 0.05f;

            switch (detailType)
            {
                case 0: // Rust streak
                    detailColor = new Color(
                        Random.Range(0.4f, 0.6f),
                        Random.Range(0.18f, 0.3f),
                        Random.Range(0.05f, 0.12f));
                    detailSmooth = 0.15f;
                    detailMetal = 0.3f;
                    break;
                case 1: // Slime patch
                    detailColor = new Color(
                        Random.Range(0.1f, 0.2f),
                        Random.Range(0.4f, 0.6f),
                        Random.Range(0.05f, 0.15f));
                    detailEmission = detailColor * 0.3f;
                    detailSmooth = 0.85f;
                    break;
                case 2: // Dark grime
                    detailColor = new Color(
                        Random.Range(0.08f, 0.15f),
                        Random.Range(0.06f, 0.12f),
                        Random.Range(0.04f, 0.08f));
                    detailSmooth = 0.1f;
                    break;
                case 3: // Moss/mold
                    detailColor = new Color(
                        Random.Range(0.15f, 0.25f),
                        Random.Range(0.3f, 0.4f),
                        Random.Range(0.08f, 0.15f));
                    detailEmission = detailColor * 0.15f;
                    detailSmooth = 0.4f;
                    break;
                default: // Mineral deposit / calcium buildup
                    detailColor = new Color(
                        Random.Range(0.6f, 0.75f),
                        Random.Range(0.55f, 0.7f),
                        Random.Range(0.45f, 0.55f));
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

            // Create detail geometry - larger patches for visibility
            GameObject detail = GameObject.CreatePrimitive(
                detailType == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
            detail.name = "PipeDetail";
            detail.transform.SetParent(parent.transform);
            detail.transform.position = pos;

            Quaternion surfaceRot = Quaternion.LookRotation(fwd, -inward);
            detail.transform.rotation = surfaceRot;

            // Flatten against wall - larger sizes for better visibility at speed
            float sizeX = Random.Range(0.5f, 1.8f);
            float sizeY = Random.Range(0.35f, 1.2f);
            float thickness = Random.Range(0.03f, 0.1f);
            detail.transform.localScale = new Vector3(sizeX, sizeY, thickness);

            detail.GetComponent<Renderer>().material = mat;
            Collider c = detail.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        Random.state = prev;
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

            for (int s = 0; s <= circumSegments; s++)
            {
                float angle = (float)s / circumSegments * Mathf.PI * 2f;
                int idx = r * vpr + s;
                verts[idx] = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
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

            Vector3 waterCenter = center + up * waterHeight;
            verts[r * 2] = waterCenter - right * waterWidth;
            verts[r * 2 + 1] = waterCenter + right * waterWidth;
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
}
