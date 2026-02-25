using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Poop Crew — 6 unique bouncing poop characters across the bottom of the screen.
/// Each has unique accessories, animated facial expressions that react to game events,
/// and the middle 4 hold up signs spelling words like POOP, GOGO, FART.
/// No window/panel — just free-floating little nuggets.
/// </summary>
public class CheerOverlay : MonoBehaviour
{
    public static CheerOverlay Instance { get; private set; }

    const int COUNT = 6;
    const int SIGN_FIRST = 1;  // poops 1-4 hold sign letters
    const int SIGN_LAST = 4;

    struct Poop
    {
        public RectTransform root;
        public RectTransform leftArm, rightArm;
        public RectTransform signBoard;
        public Text signLetter;
        public Image leftEyelid, rightEyelid;
        public RectTransform leftPupilRt, rightPupilRt;
        public RectTransform mouthRt;
        // per-poop expression defaults
        public float defLidPct;   // 0=open, 1=shut
        public float defMouthW, defMouthH;
        // animation state
        public float baseY, phase;
        public float excitement;  // 0..1 decays
        public float blinkTimer, nextBlink;
        public Vector2 pupilTarget;
        public float pupilTimer;
        // interaction & special animation state
        public float interactTimer;
        public int interactState;     // 0=none, 1=highFive, 2=fistBump, 3=hipBump
        public int interactPartner;
        public float interactT;
        public float cartwheelT;
        public float cartwheelDir;
    }

    Poop[] _p = new Poop[COUNT];

    // Sign word system
    static readonly string[] IDLE_WORDS = {
        "POOP", "GOGO", "FART", "TURD", "DOPE",
        "NICE", "YOLO", "SURF", "WIPE", "CORN"
    };
    static readonly string[] HYPE_WORDS = {
        "SICK", "WHOA", "WOOT", "YEET", "HYPE", "YEAH", "DANG"
    };
    static readonly string[] BIG_WORDS = {
        "EPIC", "GOAT", "OMG!", "WOW!", "NUTS", "FIRE"
    };

    string _currentWord = "POOP";
    string _pendingWord;
    float _wordTimer;
    bool _flipping;
    float _flipT;

    // Interaction & pyramid animation
    bool _pyramidActive;
    float _pyramidT;
    // Pyramid target positions: bottom row (0,3,5), middle row (1,4), top (2=sunglasses)
    static readonly float[] PYRAMID_X = { 0.30f, 0.40f, 0.50f, 0.50f, 0.60f, 0.70f };
    static readonly float[] PYRAMID_Y = { -140f, -40f, 60f, -140f, -40f, -140f };

    Sprite _circle;

    // X positions across screen width (anchors)
    static readonly float[] POS_X = { 0.08f, 0.24f, 0.40f, 0.60f, 0.76f, 0.92f };

    // Slight body color variation per poop
    static readonly Color[] BROWN = {
        new Color(0.42f, 0.26f, 0.15f),
        new Color(0.46f, 0.29f, 0.14f),
        new Color(0.38f, 0.24f, 0.16f),
        new Color(0.44f, 0.27f, 0.13f),
        new Color(0.40f, 0.25f, 0.17f),
        new Color(0.43f, 0.30f, 0.15f),
    };

    void Awake()
    {
        Instance = this;
        _circle = MakeCircleSprite(64);
    }

    void Start()
    {
        BuildAllPoops();
        ApplyWord(_currentWord);
        _wordTimer = Random.Range(6f, 12f);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Idle word rotation
        _wordTimer -= dt;
        if (_wordTimer <= 0f && !_flipping)
        {
            _wordTimer = Random.Range(8f, 15f);
            FlipTo(IDLE_WORDS[Random.Range(0, IDLE_WORDS.Length)]);
        }

        // Sign flip animation
        if (_flipping)
        {
            _flipT += dt * 3.5f;
            if (_flipT >= 0.5f && _pendingWord != null)
            {
                ApplyWord(_pendingWord);
                _currentWord = _pendingWord;
                _pendingWord = null;
            }
            if (_flipT >= 1f)
            {
                _flipT = 0f;
                _flipping = false;
            }
            float sy = _flipT < 0.5f
                ? Mathf.Lerp(1f, 0f, _flipT * 2f)
                : Mathf.Lerp(0f, 1f, (_flipT - 0.5f) * 2f);
            for (int i = SIGN_FIRST; i <= SIGN_LAST; i++)
                if (_p[i].signBoard != null)
                {
                    Vector3 s = _p[i].signBoard.localScale;
                    s.y = sy;
                    _p[i].signBoard.localScale = s;
                }
        }

        // Animate each poop
        for (int i = 0; i < COUNT; i++)
            Animate(i, dt);

        if (_pyramidActive)
            AnimatePyramid(dt);
    }

    // === PUBLIC API ===

    public void ShowCheer(string message, Color color, bool bigEvent = false)
    {
        // Boost excitement
        for (int i = 0; i < COUNT; i++)
        {
            float boost = bigEvent ? 0.85f : 0.35f;
            // Random variation — not all poops react the same
            if (!bigEvent) boost *= Random.Range(0.5f, 1.2f);
            _p[i].excitement = Mathf.Min(1f, _p[i].excitement + boost);
        }

        // Flip to hype/big word
        if (bigEvent)
            FlipTo(BIG_WORDS[Random.Range(0, BIG_WORDS.Length)]);
        else if (Random.value < 0.6f)
            FlipTo(HYPE_WORDS[Random.Range(0, HYPE_WORDS.Length)]);

        _wordTimer = Random.Range(4f, 8f);
    }

    /// <summary>Start cheerleading pyramid formation (race finish celebration)</summary>
    public void StartPyramid()
    {
        _pyramidActive = true;
        _pyramidT = 0f;
        for (int i = 0; i < COUNT; i++)
        {
            var p = _p[i];
            p.excitement = 1f;
            p.interactState = 0;
            p.cartwheelT = 0f;
            _p[i] = p;
        }
        FlipTo(BIG_WORDS[Random.Range(0, BIG_WORDS.Length)]);
    }

    /// <summary>End pyramid, return to normal positions</summary>
    public void StopPyramid()
    {
        _pyramidActive = false;
        for (int i = 0; i < COUNT; i++)
        {
            var p = _p[i];
            p.baseY = -140f;
            p.root.anchorMin = new Vector2(POS_X[i], 0f);
            p.root.anchorMax = new Vector2(POS_X[i], 0f);
            p.root.localRotation = Quaternion.identity;
            _p[i] = p;
        }
    }

    // === ANIMATION ===

    void Animate(int i, float dt)
    {
        var p = _p[i];
        if (p.root == null) return;

        // Decay excitement
        p.excitement = Mathf.MoveTowards(p.excitement, 0f, dt * 0.3f);

        // === BOUNCE ===
        float amp = 3f + p.excitement * 16f;
        float spd = 2.2f + p.excitement * 3f;
        float wave = Mathf.Sin(Time.time * spd + p.phase);
        float bob = Mathf.Abs(wave) * amp;

        // Squash at bottom, stretch at top
        float sx = 1f, sy = 1f;
        if (wave < -0.7f)
        {
            float q = (-wave - 0.7f) / 0.3f;
            sx = 1f + q * 0.15f;
            sy = 1f - q * 0.12f;
        }
        else if (wave > 0.85f)
        {
            float q = (wave - 0.85f) / 0.15f;
            sx = 1f - q * 0.08f;
            sy = 1f + q * 0.10f;
        }

        p.root.anchoredPosition = new Vector2(0, p.baseY + bob);
        p.root.localScale = new Vector3(sx, sy, 1f);

        // === ARMS ===
        float armBase = 50f + p.excitement * 25f;
        float armWave = Mathf.Sin(Time.time * 5f + p.phase) * (5f + p.excitement * 20f);
        if (p.leftArm != null)
            p.leftArm.localRotation = Quaternion.Euler(0, 0, armBase + armWave);
        if (p.rightArm != null)
            p.rightArm.localRotation = Quaternion.Euler(0, 0, -(armBase - armWave));

        // === BLINK ===
        p.blinkTimer -= dt;
        float lidTarget = p.defLidPct;
        bool blinking = p.blinkTimer <= 0f && p.blinkTimer > -0.12f;
        if (blinking)
            lidTarget = 1f;
        else if (p.blinkTimer <= -0.12f)
        {
            p.blinkTimer = p.nextBlink;
            p.nextBlink = Random.Range(2f, 5f);
        }

        // Excitement opens eyes (less lid)
        float exciteLid = Mathf.Lerp(p.defLidPct, Mathf.Max(0f, p.defLidPct - 0.3f), p.excitement);
        float finalLid = blinking ? 1f : exciteLid;

        float lidH = Mathf.Lerp(0f, 22f, finalLid);
        if (p.leftEyelid != null)
        {
            RectTransform lrt = p.leftEyelid.rectTransform;
            lrt.sizeDelta = new Vector2(lrt.sizeDelta.x, lidH);
        }
        if (p.rightEyelid != null)
        {
            RectTransform rrt = p.rightEyelid.rectTransform;
            rrt.sizeDelta = new Vector2(rrt.sizeDelta.x, lidH);
        }

        // === PUPILS (player-aware gaze) ===
        p.pupilTimer -= dt;
        if (p.pupilTimer <= 0f)
        {
            // Decide gaze target: 50% look at camera (player), 25% look at neighbor poop, 25% random
            float roll = Random.value;
            if (roll < 0.50f)
            {
                // Look toward screen center-bottom (where action happens)
                // Bias X toward center of screen relative to this poop's position
                float screenX = POS_X[i];
                float toCenter = (0.5f - screenX) * 2.5f; // pull toward center
                p.pupilTarget = new Vector2(
                    Mathf.Clamp(toCenter + Random.Range(-0.3f, 0.3f), -1.5f, 1.5f),
                    Random.Range(0.5f, 1.2f)); // look slightly upward (at the game)
            }
            else if (roll < 0.75f && COUNT > 1)
            {
                // Glance at a neighbor poop
                int neighbor = (i + (Random.value < 0.5f ? 1 : -1) + COUNT) % COUNT;
                float nX = POS_X[neighbor];
                float myX = POS_X[i];
                p.pupilTarget = new Vector2(
                    Mathf.Clamp((nX - myX) * 6f, -1.5f, 1.5f),
                    Random.Range(-0.5f, 0.3f));
            }
            else
            {
                // Random idle wander
                p.pupilTarget = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-1f, 1f));
            }

            // Excited poops shift gaze faster (more alert)
            p.pupilTimer = Mathf.Lerp(Random.Range(0.8f, 2.5f), Random.Range(0.3f, 0.8f), p.excitement);
        }

        // Faster tracking when excited
        float trackSpeed = Mathf.Lerp(6f, 14f, p.excitement);
        if (p.leftPupilRt != null)
            p.leftPupilRt.anchoredPosition = Vector2.Lerp(
                p.leftPupilRt.anchoredPosition, p.pupilTarget, dt * trackSpeed);
        if (p.rightPupilRt != null)
            p.rightPupilRt.anchoredPosition = Vector2.Lerp(
                p.rightPupilRt.anchoredPosition, p.pupilTarget, dt * trackSpeed);

        // === MOUTH ===
        if (p.mouthRt != null)
        {
            float mw = Mathf.Lerp(p.defMouthW, p.defMouthW * 1.4f, p.excitement);
            float mh = Mathf.Lerp(p.defMouthH, p.defMouthH + 10f, p.excitement);
            p.mouthRt.sizeDelta = new Vector2(mw, mh);
        }

        // === INTERACTIONS (high fives, fist bumps, hip bumps between neighbors) ===
        if (!_pyramidActive)
        {
            p.interactTimer -= dt;
            if (p.interactTimer <= 0f && p.interactState == 0)
            {
                // Chance scales with excitement, but can happen during idle too
                if (p.excitement > 0.15f || Random.value < 0.06f)
                {
                    int left = i - 1, right = i + 1;
                    int neighbor = -1;
                    if (left >= 0 && right < COUNT)
                        neighbor = Random.value < 0.5f ? left : right;
                    else if (left >= 0) neighbor = left;
                    else if (right < COUNT) neighbor = right;

                    if (neighbor >= 0 && _p[neighbor].interactState == 0 && _p[neighbor].cartwheelT <= 0f)
                    {
                        int type = Random.Range(1, 4); // 1=highFive, 2=fistBump, 3=hipBump
                        p.interactState = type;
                        p.interactPartner = neighbor;
                        p.interactT = 0f;
                        var n = _p[neighbor];
                        n.interactState = type;
                        n.interactPartner = i;
                        n.interactT = 0f;
                        _p[neighbor] = n;
                    }
                }
                p.interactTimer = Random.Range(3f, 7f);
            }

            if (p.interactState > 0)
            {
                p.interactT += dt * 2.2f;
                if (p.interactT >= 1f)
                {
                    p.interactState = 0;
                    p.interactT = 0f;
                }
                else
                {
                    float reach = Mathf.Sin(p.interactT * Mathf.PI); // smooth 0->1->0 arc
                    bool partnerRight = p.interactPartner > i;

                    switch (p.interactState)
                    {
                        case 1: // High five — arm shoots up toward partner
                        {
                            float target = partnerRight ? -120f : 120f;
                            float baseAngle = partnerRight ? -(armBase - armWave) : (armBase + armWave);
                            RectTransform arm = partnerRight ? p.rightArm : p.leftArm;
                            if (arm != null)
                                arm.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(baseAngle, target, reach));
                            break;
                        }
                        case 2: // Fist bump — arm extends horizontally toward partner
                        {
                            float target = partnerRight ? -5f : 5f;
                            float baseAngle = partnerRight ? -(armBase - armWave) : (armBase + armWave);
                            RectTransform arm = partnerRight ? p.rightArm : p.leftArm;
                            if (arm != null)
                                arm.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(baseAngle, target, reach));
                            break;
                        }
                        case 3: // Hip bump — lean sideways toward partner
                        {
                            float lean = (partnerRight ? -10f : 10f) * reach;
                            float xShift = (partnerRight ? 8f : -8f) * reach;
                            p.root.localRotation = Quaternion.Euler(0, 0, lean);
                            Vector2 pos = p.root.anchoredPosition;
                            pos.x += xShift;
                            p.root.anchoredPosition = pos;
                            break;
                        }
                    }
                }
            }

            // === CARTWHEEL (random during high excitement) ===
            if (p.cartwheelT > 0f)
            {
                p.cartwheelT += dt * 1.5f;
                if (p.cartwheelT >= 1f)
                {
                    p.cartwheelT = 0f;
                    p.root.localRotation = Quaternion.identity;
                }
                else
                {
                    float spin = p.cartwheelDir * 360f * p.cartwheelT;
                    float hop = Mathf.Sin(p.cartwheelT * Mathf.PI) * 35f;
                    Vector2 pos = p.root.anchoredPosition;
                    pos.y += hop;
                    p.root.anchoredPosition = pos;
                    p.root.localRotation = Quaternion.Euler(0, 0, spin);
                }
            }
            else if (p.excitement > 0.6f && p.interactState == 0 && Random.value < 0.003f)
            {
                p.cartwheelT = 0.01f;
                p.cartwheelDir = Random.value < 0.5f ? 1f : -1f;
            }
        }

        _p[i] = p;
    }

    // === PYRAMID FORMATION ===

    void AnimatePyramid(float dt)
    {
        _pyramidT = Mathf.MoveTowards(_pyramidT, 1f, dt * 0.7f);
        float t = Mathf.SmoothStep(0f, 1f, _pyramidT);

        for (int i = 0; i < COUNT; i++)
        {
            var p = _p[i];
            if (p.root == null) continue;

            // Lerp anchor X toward pyramid position
            float targetX = PYRAMID_X[i];
            float currentX = Mathf.Lerp(POS_X[i], targetX, t);
            p.root.anchorMin = new Vector2(currentX, 0f);
            p.root.anchorMax = new Vector2(currentX, 0f);

            // Lerp baseY toward pyramid height
            p.baseY = Mathf.Lerp(-140f, PYRAMID_Y[i], t);

            // Arms out wide like cheerleaders
            float armSpread = Mathf.Lerp(50f, 88f, t);
            if (p.leftArm != null)
                p.leftArm.localRotation = Quaternion.Euler(0, 0, armSpread);
            if (p.rightArm != null)
                p.rightArm.localRotation = Quaternion.Euler(0, 0, -armSpread);

            // Gentle synchronized wobble when pyramid is formed
            if (t > 0.85f)
            {
                float wobble = Mathf.Sin(Time.time * 3f + i * 0.7f) * 3f;
                p.root.localRotation = Quaternion.Euler(0, 0, wobble);
            }

            _p[i] = p;
        }
    }

    // === SIGN SYSTEM ===

    void FlipTo(string word)
    {
        if (word == _currentWord || _flipping) return;
        _pendingWord = word;
        _flipping = true;
        _flipT = 0f;
    }

    void ApplyWord(string word)
    {
        int len = Mathf.Min(word.Length, SIGN_LAST - SIGN_FIRST + 1);
        for (int i = SIGN_FIRST; i <= SIGN_LAST; i++)
        {
            int li = i - SIGN_FIRST;
            if (_p[i].signLetter != null)
                _p[i].signLetter.text = li < len ? word[li].ToString() : "";
        }
    }

    // === CONSTRUCTION ===

    void BuildAllPoops()
    {
        RectTransform container = GetComponent<RectTransform>();
        for (int i = 0; i < COUNT; i++)
            _p[i] = BuildPoop(container, i);
    }

    Poop BuildPoop(RectTransform parent, int idx)
    {
        Poop p = new Poop();
        Color b1 = BROWN[idx];
        Color b2 = b1 + new Color(0.08f, 0.07f, 0.05f, 0f);
        Color b3 = b1 + new Color(0.16f, 0.14f, 0.11f, 0f);
        Color eyeW = new Color(0.95f, 0.95f, 0.92f);
        Color pupilC = new Color(0.05f, 0.05f, 0.08f);
        Color mouthC = new Color(0.28f, 0.13f, 0.05f);
        Color lidC = b1 * 0.85f; lidC.a = 1f;

        // Root
        GameObject root = new GameObject($"Poop{idx}");
        root.transform.SetParent(parent, false);
        p.root = root.AddComponent<RectTransform>();
        p.root.anchorMin = new Vector2(POS_X[idx], 0f);
        p.root.anchorMax = new Vector2(POS_X[idx], 0f);
        p.root.pivot = new Vector2(0.5f, 0f);
        p.root.sizeDelta = new Vector2(180, 240);
        p.root.anchoredPosition = new Vector2(0, -140);
        p.baseY = -140f;
        p.phase = idx * 1.1f + Random.Range(0f, 1.5f);

        // Body
        Circle(root.transform, "Base", b1, new Vector2(0, 10), new Vector2(72, 60));
        Circle(root.transform, "Body", b2, new Vector2(0, 38), new Vector2(56, 48));
        Circle(root.transform, "Tip", b3, new Vector2(6, 68), new Vector2(34, 30));

        // Eyes
        GameObject le = Circle(root.transform, "LEye", eyeW, new Vector2(-14, 48), new Vector2(22, 22));
        p.leftPupilRt = Circle(le.transform, "LP", pupilC, Vector2.zero, new Vector2(10, 10))
            .GetComponent<RectTransform>();
        GameObject re = Circle(root.transform, "REye", eyeW, new Vector2(14, 48), new Vector2(22, 22));
        p.rightPupilRt = Circle(re.transform, "RP", pupilC, Vector2.zero, new Vector2(10, 10))
            .GetComponent<RectTransform>();

        // Eyelids (anchored at top of eye, grow downward to close)
        p.leftEyelid = Rect(le.transform, "LLid", lidC, new Vector2(0, 11f),
            new Vector2(24, 0), new Vector2(0.5f, 1f));
        p.rightEyelid = Rect(re.transform, "RLid", lidC, new Vector2(0, 11f),
            new Vector2(24, 0), new Vector2(0.5f, 1f));

        // Expression defaults per accessory/personality
        SetDefaults(ref p, idx);

        // Mouth
        GameObject mo = Circle(root.transform, "Mouth", mouthC,
            new Vector2(0, 32), new Vector2(p.defMouthW, p.defMouthH));
        p.mouthRt = mo.GetComponent<RectTransform>();

        // Arms
        p.leftArm = Arm(root.transform, "LA", b1, new Vector2(-36, 30), 50f, true);
        p.rightArm = Arm(root.transform, "RA", b1, new Vector2(36, 30), -50f, false);

        // Sign (middle 4 only)
        if (idx >= SIGN_FIRST && idx <= SIGN_LAST)
            BuildSign(root.transform, ref p);

        // Unique accessory
        BuildAccessory(root.transform, idx, b1, b3);

        // Init timers
        p.blinkTimer = Random.Range(1f, 4f);
        p.nextBlink = Random.Range(2f, 5f);
        p.pupilTimer = Random.Range(0.3f, 1.5f);
        p.pupilTarget = Vector2.zero;
        p.interactTimer = Random.Range(2f, 6f);

        return p;
    }

    void SetDefaults(ref Poop p, int idx)
    {
        // Each poop has a distinct resting expression
        switch (idx)
        {
            case 0: // Baseball Cap — happy
                p.defLidPct = 0.05f; p.defMouthW = 18f; p.defMouthH = 7f; break;
            case 1: // Bow — sparkly/excited
                p.defLidPct = 0f; p.defMouthW = 14f; p.defMouthH = 8f; break;
            case 2: // Sunglasses — cool
                p.defLidPct = 0.4f; p.defMouthW = 12f; p.defMouthH = 4f; break;
            case 3: // Bandana — tough
                p.defLidPct = 0.35f; p.defMouthW = 16f; p.defMouthH = 5f; break;
            case 4: // Crown — surprised
                p.defLidPct = 0f; p.defMouthW = 12f; p.defMouthH = 12f; break;
            case 5: // Rosy Cheeks — sweet
                p.defLidPct = 0.1f; p.defMouthW = 16f; p.defMouthH = 6f; break;
        }
    }

    void BuildSign(Transform parent, ref Poop p)
    {
        GameObject board = new GameObject("Sign");
        board.transform.SetParent(parent, false);
        p.signBoard = board.AddComponent<RectTransform>();
        p.signBoard.anchoredPosition = new Vector2(0, 116);
        p.signBoard.sizeDelta = new Vector2(52, 44);

        Image bg = board.AddComponent<Image>();
        bg.sprite = _circle;
        bg.color = new Color(0.95f, 0.92f, 0.85f);

        Outline ol = board.AddComponent<Outline>();
        ol.effectColor = new Color(0.3f, 0.25f, 0.2f);
        ol.effectDistance = new Vector2(3f, -3f);

        // Stick connecting sign to arm area
        GameObject stick = new GameObject("Stick");
        stick.transform.SetParent(parent, false);
        RectTransform stickRt = stick.AddComponent<RectTransform>();
        stickRt.anchoredPosition = new Vector2(0, 96);
        stickRt.sizeDelta = new Vector2(4, 28);
        Image stickImg = stick.AddComponent<Image>();
        stickImg.color = new Color(0.5f, 0.4f, 0.3f);

        // Letter
        GameObject ltr = new GameObject("Ltr");
        ltr.transform.SetParent(board.transform, false);
        RectTransform lrt = ltr.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        p.signLetter = ltr.AddComponent<Text>();
        p.signLetter.font = font;
        p.signLetter.fontSize = 36;
        p.signLetter.alignment = TextAnchor.MiddleCenter;
        p.signLetter.fontStyle = FontStyle.Bold;
        p.signLetter.color = new Color(0.15f, 0.1f, 0.05f);
    }

    void BuildAccessory(Transform parent, int idx, Color b1, Color b3)
    {
        switch (idx)
        {
            case 0: // Baseball cap
            {
                Color red = new Color(0.85f, 0.15f, 0.1f);
                Circle(parent, "CapBrim", red, new Vector2(-6, 78), new Vector2(48, 12));
                Circle(parent, "CapTop", red, new Vector2(2, 86), new Vector2(36, 20));
                // Little button on top
                Circle(parent, "Button", new Color(0.95f, 0.9f, 0.8f), new Vector2(2, 96), new Vector2(8, 8));
                break;
            }
            case 1: // Bow
            {
                Color pink = new Color(1f, 0.4f, 0.6f);
                Circle(parent, "BowL", pink, new Vector2(-6, 82), new Vector2(20, 16));
                Circle(parent, "BowR", pink, new Vector2(18, 82), new Vector2(20, 16));
                Circle(parent, "Knot", new Color(0.9f, 0.3f, 0.5f), new Vector2(6, 82), new Vector2(10, 10));
                break;
            }
            case 2: // Sunglasses
            {
                Color dark = new Color(0.08f, 0.06f, 0.12f, 0.92f);
                Rect(parent, "GlL", dark, new Vector2(-14, 48), new Vector2(26, 18), new Vector2(0.5f, 0.5f));
                Rect(parent, "GlR", dark, new Vector2(14, 48), new Vector2(26, 18), new Vector2(0.5f, 0.5f));
                Rect(parent, "Bridge", dark, new Vector2(0, 50), new Vector2(8, 4), new Vector2(0.5f, 0.5f));
                // Glint
                Circle(parent, "Glint", new Color(1f, 1f, 1f, 0.4f), new Vector2(-18, 52), new Vector2(6, 6));
                break;
            }
            case 3: // Bandana
            {
                Color green = new Color(0.15f, 0.6f, 0.2f);
                Rect(parent, "Band", green, new Vector2(0, 62), new Vector2(64, 10), new Vector2(0.5f, 0.5f));
                Rect(parent, "Tail1", green, new Vector2(32, 56), new Vector2(18, 8), new Vector2(0f, 1f));
                Rect(parent, "Tail2", green, new Vector2(36, 50), new Vector2(14, 6), new Vector2(0f, 1f));
                break;
            }
            case 4: // Crown
            {
                Color gold = new Color(1f, 0.85f, 0.15f);
                Rect(parent, "CrownBase", gold, new Vector2(6, 82), new Vector2(40, 10), new Vector2(0.5f, 0.5f));
                Circle(parent, "Pt1", gold, new Vector2(-6, 94), new Vector2(10, 14));
                Circle(parent, "Pt2", gold, new Vector2(6, 98), new Vector2(10, 16));
                Circle(parent, "Pt3", gold, new Vector2(18, 94), new Vector2(10, 14));
                Circle(parent, "Gem", new Color(0.9f, 0.1f, 0.15f), new Vector2(6, 86), new Vector2(8, 8));
                break;
            }
            case 5: // Rosy cheeks
            {
                Color blush = new Color(1f, 0.5f, 0.55f, 0.5f);
                Circle(parent, "BlL", blush, new Vector2(-24, 40), new Vector2(16, 12));
                Circle(parent, "BlR", blush, new Vector2(24, 40), new Vector2(16, 12));
                // Sparkle
                Circle(parent, "Sp1", new Color(1f, 1f, 1f, 0.7f), new Vector2(-18, 56), new Vector2(6, 6));
                Circle(parent, "Sp2", new Color(1f, 1f, 0.8f, 0.5f), new Vector2(20, 58), new Vector2(4, 4));
                break;
            }
        }
    }

    // === UI HELPERS ===

    GameObject Circle(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.sprite = _circle;
        img.color = color;
        return go;
    }

    Image Rect(Transform parent, string name, Color color, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.pivot = pivot;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    RectTransform Arm(Transform parent, string name, Color color, Vector2 pos, float angle, bool isLeft)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(24, 8);
        rt.pivot = isLeft ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        Image img = go.AddComponent<Image>();
        img.sprite = _circle;
        img.color = color;
        return rt;
    }

    static Sprite MakeCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = center - 1;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float a = Mathf.Clamp01((radius - dist) * 2f);
                px[y * size + x] = new Color(1, 1, 1, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
