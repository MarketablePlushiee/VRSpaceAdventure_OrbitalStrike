using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Almacena las estadísticas de la partida entre escenas sin duplicar el XR Rig.
/// </summary>
public static class MatchResultsData
{
    public static int finalScore = 0;
    public static string finalRank = "F";
    public static int shieldRemaining = 0;
    public static int targetsDestroyed = 0;
    public static float accuracyPercent = 0f;
    public static string outcomeTitle = "MISIÓN FINALIZADA";
}

/// <summary>
/// Orquesta el flujo del Vertical Slice de 3 minutos (180s) dividido en 4 tramos
/// y redirige a ResultsScene tanto en victoria como en derrota (Diagrama Game Flow GDD).
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Configuración de Tiempo (Vertical Slice)")]
    public float totalMissionDuration = 180f; // 3 minutos exactos
    public float bossAppearTime = 105f;       // Minuto 1:45
    private float elapsedTime = 0f;
    private int currentTramo = 0;
    private bool missionEnded = false;
    private bool bossSpawned = false;

    [Header("Referencias de Escena")]
    public Transform shipTransform;
    public ShipFlightController flightController;
    public TMP_Text timerText;
    public string resultsSceneName = "ResultsScene";

    [Header("Prefabs de Oleadas, Obstáculos y Jefe")]
    public GameObject bronzeTargetPrefab;
    public GameObject silverTargetPrefab;
    public GameObject goldTargetPrefab;
    public GameObject powerUpPrefab;
    public GameObject minePrefab;
    public GameObject asteroidPrefab;
    public GameObject bossDreadnoughtPrefab;

    [Header("Zona de Aparición en el Corredor")]
    public float leadTimeSeconds = 4.5f;
    public Vector2 spawnRangeX = new Vector2(-7f, 7f);
    public Vector2 spawnRangeY = new Vector2(-2f, 5f);

    private float nextSpawnTime = 1.5f;
    private float nextPowerUpTime = 16f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        EnterTramo(1);
    }

    private void Update()
    {
        if (missionEnded) return;

        elapsedTime += Time.deltaTime;
        UpdateTimerHUD();

        // 1. Condición de Derrota por escudo a 0% -> Pantalla Final con Rango F
        if (ScoreManager.Instance != null && ScoreManager.Instance.currentHealth <= 0)
        {
            EndMission("DERROTA: NAVE DESTRUIDA", false);
            return;
        }

        // 2. Condición de Derrota por tiempo agotado sin destruir al Boss -> Pantalla Final con Rango F
        if (elapsedTime >= totalMissionDuration)
        {
            EndMission("DERROTA: TIEMPO AGOTADO", false);
            return;
        }

        // 3. Control de Tramos según cronómetro del GDD
        if (elapsedTime < 30f)
        {
            if (currentTramo != 1) EnterTramo(1);
            HandleSpawning(2.2f, 1);
        }
        else if (elapsedTime < 75f)
        {
            if (currentTramo != 2) EnterTramo(2);
            HandleSpawning(1.7f, 2);
        }
        else if (elapsedTime < bossAppearTime)
        {
            if (currentTramo != 3) EnterTramo(3);
            HandleSpawning(1.3f, 3);
        }
        else
        {
            if (currentTramo != 4) EnterTramo(4);
        }
    }

    private void EnterTramo(int tramo)
    {
        currentTramo = tramo;
        switch (tramo)
        {
            case 1:
                ScoreManager.Instance?.ShowBanner("TRAMO 1: CALIBRACIÓN DE ARMAS", Color.cyan, 3.5f);
                break;
            case 2:
                ScoreManager.Instance?.ShowBanner("TRAMO 2: CINTURÓN DE ASTEROIDES Y MINAS", Color.yellow, 3.5f);
                break;
            case 3:
                ScoreManager.Instance?.ShowBanner("TRAMO 3: OLEADA TÁCTICA - OBJETIVOS ORO", new Color(1f, 0.5f, 0f), 3.5f);
                break;
            case 4:
                SpawnBossDreadnought();
                break;
        }
    }

    private void HandleSpawning(float baseInterval, int tramo)
    {
        if (shipTransform == null) return;

        if (Time.time >= nextPowerUpTime && powerUpPrefab != null)
        {
            SpawnAtRandomLane(powerUpPrefab);
            nextPowerUpTime = Time.time + 18f;
        }

        if (Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + baseInterval;

        GameObject prefabToSpawn = bronzeTargetPrefab;
        float roll = Random.value;

        if (tramo == 1)
        {
            if (roll < 0.80f || asteroidPrefab == null) prefabToSpawn = bronzeTargetPrefab;
            else prefabToSpawn = asteroidPrefab;
        }
        else if (tramo == 2)
        {
            if (roll < 0.45f) prefabToSpawn = silverTargetPrefab;
            else if (roll < 0.72f && asteroidPrefab != null) prefabToSpawn = asteroidPrefab;
            else if (roll < 0.90f && minePrefab != null) prefabToSpawn = minePrefab;
            else prefabToSpawn = bronzeTargetPrefab;
        }
        else if (tramo == 3)
        {
            if (roll < 0.35f) prefabToSpawn = goldTargetPrefab;
            else if (roll < 0.65f) prefabToSpawn = silverTargetPrefab;
            else if (roll < 0.85f && asteroidPrefab != null) prefabToSpawn = asteroidPrefab;
            else prefabToSpawn = minePrefab;
        }

        if (prefabToSpawn != null)
        {
            SpawnAtRandomLane(prefabToSpawn);
        }
    }

    private void SpawnAtRandomLane(GameObject prefab)
    {
        float currentSpeed = flightController != null ? Mathf.Max(flightController.forwardSpeed, 2f) : 15f;

        float dynamicSpawnDist = Mathf.Clamp(currentSpeed * leadTimeSeconds, 32f, 130f);
        float dynamicLifetime = (dynamicSpawnDist + 25f) / currentSpeed;

        Vector3 spawnPos = new Vector3(
            Random.Range(spawnRangeX.x, spawnRangeX.y),
            Random.Range(spawnRangeY.x, spawnRangeY.y),
            shipTransform.position.z + dynamicSpawnDist
        );

        GameObject obj = Instantiate(prefab, spawnPos, prefab.transform.rotation);
        Destroy(obj, dynamicLifetime);
    }

    private void SpawnBossDreadnought()
    {
        if (bossSpawned) return;
        bossSpawned = true;

        if (flightController != null)
        {
            flightController.isBossPhase = true;
            flightController.spawnTunnelMarkers = false;
        }

        ScoreManager.Instance?.ShowBanner("¡ALERTA DE JEFE: DREADNOUGHT MK-IV!", Color.red, 4.5f);

        if (bossDreadnoughtPrefab != null && shipTransform != null)
        {
            Vector3 bossPos = new Vector3(0f, 1.5f, shipTransform.position.z + 55f);
            GameObject bossObj = Instantiate(bossDreadnoughtPrefab, bossPos, Quaternion.identity);
            BossDreadnought bossScript = bossObj.GetComponent<BossDreadnought>();
            if (bossScript != null)
            {
                bossScript.Initialize(shipTransform, minePrefab);
            }
        }
    }

    public void OnBossDefeated()
    {
        if (missionEnded) return;
        ScoreManager.Instance?.AddScore(3000, false);
        EndMission("¡VICTORIA: DREADNOUGHT MK-IV DESTRUIDO!", true);
    }

    public void EndMission(string outcome, bool isVictory)
    {
        if (missionEnded) return;
        missionEnded = true;

        ScoreManager.Instance?.ShowBanner(outcome, isVictory ? Color.green : Color.red, 3f);

        if (ScoreManager.Instance != null)
        {
            MatchResultsData.finalScore = ScoreManager.Instance.currentScore;
            MatchResultsData.finalRank = isVictory ? ScoreManager.Instance.CalculateFinalRank() : "F";
            MatchResultsData.shieldRemaining = ScoreManager.Instance.currentHealth;
            MatchResultsData.targetsDestroyed = ScoreManager.Instance.targetsDestroyed;

            int fired = ScoreManager.Instance.totalShotsFired;
            int hits = ScoreManager.Instance.totalShotsHit;
            MatchResultsData.accuracyPercent = fired > 0 ? ((float)hits / fired) * 100f : 0f;
        }

        MatchResultsData.outcomeTitle = outcome;
        Invoke(nameof(LoadResultsScene), 2.5f);
    }

    private void LoadResultsScene()
    {
        SceneManager.LoadScene(resultsSceneName);
    }

    private void UpdateTimerHUD()
    {
        if (timerText == null) return;
        float remaining = Mathf.Max(0f, totalMissionDuration - elapsedTime);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"TIEMPO: {minutes:00}:{seconds:00}";
    }
}