using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pooper Snooper - journey progress tracker styled like a sewer pipe cross-section.
/// Shows player and AI racer progress from "The Bowl" to "Brown Town".
/// Uses larger, more readable fonts and organic sewer-themed styling.
/// </summary>
public class PooperSnooper : MonoBehaviour
{
    [Header("References")]
    public TurdController player;
    public SmoothSnakeAI aiRacer;

    [Header("Settings")]
    public float totalJourney = 1000f;

    struct Milestone
    {
        public string name;
        public string icon;
        public float distance;
        public Color color;
        public Milestone(string n, string ic, float d, Color c)
        { name = n; icon = ic; distance = d; color = c; }
    }

    static readonly Milestone[] milestones = new[]
    {
        new Milestone("THE BOWL",     "\u25CB", 0f,    new Color(0.92f, 0.9f, 0.85f)),
        new Milestone("SEPTIC TANK",  "\u2622", 100f,  new Color(0.7f, 0.55f, 0.3f)),
        new Milestone("MAIN LINE",    "\u25A0", 250f,  new Color(0.55f, 0.55f, 0.55f)),
        new Milestone("PUMP STN",     "\u2699", 500f,  new Color(0.25f, 0.65f, 0.3f)),
        new Milestone("BROWN TOWN",   "\u2605", 1000f, new Color(0.4f, 0.22f, 0.1f)),
    };

    // UI refs
    private RectTransform _playerDot;
    private RectTransform _aiDot;
    private Image _playerDotImg;
    private Image _aiDotImg;
    private RectTransform _progressFill;
    private Image _progressFillImg;
    private Text _beyondText;
    private Text _distText;
    private RectTransform[] _milestoneDots;
    private Image[] _milestoneDotImgs;
    private Text[] _milestoneLabels;
    private Text[] _milestoneIcons;

    private float _barLeft = 0.05f;
    private float _barRight = 0.95f;

    void Awake()
    {
        BuildUI();
    }

    void BuildUI()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        // Panel at top of screen - taller for readability
        rt.anchorMin = new Vector2(0.01f, 0.895f);
        rt.anchorMax = new Vector2(0.99f, 0.995f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Dark sewer background with slight green tint
        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.04f, 0.92f);

        // Inner border - pipe-like frame
        GameObject inner = MakeChild("Inner", gameObject);
        SetAnchors(inner, 0.003f, 0.04f, 0.997f, 0.96f);
        Image innerImg = inner.AddComponent<Image>();
        innerImg.color = new Color(0.12f, 0.14f, 0.07f, 0.7f);

        // Top accent line (rusty pipe edge)
        GameObject topLine = MakeChild("TopLine", gameObject);
        SetAnchors(topLine, 0f, 0.92f, 1f, 1f);
        Image topLineImg = topLine.AddComponent<Image>();
        topLineImg.color = new Color(0.35f, 0.22f, 0.08f, 0.6f);

        // Bottom accent line
        GameObject bottomLine = MakeChild("BottomLine", gameObject);
        SetAnchors(bottomLine, 0f, 0f, 1f, 0.06f);
        Image bottomLineImg = bottomLine.AddComponent<Image>();
        bottomLineImg.color = new Color(0.3f, 0.2f, 0.06f, 0.5f);

        // Title: "POOPER SNOOPER" - centered top area, larger font
        GameObject titleObj = MakeChild("Title", gameObject);
        SetAnchors(titleObj, 0.25f, 0.58f, 0.75f, 0.92f);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "POOPER SNOOPER";
        titleText.font = GetFont();
        titleText.fontSize = 13;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.8f, 0.65f, 0.3f, 0.95f);
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Distance counter on the right
        GameObject distObj = MakeChild("Distance", gameObject);
        SetAnchors(distObj, 0.78f, 0.58f, 0.98f, 0.92f);
        _distText = distObj.AddComponent<Text>();
        _distText.text = "0m";
        _distText.font = GetFont();
        _distText.fontSize = 11;
        _distText.fontStyle = FontStyle.Bold;
        _distText.alignment = TextAnchor.MiddleRight;
        _distText.color = new Color(0.6f, 0.8f, 0.4f, 0.9f);
        _distText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // === TRACK BAR ===
        // Track background (the "pipe")
        GameObject trackBg = MakeChild("TrackBg", gameObject);
        SetAnchors(trackBg, _barLeft, 0.2f, _barRight, 0.42f);
        Image trackBgImg = trackBg.AddComponent<Image>();
        trackBgImg.color = new Color(0.15f, 0.12f, 0.06f, 0.85f);

        // Track highlight (pipe reflection)
        GameObject trackHighlight = MakeChild("Highlight", trackBg);
        SetAnchors(trackHighlight, 0f, 0.6f, 1f, 0.9f);
        Image hlImg = trackHighlight.AddComponent<Image>();
        hlImg.color = new Color(0.25f, 0.2f, 0.1f, 0.4f);

        // Progress fill
        GameObject fill = MakeChild("Fill", trackBg);
        _progressFill = fill.GetComponent<RectTransform>();
        _progressFill.anchorMin = new Vector2(0f, 0.05f);
        _progressFill.anchorMax = new Vector2(0f, 0.95f);
        _progressFill.offsetMin = Vector2.zero;
        _progressFill.offsetMax = Vector2.zero;
        _progressFillImg = fill.AddComponent<Image>();
        _progressFillImg.color = new Color(0.5f, 0.35f, 0.12f, 0.75f);

        // === MILESTONES ===
        _milestoneDots = new RectTransform[milestones.Length];
        _milestoneDotImgs = new Image[milestones.Length];
        _milestoneLabels = new Text[milestones.Length];
        _milestoneIcons = new Text[milestones.Length];

        for (int i = 0; i < milestones.Length; i++)
        {
            float normX = milestones[i].distance / totalJourney;
            float anchorX = Mathf.Lerp(_barLeft, _barRight, normX);
            float dotHalf = 0.012f;

            // Milestone dot (bigger, more visible)
            GameObject dot = MakeChild("Dot_" + i, gameObject);
            SetAnchors(dot, anchorX - dotHalf, 0.15f, anchorX + dotHalf, 0.48f);
            Image dotImg = dot.AddComponent<Image>();
            dotImg.color = milestones[i].color * 0.6f;
            _milestoneDots[i] = dot.GetComponent<RectTransform>();
            _milestoneDotImgs[i] = dotImg;

            // Milestone icon (above dot)
            GameObject iconObj = MakeChild("Icon_" + i, gameObject);
            SetAnchors(iconObj, anchorX - 0.015f, 0.42f, anchorX + 0.015f, 0.62f);
            Text iconText = iconObj.AddComponent<Text>();
            iconText.text = milestones[i].icon;
            iconText.font = GetFont();
            iconText.fontSize = 10;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = milestones[i].color;
            iconText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _milestoneIcons[i] = iconText;

            // Milestone label (below the track bar)
            GameObject label = MakeChild("Label_" + i, gameObject);
            // Stagger labels to avoid overlap
            float labelWidth = 0.06f;
            SetAnchors(label, anchorX - labelWidth, 0.0f, anchorX + labelWidth, 0.2f);
            Text labelText = label.AddComponent<Text>();
            labelText.text = milestones[i].name;
            labelText.font = GetFont();
            labelText.fontSize = 7;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.6f, 0.55f, 0.4f, 0.8f);
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _milestoneLabels[i] = labelText;
        }

        // === PLAYER INDICATOR (brown poop dot with P) ===
        _playerDot = CreateIndicator("PlayerDot", gameObject,
            new Color(0.5f, 0.32f, 0.1f), "P", 14, out _playerDotImg);

        // === AI INDICATOR (green with S) ===
        _aiDot = CreateIndicator("AIDot", gameObject,
            new Color(0.2f, 0.55f, 0.15f), "S", 14, out _aiDotImg);

        // === BEYOND BROWN TOWN TEXT ===
        GameObject beyondObj = MakeChild("Beyond", gameObject);
        SetAnchors(beyondObj, 0.2f, 0.0f, 0.8f, 0.55f);
        _beyondText = beyondObj.AddComponent<Text>();
        _beyondText.text = "BEYOND BROWN TOWN!";
        _beyondText.font = GetFont();
        _beyondText.fontSize = 14;
        _beyondText.alignment = TextAnchor.MiddleCenter;
        _beyondText.color = new Color(1f, 0.8f, 0.2f);
        _beyondText.fontStyle = FontStyle.Bold;
        _beyondText.horizontalOverflow = HorizontalWrapMode.Overflow;
        beyondObj.SetActive(false);
    }

    RectTransform CreateIndicator(string name, GameObject parent, Color color,
        string label, int fontSize, out Image img)
    {
        GameObject obj = MakeChild(name, parent);
        RectTransform rt = obj.GetComponent<RectTransform>();
        // Start at left edge - will be positioned in Update
        float halfW = 0.018f;
        rt.anchorMin = new Vector2(_barLeft - halfW, 0.38f);
        rt.anchorMax = new Vector2(_barLeft + halfW, 0.62f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        img = obj.AddComponent<Image>();
        img.color = color;

        // Label
        GameObject textObj = MakeChild("Lbl", obj);
        SetAnchors(textObj, 0f, 0f, 1f, 1f);
        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = GetFont();
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;

        // Drop shadow for readability
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.6f);
        shadow.effectDistance = new Vector2(0.5f, -0.5f);

        return rt;
    }

    GameObject MakeChild(string name, GameObject parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    void SetAnchors(GameObject obj, float minX, float minY, float maxX, float maxY)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 14);
        return f;
    }

    void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.isPlaying) return;

        float playerDist = player != null ? player.DistanceTraveled : 0f;
        float aiDist = aiRacer != null ? aiRacer.DistanceTraveled : 0f;

        float playerNorm = Mathf.Clamp01(playerDist / totalJourney);
        float aiNorm = Mathf.Clamp01(aiDist / totalJourney);

        // Update distance counter
        if (_distText != null)
            _distText.text = Mathf.FloorToInt(playerDist) + "m";

        // Progress fill
        if (_progressFill != null)
        {
            Vector2 max = _progressFill.anchorMax;
            max.x = Mathf.Lerp(max.x, playerNorm, Time.deltaTime * 5f);
            _progressFill.anchorMax = max;

            // Color shifts from brown to gold as you progress
            float hueShift = playerNorm * 0.1f;
            _progressFillImg.color = new Color(
                0.5f + hueShift, 0.35f + hueShift * 0.5f, 0.12f, 0.8f);
        }

        // Move indicators
        MoveIndicator(_playerDot, playerNorm);
        MoveIndicator(_aiDot, aiNorm);

        // Pulse player dot
        float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.15f;
        if (_playerDotImg != null)
        {
            Color c = new Color(0.5f * pulse, 0.32f * pulse, 0.1f * pulse);
            _playerDotImg.color = c;
        }

        // Beyond Brown Town
        if (playerDist > totalJourney && _beyondText != null)
        {
            _beyondText.gameObject.SetActive(true);
            float flash = Mathf.PingPong(Time.time * 3f, 1f);
            _beyondText.color = Color.Lerp(
                new Color(1f, 0.85f, 0.2f), new Color(1f, 0.4f, 0.05f), flash);
        }

        UpdateMilestones(playerDist);
    }

    void MoveIndicator(RectTransform indicator, float norm)
    {
        if (indicator == null) return;
        float halfW = 0.018f;
        float anchorX = Mathf.Lerp(_barLeft, _barRight, norm);

        Vector2 min = indicator.anchorMin;
        Vector2 max = indicator.anchorMax;
        min.x = Mathf.Lerp(min.x, anchorX - halfW, Time.deltaTime * 8f);
        max.x = Mathf.Lerp(max.x, anchorX + halfW, Time.deltaTime * 8f);
        indicator.anchorMin = min;
        indicator.anchorMax = max;
    }

    void UpdateMilestones(float playerDist)
    {
        for (int i = 0; i < milestones.Length; i++)
        {
            if (_milestoneLabels[i] == null) continue;

            bool reached = playerDist >= milestones[i].distance;
            bool current = false;
            if (i < milestones.Length - 1)
                current = playerDist >= milestones[i].distance &&
                          playerDist < milestones[i + 1].distance;
            else
                current = playerDist >= milestones[i].distance;

            if (current)
            {
                // Pulsing gold for current milestone
                float p = 0.85f + Mathf.Sin(Time.time * 4f) * 0.15f;
                _milestoneLabels[i].color = new Color(1f, 0.9f, 0.4f, p);
                _milestoneDotImgs[i].color = Color.Lerp(milestones[i].color, Color.yellow, 0.4f);
                _milestoneIcons[i].color = Color.Lerp(milestones[i].color, Color.white, 0.3f);
            }
            else if (reached)
            {
                _milestoneLabels[i].color = new Color(0.85f, 0.8f, 0.6f, 0.95f);
                _milestoneDotImgs[i].color = milestones[i].color;
                _milestoneIcons[i].color = milestones[i].color;
            }
            else
            {
                _milestoneLabels[i].color = new Color(0.4f, 0.38f, 0.3f, 0.5f);
                _milestoneDotImgs[i].color = milestones[i].color * 0.4f;
                _milestoneIcons[i].color = milestones[i].color * 0.5f;
            }
        }
    }
}
