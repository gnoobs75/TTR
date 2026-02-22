using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings panel with audio sliders, control scheme selector, and haptics toggle.
/// Wired by SceneBootstrapper. Saves to PlayerPrefs.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("Panel")]
    public GameObject settingsPanel;

    [Header("Audio Sliders")]
    public Slider masterSlider;
    public Slider sfxSlider;
    public Slider musicSlider;

    [Header("Controls")]
    public Text controlSchemeLabel;
    public Button controlPrevButton;
    public Button controlNextButton;

    [Header("Haptics")]
    public Toggle hapticToggle;

    [Header("Close")]
    public Button closeButton;

    private int _controlIndex;
    private static readonly string[] ControlNames = { "Touch Zones", "Swipe", "Tilt" };

    void Start()
    {
        LoadSettings();

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (controlPrevButton != null) controlPrevButton.onClick.AddListener(PrevControl);
        if (controlNextButton != null) controlNextButton.onClick.AddListener(NextControl);
        if (hapticToggle != null) hapticToggle.onValueChanged.AddListener(OnHapticChanged);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void Open()
    {
        LoadSettings();
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayUIClick();
        HapticManager.LightTap();
    }

    public void Close()
    {
        SaveSettings();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayUIClick();
        HapticManager.LightTap();
    }

    void LoadSettings()
    {
        float master = PlayerPrefs.GetFloat("Settings_Master", 0.7f);
        float sfx = PlayerPrefs.GetFloat("Settings_SFX", 1.0f);
        float music = PlayerPrefs.GetFloat("Settings_Music", 0.4f);
        _controlIndex = PlayerPrefs.GetInt("Settings_ControlScheme", 0);
        bool haptics = PlayerPrefs.GetInt("Settings_Haptics", 1) == 1;

        if (masterSlider != null) masterSlider.value = master;
        if (sfxSlider != null) sfxSlider.value = sfx;
        if (musicSlider != null) musicSlider.value = music;
        if (hapticToggle != null) hapticToggle.isOn = haptics;
        UpdateControlLabel();

        ApplyAudio(master, sfx, music);
        HapticManager.Enabled = haptics;
        ApplyControlScheme();
    }

    void SaveSettings()
    {
        if (masterSlider != null) PlayerPrefs.SetFloat("Settings_Master", masterSlider.value);
        if (sfxSlider != null) PlayerPrefs.SetFloat("Settings_SFX", sfxSlider.value);
        if (musicSlider != null) PlayerPrefs.SetFloat("Settings_Music", musicSlider.value);
        PlayerPrefs.SetInt("Settings_ControlScheme", _controlIndex);
        PlayerPrefs.SetInt("Settings_Haptics", (hapticToggle != null && hapticToggle.isOn) ? 1 : 0);
        PlayerPrefs.Save();
    }

    void OnMasterChanged(float val)
    {
        ApplyAudio(val, sfxSlider != null ? sfxSlider.value : 1f, musicSlider != null ? musicSlider.value : 0.4f);
    }

    void OnSFXChanged(float val)
    {
        ApplyAudio(masterSlider != null ? masterSlider.value : 0.7f, val, musicSlider != null ? musicSlider.value : 0.4f);
    }

    void OnMusicChanged(float val)
    {
        ApplyAudio(masterSlider != null ? masterSlider.value : 0.7f, sfxSlider != null ? sfxSlider.value : 1f, val);
    }

    void ApplyAudio(float master, float sfx, float music)
    {
        var audio = ProceduralAudio.Instance;
        if (audio == null) return;
        audio.masterVolume = master;
        audio.sfxVolume = sfx;
        audio.musicVolume = music;
    }

    void OnHapticChanged(bool val)
    {
        HapticManager.Enabled = val;
        if (val) HapticManager.LightTap(); // Let them feel it!
    }

    void PrevControl()
    {
        _controlIndex = (_controlIndex - 1 + ControlNames.Length) % ControlNames.Length;
        UpdateControlLabel();
        ApplyControlScheme();
        HapticManager.LightTap();
    }

    void NextControl()
    {
        _controlIndex = (_controlIndex + 1) % ControlNames.Length;
        UpdateControlLabel();
        ApplyControlScheme();
        HapticManager.LightTap();
    }

    void UpdateControlLabel()
    {
        if (controlSchemeLabel != null)
            controlSchemeLabel.text = ControlNames[_controlIndex];
    }

    void ApplyControlScheme()
    {
        var input = TouchInput.Instance;
        if (input == null) return;
        input.controlScheme = (TouchInput.ControlScheme)_controlIndex;
    }
}
