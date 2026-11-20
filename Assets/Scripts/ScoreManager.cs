using UnityEngine;
using TMPro;

/// <summary>
/// Gestiona el puntaje, multiplicador de combo x1-x5, integridad del casco/escudo
/// y el HUD diegético en cabina con colores dinámicos según alerta o potenciador.
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

        currentScore += basePoints * comboMultiplier;
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
        if (comboText != null) comboText.text = $"COMBO: x{comboMultiplier}";
        if (healthText != null) healthText.text = $"ESCUDO: {currentHealth}%";
    }

    public string CalculateFinalRank()
    {
        int finalTotal = currentScore + (currentHealth * 15);
        if (finalTotal >= 14000) return "SS";
        if (finalTotal >= 11000) return "S";
        if (finalTotal >= 8500) return "A";
        if (finalTotal >= 6000) return "B";
        if (finalTotal >= 3500) return "C";
        if (finalTotal >= 1500) return "D";
        return "F";
    }
}