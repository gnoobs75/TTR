using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sewage Treatment Plant finish line and Winners Poodium.
/// Shows finish banner, plays fanfare, presents 1st/2nd/3rd on podium blocks.
/// </summary>
public class RaceFinish : MonoBehaviour
{
    [Header("UI References")]
    public Canvas finishCanvas;
    public RectTransform bannerRoot;
    public RectTransform podiumRoot;

    // Internal UI elements
    private Text _bannerText;
    private Text _placeText;
    private Text _timeText;
    private PodiumSlot[] _podiumSlots;
    private CanvasGroup _bannerGroup;
    private CanvasGroup _podiumGroup;
    private bool _initialized;
    private bool _podiumShown;

    // 3D Podium
    private GameObject _podium3D;
    private ParticleSystem _confetti;

    struct PodiumSlot
    {
        public RectTransform root;
        public Image pedestal;
        public Image colorSwatch;
        public Text nameText;
        public Text placeText;
        public Text timeText;
    }

    // Podium dimensions
    const float PODIUM_WIDTH = 120f;
    const float PODIUM_SPACING = 10f;
    const float FIRST_HEIGHT = 100f;
    const float SECOND_HEIGHT = 70f;
    const float THIRD_HEIGHT = 50f;

    static readonly Color GoldColor = new Color(1f, 0.85f, 0.1f);
    static readonly Color SilverColor = new Color(0.75f, 0.75f, 0.82f);
    static readonly Color BronzeColor = new Color(0.72f, 0.45f, 0.2f);
    static readonly Color SewageGreen = new Color(0.2f, 0.35f, 0.15f, 0.9f);
    static readonly Color PipeGray = new Color(0.35f, 0.35f, 0.32f, 0.95f);

    void Awake()
    {
        if (finishCanvas == null)
            finishCanvas = GetComponentInParent<Canvas>();
    }

    public void Initialize(Canvas canvas)
    {
        if (_initialized) return;
        _initialized = true;
        finishCanvas = canvas;

        CreateBanner();
        CreatePodium();

        // Hide everything initially
        _bannerGroup.alpha = 0f;
        _podiumGroup.alpha = 0f;
        podiumRoot.gameObject.SetActive(false);
    }

    void CreateBanner()
    {
        // Banner container (top center)
        GameObject bannerObj = new GameObject("FinishBanner");
        bannerRoot = bannerObj.AddComponent<RectTransform>();
        bannerRoot.SetParent(finishCanvas.transform, false);
        bannerRoot.anchorMin = new Vector2(0.2f, 0.7f);
        bannerRoot.anchorMax = new Vector2(0.8f, 0.95f);
        bannerRoot.offsetMin = Vector2.zero;
        bannerRoot.offsetMax = Vector2.zero;

        _bannerGroup = bannerObj.AddComponent<CanvasGroup>();

        // Banner background - sewage pipe look
        Image bannerBg = bannerObj.AddComponent<Image>();
        bannerBg.color = PipeGray;

        // Inner border
        GameObject borderObj = new GameObject("Border");
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.SetParent(bannerRoot, false);
        borderRect.anchorMin = new Vector2(0.01f, 0.03f);
        borderRect.anchorMax = new Vector2(0.99f, 0.97f);
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = SewageGreen;

        // "FINISH" text
        GameObject finishTextObj = new GameObject("FinishLabel");
        RectTransform finishRect = finishTextObj.AddComponent<RectTransform>();
        finishRect.SetParent(bannerRoot, false);
        finishRect.anchorMin = new Vector2(0.05f, 0.55f);
        finishRect.anchorMax = new Vector2(0.95f, 0.95f);
        finishRect.offsetMin = Vector2.zero;
        finishRect.offsetMax = Vector2.zero;
        _bannerText = finishTextObj.AddComponent<Text>();
        _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_bannerText.font == null)
            _bannerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _bannerText.fontSize = 36;
        _bannerText.fontStyle = FontStyle.Bold;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.color = GoldColor;
        _bannerText.text = "BROWN TOWN SEWAGE PLANT";
        Outline bannerOutline = finishTextObj.AddComponent<Outline>();
        bannerOutline.effectColor = new Color(0, 0, 0, 0.95f);
        bannerOutline.effectDistance = new Vector2(2, -2);

        // Place text (e.g., "YOU FINISHED 1ST!")
        GameObject placeObj = new GameObject("PlaceText");
        RectTransform placeRect = placeObj.AddComponent<RectTransform>();
        placeRect.SetParent(bannerRoot, false);
        placeRect.anchorMin = new Vector2(0.05f, 0.15f);
        placeRect.anchorMax = new Vector2(0.6f, 0.55f);
        placeRect.offsetMin = Vector2.zero;
        placeRect.offsetMax = Vector2.zero;
        _placeText = placeObj.AddComponent<Text>();
        _placeText.font = _bannerText.font;
        _placeText.fontSize = 28;
        _placeText.fontStyle = FontStyle.Bold;
        _placeText.alignment = TextAnchor.MiddleCenter;
        _placeText.color = Color.white;
        Outline placeOutline = placeObj.AddComponent<Outline>();
        placeOutline.effectColor = new Color(0, 0, 0, 0.9f);
        placeOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Time text
        GameObject timeObj = new GameObject("TimeText");
        RectTransform timeRect = timeObj.AddComponent<RectTransform>();
        timeRect.SetParent(bannerRoot, false);
        timeRect.anchorMin = new Vector2(0.6f, 0.15f);
        timeRect.anchorMax = new Vector2(0.95f, 0.55f);
        timeRect.offsetMin = Vector2.zero;
        timeRect.offsetMax = Vector2.zero;
        _timeText = timeObj.AddComponent<Text>();
        _timeText.font = _bannerText.font;
        _timeText.fontSize = 22;
        _timeText.alignment = TextAnchor.MiddleCenter;
        _timeText.color = new Color(0.8f, 0.8f, 0.75f);
        Outline timeOutline = timeObj.AddComponent<Outline>();
        timeOutline.effectColor = new Color(0, 0, 0, 0.85f);
        timeOutline.effectDistance = new Vector2(1, -1);
    }

    void CreatePodium()
    {
        // Podium container (center of screen)
        GameObject podObj = new GameObject("WinnersPoodium");
        podiumRoot = podObj.AddComponent<RectTransform>();
        podiumRoot.SetParent(finishCanvas.transform, false);
        podiumRoot.anchorMin = new Vector2(0.15f, 0.15f);
        podiumRoot.anchorMax = new Vector2(0.85f, 0.65f);
        podiumRoot.offsetMin = Vector2.zero;
        podiumRoot.offsetMax = Vector2.zero;

        _podiumGroup = podObj.AddComponent<CanvasGroup>();

        // Background panel
        Image podBg = podObj.AddComponent<Image>();
        podBg.color = new Color(0.1f, 0.08f, 0.05f, 0.85f);

        // "WINNERS POODIUM" title
        GameObject titleObj = new GameObject("PoodiumTitle");
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.SetParent(podiumRoot, false);
        titleRect.anchorMin = new Vector2(0.1f, 0.82f);
        titleRect.anchorMax = new Vector2(0.9f, 0.98f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null)
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.fontSize = 26;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = GoldColor;
        titleText.text = "WINNERS POODIUM";
        Outline titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0, 0, 0, 0.95f);
        titleOutline.effectDistance = new Vector2(2, -2);

        // Create 3 podium slots (2nd, 1st, 3rd order for visual layout)
        _podiumSlots = new PodiumSlot[3];
        float totalWidth = PODIUM_WIDTH * 3 + PODIUM_SPACING * 2;

        // Layout: [2nd] [1st] [3rd] - 1st in center and tallest
        float[] heights = { SECOND_HEIGHT, FIRST_HEIGHT, THIRD_HEIGHT };
        Color[] colors = { SilverColor, GoldColor, BronzeColor };
        string[] labels = { "2ND", "1ST", "3RD" };
        int[] placeOrder = { 1, 0, 2 }; // maps visual position to _podiumSlots index

        for (int vis = 0; vis < 3; vis++)
        {
            int slotIdx = placeOrder[vis];
            _podiumSlots[slotIdx] = CreatePodiumSlot(
                podiumRoot, vis, heights[vis], colors[vis], labels[vis], totalWidth);
        }
    }

    PodiumSlot CreatePodiumSlot(RectTransform parent, int visualIndex, float height, Color color, string label, float totalWidth)
    {
        PodiumSlot slot = new PodiumSlot();

        float xStart = (visualIndex * (PODIUM_WIDTH + PODIUM_SPACING));
        float xNorm = xStart / totalWidth;
        float xEndNorm = (xStart + PODIUM_WIDTH) / totalWidth;
        float yNormHeight = height / 200f; // normalize to container

        // Slot root
        GameObject slotObj = new GameObject($"Podium_{label}");
        slot.root = slotObj.AddComponent<RectTransform>();
        slot.root.SetParent(parent, false);
        slot.root.anchorMin = new Vector2(Mathf.Lerp(0.05f, 0.95f, xNorm), 0.05f);
        slot.root.anchorMax = new Vector2(Mathf.Lerp(0.05f, 0.95f, xEndNorm), 0.05f + yNormHeight);
        slot.root.offsetMin = Vector2.zero;
        slot.root.offsetMax = Vector2.zero;

        // Pedestal block
        slot.pedestal = slotObj.AddComponent<Image>();
        slot.pedestal.color = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.9f);

        // Place label on pedestal
        GameObject placeLabelObj = new GameObject("PlaceLabel");
        RectTransform placeRect = placeLabelObj.AddComponent<RectTransform>();
        placeRect.SetParent(slot.root, false);
        placeRect.anchorMin = new Vector2(0, 0);
        placeRect.anchorMax = new Vector2(1, 0.3f);
        placeRect.offsetMin = Vector2.zero;
        placeRect.offsetMax = Vector2.zero;
        slot.placeText = placeLabelObj.AddComponent<Text>();
        slot.placeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (slot.placeText.font == null)
            slot.placeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        slot.placeText.fontSize = 20;
        slot.placeText.fontStyle = FontStyle.Bold;
        slot.placeText.alignment = TextAnchor.MiddleCenter;
        slot.placeText.color = color;
        slot.placeText.text = label;
        Outline placeOutline = placeLabelObj.AddComponent<Outline>();
        placeOutline.effectColor = new Color(0, 0, 0, 0.9f);
        placeOutline.effectDistance = new Vector2(1, -1);

        // Color swatch (racer color indicator)
        GameObject swatchObj = new GameObject("ColorSwatch");
        RectTransform swatchRect = swatchObj.AddComponent<RectTransform>();
        swatchRect.SetParent(slot.root, false);
        swatchRect.anchorMin = new Vector2(0.35f, 0.85f);
        swatchRect.anchorMax = new Vector2(0.65f, 0.95f);
        swatchRect.offsetMin = Vector2.zero;
        swatchRect.offsetMax = Vector2.zero;
        slot.colorSwatch = swatchObj.AddComponent<Image>();
        slot.colorSwatch.color = Color.clear;

        // Racer name
        GameObject nameObj = new GameObject("RacerName");
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.SetParent(slot.root, false);
        nameRect.anchorMin = new Vector2(0.02f, 0.5f);
        nameRect.anchorMax = new Vector2(0.98f, 0.82f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        slot.nameText = nameObj.AddComponent<Text>();
        slot.nameText.font = slot.placeText.font;
        slot.nameText.fontSize = 14;
        slot.nameText.fontStyle = FontStyle.Bold;
        slot.nameText.alignment = TextAnchor.MiddleCenter;
        slot.nameText.color = Color.white;
        slot.nameText.text = "";
        Outline nameOutline = nameObj.AddComponent<Outline>();
        nameOutline.effectColor = new Color(0, 0, 0, 0.85f);
        nameOutline.effectDistance = new Vector2(1, -1);

        // Finish time
        GameObject timeObj = new GameObject("Time");
        RectTransform timeRect = timeObj.AddComponent<RectTransform>();
        timeRect.SetParent(slot.root, false);
        timeRect.anchorMin = new Vector2(0.05f, 0.3f);
        timeRect.anchorMax = new Vector2(0.95f, 0.5f);
        timeRect.offsetMin = Vector2.zero;
        timeRect.offsetMax = Vector2.zero;
        slot.timeText = timeObj.AddComponent<Text>();
        slot.timeText.font = slot.placeText.font;
        slot.timeText.fontSize = 12;
        slot.timeText.alignment = TextAnchor.MiddleCenter;
        slot.timeText.color = new Color(0.75f, 0.75f, 0.7f);
        slot.timeText.text = "";

        return slot;
    }

    /// <summary>Called when the player crosses the finish line.</summary>
    public void OnPlayerFinished(int place, float time)
    {
        if (!_initialized) return;

        _bannerGroup.alpha = 1f;

        string ordinal = GetOrdinal(place);
        Color placeColor = place == 1 ? GoldColor :
                           place == 2 ? SilverColor :
                           place == 3 ? BronzeColor : Color.white;

        _placeText.text = $"YOU FINISHED {place}{ordinal}!";
        _placeText.color = placeColor;
        _timeText.text = $"Time: {time:F1}s";

        // Celebratory scale punch
        StartCoroutine(BannerPunchAnimation());

        if (ProceduralAudio.Instance != null)
        {
            if (place <= 3)
                ProceduralAudio.Instance.PlayCelebration();
        }

        HapticManager.MediumTap();
    }

    IEnumerator BannerPunchAnimation()
    {
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
            bannerRoot.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        bannerRoot.localScale = Vector3.one;
    }

    /// <summary>Show the Winners Poodium with top 3 finishers.</summary>
    public void ShowPodium(List<RaceManager.RacerEntry> entries)
    {
        if (!_initialized || _podiumShown) return;
        _podiumShown = true;

        StartCoroutine(PodiumRevealSequence(entries));
    }

    IEnumerator PodiumRevealSequence(List<RaceManager.RacerEntry> entries)
    {
        // Sort by finish place
        var sorted = new List<RaceManager.RacerEntry>(entries);
        sorted.Sort((a, b) => a.finishPlace.CompareTo(b.finishPlace));

        // Build 3D podium in world space
        Create3DPodium(sorted);

        yield return new WaitForSeconds(1.5f);

        podiumRoot.gameObject.SetActive(true);

        // Fade in podium UI overlay
        float fadeTime = 0.8f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            _podiumGroup.alpha = elapsed / fadeTime;
            yield return null;
        }
        _podiumGroup.alpha = 1f;

        // Reveal each place with a delay
        for (int i = 0; i < 3 && i < sorted.Count; i++)
        {
            var entry = sorted[i];
            var slot = _podiumSlots[i];

            slot.nameText.text = entry.name;
            slot.colorSwatch.color = entry.color;

            if (i == 0)
                slot.timeText.text = $"{entry.finishTime:F1}s";
            else
                slot.timeText.text = $"+{entry.finishTime - sorted[0].finishTime:F1}s";

            // Highlight player's slot
            if (entry.isPlayer)
            {
                slot.nameText.color = GoldColor;
                slot.pedestal.color = new Color(0.4f, 0.35f, 0.05f, 0.9f);
            }

            // Punch animation per slot
            StartCoroutine(SlotPunchAnimation(slot.root));

            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayCoinCollect();

            HapticManager.LightTap();

            yield return new WaitForSeconds(0.6f);
        }

        // Start confetti after all revealed
        if (_confetti != null)
            _confetti.Play();

        // Pan camera to podium
        StartCoroutine(PodiumCameraSequence());
    }

    void Create3DPodium(List<RaceManager.RacerEntry> sorted)
    {
        // Position podium ahead of the finish line (in open space)
        Vector3 podiumPos = Vector3.zero;
        if (RaceManager.Instance != null && RaceManager.Instance.PlayerController != null)
            podiumPos = RaceManager.Instance.PlayerController.transform.position + Vector3.forward * 15f;

        _podium3D = new GameObject("Podium3D");
        _podium3D.transform.position = podiumPos;

        Shader toonLit = Shader.Find("Custom/ToonLit");
        Shader shader = toonLit != null ? toonLit : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        // Platform heights: 1st = tallest (center), 2nd = left, 3rd = right
        float[] heights = { 2.5f, 1.8f, 1.2f };
        float[] xOffsets = { 0f, -2.5f, 2.5f };
        Color[] pedestalColors = { GoldColor * 0.5f, SilverColor * 0.5f, BronzeColor * 0.5f };
        string[] labels = { "1ST", "2ND", "3RD" };

        for (int i = 0; i < 3 && i < sorted.Count; i++)
        {
            // Pedestal block
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = $"Pedestal_{labels[i]}";
            pedestal.transform.SetParent(_podium3D.transform);
            pedestal.transform.localPosition = new Vector3(xOffsets[i], heights[i] * 0.5f, 0);
            pedestal.transform.localScale = new Vector3(2f, heights[i], 2f);

            Material pedMat = new Material(shader);
            pedMat.SetColor("_BaseColor", pedestalColors[i]);
            pedMat.EnableKeyword("_EMISSION");
            pedMat.SetColor("_EmissionColor", pedestalColors[i] * 0.3f);
            pedestal.GetComponent<Renderer>().material = pedMat;

            // Place label on front of pedestal
            GameObject labelObj = new GameObject($"Label_{labels[i]}");
            labelObj.transform.SetParent(pedestal.transform);
            labelObj.transform.localPosition = new Vector3(0, 0.3f, 0.51f);
            labelObj.transform.localScale = new Vector3(0.5f / 2f, 1f / heights[i], 1f);
            TextMesh tm = labelObj.AddComponent<TextMesh>();
            tm.text = labels[i];
            tm.fontSize = 64;
            tm.characterSize = 0.08f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            Color labelColor = i == 0 ? GoldColor : (i == 1 ? SilverColor : BronzeColor);
            tm.color = labelColor;
            tm.fontStyle = FontStyle.Bold;

            // Move racer model onto podium
            var entry = sorted[i];
            if (entry.transform != null)
            {
                entry.transform.position = podiumPos + new Vector3(xOffsets[i], heights[i] + 0.5f, 0);
                entry.transform.rotation = Quaternion.LookRotation(Vector3.back); // face camera
            }
        }

        // Confetti particle system
        GameObject confettiObj = new GameObject("Confetti");
        confettiObj.transform.SetParent(_podium3D.transform);
        confettiObj.transform.localPosition = new Vector3(0, 5f, 0);

        _confetti = confettiObj.AddComponent<ParticleSystem>();
        var main = _confetti.main;
        main.startLifetime = 3f;
        main.startSpeed = 2f;
        main.startSize = 0.15f;
        main.maxParticles = 200;
        main.loop = true;
        main.startColor = new ParticleSystem.MinMaxGradient(GoldColor, new Color(0.2f, 0.8f, 0.3f));
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = _confetti.emission;
        emission.rateOverTime = 50f;

        var shape = _confetti.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(8f, 0.1f, 4f);

        var colorOverLife = _confetti.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(GoldColor, 0.5f),
                new GradientColorKey(new Color(0.8f, 0.2f, 0.2f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        // Use default particle material
        ParticleSystemRenderer psr = confettiObj.GetComponent<ParticleSystemRenderer>();
        Material confettiMat = new Material(Shader.Find("Particles/Standard Unlit"));
        if (confettiMat != null)
        {
            confettiMat.SetColor("_Color", Color.white);
            psr.material = confettiMat;
        }

        _confetti.Stop(); // Will start after reveal
    }

    IEnumerator PodiumCameraSequence()
    {
        if (_podium3D == null) yield break;

        Camera cam = Camera.main;
        if (cam == null) yield break;

        // Disable PipeCamera
        PipeCamera pipeCam = cam.GetComponent<PipeCamera>();
        if (pipeCam != null)
            pipeCam.enabled = false;

        Vector3 podiumCenter = _podium3D.transform.position + Vector3.up * 2f;
        float camDist = 8f;
        float elapsed = 0f;
        float duration = 8f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float angle = (elapsed / duration) * 180f * Mathf.Deg2Rad; // half orbit
            float height = 2f + Mathf.Sin(elapsed * 0.3f) * 1f;

            Vector3 camPos = podiumCenter + new Vector3(
                Mathf.Cos(angle) * camDist,
                height,
                Mathf.Sin(angle) * camDist
            );

            cam.transform.position = Vector3.Lerp(cam.transform.position, camPos, Time.deltaTime * 2f);
            cam.transform.LookAt(podiumCenter);

            yield return null;
        }
    }

    IEnumerator SlotPunchAnimation(RectTransform rect)
    {
        float duration = 0.4f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f;
            rect.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    /// <summary>Hide all finish UI (for race restart).</summary>
    public void Reset()
    {
        if (!_initialized) return;
        _podiumShown = false;
        _bannerGroup.alpha = 0f;
        _podiumGroup.alpha = 0f;
        podiumRoot.gameObject.SetActive(false);
        bannerRoot.localScale = Vector3.one;

        for (int i = 0; i < 3; i++)
        {
            _podiumSlots[i].nameText.text = "";
            _podiumSlots[i].timeText.text = "";
            _podiumSlots[i].colorSwatch.color = Color.clear;
        }
    }

    static string GetOrdinal(int n)
    {
        if (n == 1) return "ST";
        if (n == 2) return "ND";
        if (n == 3) return "RD";
        return "TH";
    }
}
