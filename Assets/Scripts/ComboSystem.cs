using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chains near-misses and coin collects into combos.
/// Higher combos = bigger score multiplier.
/// Visual feedback: pulsing combo counter on HUD.
/// </summary>
public class ComboSystem : MonoBehaviour
{
    public static ComboSystem Instance { get; private set; }

    public enum EventType { NearMiss, CoinCollect }

    [Header("Combo Settings")]
    public float comboTimeout = 2.5f;
    public int nearMissBonus = 25;
    public int comboMilestoneBonus = 100;

    [Header("UI")]
    public Text comboText;

    public int ComboCount { get; private set; }
    public float Multiplier => 1f + ComboCount * 0.2f;

    private float _lastEventTime;
    private int _displayedCombo;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Update()
    {
        // Timeout
        if (ComboCount > 0 && Time.time - _lastEventTime > comboTimeout)
            EndCombo();

        UpdateUI();
    }

    public void RegisterEvent(EventType type)
    {
        ComboCount++;
        _lastEventTime = Time.time;

        if (GameManager.Instance == null) return;

        switch (type)
        {
            case EventType.NearMiss:
                int bonus = Mathf.RoundToInt(nearMissBonus * Multiplier);
                GameManager.Instance.AddScore(bonus);
                break;
            case EventType.CoinCollect:
                // Coin base score handled by GameManager.CollectCoin
                // Combo adds extra coins for chains
                if (ComboCount >= 3)
                {
                    int coinBonus = Mathf.RoundToInt(5 * Multiplier);
                    GameManager.Instance.AddScore(coinBonus);
                }
                break;
        }

        GameManager.Instance.RecordCombo(ComboCount);

        // Milestone bonuses and camera effects
        if (ComboCount == 5 || ComboCount == 10 || ComboCount == 20 || ComboCount == 50)
        {
            GameManager.Instance.AddScore(comboMilestoneBonus * (ComboCount / 5));
            if (PipeCamera.Instance != null)
                PipeCamera.Instance.PunchFOV(4f);
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayComboUp();
            HapticManager.MediumTap();
        }
    }

    void EndCombo()
    {
        ComboCount = 0;
    }

    public void ResetCombo()
    {
        ComboCount = 0;
        _lastEventTime = 0f;
    }

    void UpdateUI()
    {
        if (comboText == null) return;

        if (ComboCount >= 2)
        {
            comboText.gameObject.SetActive(true);

            string label;
            if (ComboCount >= 20) label = "INSANE";
            else if (ComboCount >= 10) label = "EPIC";
            else if (ComboCount >= 5) label = "SICK";
            else label = "COMBO";

            comboText.text = $"{ComboCount}x {label}!";

            // Color ramp: yellow -> orange -> red -> magenta
            float t = Mathf.Clamp01(ComboCount / 20f);
            comboText.color = Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.2f, 0.4f), t);

            // Pulse
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.06f * Mathf.Min(ComboCount, 10);
            comboText.transform.localScale = Vector3.one * pulse;
        }
        else
        {
            comboText.gameObject.SetActive(false);
            comboText.transform.localScale = Vector3.one;
        }
    }
}
