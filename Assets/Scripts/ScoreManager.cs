using UnityEngine;
using TMPro;

/// <summary>
/// Gestiona el puntaje (con multiplicador de combo y dificultad), integridad del escudo
/// y cálculo del rango final (F a SS).
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Estado de Partida")]
    public int currentScore = 0;
    public int maxHealth = 100;
    public int currentHealth = 100;
    public int comboMultiplier = 1;
    public int consecutiveHits = 0;

    [Header("Estadísticas para Rango Final")]
    public int targetsDestroyed = 0;
    public int totalShotsFired = 0;
    public int totalShotsHit = 0;

    [Header("HUD Diegético en Cabina (TextMeshPro)")]
    public TMP_Text scoreText;
    public TMP_Text comboText;
    public TMP_Text healthText;
    public TMP_Text powerUpBannerText;

    private float bannerClearTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        currentHealth = maxHealth;
        UpdateHUD();
    }

    private void Update()
    {
        if (powerUpBannerText != null && Time.time > bannerClearTime && !string.IsNullOrEmpty(powerUpBannerText.text))
        {
            powerUpBannerText.text = "";
        }
    }

    public void RegisterShotFired()
    {
        totalShotsFired++;
    }

    public void AddScore(int basePoints, bool countsForCombo = true)
    {
        totalShotsHit++;
        targetsDestroyed++;

        if (countsForCombo)
        {
            consecutiveHits++;
            comboMultiplier = Mathf.Clamp(1 + (consecutiveHits / 3), 1, 5);
        }

        float diffMult = GameSettingsData.scoreMultiplier;
        int earned = Mathf.RoundToInt(basePoints * comboMultiplier * diffMult);
        currentScore += earned;
        UpdateHUD();
    }

    public void BreakCombo()
    {
        consecutiveHits = 0;
        comboMultiplier = 1;
        UpdateHUD();
    }

    public void DamageShip(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);
        BreakCombo();
        ShowBanner($"¡IMPACTO! -{amount}% ESCUDO", Color.red, 3f);
        UpdateHUD();
    }

    public void ShowBanner(string message, Color textColor, float duration = 3f)
    {
        if (powerUpBannerText == null) return;
        powerUpBannerText.color = textColor;
        powerUpBannerText.text = message;
        bannerClearTime = Time.time + duration;
    }

    public void ShowBanner(string message, float duration = 3f)
    {
        ShowBanner(message, Color.cyan, duration);
    }

    public void UpdateHUD()
    {
        if (scoreText != null) scoreText.text = $"PUNTOS: {currentScore:N0}";
        if (comboText != null) comboText.text = $"COMBO: x{comboMultiplier} ({GameSettingsData.scoreMultiplier:0.##}x)";
        if (healthText != null) healthText.text = $"ESCUDO: {currentHealth}%";
    }

    public string CalculateFinalRank()
    {
        float accuracy = totalShotsFired > 0 ? ((float)totalShotsHit / totalShotsFired) * 100f : 0f;
        DiegeticWeapon weapon = FindFirstObjectByType<DiegeticWeapon>();
        int recharges = weapon != null ? weapon.RechargesCount : 0;

        int accuracyBonus = Mathf.RoundToInt(accuracy * 25f);
        int shieldBonus = currentHealth * 15;
        int rechargePenalty = Mathf.Min(recharges * 250, 2000);
        int finalEvaluation = currentScore + accuracyBonus + shieldBonus - rechargePenalty;

        if (finalEvaluation >= 22000) return "SS";
        if (finalEvaluation >= 15500) return "S";
        if (finalEvaluation >= 11000) return "A";
        if (finalEvaluation >= 7500) return "B";
        return "C";
    }
}