using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Arcade-style live race leaderboard with portrait circles.
/// Each racer gets a chunky row with colored portrait, position badge, name, and gap.
/// Portrait rows smoothly animate Y-position when racers swap places.
/// Player row glows gold. Position changes trigger elastic bounce.
/// </summary>
public class RaceLeaderboard : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform panelRoot;

    // Internal state
    private RacerRow[] _rows;
    private int[] _lastPositions;
    private float[] _animTimers;
    private float[] _targetY;      // smooth Y-position targets
    private float[] _currentY;     // current animated Y positions
    private bool _initialized;

    struct RacerRow
    {
        public RectTransform root;
        public Image bgImage;
        public Image portrait;          // circular colored portrait
        public Image portraitBorder;     // portrait ring
        public Text portraitInitial;     // letter initial inside portrait
        public Text positionBadge;       // position number overlaid on portrait
        public Text nameText;
        public Text gapText;
    }

    const int MAX_RACERS = 5;
    const float ROW_HEIGHT = 62f;
    const float ROW_SPACING = 6f;
    const float PORTRAIT_SIZE = 48f;
    const float PANEL_WIDTH = 310f;
    const float Y_LERP_SPEED = 8f;  // smooth position slide speed

    // Colors
    static readonly Color PlayerHighlight = new Color(1f, 0.85f, 0.1f, 0.35f);
    static readonly Color AIRowColor = new Color(0.1f, 0.08f, 0.05f, 0.82f);
    static readonly Color[] PositionColors = {
        new Color(1f, 0.85f, 0.1f),     // 1st - gold
        new Color(0.78f, 0.78f, 0.85f), // 2nd - silver
        new Color(0.75f, 0.48f, 0.22f), // 3rd - bronze
        new Color(0.55f, 0.52f, 0.48f), // 4th - pewter
        new Color(0.42f, 0.4f, 0.36f),  // 5th - dim
    };

    // Position suffix labels
    static readonly string[] PosSuffix = { "ST", "ND", "RD", "TH", "TH" };

    void Awake()
    {
        if (panelRoot == null)
            panelRoot = GetComponent<RectTransform>();
    }

    /// <summary>Build the leaderboard rows. Called by SceneBootstrapper or at runtime.</summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _rows = new RacerRow[MAX_RACERS];
        _lastPositions = new int[MAX_RACERS];
        _animTimers = new float[MAX_RACERS];
        _targetY = new float[MAX_RACERS];
        _currentY = new float[MAX_RACERS];

        for (int i = 0; i < MAX_RACERS; i++)
        {
            float yPos = -(i * (ROW_HEIGHT + ROW_SPACING));
            _targetY[i] = yPos;
            _currentY[i] = yPos;
            _rows[i] = CreateRow(i, yPos);
            _lastPositions[i] = i + 1;
        }
    }

    RacerRow CreateRow(int index, float yPos)
    {
        RacerRow row = new RacerRow();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Row container
        GameObject rowObj = new GameObject($"RacerRow_{index}");
        row.root = rowObj.AddComponent<RectTransform>();
        row.root.SetParent(panelRoot, false);
        row.root.anchorMin = new Vector2(0, 1);
        row.root.anchorMax = new Vector2(1, 1);
        row.root.anchoredPosition = new Vector2(0, yPos);
        row.root.sizeDelta = new Vector2(0, ROW_HEIGHT);

        // Background with rounded feel (dark translucent)
        row.bgImage = rowObj.AddComponent<Image>();
        row.bgImage.color = AIRowColor;

        // === PORTRAIT CIRCLE (left side) ===
        float portraitX = 8f + PORTRAIT_SIZE * 0.5f;

        // Portrait border ring (slightly larger)
        GameObject borderObj = new GameObject("PortraitBorder");
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.SetParent(row.root, false);
        borderRect.anchorMin = new Vector2(0, 0.5f);
        borderRect.anchorMax = new Vector2(0, 0.5f);
        borderRect.anchoredPosition = new Vector2(portraitX, 0);
        borderRect.sizeDelta = new Vector2(PORTRAIT_SIZE + 6f, PORTRAIT_SIZE + 6f);
        row.portraitBorder = borderObj.AddComponent<Image>();
        row.portraitBorder.color = Color.white;

        // Neon glow on portrait ring
        NeonUIEffects.ApplyNeonGlow(borderObj, NeonUIEffects.NeonCyan, 0.6f);

        // Portrait fill (circular colored background)
        GameObject portraitObj = new GameObject("Portrait");
        RectTransform portraitRect = portraitObj.AddComponent<RectTransform>();
        portraitRect.SetParent(row.root, false);
        portraitRect.anchorMin = new Vector2(0, 0.5f);
        portraitRect.anchorMax = new Vector2(0, 0.5f);
        portraitRect.anchoredPosition = new Vector2(portraitX, 0);
        portraitRect.sizeDelta = new Vector2(PORTRAIT_SIZE, PORTRAIT_SIZE);
        row.portrait = portraitObj.AddComponent<Image>();
        row.portrait.color = Color.gray;

        // Letter initial inside portrait
        GameObject initialObj = new GameObject("Initial");
        RectTransform initialRect = initialObj.AddComponent<RectTransform>();
        initialRect.SetParent(portraitRect, false);
        initialRect.anchorMin = Vector2.zero;
        initialRect.anchorMax = Vector2.one;
        initialRect.offsetMin = Vector2.zero;
        initialRect.offsetMax = Vector2.zero;
        row.portraitInitial = initialObj.AddComponent<Text>();
        row.portraitInitial.font = font;
        row.portraitInitial.fontSize = 26;
        row.portraitInitial.fontStyle = FontStyle.Bold;
        row.portraitInitial.alignment = TextAnchor.MiddleCenter;
        row.portraitInitial.color = Color.white;
        Outline initialOutline = initialObj.AddComponent<Outline>();
        initialOutline.effectColor = new Color(0, 0, 0, 0.7f);
        initialOutline.effectDistance = new Vector2(1, -1);

        // Position badge (small overlay on bottom-right of portrait)
        GameObject badgeObj = new GameObject("PosBadge");
        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.SetParent(portraitRect, false);
        badgeRect.anchorMin = new Vector2(0.55f, -0.15f);
        badgeRect.anchorMax = new Vector2(1.2f, 0.45f);
        badgeRect.offsetMin = Vector2.zero;
        badgeRect.offsetMax = Vector2.zero;
        // Badge background
        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0.05f, 0.04f, 0.02f, 0.95f);
        // Badge text
        row.positionBadge = CreateTextChild(badgeObj, "BadgeText", font, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Outline badgeOutline = row.positionBadge.gameObject.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0, 0, 0, 0.9f);
        badgeOutline.effectDistance = new Vector2(1, -1);

        // === NAME TEXT (right of portrait) ===
        float nameX = portraitX + PORTRAIT_SIZE * 0.5f + 12f;
        GameObject nameObj = new GameObject("Name");
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.SetParent(row.root, false);
        nameRect.anchorMin = new Vector2(0, 0.45f);
        nameRect.anchorMax = new Vector2(1, 1f);
        nameRect.offsetMin = new Vector2(nameX, 0);
        nameRect.offsetMax = new Vector2(-8, -2);
        row.nameText = nameObj.AddComponent<Text>();
        row.nameText.font = font;
        row.nameText.fontSize = 22;
        row.nameText.fontStyle = FontStyle.Bold;
        row.nameText.alignment = TextAnchor.MiddleLeft;
        row.nameText.color = Color.white;
        row.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Outline nameOutline = nameObj.AddComponent<Outline>();
        nameOutline.effectColor = new Color(0, 0, 0, 0.9f);
        nameOutline.effectDistance = new Vector2(2, -2);

        // === GAP TEXT (below name, right-aligned) ===
        GameObject gapObj = new GameObject("Gap");
        RectTransform gapRect = gapObj.AddComponent<RectTransform>();
        gapRect.SetParent(row.root, false);
        gapRect.anchorMin = new Vector2(0, 0f);
        gapRect.anchorMax = new Vector2(1, 0.48f);
        gapRect.offsetMin = new Vector2(nameX, 2);
        gapRect.offsetMax = new Vector2(-8, 0);
        row.gapText = gapObj.AddComponent<Text>();
        row.gapText.font = font;
        row.gapText.fontSize = 18;
        row.gapText.fontStyle = FontStyle.Bold;
        row.gapText.alignment = TextAnchor.MiddleLeft;
        row.gapText.color = new Color(0.7f, 0.7f, 0.65f);
        row.gapText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Outline gapOutline = gapObj.AddComponent<Outline>();
        gapOutline.effectColor = new Color(0, 0, 0, 0.7f);
        gapOutline.effectDistance = new Vector2(1, -1);

        return row;
    }

    Text CreateTextChild(GameObject parent, string name, Font font, int size, FontStyle style, TextAnchor align, Color color)
    {
        GameObject obj = new GameObject(name);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.SetParent(parent.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = align;
        text.color = color;
        return text;
    }

    void Update()
    {
        if (!_initialized) return;

        // Smoothly animate rows to their target Y positions
        for (int i = 0; i < MAX_RACERS; i++)
        {
            if (Mathf.Abs(_currentY[i] - _targetY[i]) > 0.5f)
            {
                _currentY[i] = Mathf.Lerp(_currentY[i], _targetY[i], Time.deltaTime * Y_LERP_SPEED);
                _rows[i].root.anchoredPosition = new Vector2(0, _currentY[i]);
            }

            // Neon breathing pulse on portrait borders (each row at different phase)
            if (_rows[i].portraitBorder != null)
            {
                Color baseColor = _rows[i].portraitBorder.color;
                float pulse = 0.7f + Mathf.Sin(Time.time * 1.2f + i * 0.7f) * 0.3f;
                _rows[i].portraitBorder.color = new Color(
                    baseColor.r, baseColor.g, baseColor.b,
                    Mathf.Clamp01(pulse));
            }
        }
    }

    /// <summary>Update all rows from RaceManager entries.</summary>
    public void UpdatePositions(List<RaceManager.RacerEntry> entries)
    {
        if (!_initialized) Initialize();

        for (int i = 0; i < MAX_RACERS && i < entries.Count; i++)
        {
            var entry = entries[i];
            var row = _rows[i];

            // Smooth Y-position slide to new slot
            _targetY[i] = -(i * (ROW_HEIGHT + ROW_SPACING));

            // Position change bounce animation
            if (entry.position != _lastPositions[i])
            {
                _animTimers[i] = 0.5f;
                _lastPositions[i] = entry.position;
            }

            // Elastic bounce on position change
            float scale = 1f;
            if (_animTimers[i] > 0f)
            {
                _animTimers[i] -= Time.deltaTime;
                float t = 1f - (_animTimers[i] / 0.5f);
                float elastic = Mathf.Pow(2f, -8f * t) * Mathf.Sin((t - 0.1f) * Mathf.PI * 2f / 0.35f);
                scale = 1f + elastic * 0.2f;
            }
            row.root.localScale = new Vector3(scale, scale, 1f);

            // Position styling
            int posIdx = Mathf.Clamp(entry.position - 1, 0, PositionColors.Length - 1);
            Color posColor = PositionColors[posIdx];

            // Position badge
            string suffix = posIdx < PosSuffix.Length ? PosSuffix[posIdx] : "TH";
            row.positionBadge.text = $"{entry.position}{suffix}";

            // Flash position badge on change
            if (_animTimers[i] > 0.3f)
                row.positionBadge.color = Color.Lerp(posColor, Color.white, (_animTimers[i] - 0.3f) / 0.2f);
            else
                row.positionBadge.color = posColor;

            // Portrait circle = racer color
            row.portrait.color = entry.color;
            row.portraitBorder.color = posColor;

            // Initial letter
            row.portraitInitial.text = entry.name.Length > 0 ? entry.name.Substring(0, 1) : "?";

            // Name
            row.nameText.text = entry.name;

            // Player highlight
            if (entry.isPlayer)
            {
                float pulse = 0.3f + Mathf.Sin(Time.time * 2.5f) * 0.1f;
                row.bgImage.color = new Color(1f, 0.85f, 0.1f, pulse);
                row.nameText.color = new Color(1f, 0.92f, 0.3f);
                row.portraitBorder.color = new Color(1f, 0.9f, 0.3f); // extra glow
            }
            else
            {
                row.bgImage.color = AIRowColor;
                row.nameText.color = Color.white;
            }

            // Gap text
            if (entry.position == 1)
            {
                row.gapText.text = "LEADER";
                row.gapText.color = new Color(1f, 0.85f, 0.1f);
            }
            else if (entry.isFinished)
            {
                float gap = entry.finishTime - entries[0].finishTime;
                row.gapText.text = $"+{gap:F1}s";
                row.gapText.color = GapColor(gap);
            }
            else
            {
                row.gapText.text = $"+{entry.gapToLeader:F1}s";
                row.gapText.color = GapColor(entry.gapToLeader);
            }
        }
    }

    static Color GapColor(float gap)
    {
        if (gap < 1f)
            return Color.Lerp(new Color(0.3f, 1f, 0.4f), new Color(0.9f, 0.9f, 0.3f), gap);
        if (gap < 5f)
            return Color.Lerp(new Color(0.9f, 0.9f, 0.3f), new Color(1f, 0.5f, 0.2f), (gap - 1f) / 4f);
        return new Color(0.8f, 0.35f, 0.2f);
    }

    /// <summary>Show final results with finish places.</summary>
    public void ShowFinalResults(List<RaceManager.RacerEntry> entries)
    {
        if (!_initialized) Initialize();

        var sorted = new List<RaceManager.RacerEntry>(entries);
        sorted.Sort((a, b) => a.finishPlace.CompareTo(b.finishPlace));

        for (int i = 0; i < MAX_RACERS && i < sorted.Count; i++)
        {
            var entry = sorted[i];
            var row = _rows[i];

            _targetY[i] = -(i * (ROW_HEIGHT + ROW_SPACING));
            _currentY[i] = _targetY[i];
            row.root.anchoredPosition = new Vector2(0, _currentY[i]);

            int posIdx = Mathf.Clamp(entry.finishPlace - 1, 0, PositionColors.Length - 1);
            Color posColor = PositionColors[posIdx];

            string suffix = posIdx < PosSuffix.Length ? PosSuffix[posIdx] : "TH";
            row.positionBadge.text = $"{entry.finishPlace}{suffix}";
            row.positionBadge.color = posColor;

            row.portrait.color = entry.color;
            row.portraitBorder.color = posColor;
            row.portraitInitial.text = entry.name.Length > 0 ? entry.name.Substring(0, 1) : "?";
            row.nameText.text = entry.name;

            if (entry.finishPlace == 1)
            {
                row.gapText.text = $"{entry.finishTime:F1}s";
                row.gapText.color = new Color(1f, 0.85f, 0.1f);
            }
            else
            {
                float gap = entry.finishTime - sorted[0].finishTime;
                row.gapText.text = $"+{gap:F1}s";
                row.gapText.color = new Color(0.7f, 0.7f, 0.65f);
            }

            if (entry.isPlayer)
            {
                row.bgImage.color = PlayerHighlight;
                row.nameText.color = new Color(1f, 0.92f, 0.3f);
            }
            else
            {
                row.bgImage.color = AIRowColor;
                row.nameText.color = Color.white;
            }
        }
    }
}
