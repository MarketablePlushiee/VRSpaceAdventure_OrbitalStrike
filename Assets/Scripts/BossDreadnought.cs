using UnityEngine;

/// <summary>
/// Controla el desplazamiento orbital del Dreadnought MK-IV, sus ráfagas de ataque
/// escaladas por dificultad y la condición de victoria al destruir sus 4 núcleos.
/// </summary>
public class BossDreadnought : MonoBehaviour
{
    [Header("Movimiento Orbital Frontal")]
    public float distanceAheadZ = 52f;
    public float horizontalRange = 8.5f;
    public float verticalRange = 3.2f;
    public float baseMoveSpeed = 1.4f;

    [Header("Ataque de Barrera (Escalado por Dificultad)")]
    public float baseAttackInterval = 4.0f;
    public float projectileSpeed = 24f;
    public GameObject attackPrefab;

    private Transform playerShip;
    private float nextAttackTime;
    private float phaseTimer;
    private int remainingCores = 4;
    private bool isDefeated = false;

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

        BossCore[] cores = GetComponentsInChildren<BossCore>();
        remainingCores = cores.Length > 0 ? cores.Length : 4;
        nextAttackTime = Time.time + 2.0f;
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
        float[] speedMults = { 0.75f, 1.0f, 1.35f, 1.75f };
        phaseTimer += Time.deltaTime * baseMoveSpeed * speedMults[tier];

        float targetX = Mathf.Sin(phaseTimer) * horizontalRange;
        float targetY = 1.5f + Mathf.Cos(phaseTimer * 1.4f) * verticalRange;
        float targetZ = playerShip.position.z + distanceAheadZ;

        transform.position = Vector3.Lerp(transform.position, new Vector3(targetX, targetY, targetZ), Time.deltaTime * 5f);

        if (Time.time >= nextAttackTime)
        {
            FireAttackWave(tier);
            float[] intervals = { 4.8f, 4.0f, 2.7f, 1.9f };
            nextAttackTime = Time.time + intervals[tier];
        }
    }

    private void FireAttackWave(int tier)
    {
        if (attackPrefab == null || playerShip == null) return;

        int[] volleyCounts = { 1, 2, 3, 5 };
        float[] speedBonus = { 0.8f, 1.0f, 1.25f, 1.5f };

        int count = volleyCounts[tier];
        float projSpeed = projectileSpeed * speedBonus[tier];

        for (int i = 0; i < count; i++)
        {
            float spreadOffset = (count == 1) ? 0f : Mathf.Lerp(-4.5f, 4.5f, (float)i / (count - 1));
            Vector3 spawnPos = transform.position + new Vector3(spreadOffset, Random.Range(-1.2f, 1.2f), -4f);

            GameObject proj = Instantiate(attackPrefab, spawnPos, Quaternion.identity);
            Vector3 targetPoint = playerShip.position + new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-1.2f, 1.2f), 0f);
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

        remainingCores--;
        if (remainingCores <= 0)
        {
            isDefeated = true;
            GameFlowManager.Instance?.OnBossDefeated();
            Destroy(gameObject, 0.25f);
        }
    }
}