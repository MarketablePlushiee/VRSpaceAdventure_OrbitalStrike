using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossDreadnought : MonoBehaviour
{
    [Header("Movimiento Orbital Frontal")]
    public float distanceAheadZ = 52f;
    public float horizontalRange = 13.5f;
    public float verticalRange = 5.5f;
    public float baseMoveSpeed = 1.5f;

    [Header("Rotación y Ventana de Contraataque")]
    [Tooltip("Velocidad base de giro sobre el eje Z (en Normal = 16°/s).")]
    public float baseRotationSpeed = 16f;
    [Tooltip("Segundos tras lanzar una ráfaga en los que el jefe frena su movimiento y giro para apuntar a los núcleos.")]
    public float postVolleySlowDuration = 1.6f;
    [Range(0.1f, 1f)]
    public float postVolleyMoveMultiplier = 0.35f;
    [Tooltip("Segundos de pausa breve tras destruir un núcleo.")]
    public float coreBreakStaggerDuration = 1.2f;

    [Header("Ataque de Barrera (Escalado por Dificultad)")]
    public float baseAttackInterval = 4.5f;
    public float projectileSpeed = 22f;
    public GameObject attackPrefab;
    [Tooltip("Opcional: arrastra Hazard_Asteroid para mezclar rocas indestructibles que obliguen a esquivar.")]
    public GameObject secondaryObstaclePrefab;
    [Range(0f, 1f)]
    public float asteroidRatioInVolley = 0.25f;
    public int extraProjectilesPerVolley = 1;

    [Header("Maniobra Evasiva (Micro-Dash en Normal+)")]
    public bool enableMicroDash = true;
    public float microDashDistance = 5.5f;
    public float microDashDuration = 0.45f;

    [Header("Escalado de Furia al Perder Núcleos")]
    public float speedBoostPerDestroyedCore = 0.10f;
    public float fireRateBoostPerDestroyedCore = 0.08f;

    private Transform playerShip;
    private float nextAttackTime;
    private float phaseTimer;
    private float slowWindowEndTime;
    private float dashOffsetX = 0f;
    private bool isDashing = false;
    private int initialCores = 4;
    private int remainingCores = 4;
    private bool isDefeated = false;

    private readonly List<GameObject> activeProjectiles = new List<GameObject>();

    public bool IsInvulnerable => isDashing;

    private int DifficultyTier
    {
        get
        {
            float spd = GameSettingsData.selectedForwardSpeed;
            if (spd <= 10f) return 0; // Fácil
            if (spd <= 17f) return 1; // Normal
            if (spd <= 24f) return 2; // Difícil
            return 3;                 // Imposible
        }
    }

    public void Initialize(Transform ship, GameObject hazardPrefab)
    {
        playerShip = ship;
        if (attackPrefab == null) attackPrefab = hazardPrefab;

        BossCore[] cores = GetComponentsInChildren<BossCore>(true);
        initialCores = cores.Length > 0 ? cores.Length : 4;
        remainingCores = initialCores;
        nextAttackTime = Time.time + 2.5f;
    }

    private void Start()
    {
        if (playerShip == null)
        {
            ShipFlightController ship = FindFirstObjectByType<ShipFlightController>();
            if (ship != null) Initialize(ship.transform, attackPrefab);
        }
    }

    private void Update()
    {
        if (isDefeated || playerShip == null) return;

        int tier = DifficultyTier;
        int destroyedCores = Mathf.Max(0, initialCores - remainingCores);
        float rageSpeedMult = 1f + (destroyedCores * speedBoostPerDestroyedCore);
        float windowMult = (Time.time < slowWindowEndTime) ? postVolleyMoveMultiplier : 1f;

        float[] speedMults = { 0.75f, 1.0f, 1.25f, 1.50f };
        phaseTimer += Time.deltaTime * baseMoveSpeed * speedMults[tier] * rageSpeedMult * windowMult;

        float targetX = Mathf.Sin(phaseTimer) * horizontalRange + dashOffsetX;
        float targetY = 2.0f + Mathf.Cos(phaseTimer * 1.4f) * verticalRange;
        float targetZ = playerShip.position.z + distanceAheadZ;

        transform.position = Vector3.Lerp(transform.position, new Vector3(targetX, targetY, targetZ), Time.deltaTime * 5f);

        float[] rotMults = { 0.69f, 1.0f, 1.375f, 1.75f }; // ~11, 16, 22, 28 °/s
        transform.Rotate(Vector3.forward, baseRotationSpeed * rotMults[tier] * rageSpeedMult * windowMult * Time.deltaTime, Space.Self);

        if (Time.time >= nextAttackTime)
        {
            FireAttackWave(tier, destroyedCores);
            float[] intervalMults = { 1.22f, 1.0f, 0.80f, 0.62f }; // ~5.5s, 4.5s, 3.6s, 2.8s
            float rageFireMult = Mathf.Max(0.65f, 1f - (destroyedCores * fireRateBoostPerDestroyedCore));
            nextAttackTime = Time.time + (baseAttackInterval * intervalMults[tier] * rageFireMult);
            slowWindowEndTime = Time.time + postVolleySlowDuration;
        }
    }

    private void FireAttackWave(int tier, int destroyedCores)
    {
        if (attackPrefab == null || playerShip == null) return;

        activeProjectiles.RemoveAll(item => item == null);

        int[] volleyCounts = { 1, 2, 3, 4 };
        float[] speedBonus = { 0.80f, 1.0f, 1.18f, 1.38f };
        int[] scaledMineDamages = { 10, 15, 20, 25 };

        int count = Mathf.Max(1, volleyCounts[tier] + extraProjectilesPerVolley + (destroyedCores >= 3 ? 1 : 0));
        float projSpeed = projectileSpeed * speedBonus[tier];
        int guaranteedDestructibleIndex = Random.Range(0, count);

        for (int i = 0; i < count; i++)
        {
            float spreadOffset = (count == 1) ? 0f : Mathf.Lerp(-8.0f, 8.0f, (float)i / (count - 1));
            Vector3 spawnPos = transform.position + new Vector3(spreadOffset, Random.Range(-2.0f, 2.0f), -4f);

            GameObject chosenPrefab = attackPrefab;
            if (i != guaranteedDestructibleIndex && secondaryObstaclePrefab != null && Random.value < asteroidRatioInVolley)
            {
                chosenPrefab = secondaryObstaclePrefab;
            }

            GameObject proj = Instantiate(chosenPrefab, spawnPos, Quaternion.identity);
            activeProjectiles.Add(proj);

            TargetObjective target = proj.GetComponent<TargetObjective>();
            if (target != null && target.objectiveType != TargetObjective.ObjectiveType.Asteroid)
            {
                target.hullDamageOnCrash = scaledMineDamages[tier];
            }

            Vector3 targetPoint = playerShip.position + new Vector3(Random.Range(-5.5f, 5.5f), Random.Range(-3.0f, 3.0f), 0f);
            Vector3 dir = (targetPoint - spawnPos).normalized;

            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb == null) rb = proj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.linearVelocity = dir * projSpeed;

            Destroy(proj, 6f);
        }
    }

    public void OnCoreDestroyed()
    {
        if (isDefeated) return;

        remainingCores = Mathf.Max(0, remainingCores - 1);
        if (remainingCores <= 0)
        {
            isDefeated = true;
            ClearAllProjectiles();
            GameFlowManager.Instance?.OnBossDefeated();
            Destroy(gameObject, 0.25f);
            return;
        }

        slowWindowEndTime = Mathf.Max(slowWindowEndTime, Time.time + coreBreakStaggerDuration);
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 1.1f);
        ScoreManager.Instance?.ShowBanner($"¡NÚCLEO DESTRUIDO! RESTAN {remainingCores}", Color.yellow, 2.2f);

        if (enableMicroDash && DifficultyTier >= 1 && !isDashing)
        {
            StartCoroutine(PerformMicroDash());
        }
    }

    public void ClearAllProjectiles()
    {
        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            if (activeProjectiles[i] != null)
            {
                Destroy(activeProjectiles[i]);
            }
        }
        activeProjectiles.Clear();
    }

    private void OnDestroy()
    {
        ClearAllProjectiles();
    }

    private IEnumerator PerformMicroDash()
    {
        isDashing = true;
        float dir = (transform.position.x >= 0f) ? -1f : 1f;
        float targetOffset = dir * microDashDistance;
        float elapsed = 0f;

        while (elapsed < microDashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / microDashDuration);
            dashOffsetX = Mathf.Lerp(0f, targetOffset, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        dashOffsetX = 0f;
        isDashing = false;
    }
}