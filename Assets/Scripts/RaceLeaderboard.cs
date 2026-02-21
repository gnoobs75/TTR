using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// NASCAR-style live race leaderboard on the left side of the screen.
/// Shows position number, colored bar, racer name, and time gap behind leader.
/// Player row highlighted. Bouncy animations on position changes.
/// </summary>
public class RaceLeaderboard : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform panelRoot;

    // Internal state
    private RacerRow[] _rows;
    private int[] _lastPositions; // track position changes for animations
    private float[] _animTimers;  // bounce animation timers
    private bool _initialized;

    struct RacerRow
    {
        public RectTransform root;
        public Image bgImage;
        public Text positionText;
        public Text nameText;
        public Text gapText;
        public Image colorSwatch;
    }

    const int MAX_RACERS = 5;
    const float ROW_HEIGHT = 34f;
    const float ROW_SPACING = 4f;
    const float PANEL_WIDTH = 220f;

    // Colors
    static readonly Color PlayerHighlight = new Color(1f, 0.85f, 0.1f, 0.3f);
    static readonly Color AIRowColor = new Color(0.15f, 0.12f, 0.08f, 0.75f);
    static readonly Color[] PositionColors = {
        new Color(1f, 0.85f, 0.1f),     // 1st - gold
        new Color(0.75f, 0.75f, 0.8f),  // 2nd - silver
        new Color(0.72f, 0.45f, 0.2f),  // 3rd - bronze
        new Color(0.5f, 0.5f, 0.45f),   // 4th - gray
        new Color(0.4f, 0.4f, 0.35f),   // 5th - dim gray
    };

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

        for (int i = 0; i < MAX_RACERS; i++)
        {
            _rows[i] = CreateRow(i);
            _lastPositions[i] = i + 1;
        }
    }

    RacerRow CreateRow(int index)
    {
        RacerRow row = new RacerRow();

        // Row container
        GameObject rowObj = new GameObject($"RacerRow_{index}");
        row.root = rowObj.AddComponent<RectTransform>();
        row.root.SetParent(panelRoot, false);
        row.root.anchorMin = new Vector2(0, 1);
        row.root.anchorMax = new Vector2(1, 1);
        float yOffset = -(index * (ROW_HEIGHT + ROW_SPACING));
        row.root.anchoredPosition = new Vector2(0, yOffset);
        row.root.sizeDelta = new Vector2(0, ROW_HEIGHT);

        // Background
        row.bgImage = rowObj.AddComponent<Image>();
        row.bgImage.color = AIRowColor;

        // Color swatch (left edge)
        GameObject swatchObj = new GameObject("Swatch");
        RectTransform swatchRect = swatchObj.AddComponent<RectTransform>();
        swatchRect.SetParent(row.root, false);
        swatchRect.anchorMin = new Vector2(0, 0.1f);
        swatchRect.anchorMax = new Vector2(0, 0.9f);
        swatchRect.anchoredPosition = new Vector2(16, 0);
        swatchRect.sizeDelta = new Vector2(8, 0);
        row.colorSwatch = swatchObj.AddComponent<Image>();
        row.colorSwatch.color = Color.white;

        // Position number
        GameObject posObj = new GameObject("Position");
        RectTransform posRect = posObj.AddComponent<RectTransform>();
        posRect.SetParent(row.root, false);
        posRect.anchorMin = new Vector2(0, 0);
        posRect.anchorMax = new Vector2(0, 1);
        posRect.anchoredPosition = new Vector2(34, 0);
        posRect.sizeDelta = new Vector2(24, 0);
        row.positionText = posObj.AddComponent<Text>();
        row.positionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (row.positionText.font == null)
            row.positionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        row.positionText.fontSize = 20;
        row.positionText.fontStyle = FontStyle.Bold;
        row.positionText.alignment = TextAnchor.MiddleCenter;
        row.positionText.color = Color.white;
        // Outline for comic look
        Outline posOutline = posObj.AddComponent<Outline>();
        posOutline.effectColor = new Color(0, 0, 0, 0.9f);
        posOutline.effectDistance = new Vector2(1, -1);

        // Racer name
        GameObject nameObj = new GameObject("Name");
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.SetParent(row.root, false);
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(0.65f, 1);
        nameRect.anchoredPosition = new Vector2(50, 0);
        nameRect.sizeDelta = new Vector2(0, 0);
        nameRect.offsetMin = new Vector2(50, 0);
        nameRect.offsetMax = new Vector2(-2, 0);
        row.nameText = nameObj.AddComponent<Text>();
        row.nameText.font = row.positionText.font;
        row.nameText.fontSize = 14;
        row.nameText.fontStyle = FontStyle.Bold;
        row.nameText.alignment = TextAnchor.MiddleLeft;
        row.nameText.color = Color.white;
        row.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Outline nameOutline = nameObj.AddComponent<Outline>();
        nameOutline.effectColor = new Color(0, 0, 0, 0.85f);
        nameOutline.effectDistance = new Vector2(1, -1);

        // Time gap
        GameObject gapObj = new GameObject("Gap");
        RectTransform gapRect = gapObj.AddComponent<RectTransform>();
        gapRect.SetParent(row.root, false);
        gapRect.anchorMin = new Vector2(0.6f, 0);
        gapRect.anchorMax = new Vector2(1, 1);
        gapRect.offsetMin = new Vector2(0, 0);
        gapRect.offsetMax = new Vector2(-4, 0);
        row.gapText = gapObj.AddComponent<Text>();
        row.gapText.font = row.positionText.font;
        row.gapText.fontSize = 13;
        row.gapText.alignment = TextAnchor.MiddleRight;
        row.gapText.color = new Color(0.7f, 0.7f, 0.65f);
        row.gapText.horizontalOverflow = HorizontalWrapMode.Overflow;

        return row;
    }

    /// <summary>Update all rows from RaceManager entries.</summary>
    public void UpdatePositions(List<RaceManager.RacerEntry> entries)
    {
        if (!_initialized) Initialize();

        for (int i = 0; i < MAX_RACERS && i < entries.Count; i++)
        {
            var entry = entries[i];
            var row = _rows[i];

            // Position change animation
            if (entry.position != _lastPositions[i])
            {
                _animTimers[i] = 0.4f; // trigger bounce
                _lastPositions[i] = entry.position;
            }

            // Animate bounce (elastic springy feel)
            float scale = 1f;
            if (_animTimers[i] > 0f)
            {
                _animTimers[i] -= Time.deltaTime;
                float t = 1f - (_animTimers[i] / 0.4f);
                // Elastic overshoot: bigger bounce with spring-back
                float elastic = Mathf.Pow(2f, -8f * t) * Mathf.Sin((t - 0.1f) * Mathf.PI * 2f / 0.35f);
                scale = 1f + elastic * 0.25f;
            }
            row.root.localScale = new Vector3(scale, scale, 1f);

            // Flash position number on change
            if (_animTimers[i] > 0.2f)
                row.positionText.color = Color.Lerp(PositionColors[posIdx], Color.white, (_animTimers[i] - 0.2f) / 0.2f);

            // Position number with color
            int posIdx = Mathf.Clamp(entry.position - 1, 0, PositionColors.Length - 1);
            row.positionText.text = entry.position.ToString();
            row.positionText.color = PositionColors[posIdx];

            // Color swatch
            row.colorSwatch.color = entry.color;

            // Name
            row.nameText.text = entry.name;

            // Background highlight for player (gentle pulse)
            if (entry.isPlayer)
            {
                float pulse = 0.25f + Mathf.Sin(Time.time * 2f) * 0.08f;
                row.bgImage.color = new Color(1f, 0.85f, 0.1f, pulse);
                row.nameText.color = new Color(1f, 0.92f, 0.3f); // gold name
            }
            else
            {
                row.bgImage.color = AIRowColor;
                row.nameText.color = Color.white;
            }

            // Time gap with color gradient (green=close, yellow=mid, red=far)
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

    /// <summary>Color gap text: green (close) → yellow (mid) → red (far).</summary>
    static Color GapColor(float gap)
    {
        if (gap < 1f)
            return Color.Lerp(new Color(0.3f, 1f, 0.4f), new Color(0.9f, 0.9f, 0.3f), gap);
        if (gap < 5f)
            return Color.Lerp(new Color(0.9f, 0.9f, 0.3f), new Color(1f, 0.5f, 0.2f), (gap - 1f) / 4f);
        return new Color(0.8f, 0.35f, 0.2f); // far behind = red-orange
    }

    /// <summary>Show final results with finish places.</summary>
    public void ShowFinalResults(List<RaceManager.RacerEntry> entries)
    {
        if (!_initialized) Initialize();

        // Sort by finish place
        var sorted = new List<RaceManager.RacerEntry>(entries);
        sorted.Sort((a, b) => a.finishPlace.CompareTo(b.finishPlace));

        for (int i = 0; i < MAX_RACERS && i < sorted.Count; i++)
        {
            var entry = sorted[i];
            var row = _rows[i];

            int posIdx = Mathf.Clamp(entry.finishPlace - 1, 0, PositionColors.Length - 1);
            row.positionText.text = entry.finishPlace.ToString();
            row.positionText.color = PositionColors[posIdx];
            row.colorSwatch.color = entry.color;
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
