using System.Collections.Generic;
using UnityEngine;

public class TargetObjective : MonoBehaviour
{
    public enum ObjectiveType
    {
        BronzeTarget,
        SilverTarget,
        GoldTarget,
        SpecialPowerUp,
        DestructibleMine,
        Asteroid
    }

    [Header("Tipo y Puntuación")]
    public ObjectiveType objectiveType = ObjectiveType.BronzeTarget;
    public int basePoints = 100;
    public int maxHealth = 1;
    public int hullDamageOnCrash = 20;

    [Header("Comportamiento Efímero (Diana Oro)")]
    public float ephemeralDurationInRange = 0f;
    public float activationDistanceZ = 45f;

    [Header("Movimiento Oscilatorio (Opcional)")]
    public bool oscillate = false;
    public Vector3 moveAxis = Vector3.right;
    public float moveAmplitude = 2.5f;
    public float moveFrequency = 1.5f;

    [Header("Efecto Visual (Opcional)")]
    public GameObject destructionVfxPrefab;

    private int currentHealth;
    private Vector3 initialPosition;
    private float randomPhase;
    private Renderer objRenderer;
    private Transform playerShip;
    private bool ephemeralTimerStarted = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        initialPosition = transform.position;
        randomPhase = Random.Range(0f, Mathf.PI * 2f);
        objRenderer = GetComponentInChildren<Renderer>();

        ShipFlightController ship = FindFirstObjectByType<ShipFlightController>();
        if (ship != null) playerShip = ship.transform;
    }

    private void Update()
    {
        if (ephemeralDurationInRange > 0f && !ephemeralTimerStarted && playerShip != null)
        {
            float distZ = transform.position.z - playerShip.position.z;
            if (distZ <= activationDistanceZ && distZ > 0f)
            {
                ephemeralTimerStarted = true;
                Destroy(gameObject, ephemeralDurationInRange);
            }
        }

        if (oscillate)
        {
            float offset = Mathf.Sin((Time.time * moveFrequency) + randomPhase) * moveAmplitude;
            transform.position = initialPosition + (moveAxis.normalized * offset);
        }

        if (objectiveType == ObjectiveType.DestructibleMine || objectiveType == ObjectiveType.Asteroid)
        {
            transform.Rotate(new Vector3(15f, 30f, 10f) * Time.deltaTime);
        }
    }

    public void TakeDamage(int damage, DiegeticWeapon weapon)
    {
        if (objectiveType == ObjectiveType.Asteroid) return;

        if (weapon == null)
        {
            weapon = FindFirstObjectByType<DiegeticWeapon>();
        }

        currentHealth -= damage;

        if (weapon != null && objectiveType != ObjectiveType.DestructibleMine)
        {
            weapon.RefundAmmo();
        }

        if (currentHealth > 0)
        {
            if (objRenderer != null) objRenderer.material.color = Color.white;
            return;
        }

        DestroyObjective(weapon);
    }

    private void DestroyObjective(DiegeticWeapon weapon)
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(basePoints, true);
        }

        if (objectiveType == ObjectiveType.SpecialPowerUp && weapon != null)
        {
            ApplyUniquePowerUp(weapon);
        }

        if (destructionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(destructionVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        Destroy(gameObject);
    }

    private void ApplyUniquePowerUp(DiegeticWeapon weapon)
    {
        List<int> available = new List<int>();
        if (!weapon.hasTripleShot) available.Add(0);
        if (weapon.extraAmmoLeds == null || weapon.extraAmmoLeds.Length == 0 || !weapon.extraAmmoLeds[0].gameObject.activeSelf)
            available.Add(1);
        if (!weapon.hasPiercingShot) available.Add(2);
        if (!weapon.hasHomingShot) available.Add(3);

        if (available.Count == 0)
        {
            weapon.ActivateExpandedMagazine();
            ScoreManager.Instance?.AddScore(1000, false);
            ScoreManager.Instance?.ShowBanner("¡SOBRECARGA MÁXIMA! +1.000 PTS Y 10 BALAS", Color.yellow);
            return;
        }

        int chosen = available[Random.Range(0, available.Count)];
        switch (chosen)
        {
            case 0:
                weapon.hasTripleShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO TRIPLE!", Color.cyan);
                break;
            case 1:
                weapon.ActivateExpandedMagazine();
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: CARGADOR EXPANDIDO (10 BALAS)!", Color.cyan);
                break;
            case 2:
                weapon.hasPiercingShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO PERFORANTE!", Color.cyan);
                break;
            case 3:
                weapon.hasHomingShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO TELEDERIGIDO!", Color.cyan);
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerShip"))
        {
            if (objectiveType == ObjectiveType.DestructibleMine || objectiveType == ObjectiveType.Asteroid)
            {
                ScoreManager.Instance?.DamageShip(hullDamageOnCrash);
                Destroy(gameObject);
            }
        }
    }
}