using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Real pipe fork with diverging branch tunnels. The pipe physically splits into
/// two separate tubes that diverge and rejoin. Each branch has its own path nodes,
/// tube mesh, water plane, and coordinate frame system.
/// Branch 0 = Safe (left, green), Branch 1 = Risky (right, red).
/// Smooth procedural Y-junction meshes at entry and exit.
/// </summary>
public class PipeFork : MonoBehaviour
{
    public static List<PipeFork> ActiveForks = new List<PipeFork>();

    public enum BranchType { Safe, Risky }

    [System.Serializable]
    public class Branch
    {
        public BranchType type;
        public float lateralSign;         // -1 = left, +1 = right
        public float coinMultiplier;
        public float obstacleMultiplier;
    }

    [Header("Fork Settings")]
    public float forkDistance;           // distance along main pipe where fork starts
    public float rejoinDistance;         // distance where fork zone ends
    public float branchLength = 60f;
    public float maxSeparation = 3.0f;  // peak lateral offset from main path
    public float branchPipeRadius = 2.8f;

    public List<Branch> branches = new List<Branch>();

    private bool _playerAssigned;
    private int _playerBranch = -1;

    // Branch path data: sampled positions/forwards for each branch
    private Vector3[][] _branchPositions;  // [branchIdx][sampleIdx]
    private Vector3[][] _branchForwards;
    private int _sampleCount;
    private float _sampleSpacing = 2f;

    // Transition zones
    private const float TRANSITION_DIST = 30f; // meters to blend between main/branch (smooth)

    // Y-junction geometry
    private const float ENTRY_JUNCTION_LENGTH = 18f;
    private const float EXIT_JUNCTION_LENGTH = 16f;
    private const int JUNCTION_RINGS = 8;
    private const int JUNCTION_CIRC_SEGS = 16;

    public int PlayerBranch => _playerBranch;
    public bool IsPlayerInFork => _playerBranch >= 0;

    void OnEnable()
    {
        if (!ActiveForks.Contains(this))
            ActiveForks.Add(this);
    }

    void OnDisable()
    {
        ActiveForks.Remove(this);
    }

    /// <summary>Set up a 2-branch fork at the given distance.</summary>
    public void Setup(float distance, float pipeRadius, PipeGenerator pipeGen)
    {
        forkDistance = distance;
        branchLength = Random.Range(50f, 80f);
        rejoinDistance = forkDistance + branchLength;

        // Left branch (safe)
        Branch left = new Branch
        {
            type = BranchType.Safe,
            lateralSign = -1f,
            coinMultiplier = 0.6f,
            obstacleMultiplier = 0.5f
        };

        // Right branch (risky)
        Branch right = new Branch
        {
            type = BranchType.Risky,
            lateralSign = 1f,
            coinMultiplier = 2.0f,
            obstacleMultiplier = 1.8f
        };

        branches.Add(left);
        branches.Add(right);

        // Generate real branch paths
        GenerateBranchPaths(pipeGen);

        // Build visual geometry
        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader shader = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        BuildBranchTubes(shader);
        BuildYJunction(pipeGen, pipeRadius, shader, true);  // entry
        BuildYJunction(pipeGen, pipeRadius, shader, false); // exit
    }

    // ===== BRANCH PATH GENERATION =====

    void GenerateBranchPaths(PipeGenerator pipeGen)
    {
        _sampleCount = Mathf.CeilToInt(branchLength / _sampleSpacing) + 1;
        _branchPositions = new Vector3[2][];
        _branchForwards = new Vector3[2][];

        for (int b = 0; b < 2; b++)
        {
            _branchPositions[b] = new Vector3[_sampleCount];
            _branchForwards[b] = new Vector3[_sampleCount];
        }

        for (int i = 0; i < _sampleCount; i++)
        {
            float t = (float)i / (_sampleCount - 1); // 0..1 along fork zone
            float dist = forkDistance + t * branchLength;

            Vector3 center, fwd, right, up;
            pipeGen.GetPathFrame(dist, out center, out fwd, out right, out up);

            // Separation curve: sin(t * PI) gives smooth diverge → peak → converge
            float separation = Mathf.Sin(t * Mathf.PI) * maxSeparation;

            for (int b = 0; b < 2; b++)
            {
                float lateralSign = branches[b].lateralSign;
                Vector3 offset = right * (lateralSign * separation);
                _branchPositions[b][i] = center + offset;
                _branchForwards[b][i] = fwd; // branches follow main path curvature
            }
        }

        // Smooth the forward vectors by computing them from position deltas
        for (int b = 0; b < 2; b++)
        {
            for (int i = 0; i < _sampleCount; i++)
            {
                if (i < _sampleCount - 1)
                {
                    Vector3 delta = _branchPositions[b][i + 1] - _branchPositions[b][i];
                    if (delta.sqrMagnitude > 0.001f)
                        _branchForwards[b][i] = delta.normalized;
                }
                else if (i > 0)
                {
                    _branchForwards[b][i] = _branchForwards[b][i - 1];
                }
            }
        }
    }

    // ===== BRANCH FRAME QUERIES =====

    /// <summary>
    /// Get the coordinate frame for a branch at a given distance along the main path.
    /// Returns true if the distance is within the fork zone and data is valid.
    /// </summary>
    public bool GetBranchFrame(int branchIdx, float distance,
        out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        position = Vector3.zero;
        forward = Vector3.forward;
        right = Vector3.right;
        up = Vector3.up;

        if (_branchPositions == null || branchIdx < 0 || branchIdx >= 2)
            return false;

        if (distance < forkDistance || distance > rejoinDistance)
            return false;

        // Map distance to sample index
        float t = (distance - forkDistance) / branchLength;
        t = Mathf.Clamp01(t);
        float fIdx = t * (_sampleCount - 1);
        int idx = Mathf.FloorToInt(fIdx);
        float frac = fIdx - idx;

        idx = Mathf.Clamp(idx, 0, _sampleCount - 2);

        // Interpolate between samples
        position = Vector3.Lerp(_branchPositions[branchIdx][idx],
            _branchPositions[branchIdx][idx + 1], frac);
        forward = Vector3.Slerp(_branchForwards[branchIdx][idx],
            _branchForwards[branchIdx][idx + 1], frac).normalized;

        // Compute right and up from forward
        Vector3 refUp = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f)
            refUp = Vector3.forward;
        right = Vector3.Cross(forward, refUp).normalized;
        up = Vector3.Cross(right, forward).normalized;

        return true;
    }

    /// <summary>
    /// Get the blend factor for transitioning between main path and branch path.
    /// Returns 0 at fork entry/exit (use main path), 1 in the middle (use branch path).
    /// Smooth transition over TRANSITION_DIST meters.
    /// </summary>
    public float GetBranchBlend(float distance)
    {
        if (distance < forkDistance || distance > rejoinDistance)
            return 0f;

        float distIntoFork = distance - forkDistance;
        float distFromEnd = rejoinDistance - distance;

        // Ramp up over first TRANSITION_DIST, ramp down over last TRANSITION_DIST
        float entryBlend = Mathf.Clamp01(distIntoFork / TRANSITION_DIST);
        float exitBlend = Mathf.Clamp01(distFromEnd / TRANSITION_DIST);

        float raw = Mathf.Min(entryBlend, exitBlend);
        // Smoothstep for ease-in-ease-out (no sudden speed changes)
        return raw * raw * (3f - 2f * raw);
    }

    // ===== PLAYER/AI ASSIGNMENT =====

    /// <summary>
    /// Determine which branch based on circumferential angle.
    /// Left side of pipe bottom = Safe (branch 0), Right side = Risky (branch 1).
    /// </summary>
    public int GetBranchForAngle(float circumAngle)
    {
        float delta = Mathf.DeltaAngle(circumAngle, 270f);
        return delta >= 0 ? 0 : 1;
    }

    public float GetCoinMultiplier()
    {
        if (_playerBranch < 0 || _playerBranch >= branches.Count) return 1f;
        return branches[_playerBranch].coinMultiplier;
    }

    public float GetObstacleMultiplier()
    {
        if (_playerBranch < 0 || _playerBranch >= branches.Count) return 1f;
        return branches[_playerBranch].obstacleMultiplier;
    }

    public void AssignPlayer(float playerAngle)
    {
        if (_playerAssigned) return;
        _playerAssigned = true;
        _playerBranch = GetBranchForAngle(playerAngle);

        string branchName = _playerBranch == 0 ? "SAFE" : "RISKY";
        Debug.Log($"TTR: Player chose {branchName} fork at {forkDistance:F0}m (angle={playerAngle:F0})");

        if (ScorePopup.Instance != null)
        {
            string msg = _playerBranch == 0 ? "SAFE ROUTE!" : "RISKY ROUTE!";
            ScorePopup.Instance.ShowMilestone(
                Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 5f : Vector3.zero,
                msg);
        }
    }

    public int GetAIBranch(float aggression)
    {
        return Random.value < aggression ? 1 : 0;
    }

    public void ResetPlayerBranch()
    {
        _playerAssigned = false;
        _playerBranch = -1;
    }

    // ===== BRANCH TUBE MESH BUILDING =====

    void BuildBranchTubes(Shader shader)
    {
        if (_branchPositions == null) return;

        // Figure out how many samples the Y-junctions consume
        int entrySkip = Mathf.CeilToInt(ENTRY_JUNCTION_LENGTH / _sampleSpacing);
        int exitSkip = Mathf.CeilToInt(EXIT_JUNCTION_LENGTH / _sampleSpacing);
        entrySkip = Mathf.Min(entrySkip, _sampleCount / 3);
        exitSkip = Mathf.Min(exitSkip, _sampleCount / 3);

        Color[] branchTints = {
            new Color(0.28f, 0.32f, 0.22f), // greenish (safe)
            new Color(0.35f, 0.24f, 0.2f)   // reddish (risky)
        };
        Color[] waterTints = {
            new Color(0.18f, 0.35f, 0.12f), // green water
            new Color(0.32f, 0.2f, 0.1f)    // brown water
        };

        for (int b = 0; b < 2; b++)
        {
            // Pipe tube
            Material pipeMat = new Material(shader);
            pipeMat.SetColor("_BaseColor", branchTints[b]);
            pipeMat.SetFloat("_Metallic", 0.05f);
            pipeMat.SetFloat("_Smoothness", 0.3f);
            pipeMat.EnableKeyword("_EMISSION");
            pipeMat.SetColor("_EmissionColor", branchTints[b] * 0.08f);
            if (pipeMat.HasProperty("_ShadowColor"))
            {
                float h, s, v;
                Color.RGBToHSV(branchTints[b], out h, out s, out v);
                pipeMat.SetColor("_ShadowColor", Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.3f));
            }

            GameObject tube = BuildTubeMesh(b, pipeMat, entrySkip, exitSkip);
            tube.transform.SetParent(transform);

            // Water plane inside branch
            Material waterMat = new Material(shader);
            waterMat.SetColor("_BaseColor", waterTints[b]);
            waterMat.SetFloat("_Metallic", 0.6f);
            waterMat.SetFloat("_Smoothness", 0.95f);
            waterMat.EnableKeyword("_EMISSION");
            waterMat.SetColor("_EmissionColor", waterTints[b] * 0.15f);
            waterMat.SetFloat("_Cull", 0);
            if (waterMat.HasProperty("_ShadowColor"))
            {
                float wh, ws, wv;
                Color.RGBToHSV(waterTints[b], out wh, out ws, out wv);
                waterMat.SetColor("_ShadowColor", Color.HSVToRGB(wh, Mathf.Min(ws * 1.3f, 1f), wv * 0.25f));
            }

            GameObject water = BuildBranchWater(b, waterMat);
            water.transform.SetParent(tube.transform);
        }
    }

    GameObject BuildTubeMesh(int branchIdx, Material mat, int entrySkip, int exitSkip)
    {
        // Branch tubes start after the entry junction and end before the exit junction
        int startSample = Mathf.Max(0, entrySkip - 1); // overlap 1 ring for seamless join
        int endSample = Mathf.Min(_sampleCount - 1, _sampleCount - exitSkip + 1);
        int ringCount = endSample - startSample + 1;
        if (ringCount < 2) ringCount = _sampleCount; // fallback: full tube if junction is too long

        int circumSegs = 16;
        int vpr = circumSegs + 1; // verts per ring

        Vector3[] verts = new Vector3[vpr * ringCount];
        Vector2[] uvs = new Vector2[vpr * ringCount];
        int[] tris = new int[circumSegs * (ringCount - 1) * 6];

        float radius = branchPipeRadius;

        for (int r = 0; r < ringCount; r++)
        {
            int sampleIdx = startSample + r;
            if (sampleIdx >= _sampleCount) sampleIdx = _sampleCount - 1;

            Vector3 center = _branchPositions[branchIdx][sampleIdx];
            Vector3 fwd = _branchForwards[branchIdx][sampleIdx];
            Vector3 refUp = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(fwd, refUp).normalized;
            Vector3 up = Vector3.Cross(right, fwd).normalized;

            // Flare radius at exit end for a mouth-like opening
            float tTotal = (float)sampleIdx / (_sampleCount - 1);
            float flare = 1f;
            if (r == 0)
                flare = 1.05f; // slight flare to overlap with junction
            else if (tTotal > 0.92f)
                flare = Mathf.Lerp(1f, 1.4f, (tTotal - 0.92f) / 0.08f);

            float ringRadius = radius * flare;

            for (int s = 0; s <= circumSegs; s++)
            {
                float angle = (float)s / circumSegs * Mathf.PI * 2f;
                int idx = r * vpr + s;
                verts[idx] = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * ringRadius;
                uvs[idx] = new Vector2((float)s / circumSegs, (float)r / (ringCount - 1) * 3f);
            }
        }

        int tri = 0;
        for (int r = 0; r < ringCount - 1; r++)
        {
            for (int s = 0; s < circumSegs; s++)
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
        mesh.name = $"BranchPipe_{branchIdx}";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        // Flip normals inward (player is inside)
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
            normals[i] = -normals[i];
        mesh.normals = normals;

        GameObject go = new GameObject($"BranchTube_{branchIdx}");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mf.mesh = mesh;
        mr.material = mat;

        return go;
    }

    GameObject BuildBranchWater(int branchIdx, Material mat)
    {
        int ringCount = _sampleCount;
        Vector3[] verts = new Vector3[ringCount * 2];
        Vector2[] uvs = new Vector2[ringCount * 2];
        int[] tris = new int[(ringCount - 1) * 6];

        float waterWidth = branchPipeRadius * 0.85f;
        float waterHeight = -branchPipeRadius * 0.75f;

        for (int r = 0; r < ringCount; r++)
        {
            Vector3 center = _branchPositions[branchIdx][r];
            Vector3 fwd = _branchForwards[branchIdx][r];
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
            tris[tri++] = bl; tris[tri++] = br; tris[tri++] = tl;
            tris[tri++] = br; tris[tri++] = tr; tris[tri++] = tl;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"BranchWater_{branchIdx}";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        GameObject go = new GameObject($"BranchWater_{branchIdx}");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mf.mesh = mesh;
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return go;
    }

    // ===== Y-JUNCTION MESH =====

    /// <summary>
    /// Build a smooth Y-junction mesh that transitions from 1 circle (main pipe)
    /// to 2 circles (branch pipes). Entry = 1→2, Exit = 2→1 (reversed).
    /// Builds 2 half-meshes (one per arm) with color tinting.
    /// </summary>
    void BuildYJunction(PipeGenerator pipeGen, float mainPipeRadius, Shader shader, bool isEntry)
    {
        float junctionLength = isEntry ? ENTRY_JUNCTION_LENGTH : EXIT_JUNCTION_LENGTH;
        int rings = JUNCTION_RINGS;
        int circumSegs = JUNCTION_CIRC_SEGS;

        // Start/end distances
        float startDist, endDist;
        if (isEntry)
        {
            startDist = forkDistance;
            endDist = forkDistance + junctionLength;
        }
        else
        {
            startDist = rejoinDistance - junctionLength;
            endDist = rejoinDistance;
        }

        Color[] armTints = {
            new Color(0.25f, 0.35f, 0.2f),  // green (safe/left)
            new Color(0.38f, 0.22f, 0.18f)  // red (risky/right)
        };

        for (int arm = 0; arm < 2; arm++)
        {
            float lateralSign = branches[arm].lateralSign; // -1 left, +1 right

            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", armTints[arm]);
            mat.SetFloat("_Metallic", 0.05f);
            mat.SetFloat("_Smoothness", 0.3f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", armTints[arm] * 0.1f);
            if (mat.HasProperty("_ShadowColor"))
            {
                float h, s, v;
                Color.RGBToHSV(armTints[arm], out h, out s, out v);
                mat.SetColor("_ShadowColor", Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.3f));
            }

            GameObject armGo = BuildJunctionArm(pipeGen, mainPipeRadius, arm, lateralSign,
                startDist, endDist, rings, circumSegs, isEntry);
            armGo.transform.SetParent(transform);

            MeshRenderer mr = armGo.GetComponent<MeshRenderer>();
            mr.material = mat;
        }

        // Build septum (thin divider strip between the two arms at their shared seam)
        BuildSeptum(pipeGen, mainPipeRadius, startDist, endDist, rings, isEntry, shader);
    }

    /// <summary>
    /// Build one arm of the Y-junction. Each arm covers half the main pipe circle
    /// at the start and a full branch circle at the end.
    /// For entry: blend factor goes 0→1 (main→branch).
    /// For exit: blend factor goes 1→0 (branch→main).
    /// </summary>
    GameObject BuildJunctionArm(PipeGenerator pipeGen, float mainPipeRadius,
        int armIdx, float lateralSign, float startDist, float endDist,
        int rings, int circumSegs, bool isEntry)
    {
        int vpr = circumSegs + 1;
        Vector3[] verts = new Vector3[vpr * rings];
        Vector2[] uvs = new Vector2[vpr * rings];
        int[] tris = new int[circumSegs * (rings - 1) * 6];

        float mainR = mainPipeRadius;
        float branchR = branchPipeRadius;

        // Angular ranges for each arm on the main circle:
        // Left arm (lateralSign=-1): covers angles PI/2 to 3*PI/2 (left half)
        // Right arm (lateralSign=+1): covers angles -PI/2 to PI/2 (right half)
        float mainStartAngle, mainEndAngle;
        if (lateralSign < 0)
        {
            // Left arm: top (PI/2) around to bottom (3*PI/2) on the left side
            mainStartAngle = Mathf.PI * 0.5f;
            mainEndAngle = Mathf.PI * 1.5f;
        }
        else
        {
            // Right arm: bottom (-PI/2 = 3*PI/2) around to top (PI/2) on the right side
            mainStartAngle = -Mathf.PI * 0.5f;
            mainEndAngle = Mathf.PI * 0.5f;
        }

        for (int r = 0; r < rings; r++)
        {
            float ringT = (float)r / (rings - 1); // 0→1 along junction

            // Blend factor: how much we've transitioned from main to branch
            float blend = isEntry ? SmoothStep(ringT) : SmoothStep(1f - ringT);

            // Distance along pipe at this ring
            float dist = Mathf.Lerp(startDist, endDist, ringT);

            // Get main pipe frame
            Vector3 mainCenter, mainFwd, mainRight, mainUp;
            pipeGen.GetPathFrame(dist, out mainCenter, out mainFwd, out mainRight, out mainUp);

            // Get branch center: lerp laterally based on the separation curve
            float tInFork = (dist - forkDistance) / branchLength;
            tInFork = Mathf.Clamp01(tInFork);
            float separation = Mathf.Sin(tInFork * Mathf.PI) * maxSeparation;
            Vector3 branchCenter = mainCenter + mainRight * (lateralSign * separation);

            // Interpolate center position
            Vector3 center = Vector3.Lerp(mainCenter, branchCenter, blend);

            // Interpolate radius
            float radius = Mathf.Lerp(mainR, branchR, blend);

            for (int s = 0; s <= circumSegs; s++)
            {
                float segT = (float)s / circumSegs; // 0→1 around circumference

                // Source position: point on half of main circle
                float srcAngle = Mathf.Lerp(mainStartAngle, mainEndAngle, segT);
                Vector3 srcPos = mainCenter
                    + (mainRight * Mathf.Cos(srcAngle) + mainUp * Mathf.Sin(srcAngle)) * mainR;

                // Target position: point on full branch circle
                float tgtAngle = segT * Mathf.PI * 2f;
                Vector3 tgtPos = branchCenter
                    + (mainRight * Mathf.Cos(tgtAngle) + mainUp * Mathf.Sin(tgtAngle)) * branchR;

                // Blend between source and target
                int idx = r * vpr + s;
                verts[idx] = Vector3.Lerp(srcPos, tgtPos, blend);
                uvs[idx] = new Vector2(segT, ringT * 2f);
            }
        }

        // Build triangles
        int tri = 0;
        for (int r = 0; r < rings - 1; r++)
        {
            for (int s = 0; s < circumSegs; s++)
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
        mesh.name = $"YJunction_{(isEntry ? "Entry" : "Exit")}_{armIdx}";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        // Flip normals inward (player is inside)
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
            normals[i] = -normals[i];
        mesh.normals = normals;

        GameObject go = new GameObject(mesh.name);
        go.AddComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshRenderer>();

        return go;
    }

    /// <summary>
    /// Build a thin septum strip along the shared edge between the two arms.
    /// This is a narrow double-sided wall that grows from nothing at the main pipe end
    /// to the full height separating the two branch centers.
    /// </summary>
    void BuildSeptum(PipeGenerator pipeGen, float mainPipeRadius,
        float startDist, float endDist, int rings, bool isEntry, Shader shader)
    {
        // Septum: a thin strip running along the center divider
        // At blend=0 it has zero width, at blend=1 it spans between the two branch centers
        Vector3[] verts = new Vector3[rings * 4]; // top-left, top-right, bot-left, bot-right per ring
        Vector2[] uvs = new Vector2[rings * 4];
        List<int> triList = new List<int>();

        for (int r = 0; r < rings; r++)
        {
            float ringT = (float)r / (rings - 1);
            float blend = isEntry ? SmoothStep(ringT) : SmoothStep(1f - ringT);

            float dist = Mathf.Lerp(startDist, endDist, ringT);
            Vector3 mainCenter, mainFwd, mainRight, mainUp;
            pipeGen.GetPathFrame(dist, out mainCenter, out mainFwd, out mainRight, out mainUp);

            float tInFork = (dist - forkDistance) / branchLength;
            tInFork = Mathf.Clamp01(tInFork);
            float separation = Mathf.Sin(tInFork * Mathf.PI) * maxSeparation;

            // Septum height scales with blend
            float septumHeight = Mathf.Lerp(0f, branchPipeRadius * 0.8f, blend);
            // Septum sits at the main center, extending up and down
            Vector3 septumCenter = mainCenter;

            int baseIdx = r * 4;
            // Front face (two verts) and back face (two verts)
            verts[baseIdx + 0] = septumCenter + mainUp * septumHeight; // top-front
            verts[baseIdx + 1] = septumCenter - mainUp * septumHeight; // bot-front
            verts[baseIdx + 2] = septumCenter + mainUp * septumHeight; // top-back (same pos, diff normal)
            verts[baseIdx + 3] = septumCenter - mainUp * septumHeight; // bot-back

            uvs[baseIdx + 0] = new Vector2(0f, ringT);
            uvs[baseIdx + 1] = new Vector2(1f, ringT);
            uvs[baseIdx + 2] = new Vector2(0f, ringT);
            uvs[baseIdx + 3] = new Vector2(1f, ringT);
        }

        // Build quads between rings (double-sided)
        for (int r = 0; r < rings - 1; r++)
        {
            int c = r * 4;
            int n = (r + 1) * 4;

            // Front face (faces left/right arm - using right-hand rule)
            triList.Add(c + 0); triList.Add(n + 0); triList.Add(c + 1);
            triList.Add(c + 1); triList.Add(n + 0); triList.Add(n + 1);

            // Back face (opposite winding)
            triList.Add(c + 2); triList.Add(c + 3); triList.Add(n + 2);
            triList.Add(c + 3); triList.Add(n + 3); triList.Add(n + 2);
        }

        if (triList.Count == 0) return;

        Mesh mesh = new Mesh();
        mesh.name = $"YSeptum_{(isEntry ? "Entry" : "Exit")}";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = triList.ToArray();
        mesh.RecalculateNormals();

        Material septumMat = new Material(shader);
        septumMat.SetColor("_BaseColor", new Color(0.25f, 0.22f, 0.18f));
        septumMat.SetFloat("_Metallic", 0.2f);
        septumMat.SetFloat("_Smoothness", 0.2f);
        if (septumMat.HasProperty("_ShadowColor"))
            septumMat.SetColor("_ShadowColor", new Color(0.1f, 0.08f, 0.06f));

        GameObject go = new GameObject(mesh.name);
        go.transform.SetParent(transform);
        go.AddComponent<MeshFilter>().mesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.material = septumMat;
    }

    float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
