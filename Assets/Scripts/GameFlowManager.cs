using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class MatchResultsData
{
    public static int finalScore = 0;
    public static string finalRank = "F";
    public static int hullRemaining = 0;
    public static int shieldRemaining = 0;
    public static int targetsDestroyed = 0;
    public static int rechargesCount = 0;
    public static float accuracyPercent = 0f;
    public static string outcomeTitle = "MISIÓN FINALIZADA";
}

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Configuración de Tiempo (GDD: 3:00 Total / Boss 1:45)")]
    public float totalMissionDuration = 180f;
    public float bossAppearTime = 105f;
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
    public bool syncBoundsWithShip = true;
    public Vector2 spawnRangeX = new Vector2(-7.5f, 7.5f);
    public Vector2 spawnRangeY = new Vector2(-4.0f, 5.0f);

    private float nextSpawnTime = 1.0f;
    private float nextPowerUpTime = 14f;

    private static readonly float[] CorridorLanesX = { -6.5f, -3.25f, 0f, 3.25f, 6.5f };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        totalMissionDuration = 180f;
        bossAppearTime = 105f;
    }

    private void Start()
    {
        if (syncBoundsWithShip && flightController != null && shipTransform != null)
        {
            Vector3 shipStart = shipTransform.position;
            spawnRangeX = new Vector2(
                shipStart.x + flightController.movementBoundsX.x + 0.5f,
                shipStart.x + flightController.movementBoundsX.y - 0.5f
            );
            spawnRangeY = new Vector2(
                shipStart.y + flightController.movementBoundsY.x,
                shipStart.y + flightController.movementBoundsY.y
            );
        }

        EnterTramo(1);
    }

    private void Update()
    {
        if (missionEnded) return;

        elapsedTime += Time.deltaTime;
        UpdateTimerHUD();

        if (ScoreManager.Instance != null && ScoreManager.Instance.currentHealth <= 0)
        {
            EndMission("DERROTA: NAVE DESTRUIDA", false);
            return;
        }

        if (elapsedTime >= totalMissionDuration)
        {
            EndMission("DERROTA: TIEMPO AGOTADO", false);
            return;
        }

        if (elapsedTime < 30f)
        {
            if (currentTramo != 1) EnterTramo(1);
            HandlePopulatedSpawning(1.9f, 1);
        }
        else if (elapsedTime < 75f)
        {
            if (currentTramo != 2) EnterTramo(2);
            HandlePopulatedSpawning(1.5f, 2);
        }
        else if (elapsedTime < bossAppearTime)
        {
            if (currentTramo != 3) EnterTramo(3);
            HandlePopulatedSpawning(1.2f, 3);
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

    private void HandlePopulatedSpawning(float baseInterval, int tramo)
    {
        if (shipTransform == null) return;

        int tier = GameSettingsData.DifficultyTier;

        float[] intervalMults = { 1.15f, 0.95f, 0.78f, 0.62f };
        int[] minObjectsPerWave = { 1, 2, 3, 4 };
        int[] maxObjectsPerWave = { 2, 3, 4, 5 };
        float[] hazardRatioBoost = { -0.08f, 0.05f, 0.18f, 0.30f };

        if (Time.time >= nextPowerUpTime && powerUpPrefab != null)
        {
            SpawnAtSpecificX(powerUpPrefab, Random.Range(-4.5f, 4.5f), Random.Range(spawnRangeY.x + 0.5f, spawnRangeY.y - 0.5f), 0f);
            nextPowerUpTime = Time.time + 16f;
        }

        if (Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + (baseInterval * intervalMults[tier]);

        int spawnCount = Random.Range(minObjectsPerWave[tier], maxObjectsPerWave[tier] + 1);
        List<int> availableLanes = new List<int> { 0, 1, 2, 3, 4 };
        float midY = (spawnRangeY.x + spawnRangeY.y) * 0.5f;

        for (int i = 0; i < spawnCount && availableLanes.Count > 0; i++)
        {
            int pickIdx = Random.Range(0, availableLanes.Count);
            int lane = availableLanes[pickIdx];
            availableLanes.RemoveAt(pickIdx);

            bool forceTarget = (i == 0 && tramo == 1);
            GameObject prefab = forceTarget
                ? bronzeTargetPrefab
                : PickPrefabForTramo(tramo, hazardRatioBoost[tier]);

            if (prefab != null)
            {
                float laneX = Mathf.Clamp(CorridorLanesX[lane] + Random.Range(-0.8f, 0.8f), spawnRangeX.x, spawnRangeX.y);
                float laneY = (i % 2 == 1)
                    ? Random.Range(spawnRangeY.x, midY)
                    : Random.Range(spawnRangeY.x, spawnRangeY.y);

                float zOffset = i * 3.5f;
                SpawnAtSpecificX(prefab, laneX, laneY, zOffset);
            }
        }

        if (asteroidPrefab != null && tier >= 1)
        {
            float side = Random.value < 0.5f ? -11.5f : 11.5f;
            SpawnAtSpecificX(asteroidPrefab, side, Random.Range(spawnRangeY.x, spawnRangeY.y + 1f), Random.Range(2f, 10f));
        }
    }

    private GameObject PickPrefabForTramo(int tramo, float hazardBoost)
    {
        float roll = Random.value;

        if (tramo == 1)
        {
            float hazardChance = Mathf.Clamp(0.22f + hazardBoost, 0.08f, 0.65f);
            if (roll < hazardChance)
            {
                return (Random.value < 0.7f && asteroidPrefab != null) ? asteroidPrefab : minePrefab;
            }
            return bronzeTargetPrefab;
        }
        else if (tramo == 2)
        {
            float hazardChance = Mathf.Clamp(0.45f + hazardBoost, 0.20f, 0.78f);
            if (roll < hazardChance)
            {
                return (Random.value < 0.55f && asteroidPrefab != null) ? asteroidPrefab : minePrefab;
            }
            return (Random.value < 0.65f && silverTargetPrefab != null) ? silverTargetPrefab : bronzeTargetPrefab;
        }
        else
        {
            float hazardChance = Mathf.Clamp(0.48f + hazardBoost, 0.25f, 0.80f);
            if (roll < hazardChance)
            {
                return (Random.value < 0.50f && asteroidPrefab != null) ? asteroidPrefab : minePrefab;
            }
            return (Random.value < 0.50f && goldTargetPrefab != null) ? goldTargetPrefab : silverTargetPrefab;
        }
    }

    private void SpawnAtSpecificX(GameObject prefab, float xPos, float yPos, float extraZ)
    {
        float currentSpeed = flightController != null ? Mathf.Max(flightController.forwardSpeed, 2f) : 15f;

        float dynamicSpawnDist = Mathf.Clamp(currentSpeed * leadTimeSeconds, 34f, 130f) + extraZ;
        float dynamicLifetime = (dynamicSpawnDist + 25f) / currentSpeed;

        Vector3 spawnPos = new Vector3(xPos, yPos, shipTransform.position.z + dynamicSpawnDist);
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

        foreach (TargetObjective leftover in FindObjectsByType<TargetObjective>(FindObjectsSortMode.None))
        {
            if (leftover != null) Destroy(leftover.gameObject);
        }

        ScoreManager.Instance?.ShowBanner("¡ALERTA DE JEFE: DREADNOUGHT MK-IV!", Color.red, 4.5f);

        if (bossDreadnoughtPrefab != null && shipTransform != null)
        {
            Vector3 bossPos = new Vector3(0f, 1.0f, shipTransform.position.z + 55f);
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
            MatchResultsData.hullRemaining = ScoreManager.Instance.currentHealth;
            MatchResultsData.shieldRemaining = ScoreManager.Instance.currentHealth;
            MatchResultsData.targetsDestroyed = ScoreManager.Instance.targetsDestroyed;

            int fired = ScoreManager.Instance.totalShotsFired;
            int hits = ScoreManager.Instance.totalShotsHit;
            MatchResultsData.accuracyPercent = fired > 0 ? ((float)hits / fired) * 100f : 0f;
        }

        DiegeticWeapon weapon = FindFirstObjectByType<DiegeticWeapon>();
        MatchResultsData.rechargesCount = weapon != null ? weapon.RechargesCount : 0;

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