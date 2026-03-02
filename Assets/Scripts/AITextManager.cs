using UnityEngine;
using System;
using System.Collections.Generic;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Manages AI-generated text content via Apple Foundation Models on iOS 26+.
/// Falls back to static string arrays on unsupported platforms.
/// Singleton pattern - attach to a persistent GameObject named "AITextManager".
/// </summary>
public class AITextManager : MonoBehaviour
{
    public static AITextManager Instance { get; private set; }

    // AI availability
    public bool AIAvailable { get; private set; }
    public bool AIEnabled => AIAvailable && PlayerPrefs.GetInt("AITextEnabled", 1) == 1;
    public bool Initialized { get; private set; }

    // Pre-generated content pools
    readonly Queue<BarkData> _barkPool = new Queue<BarkData>();
    readonly Queue<string> _graffitiPool = new Queue<string>();
    readonly Queue<string> _commentaryPool = new Queue<string>();

    // Pending callbacks
    Action<string> _pendingDeathQuipCallback;
    Action<string> _pendingCommentaryCallback;

    // Pool thresholds
    const int BARK_POOL_TARGET = 20;
    const int GRAFFITI_POOL_TARGET = 30;
    const int BARK_REFILL_THRESHOLD = 5;
    const int GRAFFITI_REFILL_THRESHOLD = 10;
    bool _barkBatchPending;
    bool _graffitiBatchPending;

    // ========== DATA TYPES ==========

    [Serializable]
    public struct BarkData
    {
        public string line;
        public string emotion;
    }

    [Serializable]
    struct BarkArray { public BarkData[] items; }

    [Serializable]
    struct GraffitiData
    {
        public string text;
        public string style;
    }

    [Serializable]
    struct GraffitiArray { public GraffitiData[] items; }

    [Serializable]
    struct CommentaryData
    {
        public string line;
        public int energy;
    }

    // ========== NATIVE BRIDGE ==========

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern bool _FM_IsAvailable();
    [DllImport("__Internal")] static extern void _FM_InitSession();
    [DllImport("__Internal")] static extern void _FM_SetCallbackObject(string objectName);
    [DllImport("__Internal")] static extern void _FM_GenerateBark(string eventType);
    [DllImport("__Internal")] static extern void _FM_GenerateDeathQuip(string context);
    [DllImport("__Internal")] static extern void _FM_GenerateBarkBatch(int count);
    [DllImport("__Internal")] static extern void _FM_GenerateGraffitiBatch(int count, string zones);
    [DllImport("__Internal")] static extern void _FM_GenerateCommentary(string raceState);
#endif

    // ========== LIFECYCLE ==========

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitAI();
    }

    void InitAI()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            AIAvailable = _FM_IsAvailable();
            if (AIAvailable)
            {
                _FM_SetCallbackObject(gameObject.name);
                _FM_InitSession();
                // Pre-generate content pools
                RequestBarkBatch(BARK_POOL_TARGET);
                RequestGraffitiBatch(GRAFFITI_POOL_TARGET);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AIText] Foundation Models init failed: {e.Message}");
            AIAvailable = false;
        }
#else
        AIAvailable = false;
#endif
        Initialized = true;

#if UNITY_EDITOR
        Debug.Log($"[AIText] Initialized. AI available: {AIAvailable}");
#endif
    }

    void Update()
    {
        // Auto-refill pools when running low
        if (!AIEnabled) return;

        if (_barkPool.Count < BARK_REFILL_THRESHOLD && !_barkBatchPending)
            RequestBarkBatch(BARK_POOL_TARGET - _barkPool.Count);

        if (_graffitiPool.Count < GRAFFITI_REFILL_THRESHOLD && !_graffitiBatchPending)
            RequestGraffitiBatch(GRAFFITI_POOL_TARGET - _graffitiPool.Count);
    }

    // ========== PUBLIC API ==========

    /// <summary>
    /// Get a bark line for a game event. Returns AI-generated if available, else static fallback.
    /// </summary>
    public string GetBark(string eventType)
    {
        if (AIEnabled && _barkPool.Count > 0)
        {
            var bark = _barkPool.Dequeue();
            if (!string.IsNullOrEmpty(bark.line))
                return bark.line;
        }
        return GetFallbackBark(eventType);
    }

    /// <summary>
    /// Get a bark with emotion data.
    /// </summary>
    public BarkData GetBarkWithEmotion(string eventType)
    {
        if (AIEnabled && _barkPool.Count > 0)
        {
            var bark = _barkPool.Dequeue();
            if (!string.IsNullOrEmpty(bark.line))
                return bark;
        }
        return new BarkData { line = GetFallbackBark(eventType), emotion = "excited" };
    }

    /// <summary>
    /// Request a contextual death quip. Callback receives the quip string (or empty for fallback).
    /// </summary>
    public void RequestDeathQuip(string context, Action<string> callback)
    {
        if (!AIEnabled)
        {
            callback?.Invoke("");
            return;
        }

        _pendingDeathQuipCallback = callback;
#if UNITY_IOS && !UNITY_EDITOR
        _FM_GenerateDeathQuip(context);
#endif
        // Timeout: if no response in 3s, use fallback
        StartCoroutine(DeathQuipTimeout(callback));
    }

    System.Collections.IEnumerator DeathQuipTimeout(Action<string> callback)
    {
        yield return new WaitForSecondsRealtime(3f);
        if (_pendingDeathQuipCallback == callback)
        {
            _pendingDeathQuipCallback = null;
            callback?.Invoke("");
        }
    }

    /// <summary>
    /// Get a graffiti text string. Returns AI-generated if available, else empty (caller uses static).
    /// </summary>
    public string GetGraffiti()
    {
        if (AIEnabled && _graffitiPool.Count > 0)
            return _graffitiPool.Dequeue();
        return "";
    }

    /// <summary>
    /// Request race commentary. Callback receives the line (or empty for fallback).
    /// </summary>
    public void RequestCommentary(string raceState, Action<string> callback)
    {
        if (!AIEnabled)
        {
            callback?.Invoke("");
            return;
        }

        _pendingCommentaryCallback = callback;
#if UNITY_IOS && !UNITY_EDITOR
        _FM_GenerateCommentary(raceState);
#endif
        StartCoroutine(CommentaryTimeout(callback));
    }

    System.Collections.IEnumerator CommentaryTimeout(Action<string> callback)
    {
        yield return new WaitForSecondsRealtime(2f);
        if (_pendingCommentaryCallback == callback)
        {
            _pendingCommentaryCallback = null;
            callback?.Invoke("");
        }
    }

    // ========== BATCH REQUESTS ==========

    void RequestBarkBatch(int count)
    {
        if (!AIEnabled || _barkBatchPending) return;
        _barkBatchPending = true;
#if UNITY_IOS && !UNITY_EDITOR
        _FM_GenerateBarkBatch(count);
#endif
    }

    void RequestGraffitiBatch(int count)
    {
        if (!AIEnabled || _graffitiBatchPending) return;
        _graffitiBatchPending = true;
#if UNITY_IOS && !UNITY_EDITOR
        _FM_GenerateGraffitiBatch(count, "Porcelain,Grimy,Toxic,Rusty,Hellsewer");
#endif
    }

    // ========== NATIVE CALLBACKS (called by UnitySendMessage) ==========

    void OnBarkGenerated(string json)
    {
        try
        {
            var bark = JsonUtility.FromJson<BarkData>(json);
            if (!string.IsNullOrEmpty(bark.line))
                _barkPool.Enqueue(bark);
        }
        catch { }
    }

    void OnBarkBatchGenerated(string json)
    {
        _barkBatchPending = false;
        try
        {
            // JSON is an array, wrap for JsonUtility
            string wrapped = "{\"items\":" + json + "}";
            var batch = JsonUtility.FromJson<BarkArray>(wrapped);
            if (batch.items != null)
            {
                foreach (var bark in batch.items)
                {
                    if (!string.IsNullOrEmpty(bark.line))
                        _barkPool.Enqueue(bark);
                }
            }
#if UNITY_EDITOR
            Debug.Log($"[AIText] Bark pool: {_barkPool.Count} items");
#endif
        }
        catch (Exception e)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[AIText] Bark batch parse error: {e.Message}");
#endif
        }
    }

    void OnGraffitiBatchGenerated(string json)
    {
        _graffitiBatchPending = false;
        try
        {
            string wrapped = "{\"items\":" + json + "}";
            var batch = JsonUtility.FromJson<GraffitiArray>(wrapped);
            if (batch.items != null)
            {
                foreach (var g in batch.items)
                {
                    if (!string.IsNullOrEmpty(g.text))
                        _graffitiPool.Enqueue(g.text);
                }
            }
#if UNITY_EDITOR
            Debug.Log($"[AIText] Graffiti pool: {_graffitiPool.Count} items");
#endif
        }
        catch (Exception e)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[AIText] Graffiti batch parse error: {e.Message}");
#endif
        }
    }

    void OnDeathQuipGenerated(string quip)
    {
        var cb = _pendingDeathQuipCallback;
        _pendingDeathQuipCallback = null;
        cb?.Invoke(quip);
    }

    void OnCommentaryGenerated(string json)
    {
        var cb = _pendingCommentaryCallback;
        _pendingCommentaryCallback = null;
        try
        {
            var data = JsonUtility.FromJson<CommentaryData>(json);
            cb?.Invoke(!string.IsNullOrEmpty(data.line) ? data.line : "");
        }
        catch
        {
            cb?.Invoke("");
        }
    }

    // ========== STATIC FALLBACKS ==========

    static readonly string[] FallbackBarks_NearMiss = {
        "CLOSE!", "WHEW!", "YIKES!", "SCARY!", "ALMOST!", "TOO CLOSE!", "HAIR'S WIDTH!"
    };
    static readonly string[] FallbackBarks_Stomp = {
        "SQUASH!", "SPLAT!", "CRUSHED!", "STOMPED!", "FLATTENED!", "PANCAKED!"
    };
    static readonly string[] FallbackBarks_Hit = {
        "OUCH!", "EEK!", "OOF!", "YIKES!", "SPLAT!", "NOOOO!"
    };
    static readonly string[] FallbackBarks_Boost = {
        "ZOOM!", "WHEEE!", "TURBO!", "YEET!", "ZOOOOM!", "GOTTA GO FAST!"
    };
    static readonly string[] FallbackBarks_Combo = {
        "SICK!", "RADICAL!", "EPIC!", "BONKERS!", "INSANE!", "LEGENDARY!"
    };
    static readonly string[] FallbackBarks_Generic = {
        "WHOA!", "DOPE!", "NICE!", "SWEET!", "LET'S GO!", "CORN POWER!"
    };

    string GetFallbackBark(string eventType)
    {
        string[] pool;
        switch (eventType)
        {
            case "near-miss": pool = FallbackBarks_NearMiss; break;
            case "stomp": pool = FallbackBarks_Stomp; break;
            case "hit": pool = FallbackBarks_Hit; break;
            case "boost": pool = FallbackBarks_Boost; break;
            case "combo": pool = FallbackBarks_Combo; break;
            default: pool = FallbackBarks_Generic; break;
        }
        return pool[UnityEngine.Random.Range(0, pool.Length)];
    }
}
