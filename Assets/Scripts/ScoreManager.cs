using System.Collections;
using UnityEngine;
using TMPro;

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

    [Header("Feedback de Impacto en Cabina (GDD 5.5 / 8.2)")]
    [Tooltip("Arrastra aquí Cockpit_Greybox para sacudir la estructura sin marear la cámara VR.")]
    public Transform cockpitToShake;
    public float shakeDuration = 0.22f;
    public float shakeMagnitude = 0.045f;
    public AudioSource shipAudioSource;
    public AudioClip hullDamageClip;

    private float bannerClearTime;
    private Vector3 initialCockpitLocalPos;
    private Coroutine shakeCoroutine;

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

    private void Start()
    {
        if (cockpitToShake != null)
        {
            initialCockpitLocalPos = cockpitToShake.localPosition;
        }
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
        ShowBanner($"¡IMPACTO! -{amount}% CASCO", Color.red, 3f);

        if (shipAudioSource != null && hullDamageClip != null)
        {
            shipAudioSource.PlayOneShot(hullDamageClip);
        }

        if (cockpitToShake != null)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeCockpitRoutine(shakeDuration, shakeMagnitude));
        }

        UpdateHUD();
    }

    private IEnumerator ShakeCockpitRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            cockpitToShake.localPosition = initialCockpitLocalPos + (Random.insideUnitSphere * magnitude);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cockpitToShake.localPosition = initialCockpitLocalPos;
        shakeCoroutine = null;
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
        if (healthText != null) healthText.text = $"CASCO: {currentHealth}%";
    }

    public string CalculateFinalRank()
    {
        float accuracy = totalShotsFired > 0 ? ((float)totalShotsHit / totalShotsFired) * 100f : 0f;
        DiegeticWeapon weapon = FindFirstObjectByType<DiegeticWeapon>();
        int recharges = weapon != null ? weapon.RechargesCount : 0;

        int accuracyBonus = Mathf.RoundToInt(accuracy * 30f);
        int hullBonus = currentHealth * 20;
        int rechargePenalty = Mathf.Min(recharges * 250, 2000);
        int finalEvaluation = currentScore + accuracyBonus + hullBonus - rechargePenalty;

        int tier = GameSettingsData.DifficultyTier;
        int[] ssReq = { 90000, 150000, 240000, 350000 };
        int[] sReq = { 55000, 95000, 150000, 220000 };
        int[] aReq = { 30000, 55000, 85000, 125000 };
        int[] bReq = { 15000, 28000, 45000, 65000 };
        int[] cReq = { 7000, 12000, 20000, 30000 };
        int[] dReq = { 3000, 5000, 8500, 13000 };

        if (finalEvaluation >= ssReq[tier]) return "SS";
        if (finalEvaluation >= sReq[tier]) return "S";
        if (finalEvaluation >= aReq[tier]) return "A";
        if (finalEvaluation >= bReq[tier]) return "B";
        if (finalEvaluation >= cReq[tier]) return "C";
        if (finalEvaluation >= dReq[tier]) return "D";
        return "E";
    }
}