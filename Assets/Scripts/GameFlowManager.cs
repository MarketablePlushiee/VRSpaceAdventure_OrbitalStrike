using System.Collections;
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
    private bool bossSequenceStarted = false;
    private bool stopSpawning = false;

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
    public float leadTimeSeconds = 5.5f;
    [Tooltip("Distancia mínima en Z para spawnear (ponla mayor a Fog End = 95 para que nazcan detrás de la niebla).")]
    public float minSpawnDistanceZ = 105f;
    public bool syncBoundsWithShip = true;
    public Vector2 spawnBoundsMargin = new Vector2(1.5f, 1.0f);
    public Vector2 spawnRangeX = new Vector2(-18.5f, 18.5f);
    public Vector2 spawnRangeY = new Vector2(-9.0f, 14.0f);
    [Range(3, 11)]
    public int corridorLaneCount = 7;

    [Header("Densidad y Movimiento de Oleadas")]
    [Tooltip("Multiplica el tiempo entre oleadas. Menor = salen más seguido.")]
    public float spawnIntervalMultiplier = 0.8f;
    [Tooltip("Objetos extra por oleada para llenar el ancho del corredor.")]
    public int extraObjectsPerWave = 2;
    public float waveZSpacing = 4.5f;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que una diana o mina se mueva de lado a lado en Tramos 2 y 3.")]
    public float movingObjectChance = 0.35f;
    public bool spawnAmbientSideAsteroids = true;
    public float ambientAsteroidOuterOffsetX = 4.5f;

    [Header("Suministros Tácticos en Combate contra el Jefe")]
    [Tooltip("Intervalo en segundos entre potenciadores durante la batalla contra el Jefe.")]
    public float bossPowerUpInterval = 16f;
    [Tooltip("Distancia delante de la nave donde flota el potenciador (menor que la del jefe = 52 para que no quede tapado).")]
    public float bossPowerUpDistanceZ = 34f;
    [Tooltip("Segundos que el potenciador acompaña al jugador antes de desaparecer si no le disparas.")]
    public float bossPowerUpDuration = 11f;

    [Header("Nebulosa y Tormenta de Entrada del Jefe")]
    [Tooltip("Material Skybox/Procedural del color de la niebla para los Tramos 1-3.")]
    public Material tunnelSkybox;
    [Tooltip("Skybox del espacio con estrellas para la batalla contra el Jefe.")]
    public Material bossSpaceSkybox;
    public bool controlNebulaFog = true;
    public Color normalFogColor = new Color(0.14f, 0.06f, 0.21f);
    public Color stormFogColor = new Color(0.25f, 0.07f, 0.35f);
    public float normalFogStart = 35f;
    public float normalFogEnd = 95f;
    public float stormFogStart = 2f;
    public float stormFogEnd = 10f;
    public float bossFogEnd = 90f;
    [Tooltip("Segundos antes de Boss Appear Time en que empieza la alerta y se cierra la niebla.")]
    public float warningLeadSeconds = 6f;
    public AudioClip bossAlarmClip;

    private float nextSpawnTime = 1.0f;
    private float nextPowerUpTime = 14f;

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
        SyncSpawnBounds();

        if (tunnelSkybox != null)
        {
            RenderSettings.skybox = tunnelSkybox;
        }

        if (controlNebulaFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = normalFogColor;
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
        }

        EnterTramo(1);
    }

    private void SyncSpawnBounds()
    {
        if (syncBoundsWithShip && flightController != null && shipTransform != null)
        {
            Vector3 shipStart = shipTransform.position;
            float minX = Mathf.Min(flightController.movementBoundsX.x, flightController.movementBoundsX.y);
            float maxX = Mathf.Max(flightController.movementBoundsX.x, flightController.movementBoundsX.y);
            float minY = Mathf.Min(flightController.movementBoundsY.x, flightController.movementBoundsY.y);
            float maxY = Mathf.Max(flightController.movementBoundsY.x, flightController.movementBoundsY.y);

            spawnRangeX = new Vector2(
                shipStart.x + minX + spawnBoundsMargin.x,
                shipStart.x + maxX - spawnBoundsMargin.x
            );
            spawnRangeY = new Vector2(
                shipStart.y + minY + spawnBoundsMargin.y,
                shipStart.y + maxY - spawnBoundsMargin.y
            );
        }
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

        if (!bossSequenceStarted && elapsedTime >= (bossAppearTime - warningLeadSeconds))
        {
            StartBossNebulaTransition();
        }

        // Suministros tácticos periódicos que acompañan al jugador durante el combate contra el Jefe
        if (stopSpawning)
        {
            if (currentTramo == 4 && Time.time >= nextPowerUpTime && powerUpPrefab != null && shipTransform != null)
            {
                SpawnBossEscortPowerUp();
                nextPowerUpTime = Time.time + Mathf.Max(8f, bossPowerUpInterval);
            }
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
    }

    private void SpawnBossEscortPowerUp()
    {
        float safeMinX = spawnRangeX.x * 0.65f;
        float safeMaxX = spawnRangeX.y * 0.65f;
        float safeMinY = spawnRangeY.x + 1.0f;
        float safeMaxY = spawnRangeY.y - 1.0f;

        float startX = Random.Range(safeMinX, safeMaxX);
        float startY = Random.Range(safeMinY, safeMaxY);
        Vector3 spawnPos = new Vector3(startX, startY, shipTransform.position.z + bossPowerUpDistanceZ);

        GameObject powerUpObj = Instantiate(powerUpPrefab, spawnPos, powerUpPrefab.transform.rotation);

        BossTacticalPowerUpMover escortMover = powerUpObj.AddComponent<BossTacticalPowerUpMover>();
        escortMover.Initialize(
            shipTransform,
            bossPowerUpDistanceZ,
            new Vector2(safeMinX, safeMaxX),
            new Vector2(safeMinY, safeMaxY),
            bossPowerUpDuration
        );
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
        }
    }

    private void HandlePopulatedSpawning(float baseInterval, int tramo)
    {
        if (shipTransform == null) return;

        int tier = GameSettingsData.DifficultyTier;

        float[] intervalMults = { 1.15f, 0.95f, 0.78f, 0.62f };
        int[] minObjectsPerWave = { 1, 2, 3, 4 };
        int[] maxObjectsPerWave = { 2, 3, 4, 5 };
        float[] hazardRatioBoost = { -0.05f, 0.08f, 0.20f, 0.32f };

        if (Time.time >= nextPowerUpTime && powerUpPrefab != null)
        {
            float powerUpX = Random.Range(spawnRangeX.x * 0.75f, spawnRangeX.y * 0.75f);
            float powerUpY = Random.Range(spawnRangeY.x + 0.5f, spawnRangeY.y - 0.5f);
            SpawnAtSpecificX(powerUpPrefab, powerUpX, powerUpY, 0f, false);
            nextPowerUpTime = Time.time + 16f;
        }

        if (Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + (baseInterval * intervalMults[tier] * Mathf.Max(0.25f, spawnIntervalMultiplier));

        int minCount = Mathf.Max(1, minObjectsPerWave[tier] + extraObjectsPerWave);
        int maxCount = Mathf.Max(minCount, maxObjectsPerWave[tier] + extraObjectsPerWave);
        int spawnCount = Random.Range(minCount, maxCount + 1);

        int lanes = Mathf.Max(3, corridorLaneCount);
        List<int> availableLanes = new List<int>(lanes);
        for (int l = 0; l < lanes; l++) availableLanes.Add(l);

        float midY = (spawnRangeY.x + spawnRangeY.y) * 0.5f;
        float laneStep = (spawnRangeX.y - spawnRangeX.x) / (lanes - 1);

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
                float laneCenterX = Mathf.Lerp(spawnRangeX.x, spawnRangeX.y, (float)lane / (lanes - 1));
                float jitterX = Random.Range(-laneStep * 0.3f, laneStep * 0.3f);
                float laneX = Mathf.Clamp(laneCenterX + jitterX, spawnRangeX.x, spawnRangeX.y);

                float laneY = (i % 2 == 1)
                    ? Random.Range(spawnRangeY.x, midY)
                    : Random.Range(spawnRangeY.x, spawnRangeY.y);

                float zOffset = i * waveZSpacing;
                bool allowMove = (tramo >= 2) && (Random.value < movingObjectChance);
                SpawnAtSpecificX(prefab, laneX, laneY, zOffset, allowMove);
            }
        }

        if (spawnAmbientSideAsteroids && asteroidPrefab != null && tier >= 1)
        {
            float side = Random.value < 0.5f
                ? (spawnRangeX.x - ambientAsteroidOuterOffsetX)
                : (spawnRangeX.y + ambientAsteroidOuterOffsetX);
            SpawnAtSpecificX(asteroidPrefab, side, Random.Range(spawnRangeY.x, spawnRangeY.y + 1f), Random.Range(2f, 10f), false);
        }
    }

    private GameObject PickPrefabForTramo(int tramo, float hazardBoost)
    {
        float roll = Random.value;

        if (tramo == 1)
        {
            float hazardChance = Mathf.Clamp(0.25f + hazardBoost, 0.10f, 0.65f);
            if (roll < hazardChance)
            {
                return (Random.value < 0.7f && asteroidPrefab != null) ? asteroidPrefab : minePrefab;
            }
            return bronzeTargetPrefab;
        }
        else if (tramo == 2)
        {
            float hazardChance = Mathf.Clamp(0.48f + hazardBoost, 0.22f, 0.78f);
            if (roll < hazardChance)
            {
                return (Random.value < 0.55f && asteroidPrefab != null) ? asteroidPrefab : minePrefab;
            }
            return (Random.value < 0.65f && silverTargetPrefab != null) ? silverTargetPrefab : bronzeTargetPrefab;
        }
        else
        {
            float hazardChance = Mathf.Clamp(0.52f + hazardBoost, 0.28f, 0.80f);
            if (roll < hazardChance)
            {
                return (Random.value < 0.50f && asteroidPrefab != null) ? asteroidPrefab : minePrefab;
            }
            return (Random.value < 0.50f && goldTargetPrefab != null) ? goldTargetPrefab : silverTargetPrefab;
        }
    }

    private void SpawnAtSpecificX(GameObject prefab, float xPos, float yPos, float extraZ, bool enableOscillation)
    {
        float currentSpeed = flightController != null ? Mathf.Max(flightController.forwardSpeed, 2f) : 15f;

        float dynamicSpawnDist = Mathf.Max(minSpawnDistanceZ, currentSpeed * leadTimeSeconds) + extraZ;
        float dynamicLifetime = (dynamicSpawnDist + 30f) / currentSpeed;

        Vector3 spawnPos = new Vector3(xPos, yPos, shipTransform.position.z + dynamicSpawnDist);
        GameObject obj = Instantiate(prefab, spawnPos, prefab.transform.rotation);

        if (enableOscillation)
        {
            TargetObjective target = obj.GetComponent<TargetObjective>();
            if (target != null && target.objectiveType != TargetObjective.ObjectiveType.Asteroid)
            {
                target.oscillate = true;
                target.moveAxis = Random.value < 0.7f ? Vector3.right : Vector3.up;
                target.moveAmplitude = Random.Range(2.0f, 4.0f);
                target.moveFrequency = Random.Range(1.2f, 2.2f);
            }
        }

        Destroy(obj, dynamicLifetime);
    }

    private void StartBossNebulaTransition()
    {
        bossSequenceStarted = true;
        stopSpawning = true;
        currentTramo = 4;

        if (flightController != null)
        {
            flightController.spawnTunnelMarkers = false;
        }

        ScoreManager.Instance?.ShowBanner("¡ALERTA: DREADNOUGHT DETECTADO!\nENTRANDO EN NEBULOSA DENSA...", Color.red, 5.5f);

        if (bossAlarmClip != null && shipTransform != null)
        {
            AudioSource.PlayClipAtPoint(bossAlarmClip, shipTransform.position, 1.0f);
        }

        StartCoroutine(NebulaBossStormRoutine());
    }

    private IEnumerator NebulaBossStormRoutine()
    {
        float fogInTime = 2.5f;
        float elapsed = 0f;

        while (elapsed < fogInTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fogInTime);
            if (controlNebulaFog)
            {
                RenderSettings.fogStartDistance = Mathf.Lerp(normalFogStart, stormFogStart, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(normalFogEnd, stormFogEnd, t);
                RenderSettings.fogColor = Color.Lerp(normalFogColor, stormFogColor, t);
            }
            yield return null;
        }

        if (flightController != null)
        {
            flightController.isBossPhase = true;
            flightController.ClearAllTunnelMarkers();
        }

        foreach (TargetObjective leftover in FindObjectsByType<TargetObjective>(FindObjectsSortMode.None))
        {
            if (leftover != null) Destroy(leftover.gameObject);
        }

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

        if (bossSpaceSkybox != null)
        {
            RenderSettings.skybox = bossSpaceSkybox;
        }

        // Primer potenciador táctico llega 6s después de iniciar el combate contra el Jefe
        nextPowerUpTime = Time.time + 6f;

        yield return new WaitForSeconds(0.8f);

        ScoreManager.Instance?.ShowBanner("¡DREADNOUGHT MK-IV AL FRENTE!\nDESTRUYE LOS 4 NÚCLEOS", Color.red, 4.0f);

        float fogOutTime = 2.2f;
        elapsed = 0f;
        while (elapsed < fogOutTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fogOutTime);
            if (controlNebulaFog)
            {
                RenderSettings.fogStartDistance = Mathf.Lerp(stormFogStart, normalFogStart, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(stormFogEnd, bossFogEnd, t);
                RenderSettings.fogColor = Color.Lerp(stormFogColor, normalFogColor, t);
            }
            yield return null;
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

/// <summary>
/// Mantiene los potenciadores en una órbita estable delante de la nave durante la batalla contra el Jefe
/// para que no pasen de largo en un segundo y el jugador pueda dispararles cómodamente.
/// </summary>
public class BossTacticalPowerUpMover : MonoBehaviour
{
    private Transform shipTransform;
    private float distanceAheadZ;
    private Vector2 boundsX;
    private Vector2 boundsY;
    private float anchorX;
    private float anchorY;
    private float timer;
    private float phaseOffset;

    public void Initialize(Transform ship, float zOffset, Vector2 xRange, Vector2 yRange, float lifetime)
    {
        shipTransform = ship;
        distanceAheadZ = zOffset;
        boundsX = xRange;
        boundsY = yRange;
        anchorX = transform.position.x;
        anchorY = transform.position.y;
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);

        Destroy(gameObject, Mathf.Max(4f, lifetime));
    }

    private void LateUpdate()
    {
        if (shipTransform == null) return;

        timer += Time.deltaTime;
        float targetX = Mathf.Clamp(anchorX + Mathf.Sin(timer * 1.15f + phaseOffset) * 3.2f, boundsX.x, boundsX.y);
        float targetY = Mathf.Clamp(anchorY + Mathf.Cos(timer * 1.5f + phaseOffset) * 1.4f, boundsY.x, boundsY.y);
        float targetZ = shipTransform.position.z + distanceAheadZ;

        transform.position = new Vector3(targetX, targetY, targetZ);
    }
}