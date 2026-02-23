using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Sewer Tour mode — slow ride through the sewer to inspect all assets.
/// Auto-advances along the pipe path. Player visible but invincible.
/// Spawns a curated showcase of every object type with debug labels and bounding boxes.
/// Left/right input rotates camera angle for free-look around the pipe.
/// </summary>
public class SewerTour : MonoBehaviour
{
    public static SewerTour Instance { get; private set; }

    [Header("Tour Settings")]
    public float tourSpeed = 2f;
    public float lookRotateSpeed = 90f; // degrees per second
    public float pipeRadius = 3.5f;

    [Header("References")]
    public TurdController player;
    public PipeGenerator pipeGen;

    [Header("Prefab Lists (set by bootstrapper)")]
    public GameObject[] obstaclePrefabs;
    public GameObject coinPrefab;
    public GameObject bonusCoinPrefab;
    public GameObject speedBoostPrefab;
    public GameObject jumpRampPrefab;
    public GameObject bigAirRampPrefab;
    public GameObject gratePrefab;
    public GameObject dropZonePrefab;
    public GameObject squirtPrefab;
    public GameObject shieldPrefab;
    public GameObject magnetPrefab;
    public GameObject slowMoPrefab;
    public GameObject[] sceneryPrefabs;
    public GameObject[] grossPrefabs;
    public GameObject[] signPrefabs;

    // Tour state
    private bool _active = false;
    private float _tourDist = 5f;
    private float _lookAngle = 0f; // extra yaw offset for free-look
    private List<GameObject> _spawned = new List<GameObject>();
    private List<GameObject> _labels = new List<GameObject>();
    private bool _hasSpawnedShowcase = false;

    // Audio cue tracking
    private string _lastCueCategory = "";
    private float _lastCueTime;

    // UI
    private Canvas _labelCanvas;
    private Text _headerText;
    private Text _currentObjText;
    private Text _distText;
    private Button _exitButton;
    private Font _font;

    // Label tracking for screen projection
    struct LabelInfo
    {
        public Transform target;
        public RectTransform rt;
        public Text text;
    }
    private List<LabelInfo> _screenLabels = new List<LabelInfo>();

    void Awake()
    {
        Instance = this;
        enabled = false; // disabled until StartTour called
    }

    public void StartTour()
    {
        _active = true;
        enabled = true;
        _tourDist = 5f;
        _lookAngle = 0f;
        _hasSpawnedShowcase = false;

        // Disable normal spawners
        DisableSpawner<ObstacleSpawner>();
        DisableSpawner<PowerUpSpawner>();
        DisableSpawner<ScenerySpawner>();
        DisableSpawner<WaterCreatureSpawner>();

        // Disable race system
        if (RaceManager.Instance != null)
            RaceManager.Instance.enabled = false;

        // Disable AI racers
        foreach (var ai in Object.FindObjectsByType<RacerAI>(FindObjectsSortMode.None))
        {
            ai.enabled = false;
            ai.gameObject.SetActive(false);
        }
        foreach (var ai in Object.FindObjectsByType<SmoothSnakeAI>(FindObjectsSortMode.None))
        {
            ai.enabled = false;
            ai.gameObject.SetActive(false);
        }

        // Make player invincible and slow
        if (player != null)
        {
            player.forwardSpeed = tourSpeed;
            player.maxSpeed = tourSpeed;
            player.acceleration = 0f;
        }

        // Audio: fade out race music, play entry chime, start ambient
        if (ProceduralAudio.Instance != null)
        {
            ProceduralAudio.Instance.FadeOutMusic(0.5f);
            ProceduralAudio.Instance.PlayTourEntry();
            ProceduralAudio.Instance.StartTourAmbient();
        }

        // Build tour HUD
        BuildTourUI();

        // Spawn the curated showcase
        SpawnShowcase();
    }

    void DisableSpawner<T>() where T : MonoBehaviour
    {
        T spawner = Object.FindFirstObjectByType<T>();
        if (spawner != null)
            spawner.enabled = false;
    }

    void Update()
    {
        if (!_active || pipeGen == null) return;

        // Track player distance
        if (player != null)
            _tourDist = player.DistanceTraveled;

        // Update distance display
        if (_distText != null)
            _distText.text = $"{Mathf.FloorToInt(_tourDist)}m";

        // Find nearest showcase object for current-object display
        UpdateNearestLabel();

        // Project 3D labels to screen
        UpdateScreenLabels();
    }

    // === SHOWCASE SPAWNING ===

    void SpawnShowcase()
    {
        if (_hasSpawnedShowcase) return;
        _hasSpawnedShowcase = true;

        float dist = 20f; // start spawning at 20m
        float spacing = 16f; // space between showcase items (generous for inspection)

        // OBSTACLES
        if (obstaclePrefabs != null)
        {
            for (int i = 0; i < obstaclePrefabs.Length; i++)
            {
                if (obstaclePrefabs[i] == null) continue;
                string name = obstaclePrefabs[i].name.Replace("_Gross", "").Replace("_", " ");
                SpawnShowcaseItem(obstaclePrefabs[i], dist, 270f, 0.6f, name, "Obstacle");
                dist += spacing;
            }
        }

        // COINS
        if (coinPrefab != null)
        {
            SpawnShowcaseItem(coinPrefab, dist, 270f, 0.72f, "Fartcoin", "Collectible");
            dist += spacing;
        }
        if (bonusCoinPrefab != null)
        {
            SpawnShowcaseItem(bonusCoinPrefab, dist, 270f, 0.5f, "Bonus Fartcoin", "Collectible");
            dist += spacing;
        }

        // POWER-UPS
        if (speedBoostPrefab != null)
        {
            SpawnShowcaseItem(speedBoostPrefab, dist, 270f, 0.82f, "Speed Boost", "PowerUp");
            dist += spacing;
        }
        if (jumpRampPrefab != null)
        {
            SpawnShowcaseItem(jumpRampPrefab, dist, 270f, 0.82f, "Jump Ramp", "PowerUp");
            dist += spacing;
        }
        if (shieldPrefab != null)
        {
            SpawnShowcaseItem(shieldPrefab, dist, 270f, 0.82f, "Shield", "PowerUp");
            dist += spacing;
        }
        if (magnetPrefab != null)
        {
            SpawnShowcaseItem(magnetPrefab, dist, 270f, 0.82f, "Magnet", "PowerUp");
            dist += spacing;
        }
        if (slowMoPrefab != null)
        {
            SpawnShowcaseItem(slowMoPrefab, dist, 270f, 0.82f, "Slow-Mo", "PowerUp");
            dist += spacing;
        }

        // SPECIAL EVENTS
        if (bigAirRampPrefab != null)
        {
            SpawnShowcaseItem(bigAirRampPrefab, dist, 270f, 0.65f, "Big Air Ramp", "Event");
            dist += spacing;
        }
        if (gratePrefab != null)
        {
            SpawnShowcaseItem(gratePrefab, dist, 0f, 0f, "Grate", "Event");
            dist += spacing;
        }
        if (dropZonePrefab != null)
        {
            SpawnShowcaseItem(dropZonePrefab, dist, 0f, 0f, "Drop Zone", "Event");
            dist += spacing;
        }

        // SCENERY PROPS
        if (sceneryPrefabs != null)
        {
            for (int i = 0; i < sceneryPrefabs.Length; i++)
            {
                if (sceneryPrefabs[i] == null) continue;
                string name = sceneryPrefabs[i].name.Replace("_Scenery", "").Replace("_", " ");
                // Scenery on walls/ceiling
                float angle = (i % 3 == 0) ? 90f : (i % 3 == 1) ? 180f : 0f;
                SpawnShowcaseItem(sceneryPrefabs[i], dist, angle, 0.75f, name, "Scenery");
                dist += spacing * 0.7f;
            }
        }

        // GROSS DECOR
        if (grossPrefabs != null)
        {
            for (int i = 0; i < grossPrefabs.Length; i++)
            {
                if (grossPrefabs[i] == null) continue;
                string name = grossPrefabs[i].name.Replace("_Gross", "").Replace("_", " ");
                SpawnShowcaseItem(grossPrefabs[i], dist, 180f + i * 45f, 0.75f, name, "Decor");
                dist += spacing * 0.7f;
            }
        }

        // SIGNS / GRAFFITI
        if (signPrefabs != null)
        {
            for (int i = 0; i < signPrefabs.Length; i++)
            {
                if (signPrefabs[i] == null) continue;
                string name = signPrefabs[i].name.Replace("_Gross_", " #").Replace("_", " ");
                float angle = (i % 2 == 0) ? 180f : 0f; // alternate left/right walls
                SpawnShowcaseItem(signPrefabs[i], dist, angle, 0.93f, name, "Sign");
                dist += spacing * 0.6f;
            }
        }

        // WATER CREATURES
        if (squirtPrefab != null)
        {
            SpawnShowcaseItem(squirtPrefab, dist, 270f, 0.82f, "Sewer Squirt", "Creature");
            dist += spacing;
        }

        Debug.Log($"[SewerTour] Spawned showcase: {_spawned.Count} items across {dist - 20f:F0}m");
    }

    void SpawnShowcaseItem(GameObject prefab, float dist, float angleDeg, float radiusPct, string displayName, string category)
    {
        if (pipeGen == null) return;

        Vector3 center, forward, right, up;
        pipeGen.GetPathFrame(dist, out center, out forward, out right, out up);

        Vector3 pos;
        Quaternion rot;

        if (radiusPct <= 0.01f)
        {
            // Center-placed items (grate, drop zone)
            pos = center;
            rot = Quaternion.LookRotation(forward, up);
        }
        else
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float spawnR = pipeRadius * radiusPct;
            pos = center + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * spawnR;
            Vector3 inward = (center - pos).normalized;
            rot = Quaternion.LookRotation(forward, inward);
        }

        // Signs face inward so player can read them
        // Sign text is at +Z in local space, so sign's forward must point toward center (inward)
        if (category == "Sign")
            rot = Quaternion.LookRotation((center - pos).normalized, forward);

        GameObject obj = Instantiate(prefab, pos, rot, transform);

        // Scale up obstacles and creatures for easier inspection
        if (category == "Obstacle" || category == "Creature")
            obj.transform.localScale *= 1.5f;

        // Disable colliders and behaviors so items stay still during tour
        foreach (Collider c in obj.GetComponentsInChildren<Collider>())
            c.enabled = false;
        foreach (ObstacleBehavior ob in obj.GetComponentsInChildren<ObstacleBehavior>())
            ob.enabled = false;
        foreach (Obstacle o in obj.GetComponentsInChildren<Obstacle>())
            o.doesRotate = false;

        _spawned.Add(obj);

        // Add debug label and bounding box
        AddDebugLabel(obj, pos, displayName, category);
        AddBoundingBox(obj);
    }

    // === DEBUG LABELS ===

    void AddDebugLabel(GameObject obj, Vector3 worldPos, string name, string category)
    {
        // 3D TextMesh label above the object
        GameObject labelObj = new GameObject($"Label_{name}");
        labelObj.transform.SetParent(obj.transform, false);

        // Position above the object
        Bounds bounds = GetBounds(obj);
        float height = bounds.size.y > 0 ? bounds.extents.y + 0.3f : 0.5f;
        labelObj.transform.localPosition = Vector3.up * height;

        // Billboard text
        TextMesh tm = labelObj.AddComponent<TextMesh>();
        tm.text = $"{name}\n[{category}]";
        tm.fontSize = 32;
        tm.characterSize = 0.04f;
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;

        // Color code by category
        Color labelColor;
        switch (category)
        {
            case "Obstacle": labelColor = new Color(1f, 0.3f, 0.2f); break;
            case "Collectible": labelColor = new Color(1f, 0.9f, 0.2f); break;
            case "PowerUp": labelColor = new Color(0.2f, 0.9f, 1f); break;
            case "Event": labelColor = new Color(1f, 0.5f, 0f); break;
            case "Scenery": labelColor = new Color(0.6f, 0.8f, 0.5f); break;
            case "Decor": labelColor = new Color(0.7f, 0.6f, 0.4f); break;
            case "Sign": labelColor = new Color(0.9f, 0.7f, 0.9f); break;
            case "Creature": labelColor = new Color(0.3f, 1f, 0.6f); break;
            default: labelColor = Color.white; break;
        }
        tm.color = labelColor;
        tm.fontStyle = FontStyle.Bold;

        // Make it face camera (add billboard behavior)
        labelObj.AddComponent<TourBillboard>();

        _labels.Add(labelObj);
    }

    void AddBoundingBox(GameObject obj)
    {
        Bounds bounds = GetBounds(obj);
        if (bounds.size.sqrMagnitude < 0.001f) return;

        // Create wireframe box using LineRenderer
        GameObject boxObj = new GameObject("BoundingBox");
        boxObj.transform.SetParent(obj.transform, false);
        boxObj.transform.localPosition = bounds.center - obj.transform.position;
        boxObj.transform.localRotation = Quaternion.identity;

        LineRenderer lr = boxObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.widthMultiplier = 0.015f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0f, 1f, 0.4f, 0.5f);
        lr.endColor = new Color(0f, 1f, 0.4f, 0.5f);

        // Wireframe cube path (16 points to trace all 12 edges)
        Vector3 e = bounds.extents;
        Vector3 localCenter = bounds.center - obj.transform.position;
        Vector3[] corners = new Vector3[8]
        {
            localCenter + new Vector3(-e.x, -e.y, -e.z),
            localCenter + new Vector3( e.x, -e.y, -e.z),
            localCenter + new Vector3( e.x,  e.y, -e.z),
            localCenter + new Vector3(-e.x,  e.y, -e.z),
            localCenter + new Vector3(-e.x, -e.y,  e.z),
            localCenter + new Vector3( e.x, -e.y,  e.z),
            localCenter + new Vector3( e.x,  e.y,  e.z),
            localCenter + new Vector3(-e.x,  e.y,  e.z),
        };

        // Path that visits all edges
        Vector3[] path = new Vector3[]
        {
            corners[0], corners[1], corners[2], corners[3], corners[0], // bottom face
            corners[4], corners[5], corners[6], corners[7], corners[4], // top face
            corners[5], corners[1], corners[2], corners[6], corners[7], corners[3], // connecting edges
        };

        lr.positionCount = path.Length;
        lr.SetPositions(path);
    }

    Bounds GetBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one * 0.5f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // === NEAREST OBJECT DISPLAY ===

    void UpdateNearestLabel()
    {
        if (_currentObjText == null || player == null) return;

        float closestDist = float.MaxValue;
        string closestName = "";

        for (int i = 0; i < _labels.Count; i++)
        {
            if (_labels[i] == null) continue;
            float d = Vector3.Distance(_labels[i].transform.position, player.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                TextMesh tm = _labels[i].GetComponent<TextMesh>();
                if (tm != null) closestName = tm.text.Replace("\n", " - ");
            }
        }

        if (closestDist < 15f && closestName.Length > 0)
        {
            _currentObjText.text = closestName;

            // Category-based audio cue when approaching a new item
            if (closestDist < 8f && Time.time - _lastCueTime > 2f)
            {
                string cat = "";
                int b1 = closestName.IndexOf('[');
                int b2 = closestName.IndexOf(']');
                if (b1 >= 0 && b2 > b1)
                    cat = closestName.Substring(b1 + 1, b2 - b1 - 1);

                if (cat.Length > 0 && cat != _lastCueCategory)
                {
                    _lastCueCategory = cat;
                    _lastCueTime = Time.time;
                    if (ProceduralAudio.Instance != null)
                    {
                        switch (cat)
                        {
                            case "Obstacle": ProceduralAudio.Instance.PlayDangerPing(1f); break;
                            case "Collectible": ProceduralAudio.Instance.PlayBubblePop(); break;
                            case "PowerUp": ProceduralAudio.Instance.PlayCoinMagnet(); break;
                            case "Creature": ProceduralAudio.Instance.PlayBubblePop(); break;
                            default: ProceduralAudio.Instance.PlayUIClick(); break;
                        }
                    }
                }
            }
        }
        else
            _currentObjText.text = "";
    }

    void UpdateScreenLabels()
    {
        // The 3D TextMesh labels handle their own visibility via TourBillboard
    }

    // === TOUR UI ===

    void BuildTourUI()
    {
        // Find existing game canvas
        Canvas gameCanvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 100)
            {
                gameCanvas = c;
                break;
            }
        }
        if (gameCanvas == null) return;

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Tour overlay panel (top strip)
        GameObject panel = new GameObject("TourPanel");
        panel.transform.SetParent(gameCanvas.transform, false);
        RectTransform panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0, 0.92f);
        panelRt.anchorMax = new Vector2(1, 1f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.02f, 0.85f);

        // SEWER TOUR header
        _headerText = MakeTourText(panel.transform, "Header", "SEWER TOUR",
            28, TextAnchor.MiddleLeft, new Color(0.3f, 1f, 0.5f),
            new Vector2(0.02f, 0f), new Vector2(0.4f, 1f));
        _headerText.fontStyle = FontStyle.Bold;

        // Distance
        _distText = MakeTourText(panel.transform, "Dist", "0m",
            22, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.7f),
            new Vector2(0.4f, 0f), new Vector2(0.6f, 1f));

        // Exit button
        GameObject exitObj = new GameObject("ExitBtn");
        exitObj.transform.SetParent(panel.transform, false);
        RectTransform exitRt = exitObj.AddComponent<RectTransform>();
        exitRt.anchorMin = new Vector2(0.8f, 0.1f);
        exitRt.anchorMax = new Vector2(0.98f, 0.9f);
        exitRt.offsetMin = Vector2.zero;
        exitRt.offsetMax = Vector2.zero;
        Image exitBg = exitObj.AddComponent<Image>();
        exitBg.color = new Color(0.6f, 0.12f, 0.08f);
        _exitButton = exitObj.AddComponent<Button>();
        _exitButton.onClick.AddListener(ExitTour);

        Text exitText = MakeTourText(exitObj.transform, "ExitText", "EXIT",
            20, TextAnchor.MiddleCenter, Color.white,
            Vector2.zero, Vector2.one);
        exitText.fontStyle = FontStyle.Bold;
        // Make it stretch to fill button
        RectTransform etRt = exitText.GetComponent<RectTransform>();
        etRt.anchorMin = Vector2.zero;
        etRt.anchorMax = Vector2.one;
        etRt.offsetMin = Vector2.zero;
        etRt.offsetMax = Vector2.zero;

        // Current object name (bottom of screen)
        GameObject objPanel = new GameObject("ObjPanel");
        objPanel.transform.SetParent(gameCanvas.transform, false);
        RectTransform objRt = objPanel.AddComponent<RectTransform>();
        objRt.anchorMin = new Vector2(0.1f, 0.02f);
        objRt.anchorMax = new Vector2(0.9f, 0.08f);
        objRt.offsetMin = Vector2.zero;
        objRt.offsetMax = Vector2.zero;
        Image objBg = objPanel.AddComponent<Image>();
        objBg.color = new Color(0, 0, 0, 0.6f);

        _currentObjText = MakeTourText(objPanel.transform, "ObjName", "",
            24, TextAnchor.MiddleCenter, new Color(1f, 1f, 0.8f),
            Vector2.zero, Vector2.one);
        RectTransform coRt = _currentObjText.GetComponent<RectTransform>();
        coRt.anchorMin = Vector2.zero;
        coRt.anchorMax = Vector2.one;
        coRt.offsetMin = new Vector2(10, 0);
        coRt.offsetMax = new Vector2(-10, 0);
    }

    Text MakeTourText(Transform parent, string name, string content, int fontSize,
        TextAnchor align, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text t = go.AddComponent<Text>();
        t.text = content;
        t.font = _font;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        Outline ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0, 0, 0, 0.9f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        return t;
    }

    void ExitTour()
    {
        // Audio feedback before restart
        if (ProceduralAudio.Instance != null)
        {
            ProceduralAudio.Instance.PlayUIClick();
            ProceduralAudio.Instance.PlayTourExit();
            ProceduralAudio.Instance.StopTourAmbient();
        }

        // Reload scene to reset everything
        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
    }
}

/// <summary>
/// Simple billboard component — makes a 3D TextMesh always face the camera.
/// </summary>
public class TourBillboard : MonoBehaviour
{
    private Camera _cam;

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // Face camera
        transform.rotation = Quaternion.LookRotation(
            transform.position - _cam.transform.position, Vector3.up);
    }
}
