using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Records player position+rotation during a run and replays a translucent ghost
/// of the best run on subsequent plays. Records at 10fps, saves to PlayerPrefs
/// on new high score (keyed by mode: endless vs race).
/// </summary>
public class GhostRecorder : MonoBehaviour
{
    public static GhostRecorder Instance { get; private set; }

    private const float RECORD_INTERVAL = 0.1f; // 10fps
    private const int MAX_FRAMES = 3000;         // 5 minutes max
    private const string KEY_ENDLESS = "GhostData_Endless";
    private const string KEY_RACE = "GhostData_Race";

    [System.Serializable]
    private struct GhostFrame
    {
        public float px, py, pz; // position
        public float rx, ry, rz, rw; // rotation quaternion

        public GhostFrame(Vector3 pos, Quaternion rot)
        {
            px = pos.x; py = pos.y; pz = pos.z;
            rx = rot.x; ry = rot.y; rz = rot.z; rw = rot.w;
        }

        public Vector3 Position => new Vector3(px, py, pz);
        public Quaternion Rotation => new Quaternion(rx, ry, rz, rw);
    }

    // Recording
    private List<GhostFrame> _recordFrames = new List<GhostFrame>();
    private float _recordTimer;
    private bool _isRecording;
    private Transform _recordTarget;

    // Playback
    private GhostFrame[] _playbackFrames;
    private float _playbackTimer;
    private bool _isPlaying;
    private GameObject _ghostModel;
    private Material _ghostMat;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Update()
    {
        if (_isRecording && _recordTarget != null)
        {
            _recordTimer += Time.deltaTime;
            if (_recordTimer >= RECORD_INTERVAL)
            {
                _recordTimer -= RECORD_INTERVAL;
                if (_recordFrames.Count < MAX_FRAMES)
                    _recordFrames.Add(new GhostFrame(_recordTarget.position, _recordTarget.rotation));
            }
        }

        if (_isPlaying && _ghostModel != null && _playbackFrames != null)
        {
            _playbackTimer += Time.deltaTime;
            float frameIdx = _playbackTimer / RECORD_INTERVAL;
            int idx = Mathf.FloorToInt(frameIdx);
            float frac = frameIdx - idx;

            if (idx >= _playbackFrames.Length - 1)
            {
                // Playback done
                _isPlaying = false;
                if (_ghostModel != null) _ghostModel.SetActive(false);
                return;
            }

            // Interpolate between frames
            GhostFrame a = _playbackFrames[idx];
            GhostFrame b = _playbackFrames[Mathf.Min(idx + 1, _playbackFrames.Length - 1)];
            _ghostModel.transform.position = Vector3.Lerp(a.Position, b.Position, frac);
            _ghostModel.transform.rotation = Quaternion.Slerp(a.Rotation, b.Rotation, frac);
        }
    }

    /// <summary>Start recording the player's run.</summary>
    public void StartRecording(Transform target)
    {
        _recordTarget = target;
        _recordFrames.Clear();
        _recordTimer = 0f;
        _isRecording = true;
    }

    /// <summary>Stop recording and save if this was a new high score.</summary>
    public void StopRecording(bool isHighScore)
    {
        _isRecording = false;
        if (!isHighScore || _recordFrames.Count < 10) return;

        string key = GetGhostKey();
        SaveGhostData(key);
#if UNITY_EDITOR
        Debug.Log($"[GHOST] Saved {_recordFrames.Count} frames to {key}");
#endif
    }

    /// <summary>Start playing back a ghost from the best previous run.</summary>
    public void StartPlayback()
    {
        string key = GetGhostKey();
        _playbackFrames = LoadGhostData(key);

        if (_playbackFrames == null || _playbackFrames.Length < 10)
        {
            _isPlaying = false;
            return;
        }

        CreateGhostModel();
        _playbackTimer = 0f;
        _isPlaying = true;
#if UNITY_EDITOR
        Debug.Log($"[GHOST] Playing back {_playbackFrames.Length} frames from {key}");
#endif
    }

    public void StopPlayback()
    {
        _isPlaying = false;
        if (_ghostModel != null)
        {
            Destroy(_ghostModel);
            _ghostModel = null;
        }
    }

    string GetGhostKey()
    {
        bool isRace = RaceManager.Instance != null && RaceManager.Instance.RaceState != RaceManager.State.PreRace;
        return isRace ? KEY_RACE : KEY_ENDLESS;
    }

    void CreateGhostModel()
    {
        if (_ghostModel != null) Destroy(_ghostModel);

        // Simple capsule ghost (representing Mr. Corny's silhouette)
        _ghostModel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        _ghostModel.name = "GhostRacer";
        _ghostModel.transform.localScale = new Vector3(0.3f, 0.15f, 0.6f);
        Object.Destroy(_ghostModel.GetComponent<Collider>());

        // Transparent white material
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        _ghostMat = new Material(shader);
        _ghostMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.25f));
        _ghostMat.SetFloat("_Smoothness", 0.8f);
        _ghostMat.EnableKeyword("_EMISSION");
        _ghostMat.SetColor("_EmissionColor", new Color(0.5f, 0.7f, 1f) * 0.15f);
        // Transparent rendering
        _ghostMat.SetFloat("_Surface", 1);
        _ghostMat.SetFloat("_Blend", 0);
        _ghostMat.SetOverrideTag("RenderType", "Transparent");
        _ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _ghostMat.SetInt("_ZWrite", 0);
        _ghostMat.renderQueue = 3000;

        _ghostModel.GetComponent<Renderer>().material = _ghostMat;
    }

    // === PERSISTENCE ===
    // Stores ghost data as a base64-encoded binary blob in PlayerPrefs
    // Each frame = 7 floats (28 bytes). Max 3000 frames = 84KB.

    void SaveGhostData(string key)
    {
        int count = _recordFrames.Count;
        byte[] data = new byte[4 + count * 28]; // 4 byte count header + frame data

        System.Buffer.BlockCopy(System.BitConverter.GetBytes(count), 0, data, 0, 4);
        for (int i = 0; i < count; i++)
        {
            int offset = 4 + i * 28;
            GhostFrame f = _recordFrames[i];
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(f.px), 0, data, offset, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(f.py), 0, data, offset + 4, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(f.pz), 0, data, offset + 8, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(f.rx), 0, data, offset + 12, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(f.ry), 0, data, offset + 16, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(f.rz), 0, data, offset + 20, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(f.rw), 0, data, offset + 24, 4);
        }

        PlayerPrefs.SetString(key, System.Convert.ToBase64String(data));
        PlayerPrefs.Save();
    }

    GhostFrame[] LoadGhostData(string key)
    {
        string b64 = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(b64)) return null;

        byte[] data;
        try { data = System.Convert.FromBase64String(b64); }
        catch { return null; }

        if (data.Length < 4) return null;

        int count = System.BitConverter.ToInt32(data, 0);
        if (count < 1 || data.Length < 4 + count * 28) return null;

        GhostFrame[] frames = new GhostFrame[count];
        for (int i = 0; i < count; i++)
        {
            int offset = 4 + i * 28;
            frames[i] = new GhostFrame
            {
                px = System.BitConverter.ToSingle(data, offset),
                py = System.BitConverter.ToSingle(data, offset + 4),
                pz = System.BitConverter.ToSingle(data, offset + 8),
                rx = System.BitConverter.ToSingle(data, offset + 12),
                ry = System.BitConverter.ToSingle(data, offset + 16),
                rz = System.BitConverter.ToSingle(data, offset + 20),
                rw = System.BitConverter.ToSingle(data, offset + 24)
            };
        }
        return frames;
    }
}
