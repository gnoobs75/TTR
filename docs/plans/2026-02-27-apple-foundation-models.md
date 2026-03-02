# Apple Foundation Models Integration - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Integrate Apple's on-device Foundation Models framework to generate dynamic turd barks, contextual death quips, procedural graffiti, and race commentary -- with static fallbacks on non-Apple platforms.

**Architecture:** Native Swift iOS plugin bridge (matching existing HapticBridge.mm pattern) exposes Foundation Models to Unity via C-callable functions. A C# `AITextManager` singleton queues generation requests and pre-generates content pools during loading. All existing static string arrays remain as fallbacks when the AI is unavailable.

**Tech Stack:** Swift 6 / Foundation Models framework (iOS 26+), Objective-C++ bridge (.mm), Unity C# with `#if UNITY_IOS` guards, async callback pattern via `UnitySendMessage`.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│  Unity C# Layer                                      │
│                                                      │
│  AITextManager.cs (singleton)                        │
│  ├── Request queue (type, context, callback)         │
│  ├── Pre-gen pools: barks[], quips[], graffiti[]     │
│  ├── Fallback to static arrays when AI unavailable   │
│  └── Hooks into: GameUI, ScorePopup, CheerOverlay,  │
│       RaceManager, ScenerySpawner, TurdController    │
│                                                      │
│  #if UNITY_IOS && !UNITY_EDITOR                      │
│  ├── [DllImport] → FoundationModelsBridge.mm         │
│  └── [MonoPInvokeCallback] for async results         │
│                                                      │
├──────────────────────────────────────────────────────│
│  Native iOS Layer (Assets/Plugins/iOS/)              │
│                                                      │
│  FoundationModelsBridge.mm (ObjC++ → Swift)          │
│  FoundationModelsPlugin.swift (Swift API calls)      │
│  ├── @Generable structs: TurdBark, DeathQuip,        │
│  │   GraffitiText, RaceCommentary                    │
│  ├── LanguageModelSession with system prompts        │
│  └── Availability check + graceful fallback          │
└─────────────────────────────────────────────────────┘
```

## Content Generation Types

| Type | When Generated | Context Sent | Output |
|------|---------------|--------------|--------|
| **Turd Bark** | Pre-gen pool of 20 at game start + on-demand | Event type (near-miss/stomp/hit/boost/combo) | 1-liner + emotion |
| **Death Quip** | On death (player is waiting) | How died, distance, zone, combo, creature | Contextual joke |
| **Graffiti** | Batch of 30 at level load | Zone name, depth | Spray-paint text (2-4 words per line) |
| **Race Commentary** | On race events (position change, halfway, finish) | Race state, position, distance | Sports-style call |

---

## Task 1: Native Swift Plugin - Foundation Models API

**Files:**
- Create: `Assets/Plugins/iOS/FoundationModelsPlugin.swift`
- Create: `Assets/Plugins/iOS/FoundationModelsBridge.mm`

**Step 1: Create the Swift plugin with @Generable types and generation methods**

```swift
// Assets/Plugins/iOS/FoundationModelsPlugin.swift
import Foundation
import FoundationModels

// MARK: - Generable Types

@Generable
struct TurdBark {
    @Guide(description: "A short funny one-liner (under 40 chars) a cartoon turd character says during a sewer race. Poop puns encouraged.")
    var line: String
    @Guide(description: "The emotion behind the line", .anyOf(["excited", "scared", "cocky", "disgusted", "surprised", "relieved"]))
    var emotion: String
}

@Generable
struct DeathQuip {
    @Guide(description: "A funny death message (under 50 chars) for a cartoon turd who just crashed. Dark humor and poop puns. Reference the context.")
    var quip: String
}

@Generable
struct GraffitiText {
    @Guide(description: "Crude sewer wall graffiti (2-4 words per line, max 3 lines, separated by newlines). Toilet humor.")
    var text: String
    @Guide(description: "Spray paint style", .anyOf(["crude", "bold", "dripping", "stencil"]))
    var style: String
}

@Generable
struct RaceCommentary {
    @Guide(description: "Excited sports commentary (under 60 chars) about a sewer turd race. Over-the-top enthusiasm.")
    var line: String
    @Guide(description: "Energy level", .range(1...5))
    var energy: Int
}

// MARK: - Plugin Manager

@objc public class FoundationModelsPlugin: NSObject {

    private static var session: LanguageModelSession?
    private static let systemPrompt = """
    You are the voice of MrCorny, a corn-studded cartoon turd racing through sewer pipes. \
    Your humor is silly, punny, and toilet-themed. Keep responses SHORT. \
    Poop puns, sewer jokes, and bathroom humor are your specialty. \
    Never be mean-spirited. Always fun and goofy.
    """

    @objc public static func isAvailable() -> Bool {
        if #available(iOS 26.0, *) {
            return SystemLanguageModel.default.isAvailable
        }
        return false
    }

    @objc public static func initSession() {
        if #available(iOS 26.0, *) {
            session = LanguageModelSession(
                instructions: systemPrompt
            )
        }
    }

    // MARK: - Generation Methods

    @objc public static func generateBark(
        eventType: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *), let session = session else {
            callback("{\"line\":\"\",\"emotion\":\"\"}")
            return
        }

        Task {
            do {
                let prompt = "The player just experienced: \(eventType). Generate a reaction bark."
                let response = try await session.respond(
                    to: prompt,
                    generating: TurdBark.self
                )
                let json = "{\"line\":\"\(Self.escapeJSON(response.line))\",\"emotion\":\"\(response.emotion)\"}"
                DispatchQueue.main.async { callback(json) }
            } catch {
                DispatchQueue.main.async { callback("{\"line\":\"\",\"emotion\":\"\"}") }
            }
        }
    }

    @objc public static func generateDeathQuip(
        context: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *), let session = session else {
            callback("")
            return
        }

        Task {
            do {
                let prompt = "The turd just died: \(context). Write a funny death message."
                let response = try await session.respond(
                    to: prompt,
                    generating: DeathQuip.self
                )
                DispatchQueue.main.async { callback(response.quip) }
            } catch {
                DispatchQueue.main.async { callback("") }
            }
        }
    }

    @objc public static func generateGraffiti(
        zone: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *), let session = session else {
            callback("{\"text\":\"\",\"style\":\"crude\"}")
            return
        }

        Task {
            do {
                let prompt = "Write sewer wall graffiti for the \(zone) zone. 2-4 words per line, max 3 lines."
                let response = try await session.respond(
                    to: prompt,
                    generating: GraffitiText.self
                )
                let json = "{\"text\":\"\(Self.escapeJSON(response.text))\",\"style\":\"\(response.style)\"}"
                DispatchQueue.main.async { callback(json) }
            } catch {
                DispatchQueue.main.async { callback("{\"text\":\"\",\"style\":\"crude\"}") }
            }
        }
    }

    @objc public static func generateCommentary(
        raceState: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *), let session = session else {
            callback("{\"line\":\"\",\"energy\":3}")
            return
        }

        Task {
            do {
                let prompt = "Commentate this race moment: \(raceState)"
                let response = try await session.respond(
                    to: prompt,
                    generating: RaceCommentary.self
                )
                let json = "{\"line\":\"\(Self.escapeJSON(response.line))\",\"energy\":\(response.energy)}"
                DispatchQueue.main.async { callback(json) }
            } catch {
                DispatchQueue.main.async { callback("{\"line\":\"\",\"energy\":3}") }
            }
        }
    }

    // MARK: - Batch Generation

    @objc public static func generateBarkBatch(
        count: Int,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *) else {
            callback("[]")
            return
        }

        Task {
            // Fresh session per batch to avoid context buildup
            let batchSession = LanguageModelSession(instructions: systemPrompt)
            var results: [String] = []
            let events = ["near-miss dodge", "stomping an obstacle", "getting hit",
                          "speed boost", "high combo", "coin grab", "jumping",
                          "entering toxic zone", "close call", "racing overtake"]

            for i in 0..<count {
                let event = events[i % events.count]
                do {
                    let response = try await batchSession.respond(
                        to: "React to: \(event). Be unique, don't repeat.",
                        generating: TurdBark.self
                    )
                    let json = "{\"line\":\"\(Self.escapeJSON(response.line))\",\"emotion\":\"\(response.emotion)\"}"
                    results.append(json)
                } catch {
                    // Skip failures silently
                }
            }

            let arrayJSON = "[\(results.joined(separator: ","))]"
            DispatchQueue.main.async { callback(arrayJSON) }
        }
    }

    @objc public static func generateGraffitiBatch(
        count: Int,
        zones: String,
        callback: @escaping (String) -> Void
    ) {
        guard #available(iOS 26.0, *) else {
            callback("[]")
            return
        }

        Task {
            let batchSession = LanguageModelSession(instructions: systemPrompt)
            var results: [String] = []
            let zoneList = zones.split(separator: ",").map(String.init)

            for i in 0..<count {
                let zone = zoneList[i % zoneList.count]
                do {
                    let response = try await batchSession.respond(
                        to: "Write unique sewer graffiti for the \(zone) zone. Short, crude, funny.",
                        generating: GraffitiText.self
                    )
                    let json = "{\"text\":\"\(Self.escapeJSON(response.text))\",\"style\":\"\(response.style)\"}"
                    results.append(json)
                } catch {
                    // Skip failures
                }
            }

            let arrayJSON = "[\(results.joined(separator: ","))]"
            DispatchQueue.main.async { callback(arrayJSON) }
        }
    }

    // MARK: - Helpers

    private static func escapeJSON(_ str: String) -> String {
        str.replacingOccurrences(of: "\\", with: "\\\\")
           .replacingOccurrences(of: "\"", with: "\\\"")
           .replacingOccurrences(of: "\n", with: "\\n")
           .replacingOccurrences(of: "\r", with: "")
           .replacingOccurrences(of: "\t", with: " ")
    }
}
```

**Step 2: Create the ObjC++ bridge that exposes C functions to Unity**

```objc
// Assets/Plugins/iOS/FoundationModelsBridge.mm
#import <Foundation/Foundation.h>

// Forward declare the Swift class (generated header)
@class FoundationModelsPlugin;

// Unity callback function pointer type
typedef void (*StringCallback)(const char* result);

// Store Unity game object name for UnitySendMessage
static NSString* _unityCallbackObject = @"AITextManager";

extern "C" {
    // Check if Foundation Models is available on this device
    bool _FM_IsAvailable() {
        if (@available(iOS 26.0, *)) {
            return [FoundationModelsPlugin isAvailable];
        }
        return NO;
    }

    // Initialize a session
    void _FM_InitSession() {
        if (@available(iOS 26.0, *)) {
            [FoundationModelsPlugin initSession];
        }
    }

    // Set the Unity callback object name
    void _FM_SetCallbackObject(const char* objectName) {
        _unityCallbackObject = [NSString stringWithUTF8String:objectName];
    }

    // Generate a single bark (async - result via UnitySendMessage)
    void _FM_GenerateBark(const char* eventType) {
        if (@available(iOS 26.0, *)) {
            NSString* event = [NSString stringWithUTF8String:eventType];
            [FoundationModelsPlugin generateBarkWithEventType:event callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnBarkGenerated",
                    [result UTF8String]
                );
            }];
        }
    }

    // Generate a death quip (async)
    void _FM_GenerateDeathQuip(const char* context) {
        if (@available(iOS 26.0, *)) {
            NSString* ctx = [NSString stringWithUTF8String:context];
            [FoundationModelsPlugin generateDeathQuipWithContext:ctx callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnDeathQuipGenerated",
                    [result UTF8String]
                );
            }];
        }
    }

    // Generate a batch of barks (async)
    void _FM_GenerateBarkBatch(int count) {
        if (@available(iOS 26.0, *)) {
            [FoundationModelsPlugin generateBarkBatchWithCount:count callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnBarkBatchGenerated",
                    [result UTF8String]
                );
            }];
        }
    }

    // Generate a batch of graffiti (async)
    void _FM_GenerateGraffitiBatch(int count, const char* zones) {
        if (@available(iOS 26.0, *)) {
            NSString* z = [NSString stringWithUTF8String:zones];
            [FoundationModelsPlugin generateGraffitiBatchWithCount:count zones:z callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnGraffitiBatchGenerated",
                    [result UTF8String]
                );
            }];
        }
    }

    // Generate race commentary (async)
    void _FM_GenerateCommentary(const char* raceState) {
        if (@available(iOS 26.0, *)) {
            NSString* state = [NSString stringWithUTF8String:raceState];
            [FoundationModelsPlugin generateCommentaryWithRaceState:state callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnCommentaryGenerated",
                    [result UTF8String]
                );
            }];
        }
    }
}
```

**Step 3: Verify file placement**

Both files go in `Assets/Plugins/iOS/` alongside the existing `HapticBridge.mm`. Unity automatically includes files in this directory in iOS builds.

**Step 4: Commit**

```bash
git add Assets/Plugins/iOS/FoundationModelsPlugin.swift Assets/Plugins/iOS/FoundationModelsBridge.mm
git commit -m "feat: add Apple Foundation Models native iOS plugin bridge

Swift plugin with @Generable types for turd barks, death quips,
graffiti, and race commentary. ObjC++ bridge exposes C functions
to Unity via UnitySendMessage async callbacks."
```

---

## Task 2: Unity C# Manager - AITextManager.cs

**Files:**
- Create: `Assets/Scripts/AITextManager.cs`

**Step 1: Create the AITextManager singleton with native bridge and fallback pools**

```csharp
// Assets/Scripts/AITextManager.cs
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
        if (!AIAvailable) return;

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
        if (AIAvailable && _barkPool.Count > 0)
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
        if (AIAvailable && _barkPool.Count > 0)
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
        if (!AIAvailable)
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
        if (AIAvailable && _graffitiPool.Count > 0)
            return _graffitiPool.Dequeue();
        return "";
    }

    /// <summary>
    /// Request race commentary. Callback receives the line (or empty for fallback).
    /// </summary>
    public void RequestCommentary(string raceState, Action<string> callback)
    {
        if (!AIAvailable)
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
        if (!AIAvailable || _barkBatchPending) return;
        _barkBatchPending = true;
#if UNITY_IOS && !UNITY_EDITOR
        _FM_GenerateBarkBatch(count);
#endif
    }

    void RequestGraffitiBatch(int count)
    {
        if (!AIAvailable || _graffitiBatchPending) return;
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
```

**Step 2: Commit**

```bash
git add Assets/Scripts/AITextManager.cs
git commit -m "feat: add AITextManager C# singleton for AI text generation

Bridges to Apple Foundation Models on iOS 26+.
Pre-generates bark and graffiti pools at startup.
Falls back to static arrays on non-Apple platforms.
Async callbacks via UnitySendMessage pattern."
```

---

## Task 3: Hook Into Game Systems - Death Quips

**Files:**
- Modify: `Assets/Scripts/GameUI.cs` (death quip selection in ShowGameOver)

**Step 1: Modify GameUI to request AI death quips with context**

In `GameUI.cs`, find where `DeathQuips` array is indexed (in `ShowGameOver` or wherever the quip is selected). Replace the random selection with an AI request that falls back to static:

```csharp
// In the method that shows game-over (around the DeathQuips selection):

// BEFORE (static random):
// string quip = DeathQuips[Random.Range(0, DeathQuips.Length)];

// AFTER (AI with fallback):
string quip = DeathQuips[Random.Range(0, DeathQuips.Length)]; // default fallback

if (AITextManager.Instance != null && AITextManager.Instance.AIAvailable)
{
    // Build context string from current run stats
    string creature = GameManager.Instance.lastHitCreature ?? "unknown obstacle";
    string zone = PipeZoneSystem.Instance?.CurrentZoneName ?? "sewer";
    float dist = GameManager.Instance.Distance;
    int combo = ComboSystem.Instance?.CurrentCombo ?? 0;
    float speed = GameManager.Instance.CurrentSpeed;

    string context = $"Hit {creature} at {dist:F0}m in {zone} zone, speed {speed:F0}, combo was {combo}";

    AITextManager.Instance.RequestDeathQuip(context, (aiQuip) =>
    {
        if (!string.IsNullOrEmpty(aiQuip))
        {
            // Update the already-displayed quip text with AI version
            // (arrives within ~1-2s, player is reading game-over screen)
            SetDeathQuipText(aiQuip);
        }
    });
}
```

Add a helper method to update the quip text after it arrives:

```csharp
void SetDeathQuipText(string quip)
{
    if (_goTitle != null)
    {
        var tmp = _goTitle.GetComponent<TMPro.TextMeshProUGUI>();
        if (tmp != null) tmp.text = quip;
    }
}
```

**Notes:**
- Show static quip immediately (no delay), then upgrade to AI quip when it arrives
- Player sees instant feedback, then text subtly updates 1-2s later (or stays if AI fails)
- `lastHitCreature` field may need to be added to GameManager if not present

**Step 2: Add `lastHitCreature` tracking to GameManager if needed**

In `GameManager.cs`, add:
```csharp
public string lastHitCreature;
```

Set it in the hit callback:
```csharp
// In OnPlayerHit or wherever obstacle type is known:
lastHitCreature = obstacle?.gameObject.name ?? "something gross";
```

**Step 3: Commit**

```bash
git add Assets/Scripts/GameUI.cs Assets/Scripts/GameManager.cs
git commit -m "feat: hook AI death quips into game-over screen

Shows static quip instantly, upgrades to AI-generated contextual
quip when Foundation Models responds. Includes hit creature,
zone, speed, and combo in context for funnier results."
```

---

## Task 4: Hook Into Game Systems - Dynamic Barks

**Files:**
- Modify: `Assets/Scripts/TurdController.cs` (hit barks)
- Modify: `Assets/Scripts/ScorePopup.cs` (near-miss, stomp, combo barks)
- Modify: `Assets/Scripts/CheerOverlay.cs` (poop crew sign text)

**Step 1: Replace static bark words with AI-generated ones**

In `TurdController.cs` where hit words are shown (e.g., "OUCH!", "EEK!"):
```csharp
// BEFORE:
// string[] hitWords = { "OUCH!", "EEK!", "OOF!", "YIKES!", "SPLAT!" };
// string word = hitWords[Random.Range(0, hitWords.Length)];

// AFTER:
string word = AITextManager.Instance != null
    ? AITextManager.Instance.GetBark("hit")
    : "OUCH!";
```

In `ScorePopup.cs` for near-miss popups:
```csharp
// In ShowNearMiss():
string word = AITextManager.Instance != null
    ? AITextManager.Instance.GetBark("near-miss")
    : nearMissWords[Random.Range(0, nearMissWords.Length)];
```

In `ScorePopup.cs` for stomp popups:
```csharp
// In ShowStomp():
string word = AITextManager.Instance != null
    ? AITextManager.Instance.GetBark("stomp")
    : stompWords[Random.Range(0, stompWords.Length)];
```

In `CheerOverlay.cs`, when updating sign text for hype moments:
```csharp
// When choosing sign words for poop crew:
string word = AITextManager.Instance != null
    ? AITextManager.Instance.GetBark("combo")
    : HYPE_WORDS[Random.Range(0, HYPE_WORDS.Length)];
```

**Step 2: Commit**

```bash
git add Assets/Scripts/TurdController.cs Assets/Scripts/ScorePopup.cs Assets/Scripts/CheerOverlay.cs
git commit -m "feat: hook AI barks into hit/stomp/near-miss/combo popups

AI-generated one-liners replace static word arrays when Foundation
Models is available. Falls back to original words on other platforms."
```

---

## Task 5: Hook Into Game Systems - Procedural Graffiti

**Files:**
- Modify: `Assets/Scripts/Editor/SceneBootstrapper.cs` (graffiti text source)
- Modify: `Assets/Scripts/ScenerySpawner.cs` (runtime graffiti text)

**Step 1: Add AI graffiti injection to ScenerySpawner**

In `ScenerySpawner.cs`, modify `SpawnSign()` to optionally use AI graffiti:

```csharp
void SpawnSign(float dist)
{
    if (signPrefabs == null || signPrefabs.Length == 0) return;
    if (_pipeGen == null) return;

    // Try to get AI-generated graffiti text
    string aiGraffiti = AITextManager.Instance != null
        ? AITextManager.Instance.GetGraffiti()
        : "";

    // ... existing positioning code ...

    GameObject prefab = signPrefabs[Random.Range(0, signPrefabs.Length)];
    GameObject obj = Instantiate(prefab, pos, rot, transform);

    // If AI graffiti available, replace the TextMesh content
    if (!string.IsNullOrEmpty(aiGraffiti))
    {
        var tm = obj.GetComponentInChildren<TextMesh>();
        if (tm != null)
            tm.text = aiGraffiti;

        var tmp = obj.GetComponentInChildren<TMPro.TextMeshPro>();
        if (tmp != null)
            tmp.text = aiGraffiti;
    }

    _spawnedObjects.Add(obj);
}
```

**Notes:**
- Graffiti pool is pre-generated at game start (30 items)
- Auto-refills when pool drops below 10
- Static prefab graffiti remains as fallback
- Only graffiti-type signs get text replaced (warning signs, arrows, wanted posters keep static text)

**Step 2: Commit**

```bash
git add Assets/Scripts/ScenerySpawner.cs
git commit -m "feat: inject AI-generated graffiti into sewer wall signs

Replaces TextMesh content on graffiti prefabs with AI-generated
toilet humor when Foundation Models pool has content available."
```

---

## Task 6: Hook Into Game Systems - Race Commentary

**Files:**
- Modify: `Assets/Scripts/RaceManager.cs` (race event commentary)

**Step 1: Add AI commentary to race milestone events**

In `RaceManager.cs`, at key race moments (position changes, halfway, final stretch):

```csharp
// At position change events:
void OnPositionChanged(int oldPos, int newPos)
{
    // Existing quip logic
    string staticQuip = newPos < oldPos
        ? GAIN_QUIPS[Random.Range(0, GAIN_QUIPS.Length)]
        : LOSE_QUIPS[Random.Range(0, LOSE_QUIPS.Length)];

    // Show static immediately
    ShowRacePopup(staticQuip);

    // Request AI commentary upgrade
    if (AITextManager.Instance != null && AITextManager.Instance.AIAvailable)
    {
        string dir = newPos < oldPos ? "gained" : "lost";
        string state = $"Player {dir} position, now in {newPos} of 5 at {_raceDistance:F0}m";
        AITextManager.Instance.RequestCommentary(state, (aiLine) =>
        {
            if (!string.IsNullOrEmpty(aiLine))
                ShowRacePopup(aiLine);
        });
    }
}

// At halfway point:
string halfwayState = $"Halfway through the race! Player in position {playerPos} of 5";
AITextManager.Instance?.RequestCommentary(halfwayState, (line) =>
{
    if (!string.IsNullOrEmpty(line))
        ShowRacePopup(line);
});

// At final stretch:
string finalState = $"FINAL STRETCH! Player in {playerPos}, {distToFinish:F0}m to go!";
AITextManager.Instance?.RequestCommentary(finalState, (line) =>
{
    if (!string.IsNullOrEmpty(line))
        ShowRacePopup(line);
});
```

**Step 2: Commit**

```bash
git add Assets/Scripts/RaceManager.cs
git commit -m "feat: hook AI race commentary into position changes and milestones

Shows static quip immediately, AI-generated sports commentary
follows 1-2s later for race events."
```

---

## Task 7: SceneBootstrapper Integration

**Files:**
- Modify: `Assets/Scripts/Editor/SceneBootstrapper.cs` (create AITextManager GO)

**Step 1: Add AITextManager creation to scene setup**

In `SceneBootstrapper.cs`, in the main scene setup method, add:

```csharp
// ===== AI TEXT MANAGER =====
GameObject aiTextGO = new GameObject("AITextManager");
aiTextGO.AddComponent<AITextManager>();
Debug.Log("TTR: Created AITextManager (Apple Foundation Models bridge).");
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Editor/SceneBootstrapper.cs
git commit -m "feat: add AITextManager to SceneBootstrapper scene setup"
```

---

## Task 8: iOS Build Configuration

**Files:**
- Modify: `Assets/Scripts/Editor/iOSBuildConfig.cs` (post-process build for Swift support)

**Step 1: Add Swift compilation flags to iOS build post-processor**

In `iOSBuildConfig.cs`, add post-process build step:

```csharp
// Add to the PostProcessBuild method:

// Enable Swift support for Foundation Models plugin
pbxProject.SetBuildProperty(targetGuid, "SWIFT_VERSION", "6.0");
pbxProject.SetBuildProperty(targetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

// Foundation Models requires iOS 26
pbxProject.SetBuildProperty(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "26.0");

// Add FoundationModels framework (weak link for backward compat)
pbxProject.AddFrameworkToProject(targetGuid, "FoundationModels.framework", true); // true = weak

// Bridge header for Swift ↔ ObjC++
string bridgingHeader = "Libraries/Plugins/iOS/TTR-Bridging-Header.h";
pbxProject.SetBuildProperty(targetGuid, "SWIFT_OBJC_BRIDGING_HEADER", bridgingHeader);
```

**Step 2: Create bridging header**

Create `Assets/Plugins/iOS/TTR-Bridging-Header.h`:
```objc
// TTR-Bridging-Header.h
// Allows Swift code to see ObjC/C++ declarations

#import <Foundation/Foundation.h>

// Unity's UnitySendMessage is available in the main Unity framework
extern void UnitySendMessage(const char* obj, const char* method, const char* msg);
```

**Step 3: Commit**

```bash
git add Assets/Scripts/Editor/iOSBuildConfig.cs Assets/Plugins/iOS/TTR-Bridging-Header.h
git commit -m "feat: configure iOS build for Swift + Foundation Models framework

Sets Swift 6.0, weak-links FoundationModels.framework,
adds bridging header for UnitySendMessage access from Swift."
```

---

## Task 9: Settings UI - AI Toggle

**Files:**
- Modify: `Assets/Scripts/SettingsMenu.cs` or `Assets/Scripts/PauseMenu.cs`

**Step 1: Add AI text toggle to settings**

```csharp
// In settings UI setup, add toggle:
// "AI Voices" toggle - only visible on iOS with Foundation Models

bool aiSupported = AITextManager.Instance != null && AITextManager.Instance.AIAvailable;

if (aiSupported)
{
    // Add toggle row
    bool aiEnabled = PlayerPrefs.GetInt("AITextEnabled", 1) == 1;
    // ... create toggle UI element ...

    // On toggle change:
    PlayerPrefs.SetInt("AITextEnabled", enabled ? 1 : 0);
    // AITextManager checks this pref
}
```

In `AITextManager.cs`, respect the setting:
```csharp
public bool AIEnabled => AIAvailable && PlayerPrefs.GetInt("AITextEnabled", 1) == 1;
```

Update all `AIAvailable` checks to use `AIEnabled` instead.

**Step 2: Commit**

```bash
git add Assets/Scripts/AITextManager.cs Assets/Scripts/SettingsMenu.cs
git commit -m "feat: add AI Voices toggle in settings menu

Players can disable AI text generation on supported devices.
Toggle only appears when Foundation Models is available."
```

---

## Task 10: Testing & Polish

**Step 1: Editor testing (non-iOS)**

Run in Unity Editor:
- Verify all fallback paths work (AIAvailable = false)
- Verify no null references when AITextManager is present but AI unavailable
- Verify death quips still show from static array
- Verify graffiti still uses prefab text
- Verify race commentary still uses static quips
- Verify score popups still use static words

**Step 2: iOS device testing**

Build to iOS 26 device:
- Verify `_FM_IsAvailable()` returns true on A17 Pro+ with Apple Intelligence enabled
- Verify bark pool fills within ~10s of launch
- Verify graffiti pool fills within ~15s of launch
- Verify death quip arrives within 3s timeout
- Verify race commentary arrives within 2s timeout
- Verify graceful fallback on older devices (iOS 15-25)

**Step 3: Performance profiling**

- Monitor frame rate during batch generation (should not hitch main thread)
- Check memory usage of bark/graffiti pools
- Verify UnitySendMessage doesn't cause GC spikes

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: Apple Foundation Models integration complete

On-device AI generates dynamic turd barks, contextual death quips,
procedural sewer graffiti, and race commentary on iOS 26+.
Static fallbacks on all other platforms. Player toggle in settings."
```

---

## Summary

| Task | What | Files | Est. Complexity |
|------|------|-------|----------------|
| 1 | Swift + ObjC++ native plugin | 2 new files | Medium |
| 2 | AITextManager C# singleton | 1 new file | Medium |
| 3 | Death quip hooks | GameUI.cs, GameManager.cs | Low |
| 4 | Dynamic bark hooks | TurdController, ScorePopup, CheerOverlay | Low |
| 5 | Procedural graffiti hooks | ScenerySpawner.cs | Low |
| 6 | Race commentary hooks | RaceManager.cs | Low |
| 7 | SceneBootstrapper integration | SceneBootstrapper.cs | Trivial |
| 8 | iOS build config | iOSBuildConfig.cs + bridging header | Medium |
| 9 | Settings UI toggle | SettingsMenu.cs + AITextManager.cs | Low |
| 10 | Testing & polish | All files | Medium |

**Total new files:** 4 (Swift plugin, ObjC++ bridge, bridging header, AITextManager.cs)
**Total modified files:** ~8 (GameUI, GameManager, TurdController, ScorePopup, CheerOverlay, ScenerySpawner, RaceManager, SceneBootstrapper, iOSBuildConfig, SettingsMenu)
