using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Comical floating score popup system. Shows "+200 FLIP!" "+50 STOMP!" etc.
/// with big bold styling, scale punch, rotation wobble, and shake for impact.
/// Pooled for performance.
/// </summary>
public class ScorePopup : MonoBehaviour
{
    public static ScorePopup Instance { get; private set; }

    public enum PopupType { Coin, NearMiss, Trick, Stomp, Milestone, Combo }

    struct ActivePopup
    {
        public GameObject obj;
        public Text text;
        public Outline outline;
        public float spawnTime;
        public float lifetime;
        public Vector3 worldPos;
        public Vector3 velocity;
        public float startScale;
        public float wobbleAngle;
        public float wobbleSpeed;
        public Color baseColor;
    }

    private Canvas _canvas;
    private Camera _cam;
    private List<ActivePopup> _active = new List<ActivePopup>();
    private Queue<GameObject> _pool = new Queue<GameObject>();
    private Font _font;

    // Bold comic colors
    static readonly Color CoinColor = new Color(1f, 0.92f, 0.15f);
    static readonly Color NearMissColor = new Color(0.1f, 1f, 0.85f);
    static readonly Color TrickColor = new Color(1f, 0.25f, 0.9f);
    static readonly Color StompColor = new Color(1f, 0.5f, 0f);
    static readonly Color MilestoneColor = new Color(1f, 1f, 0.6f);
    static readonly Color ComboColor = new Color(0.3f, 1f, 0.3f);

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _cam = Camera.main;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Create overlay canvas for popups
        GameObject canvasObj = new GameObject("ScorePopupCanvas");
        canvasObj.transform.SetParent(transform);
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;
        canvasObj.AddComponent<CanvasScaler>();

        // Pre-pool some popup objects
        for (int i = 0; i < 15; i++)
            _pool.Enqueue(CreatePopupObject());
    }

    GameObject CreatePopupObject()
    {
        GameObject obj = new GameObject("Popup");
        obj.transform.SetParent(_canvas.transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 80);

        Text txt = obj.AddComponent<Text>();
        txt.font = _font;
        txt.fontSize = 42;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.fontStyle = FontStyle.Bold;

        // Thick black outline for comic book readability
        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 1f);
        outline.effectDistance = new Vector2(3, -3);

        // Second outline for extra thickness
        Outline outline2 = obj.AddComponent<Outline>();
        outline2.effectColor = new Color(0, 0, 0, 0.7f);
        outline2.effectDistance = new Vector2(-2, 2);

        obj.SetActive(false);
        return obj;
    }

    public void Show(string message, Vector3 worldPosition, PopupType type, float scale = 1f)
    {
        GameObject obj;
        if (_pool.Count > 0)
            obj = _pool.Dequeue();
        else
            obj = CreatePopupObject();

        Text txt = obj.GetComponent<Text>();
        int baseFontSize = 42;

        // Scale up font size for impact
        txt.fontSize = Mathf.RoundToInt(baseFontSize * scale);

        Color col;
        switch (type)
        {
            case PopupType.Coin: col = CoinColor; break;
            case PopupType.NearMiss: col = NearMissColor; break;
            case PopupType.Trick: col = TrickColor; break;
            case PopupType.Stomp: col = StompColor; break;
            case PopupType.Milestone: col = MilestoneColor; break;
            case PopupType.Combo: col = ComboColor; break;
            default: col = Color.white; break;
        }
        txt.text = message;
        txt.color = col;

        obj.SetActive(true);

        float wobbleAngle = Random.Range(-15f, 15f);
        float wobbleSpeed = Random.Range(8f, 14f);

        obj.transform.localScale = Vector3.one * 2.2f; // start big for punch-in
        obj.transform.localRotation = Quaternion.Euler(0, 0, wobbleAngle);

        _active.Add(new ActivePopup
        {
            obj = obj,
            text = txt,
            outline = obj.GetComponent<Outline>(),
            spawnTime = Time.time,
            lifetime = type == PopupType.Milestone ? 2.5f : 1.4f,
            worldPos = worldPosition,
            velocity = Vector3.up * 2.5f + Random.insideUnitSphere * 0.4f,
            startScale = scale,
            wobbleAngle = wobbleAngle,
            wobbleSpeed = wobbleSpeed,
            baseColor = col
        });
    }

    // Convenience methods with comical text
    public void ShowCoin(Vector3 pos, int amount)
    {
        string[] coinWords = { "FARTCOIN!", "YOINK!", "SHINY!", "LOOT!", "FILTHY LUCRE!", "SWEET!" };
        string word = coinWords[Random.Range(0, coinWords.Length)];
        Show($"+{amount} {word}", pos, PopupType.Coin, 0.85f);
    }

    public void ShowTrick(Vector3 pos, string trickName, int points)
    {
        Show($"+{points}\n{trickName}!", pos, PopupType.Trick, 1.4f);
        CheerOverlay.Instance?.ShowCheer($"{trickName}! +{points}", TrickColor);
    }

    public void ShowStomp(Vector3 pos, int combo, int points)
    {
        string[] stompWords = { "SQUASH!", "SPLAT!", "CRUSHED!", "FLATTENED!", "STOMPED!" };
        string word = stompWords[Mathf.Min(combo - 1, stompWords.Length - 1)];
        if (combo > 1) word = $"x{combo} {word}";
        Show($"+{points}\n{word}", pos, PopupType.Stomp, 1.1f + combo * 0.2f);
        CheerOverlay.Instance?.ShowCheer($"{word} +{points}", StompColor, combo >= 3);
    }

    public void ShowNearMiss(Vector3 pos, int bonus)
    {
        string[] nearWords = { "CLOSE!", "WHEW!", "YIKES!", "SCARY!", "ALMOST!" };
        string word = nearWords[Random.Range(0, nearWords.Length)];
        string msg = bonus > 0 ? $"+{bonus} {word}" : word;
        Show(msg, pos, PopupType.NearMiss, 1.0f);
    }

    public void ShowMilestone(Vector3 pos, string name)
    {
        Show(name, pos, PopupType.Milestone, 2.0f);
        CheerOverlay.Instance?.ShowCheer(name, MilestoneColor, true);
    }

    public void ShowCombo(Vector3 pos, int combo)
    {
        string label;
        if (combo >= 20) label = "INSANE!";
        else if (combo >= 15) label = "BONKERS!";
        else if (combo >= 10) label = "EPIC!";
        else if (combo >= 7) label = "RADICAL!";
        else if (combo >= 5) label = "SICK!";
        else label = "COMBO!";
        Show($"{combo}x\n{label}", pos, PopupType.Combo, 1.1f + combo * 0.06f);
    }

    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var p = _active[i];
            float elapsed = Time.time - p.spawnTime;
            float t = elapsed / p.lifetime;

            if (t >= 1f || p.obj == null)
            {
                if (p.obj != null)
                {
                    p.obj.SetActive(false);
                    _pool.Enqueue(p.obj);
                }
                _active.RemoveAt(i);
                continue;
            }

            // Move world position upward with deceleration
            p.worldPos += p.velocity * Time.deltaTime;
            p.velocity *= 0.96f;
            _active[i] = p;

            // Project to screen
            Vector3 screenPos = _cam.WorldToScreenPoint(p.worldPos);
            if (screenPos.z < 0) { p.obj.SetActive(false); continue; }

            p.obj.SetActive(true);
            p.obj.GetComponent<RectTransform>().position = screenPos;

            // Scale: BIG punch-in then settle with slight bounce
            float scaleT;
            if (t < 0.1f)
                scaleT = Mathf.Lerp(2.2f, 0.85f, t / 0.1f); // overshoot small
            else if (t < 0.2f)
                scaleT = Mathf.Lerp(0.85f, 1.05f, (t - 0.1f) / 0.1f); // bounce back
            else if (t < 0.25f)
                scaleT = Mathf.Lerp(1.05f, 1f, (t - 0.2f) / 0.05f); // settle
            else
                scaleT = 1f;

            p.obj.transform.localScale = Vector3.one * scaleT;

            // Wobble rotation - starts strong, decays
            float wobbleDecay = 1f - Mathf.Clamp01(t * 2f);
            float rot = p.wobbleAngle * wobbleDecay * Mathf.Sin(elapsed * p.wobbleSpeed);
            p.obj.transform.localRotation = Quaternion.Euler(0, 0, rot);

            // Color: flash white at start then settle to base
            Color c = p.baseColor;
            if (t < 0.08f)
                c = Color.Lerp(Color.white, p.baseColor, t / 0.08f);

            // Fade out in last 35%
            c.a = t > 0.65f ? Mathf.Lerp(1f, 0f, (t - 0.65f) / 0.35f) : 1f;
            p.text.color = c;
        }
    }
}
