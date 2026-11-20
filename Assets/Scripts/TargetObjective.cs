using UnityEngine;

/// <summary>
/// Define el comportamiento de las dianas de puntuación, dianas potenciadoras,
/// minas destructibles y asteroides dentro del corredor orbital.
/// </summary>
public class TargetObjective : MonoBehaviour
{
    public enum ObjectiveType
    {
        BronzeTarget,       // 100 pts, 1 impacto, reembolsa bala
        SilverTarget,       // 250 pts, 1 impacto, movimiento suave, reembolsa bala
        GoldTarget,         // 500 pts, 2 impactos, movimiento rápido, reembolsa bala
        SpecialPowerUp,     // 300 pts, 1 impacto, otorga mejora temporal al arma
        DestructibleMine,   // 150 pts al destruirla; daña la nave si choca
        Asteroid            // Obstáculo sólido; daña la nave si choca
    }

    [Header("Tipo y Puntuación")]
    public ObjectiveType objectiveType = ObjectiveType.BronzeTarget;
    public int basePoints = 100;
    public int maxHealth = 1;
    public int hullDamageOnCrash = 20;

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

    private void Awake()
    {
        currentHealth = maxHealth;
        initialPosition = transform.position;
        randomPhase = Random.Range(0f, Mathf.PI * 2f);
        objRenderer = GetComponentInChildren<Renderer>();
    }

    private void Update()
    {
        // Oscilación sinusoidal para dianas Plata y Oro
        if (oscillate)
        {
            float offset = Mathf.Sin((Time.time * moveFrequency) + randomPhase) * moveAmplitude;
            transform.position = initialPosition + (moveAxis.normalized * offset);
        }

        // Rotación lenta decorativa para minas y asteroides
        if (objectiveType == ObjectiveType.DestructibleMine || objectiveType == ObjectiveType.Asteroid)
        {
            transform.Rotate(new Vector3(15f, 30f, 10f) * Time.deltaTime);
        }
    }

    public void TakeDamage(int damage, DiegeticWeapon weapon)
    {
        // Los asteroides no se destruyen con disparos estándar
        if (objectiveType == ObjectiveType.Asteroid) return;

        currentHealth -= damage;

        // Reembolsar +1 bala al jugador por cada impacto válido en diana o potenciador
        if (weapon != null && objectiveType != ObjectiveType.DestructibleMine)
        {
            weapon.RefundAmmo();
        }

        // Si es diana Oro y aún le queda 1 punto de vida, cambia de tono para dar feedback visual
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

        // Si es una diana especial, activa uno de los 4 potenciadores en el arma
        if (objectiveType == ObjectiveType.SpecialPowerUp && weapon != null)
        {
            ApplyRandomPowerUp(weapon);
        }

        if (destructionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(destructionVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        Destroy(gameObject);
    }

    private void ApplyRandomPowerUp(DiegeticWeapon weapon)
    {
        int roll = Random.Range(0, 4);
        switch (roll)
        {
            case 0:
                weapon.hasTripleShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO TRIPLE!");
                break;
            case 1:
                weapon.ActivateExpandedMagazine();
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: CARGADOR EXPANDIDO (10 BALAS)!");
                break;
            case 2:
                weapon.hasPiercingShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO PERFORANTE!");
                break;
            case 3:
                weapon.hasHomingShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO TELEDERIGIDO!");
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Detecta choque directo contra el casco de la nave del jugador
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