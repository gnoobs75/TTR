using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Floating score popup system. Shows "+200 FLIP!" "+50 STOMP!" etc.
/// at the point of action, rising and fading. Pooled for performance.
/// </summary>
public class ScorePopup : MonoBehaviour
{
    public static ScorePopup Instance { get; private set; }

    public enum PopupType { Coin, NearMiss, Trick, Stomp, Milestone, Combo }

    struct ActivePopup
    {
        public GameObject obj;
        public Text text;
        public float spawnTime;
        public float lifetime;
        public Vector3 worldPos;
        public Vector3 velocity;
    }

    private Canvas _canvas;
    private Camera _cam;
    private List<ActivePopup> _active = new List<ActivePopup>();
    private Queue<GameObject> _pool = new Queue<GameObject>();
    private Font _font;

    // Colors per type
    static readonly Color CoinColor = new Color(1f, 0.9f, 0.2f);
    static readonly Color NearMissColor = new Color(0.3f, 1f, 0.9f);
    static readonly Color TrickColor = new Color(1f, 0.4f, 1f);
    static readonly Color StompColor = new Color(1f, 0.6f, 0.1f);
    static readonly Color MilestoneColor = new Color(1f, 1f, 1f);
    static readonly Color ComboColor = new Color(0.4f, 1f, 0.4f);

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
        for (int i = 0; i < 10; i++)
            _pool.Enqueue(CreatePopupObject());
    }

    GameObject CreatePopupObject()
    {
        GameObject obj = new GameObject("Popup");
        obj.transform.SetParent(_canvas.transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 50);

        Text txt = obj.AddComponent<Text>();
        txt.font = _font;
        txt.fontSize = 32;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.fontStyle = FontStyle.Bold;

        // Outline for readability
        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.8f);
        outline.effectDistance = new Vector2(2, -2);

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
        txt.text = message;
        txt.fontSize = Mathf.RoundToInt(32 * scale);

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
        txt.color = col;

        obj.SetActive(true);
        obj.transform.localScale = Vector3.one * 1.5f; // start big, shrink to 1

        _active.Add(new ActivePopup
        {
            obj = obj,
            text = txt,
            spawnTime = Time.time,
            lifetime = type == PopupType.Milestone ? 2.5f : 1.2f,
            worldPos = worldPosition,
            velocity = Vector3.up * 2f + Random.insideUnitSphere * 0.3f
        });
    }

    // Convenience methods
    public void ShowCoin(Vector3 pos, int amount)
    {
        Show($"+{amount}", pos, PopupType.Coin, 0.8f);
    }

    public void ShowTrick(Vector3 pos, string trickName, int points)
    {
        Show($"+{points} {trickName}!", pos, PopupType.Trick, 1.3f);
    }

    public void ShowStomp(Vector3 pos, int combo, int points)
    {
        string label = combo > 1 ? $"STOMP x{combo}!" : "STOMP!";
        Show($"+{points} {label}", pos, PopupType.Stomp, 1f + combo * 0.15f);
    }

    public void ShowNearMiss(Vector3 pos, int bonus)
    {
        Show($"+{bonus} CLOSE!", pos, PopupType.NearMiss);
    }

    public void ShowMilestone(Vector3 pos, string name)
    {
        Show(name, pos, PopupType.Milestone, 1.8f);
    }

    public void ShowCombo(Vector3 pos, int combo)
    {
        string label;
        if (combo >= 20) label = "INSANE!";
        else if (combo >= 10) label = "EPIC!";
        else if (combo >= 5) label = "SICK!";
        else label = "COMBO!";
        Show($"{combo}x {label}", pos, PopupType.Combo, 1f + combo * 0.05f);
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

            // Move world position upward
            p.worldPos += p.velocity * Time.deltaTime;
            p.velocity *= 0.97f; // slow down
            _active[i] = p;

            // Project to screen
            Vector3 screenPos = _cam.WorldToScreenPoint(p.worldPos);
            if (screenPos.z < 0) { p.obj.SetActive(false); continue; }

            p.obj.SetActive(true);
            p.obj.GetComponent<RectTransform>().position = screenPos;

            // Scale: pop in then settle
            float scaleT = t < 0.15f ? Mathf.Lerp(1.5f, 1f, t / 0.15f) : 1f;
            p.obj.transform.localScale = Vector3.one * scaleT;

            // Fade out in last 40%
            Color c = p.text.color;
            c.a = t > 0.6f ? Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f) : 1f;
            p.text.color = c;
        }
    }
}
