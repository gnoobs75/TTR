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
    private float _eventPunchTime; // for pop animation on each event

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
        _eventPunchTime = Time.time; // trigger pop animation

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

        // Escalating screen effects for sustained combos
        if (ComboCount >= 5 && ScreenEffects.Instance != null)
        {
            // Intensity scales with combo (capped at 20)
            float intensity = Mathf.Clamp01((ComboCount - 5f) / 15f);

            // Edge glow at 5+ (golden, subtle and building)
            ScreenEffects.Instance.SetComboGlow(intensity);

            // Chromatic pulse per event at 10+
            if (ComboCount >= 10)
                ScreenEffects.Instance.TriggerComboPulse(intensity);
        }

        // Per-event camera micro-shake scales with combo
        if (ComboCount >= 3 && PipeCamera.Instance != null)
        {
            float microShake = Mathf.Min(ComboCount * 0.008f, 0.1f);
            PipeCamera.Instance.Shake(microShake);
        }

        // Per-event haptic escalation
        if (ComboCount >= 8)
            HapticManager.LightTap();

        // Milestone bonuses and camera effects
        if (ComboCount == 5 || ComboCount == 10 || ComboCount == 20 || ComboCount == 50)
        {
            GameManager.Instance.AddScore(comboMilestoneBonus * (ComboCount / 5));
            if (PipeCamera.Instance != null)
            {
                PipeCamera.Instance.PunchFOV(4f);
                PipeCamera.Instance.Shake(0.1f + ComboCount * 0.005f);
            }
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayComboUp();
            if (ScreenEffects.Instance != null)
                ScreenEffects.Instance.TriggerMilestoneFlash();

            // Particle celebration at milestones
            if (ParticleManager.Instance != null && GameManager.Instance.player != null)
                ParticleManager.Instance.PlayComboFlash(
                    GameManager.Instance.player.transform.position);

            HapticManager.MediumTap();

            // Hype the combo in the Poop Crew overlay
            string label;
            if (ComboCount >= 50) label = "LEGENDARY";
            else if (ComboCount >= 20) label = "INSANE";
            else if (ComboCount >= 10) label = "EPIC";
            else label = "SICK";
            Color col = Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.2f, 0.4f),
                Mathf.Clamp01(ComboCount / 20f));
            CheerOverlay.Instance?.ShowCheer($"{ComboCount}x {label}!", col, ComboCount >= 10);
        }
    }

    void EndCombo()
    {
        if (ComboCount >= 3 && ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayComboBreak();
        ComboCount = 0;
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.SetComboGlow(0f);
    }

    public void ResetCombo()
    {
        ComboCount = 0;
        _lastEventTime = 0f;
        if (ScreenEffects.Instance != null)
            ScreenEffects.Instance.SetComboGlow(0f);
    }

    void UpdateUI()
    {
        if (comboText == null) return;

        if (ComboCount >= 1)
        {
            comboText.gameObject.SetActive(true);

            // Compact counter — hype labels go to CheerOverlay
            comboText.text = $"{ComboCount}x";

            // Color ramp: yellow -> red (intensifies with combo)
            float t = Mathf.Clamp01(ComboCount / 20f);
            comboText.color = Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.2f, 0.4f), t);

            // Organic pulse (stronger with higher combo)
            float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.1f * Mathf.Min(ComboCount, 10);

            // Pop animation on each new event (decays over 0.2s)
            float popElapsed = Time.time - _eventPunchTime;
            if (popElapsed < 0.2f)
            {
                float pop = 1f + (1f - popElapsed / 0.2f) * 0.35f;
                pulse *= pop;
            }

            comboText.transform.localScale = Vector3.one * pulse;
        }
        else
        {
            comboText.gameObject.SetActive(false);
            comboText.transform.localScale = Vector3.one;
        }
    }
}
