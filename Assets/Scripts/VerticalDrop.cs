using UnityEngine;

/// <summary>
/// Underwater zone trigger. The pipe floods and Mr. Corny free-falls into the water,
/// then floats in the center dodging obstacles with 2D arrow-key movement.
/// Filled with nasty floaties, floating poop buddies (billboard), and bubble effects.
/// Ends with a dramatic "plunge flush" that launches forward at super speed.
/// Spawned after 200m, every 400-600m.
/// </summary>
public class VerticalDrop : MonoBehaviour
{
    [Header("Underwater Settings")]
    public float swimDuration = 14f;          // total underwater time
    public float swimSpeed = 8f;              // gentle forward drift (slower than normal)
    public float moveRadius = 2.8f;           // 2D movement area (most of pipe cross-section)
    public float moveSpeed = 12f;             // floaty movement responsiveness
    public float plungeSpeedBoost = 2.0f;     // exit speed multiplier (BIG boost)
    public float plungeBoostDuration = 4f;    // how long the flush speed lasts

    [Header("Obstacle Spawning")]
    public int obstacleCount = 20;
    public float obstacleSpacing = 7f;
    public float obstacleRadius = 2.2f;

    [Header("Poop Buddies")]
    public int poopBuddyCount = 12;
    public float buddySpacing = 10f;

    [Header("Bubble Effects")]
    public int bubbleClusterCount = 15;

    [Header("Ring Spawning")]
    public int ringCount = 20;
    public float ringSpacing = 8f;
    public float ringRadius = 2f;
    public GameObject ringPrefab;

    private bool _triggered;

    // Shared materials for underwater objects
    private Material _poopMat;
    private Material _eyeWhiteMat;
    private Material _pupilMat;
    private Material _debrisMat;
    private Material _bubbleMat;
    private Material _floatyMat;
    private Shader _shader;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Pre-create materials
        Shader toonLit = Shader.Find("Custom/ToonLit");
        _shader = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (_shader == null) _shader = Shader.Find("Standard");

        _poopMat = MakeMat(new Color(0.4f, 0.25f, 0.1f), 0.05f, 0.65f);
        _eyeWhiteMat = MakeMat(new Color(0.95f, 0.95f, 0.92f), 0f, 0.8f);
        _pupilMat = MakeMat(new Color(0.05f, 0.05f, 0.05f), 0f, 0.9f);
        _debrisMat = MakeMat(new Color(0.4f, 0.3f, 0.18f), 0.1f, 0.2f);
        _floatyMat = MakeMat(new Color(0.35f, 0.28f, 0.15f), 0.05f, 0.3f);

        _bubbleMat = new Material(_shader);
        _bubbleMat.SetColor("_BaseColor", new Color(0.6f, 0.85f, 0.7f, 0.5f));
        _bubbleMat.SetFloat("_Smoothness", 0.95f);
        _bubbleMat.EnableKeyword("_EMISSION");
        _bubbleMat.SetColor("_EmissionColor", new Color(0.2f, 0.4f, 0.25f));
    }

    Material MakeMat(Color color, float metallic, float smooth)
    {
        Material m = new Material(_shader);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_ShadowColor"))
        {
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);
            m.SetColor("_ShadowColor", Color.HSVToRGB(h, Mathf.Min(s * 1.2f, 1f), v * 0.3f));
        }
        return m;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        TurdController tc = other.GetComponent<TurdController>();
        if (tc == null) return;

        _triggered = true;

        float startDist = tc.DistanceTraveled + 10f;

        // Spawn rings to collect
        SpawnDropRings(startDist);

        // Spawn poop buddies floating around (billboard obstacles)
        SpawnPoopBuddies(startDist);

        // Spawn nasty floaties and debris
        SpawnUnderwaterObstacles(startDist);

        // Spawn bubble clusters for atmosphere
        SpawnBubbleClusters(startDist);

        // Spawn the water wall visual (murky curtain)
        SpawnWaterWall(startDist, tc);

        // Tell TurdController to enter underwater mode
        tc.EnterDrop(swimDuration, swimSpeed, moveRadius, moveSpeed,
            plungeSpeedBoost, plungeBoostDuration);

        // Dramatic underwater entry - free-fall feeling
        if (PipeCamera.Instance != null)
        {
            PipeCamera.Instance.Shake(0.4f);
            PipeCamera.Instance.PunchFOV(-5f); // zoom in for plunge feel
        }

        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayWaterSplosh();

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowMilestone(tc.transform.position + Vector3.up * 2f, "DIVE!");

        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayWaterSplash(tc.transform.position);

        HapticManager.HeavyTap();
    }

    void SpawnDropRings(float startDist)
    {
        PipeGenerator pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (pipeGen == null || ringPrefab == null) return;

        for (int i = 0; i < ringCount; i++)
        {
            float dist = startDist + i * ringSpacing;
            Vector3 center, forward, right, up;
            pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

            float angle = (i * 137.5f) * Mathf.Deg2Rad;
            float r = ringRadius * (0.3f + 0.7f * ((i % 3) / 2f));
            Vector3 offset = right * Mathf.Cos(angle) * r + up * Mathf.Sin(angle) * r;

            Vector3 pos = center + offset;
            Quaternion rot = Quaternion.LookRotation(forward, up);
            Instantiate(ringPrefab, pos, rot, transform);
        }
    }

    // ===== POOP BUDDIES (billboard floating turds with googly eyes) =====

    void SpawnPoopBuddies(float startDist)
    {
        PipeGenerator pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (pipeGen == null) return;

        for (int i = 0; i < poopBuddyCount; i++)
        {
            float dist = startDist + 20f + i * buddySpacing + Random.Range(-3f, 3f);
            Vector3 center, forward, right, up;
            pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = Random.Range(0.5f, obstacleRadius);
            Vector3 offset = right * Mathf.Cos(angle) * r + up * Mathf.Sin(angle) * r;
            Vector3 pos = center + offset;

            GameObject buddy = CreateFloatingPoopBuddy(i % 4);
            buddy.transform.position = pos;
            buddy.transform.SetParent(transform);

            // Billboard: face camera, bob gently
            UnderwaterPoopBuddy bScript = buddy.AddComponent<UnderwaterPoopBuddy>();
            bScript.bobSpeed = Random.Range(0.8f, 1.8f);
            bScript.bobAmount = Random.Range(0.1f, 0.25f);
            bScript.spinSpeed = Random.Range(10f, 30f);
        }
    }

    GameObject CreateFloatingPoopBuddy(int faceType)
    {
        var root = new GameObject("UW_PoopBuddy");
        float scale = Random.Range(0.12f, 0.22f);

        // Classic poop emoji shape: 3 stacked spheres
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

        // Tip
        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Tip";
        tip.transform.SetParent(root.transform);
        tip.transform.localPosition = new Vector3(scale * 0.03f, scale * 0.78f, 0);
        tip.transform.localScale = new Vector3(scale * 0.2f, scale * 0.22f, scale * 0.2f);
        tip.GetComponent<Renderer>().material = _poopMat;
        Object.Destroy(tip.GetComponent<Collider>());

        // Big googly eyes
        float eyeSize = scale * 0.3f;
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = new GameObject(side < 0 ? "LeftEye" : "RightEye");
            eye.transform.SetParent(root.transform);
            eye.transform.localPosition = new Vector3(
                side * scale * 0.22f, scale * 0.5f, scale * 0.35f);

            var white = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            white.name = "White";
            white.transform.SetParent(eye.transform);
            white.transform.localPosition = Vector3.zero;
            white.transform.localScale = Vector3.one * eyeSize * 1.3f;
            white.GetComponent<Renderer>().material = _eyeWhiteMat;
            Object.Destroy(white.GetComponent<Collider>());

            var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil";
            pupil.transform.SetParent(eye.transform);
            pupil.transform.localPosition = new Vector3(0, 0, eyeSize * 0.35f);
            pupil.transform.localScale = Vector3.one * eyeSize * 0.55f;
            pupil.GetComponent<Renderer>().material = _pupilMat;
            Object.Destroy(pupil.GetComponent<Collider>());
        }

        // Face expression based on type
        switch (faceType)
        {
            case 0: // Surprised O face
                AddMouth(root.transform, scale, "O", _pupilMat);
                break;
            case 1: // Happy grin (capsule)
                AddMouth(root.transform, scale, "grin", _pupilMat);
                break;
            case 2: // Dead X eyes (replace pupils with X shapes)
                AddDeadEyes(root.transform, scale);
                break;
            default: // Scared wavy mouth
                AddMouth(root.transform, scale, "wavy", _pupilMat);
                break;
        }

        // Collider for hit detection
        SphereCollider sc = root.AddComponent<SphereCollider>();
        sc.radius = scale * 2.5f;
        sc.isTrigger = true;
        root.AddComponent<Obstacle>();

        return root;
    }

    void AddMouth(Transform parent, float scale, string type, Material mat)
    {
        var mouth = GameObject.CreatePrimitive(
            type == "O" ? PrimitiveType.Sphere : PrimitiveType.Capsule);
        mouth.name = "Mouth";
        mouth.transform.SetParent(parent);
        Object.Destroy(mouth.GetComponent<Collider>());
        mouth.GetComponent<Renderer>().material = mat;

        switch (type)
        {
            case "O":
                mouth.transform.localPosition = new Vector3(0, scale * 0.32f, scale * 0.42f);
                mouth.transform.localScale = new Vector3(scale * 0.15f, scale * 0.18f, scale * 0.08f);
                break;
            case "grin":
                mouth.transform.localPosition = new Vector3(0, scale * 0.28f, scale * 0.42f);
                mouth.transform.localScale = new Vector3(scale * 0.25f, scale * 0.06f, scale * 0.06f);
                mouth.transform.localRotation = Quaternion.Euler(0, 0, 90);
                break;
            default: // wavy
                mouth.transform.localPosition = new Vector3(0, scale * 0.3f, scale * 0.42f);
                mouth.transform.localScale = new Vector3(scale * 0.2f, scale * 0.04f, scale * 0.04f);
                mouth.transform.localRotation = Quaternion.Euler(0, 0, 90);
                break;
        }
    }

    void AddDeadEyes(Transform parent, float scale)
    {
        // Small X crosses over the eyes using thin cubes
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 eyePos = new Vector3(side * scale * 0.22f, scale * 0.55f, scale * 0.45f);
            float xs = scale * 0.12f;

            for (int d = 0; d < 2; d++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "DeadX";
                line.transform.SetParent(parent);
                line.transform.localPosition = eyePos;
                line.transform.localScale = new Vector3(xs, xs * 0.15f, xs * 0.15f);
                line.transform.localRotation = Quaternion.Euler(0, 0, d == 0 ? 45f : -45f);
                line.GetComponent<Renderer>().material = _pupilMat;
                Object.Destroy(line.GetComponent<Collider>());
            }
        }
    }

    // ===== NASTY FLOATIES & DEBRIS =====

    void SpawnUnderwaterObstacles(float startDist)
    {
        PipeGenerator pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (pipeGen == null) return;

        for (int i = 0; i < obstacleCount; i++)
        {
            float dist = startDist + 15f + i * obstacleSpacing;
            Vector3 center, forward, right, up;
            pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = Random.Range(0.3f, obstacleRadius);
            Vector3 offset = right * Mathf.Cos(angle) * r + up * Mathf.Sin(angle) * r;
            Vector3 pos = center + offset;

            int type = Random.Range(0, 5);
            GameObject obs = CreateFloaty(type);
            obs.transform.position = pos;
            obs.transform.rotation = Quaternion.LookRotation(forward, up)
                * Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(-20f, 20f), Random.Range(-180f, 180f));
            obs.transform.SetParent(transform);

            UnderwaterBob bob = obs.AddComponent<UnderwaterBob>();
            bob.bobSpeed = Random.Range(1f, 2.5f);
            bob.bobAmount = Random.Range(0.1f, 0.3f);
        }
    }

    GameObject CreateFloaty(int type)
    {
        GameObject obj;
        string name;

        switch (type)
        {
            case 0: // Floating brown chunk
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                name = "BrownChunk";
                float s = Random.Range(0.25f, 0.5f);
                obj.transform.localScale = new Vector3(s, s * 0.6f, s * 1.2f);
                obj.GetComponent<Renderer>().material = _debrisMat;
                break;

            case 1: // Toxic glob (green sphere)
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                name = "ToxicGlob";
                float gs = Random.Range(0.2f, 0.4f);
                obj.transform.localScale = Vector3.one * gs;
                Material toxicMat = MakeMat(
                    new Color(0.3f, 0.65f, 0.15f), 0.1f, 0.85f);
                toxicMat.EnableKeyword("_EMISSION");
                toxicMat.SetColor("_EmissionColor", new Color(0.2f, 0.45f, 0.1f));
                obj.GetComponent<Renderer>().material = toxicMat;
                break;

            case 2: // Sewage clump (multi-sphere)
                obj = new GameObject();
                name = "SewageClump";
                for (int c = 0; c < 3; c++)
                {
                    GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    blob.transform.SetParent(obj.transform, false);
                    float cs = Random.Range(0.12f, 0.3f);
                    blob.transform.localScale = Vector3.one * cs;
                    blob.transform.localPosition = Random.insideUnitSphere * 0.15f;
                    blob.GetComponent<Renderer>().material = _floatyMat;
                    Object.Destroy(blob.GetComponent<Collider>());
                }
                SphereCollider sCol = obj.AddComponent<SphereCollider>();
                sCol.radius = 0.3f;
                break;

            case 3: // Rusty pipe chunk
                obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                name = "PipeChunk";
                obj.transform.localScale = new Vector3(
                    Random.Range(0.1f, 0.2f),
                    Random.Range(0.25f, 0.6f),
                    Random.Range(0.1f, 0.2f));
                Material rustMat = MakeMat(new Color(0.5f, 0.35f, 0.2f), 0.6f, 0.3f);
                obj.GetComponent<Renderer>().material = rustMat;
                break;

            default: // Nasty tissue wad
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                name = "NastyTissue";
                float ts = Random.Range(0.15f, 0.35f);
                obj.transform.localScale = new Vector3(ts * 1.4f, ts * 0.3f, ts);
                Material tissueMat = MakeMat(
                    new Color(0.7f, 0.65f, 0.55f), 0f, 0.2f);
                obj.GetComponent<Renderer>().material = tissueMat;
                break;
        }

        obj.name = name;

        Collider col = obj.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        else
        {
            SphereCollider addCol = obj.AddComponent<SphereCollider>();
            addCol.isTrigger = true;
            addCol.radius = 0.3f;
        }

        obj.AddComponent<Obstacle>();
        return obj;
    }

    // ===== BUBBLE CLUSTERS =====

    void SpawnBubbleClusters(float startDist)
    {
        PipeGenerator pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (pipeGen == null) return;

        float totalSwimDist = swimSpeed * swimDuration;

        for (int i = 0; i < bubbleClusterCount; i++)
        {
            float dist = startDist + 5f + (totalSwimDist * 0.9f) * ((float)i / bubbleClusterCount);
            Vector3 center, forward, right, up;
            pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

            // Cluster of 4-8 bubbles at random positions
            int count = Random.Range(4, 9);
            for (int b = 0; b < count; b++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = Random.Range(0.2f, 2.8f);
                Vector3 offset = right * Mathf.Cos(angle) * r + up * Mathf.Sin(angle) * r;
                offset += forward * Random.Range(-2f, 2f);

                GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bubble.name = "Bubble";
                float bs = Random.Range(0.04f, 0.15f);
                bubble.transform.localScale = Vector3.one * bs;
                bubble.transform.position = center + offset;
                bubble.transform.SetParent(transform);
                bubble.GetComponent<Renderer>().material = _bubbleMat;
                Object.Destroy(bubble.GetComponent<Collider>());

                // Bubbles rise gently
                BubbleRise rise = bubble.AddComponent<BubbleRise>();
                rise.riseSpeed = Random.Range(0.2f, 0.8f);
                rise.wobble = Random.Range(0.05f, 0.15f);
                rise.lifetime = Random.Range(3f, 8f);
            }
        }
    }

    // ===== WATER WALL VISUAL (murky curtain at entry) =====

    void SpawnWaterWall(float startDist, TurdController tc)
    {
        PipeGenerator pipeGen = Object.FindFirstObjectByType<PipeGenerator>();
        if (pipeGen == null) return;

        Vector3 center, forward, right, up;
        pipeGen.GetPathFrame(startDist - 5f, out center, out forward, out right, out up);

        // Murky water wall - a large semi-transparent disc blocking the tunnel
        GameObject waterWall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waterWall.name = "WaterWall";
        waterWall.transform.SetParent(transform);
        waterWall.transform.position = center;
        waterWall.transform.rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(90, 0, 0);
        waterWall.transform.localScale = new Vector3(7f, 0.3f, 7f); // flat disc

        Material waterWallMat = new Material(_shader);
        waterWallMat.SetColor("_BaseColor", new Color(0.15f, 0.3f, 0.2f, 0.6f));
        waterWallMat.SetFloat("_Smoothness", 0.9f);
        waterWallMat.EnableKeyword("_EMISSION");
        waterWallMat.SetColor("_EmissionColor", new Color(0.08f, 0.2f, 0.1f));
        waterWallMat.SetFloat("_Cull", 0); // double-sided
        if (waterWallMat.HasProperty("_ShadowColor"))
            waterWallMat.SetColor("_ShadowColor", new Color(0.05f, 0.12f, 0.06f));
        waterWall.GetComponent<Renderer>().material = waterWallMat;
        Object.Destroy(waterWall.GetComponent<Collider>());

        // "DIVE!" sign above the water wall
        GameObject diveSign = new GameObject("DiveSign");
        diveSign.transform.SetParent(transform);
        diveSign.transform.position = center + up * 2.5f;
        diveSign.transform.rotation = Quaternion.LookRotation(-forward, up);
        TextMesh diveTM = diveSign.AddComponent<TextMesh>();
        diveTM.text = "DIVE!";
        diveTM.fontSize = 64;
        diveTM.characterSize = 0.12f;
        diveTM.alignment = TextAlignment.Center;
        diveTM.anchor = TextAnchor.MiddleCenter;
        diveTM.color = new Color(0.2f, 0.9f, 0.4f);
        diveTM.fontStyle = FontStyle.Bold;

        // Self-destruct the wall visual after player passes through
        Object.Destroy(waterWall, 3f);
    }
}

/// <summary>
/// Billboard poop buddy that floats underwater, always facing the camera.
/// Gentle bobbing and slow spin animation.
/// </summary>
public class UnderwaterPoopBuddy : MonoBehaviour
{
    public float bobSpeed = 1.2f;
    public float bobAmount = 0.15f;
    public float spinSpeed = 15f;
    private Vector3 _startPos;
    private float _phase;

    void Start()
    {
        _startPos = transform.position;
        _phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        // Billboard: face camera
        if (Camera.main != null)
        {
            Vector3 toCamera = Camera.main.transform.position - transform.position;
            toCamera.y = 0; // keep upright
            if (toCamera.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
            }
        }

        // Gentle bob
        float bob = Mathf.Sin((Time.time * bobSpeed) + _phase) * bobAmount;
        transform.position = _startPos + Vector3.up * bob;

        // Slow lazy spin on Y
        _startPos += Vector3.zero; // keep reference pos stable
    }
}

/// <summary>
/// Simple bobbing animation for underwater obstacles.
/// </summary>
public class UnderwaterBob : MonoBehaviour
{
    public float bobSpeed = 1.5f;
    public float bobAmount = 0.2f;
    private Vector3 _startPos;
    private float _phase;

    void Start()
    {
        _startPos = transform.position;
        _phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        float bob = Mathf.Sin((Time.time * bobSpeed) + _phase) * bobAmount;
        transform.position = _startPos + transform.up * bob;
        transform.Rotate(0, 15f * Time.deltaTime, 5f * Time.deltaTime, Space.Self);
    }
}

/// <summary>
/// Bubble rising behavior. Wobbles upward and pops after lifetime.
/// </summary>
public class BubbleRise : MonoBehaviour
{
    public float riseSpeed = 0.5f;
    public float wobble = 0.1f;
    public float lifetime = 5f;
    private float _age;
    private float _phase;

    void Start()
    {
        _phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        // Rise upward
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;
        // Side wobble
        float wobbleX = Mathf.Sin(Time.time * 2f + _phase) * wobble;
        float wobbleZ = Mathf.Cos(Time.time * 1.7f + _phase * 0.5f) * wobble;
        transform.position += new Vector3(wobbleX, 0, wobbleZ) * Time.deltaTime;

        // Grow slightly as they rise (pressure decreasing)
        float growFactor = 1f + (_age / lifetime) * 0.3f;
        transform.localScale = transform.localScale.normalized * (transform.localScale.x * growFactor);

        _age += Time.deltaTime;
        if (_age >= lifetime)
            Destroy(gameObject);
    }
}
