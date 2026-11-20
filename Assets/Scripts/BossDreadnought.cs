using UnityEngine;

/// <summary>
/// Controla al jefe final Dreadnought MK-IV: mantiene distancia frontal constante,
/// oscila en el espacio, lanza minas hacia el jugador y evalúa sus 4 núcleos destructibles.
/// </summary>
public class BossDreadnought : MonoBehaviour
{
    [Header("Posicionamiento y Movimiento")]
    public float hoverDistanceZ = 52f;
    public float swayAmplitudeX = 5.5f;
    public float swayAmplitudeY = 2.2f;
    public float swaySpeed = 0.9f;

    [Header("Núcleos Débiles (Asignar los 4 hijos con TargetObjective)")]
    public TargetObjective[] weakPointCores;

    [Header("Ataque de Minas")]
    public float mineAttackInterval = 5.5f;
    public float mineProjectileSpeed = 24f;

    private Transform playerShip;
    private GameObject minePrefab;
    private float nextAttackTime;
    private bool isDefeated = false;

    public void Initialize(Transform ship, GameObject mine)
    {
        playerShip = ship;
        minePrefab = mine;
        nextAttackTime = Time.time + 3f;
    }

    private void Update()
    {
        if (isDefeated) return;

        // 1. Mantener distancia frontal respecto al avance de la nave y oscilar
        if (playerShip != null)
        {
            float targetX = Mathf.Sin(Time.time * swaySpeed) * swayAmplitudeX;
            float targetY = 1.5f + Mathf.Cos(Time.time * swaySpeed * 0.75f) * swayAmplitudeY;
            float targetZ = playerShip.position.z + hoverDistanceZ;

            Vector3 desiredPos = new Vector3(targetX, targetY, targetZ);
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * 4f);
        }

        // 2. Lanzar minas hacia la posición del jugador
        if (Time.time >= nextAttackTime)
        {
            LaunchMineTowardPlayer();
            nextAttackTime = Time.time + mineAttackInterval;
        }

        // 3. Verificar si los 4 núcleos ya fueron destruidos
        int activeCores = 0;
        if (weakPointCores != null)
        {
            foreach (var core in weakPointCores)
            {
                if (core != null) activeCores++;
            }
        }

        if (activeCores == 0)
        {
            isDefeated = true;
            GameFlowManager.Instance?.OnBossDefeated();
            Destroy(gameObject, 0.3f);
        }
    }

    private void LaunchMineTowardPlayer()
    {
        if (minePrefab == null || playerShip == null) return;

        Vector3 spawnPos = transform.position + Vector3.back * 4f;
        GameObject mine = Instantiate(minePrefab, spawnPos, Quaternion.identity);

        // Impulsar la mina en dirección aproximada al carril actual de la nave
        Vector3 targetPoint = playerShip.position + new Vector3(Random.Range(-1.5f, 1.5f), 0.5f, 10f);
        Vector3 dir = (targetPoint - spawnPos).normalized;

        Rigidbody rb = mine.GetComponent<Rigidbody>();
        if (rb == null) rb = mine.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.linearVelocity = dir * mineProjectileSpeed;

        Destroy(mine, 8f);
    }
}