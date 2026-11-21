using UnityEngine;

public class BossCore : MonoBehaviour
{
    [Header("Escalado de Vida por Dificultad")]
    public bool scaleHealthByDifficulty = true;
    public int easyCoreHealth = 150;       // 3 disparos (600 HP total Jefe)
    public int normalCoreHealth = 250;     // 5 disparos (1.000 HP total Jefe - GDD)
    public int hardCoreHealth = 350;       // 7 disparos (1.400 HP total Jefe)
    public int impossibleCoreHealth = 450; // 9 disparos (1.800 HP total Jefe)

    [Header("Daño, Puntos y Audio 3D")]
    public int damagePerLaserHit = 50;
    public int bonusPointsOnDestroy = 500;
    public AudioClip coreDestroySfxClip;
    [Range(0f, 1f)]
    public float coreDestroySfxVolume = 1.0f;

    private int maxCoreHealth;
    private int currentCoreHealth;
    private bool isDestroyed = false;
    private BossDreadnought parentBoss;
    private Renderer coreRenderer;
    private Color originalColor = Color.red;

    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        if (scaleHealthByDifficulty)
        {
            int[] healthByTier = { easyCoreHealth, normalCoreHealth, hardCoreHealth, impossibleCoreHealth };
            maxCoreHealth = healthByTier[GameSettingsData.DifficultyTier];
        }
        else
        {
            maxCoreHealth = normalCoreHealth;
        }

        currentCoreHealth = maxCoreHealth;
        parentBoss = GetComponentInParent<BossDreadnought>();
        coreRenderer = GetComponent<Renderer>();
        if (coreRenderer != null)
        {
            originalColor = coreRenderer.material.color;
        }
    }

    public void TakeCoreDamage(int rawDamage, DiegeticWeapon weapon)
    {
        if (isDestroyed) return;

        if (weapon != null)
        {
            weapon.RefundAmmo();
        }

        int dmg = Mathf.Max(rawDamage, damagePerLaserHit);
        currentCoreHealth -= dmg;

        ScoreManager.Instance?.AddScore(100, true);
        FlashCore();

        if (currentCoreHealth <= 0)
        {
            isDestroyed = true;
            ScoreManager.Instance?.AddScore(bonusPointsOnDestroy, true);
            ScoreManager.Instance?.ShowBanner("¡NÚCLEO DE ENERGÍA DESTRUIDO!", Color.yellow, 2.5f);

            if (coreDestroySfxClip != null)
            {
                AudioSource.PlayClipAtPoint(coreDestroySfxClip, transform.position, coreDestroySfxVolume);
            }

            if (parentBoss != null)
            {
                parentBoss.OnCoreDestroyed();
            }

            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerBullet"))
        {
            DiegeticWeapon weapon = FindFirstObjectByType<DiegeticWeapon>();
            TakeCoreDamage(damagePerLaserHit, weapon);
            Destroy(other.gameObject);
        }
    }

    private void FlashCore()
    {
        if (coreRenderer == null) return;
        coreRenderer.material.color = Color.white;
        CancelInvoke(nameof(RestoreColor));
        Invoke(nameof(RestoreColor), 0.08f);
    }

    private void RestoreColor()
    {
        if (coreRenderer != null)
        {
            coreRenderer.material.color = originalColor;
        }
    }
}