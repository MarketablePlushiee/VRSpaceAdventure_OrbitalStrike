using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Gestiona el disparo 6DOF, cargador base de 5 balas, fila superior temporal de 5 LEDs
/// para cargador expandido, reembolso por acierto y recarga física en estación.
/// </summary>
public class DiegeticWeapon : MonoBehaviour
{
    [Header("Configuración de Munición")]
    public int baseMagazineSize = 5;
    private int currentMaxAmmo;
    private int currentAmmo;
    private bool isLocked = false;

    [Header("Disparo y Proyectiles")]
    public GameObject bulletPrefab;
    public Transform muzzlePoint;
    public float bulletSpeed = 90f;
    public float fireCooldown = 0.15f;
    private float lastFireTime;

    [Header("Potenciadores Activos")]
    public bool hasTripleShot = false;
    public bool hasPiercingShot = false;
    public bool hasHomingShot = false;

    [Header("Indicadores LED Físicos")]
    [Tooltip("Fila principal de 5 LEDs en el arma.")]
    public Renderer[] ammoLeds;
    [Tooltip("Fila superior de 5 LEDs que aparece solo con Cargador Expandido.")]
    public Renderer[] extraAmmoLeds;
    public Color availableColor = Color.green;
    public Color emptyColor = Color.red;

    [Header("Audio y VR")]
    public AudioSource weaponAudioSource;
    public AudioClip shootClip;
    public AudioClip emptyLockClip;
    public AudioClip rechargeClip;
    private XRGrabInteractable grabInteractable;

    [Header("Prueba en Editor (Sin Visor VR)")]
    public bool enableMouseDebug = true;

    public int RechargesCount { get; private set; } = 0;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        currentMaxAmmo = baseMagazineSize;
        currentAmmo = currentMaxAmmo;
        UpdateAmmoLeds();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.activated.AddListener(OnTriggerPulled);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.activated.RemoveListener(OnTriggerPulled);
    }

    private void Update()
    {
        if (enableMouseDebug)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryFire();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) RechargeWeapon();
        }
    }

    private void OnTriggerPulled(ActivateEventArgs args)
    {
        TryFire();
    }

    public void TryFire()
    {
        if (Time.time - lastFireTime < fireCooldown) return;
        lastFireTime = Time.time;

        if (isLocked || currentAmmo <= 0)
        {
            isLocked = true;
            PlaySound(emptyLockClip, 1f);
            SendHaptic(0.3f, 0.1f);
            return;
        }

        currentAmmo--;

        // Si tenía cargador expandido y ya gastó las 5 balas extra, vuelve a 5 y oculta los LEDs superiores
        if (currentMaxAmmo > baseMagazineSize && currentAmmo <= baseMagazineSize)
        {
            currentMaxAmmo = baseMagazineSize;
        }

        if (currentAmmo <= 0)
        {
            currentAmmo = 0;
            isLocked = true;
        }

        SpawnProjectile(muzzlePoint.rotation);
        if (hasTripleShot)
        {
            SpawnProjectile(muzzlePoint.rotation * Quaternion.Euler(0f, -6f, 0f));
            SpawnProjectile(muzzlePoint.rotation * Quaternion.Euler(0f, 6f, 0f));
        }

        PlaySound(shootClip, Random.Range(0.95f, 1.05f));
        SendHaptic(0.6f, 0.08f);
        UpdateAmmoLeds();
    }

    private void SpawnProjectile(Quaternion rotation)
    {
        if (bulletPrefab == null || muzzlePoint == null) return;

        GameObject proj = Instantiate(bulletPrefab, muzzlePoint.position, rotation);
        LaserProjectile laser = proj.GetComponent<LaserProjectile>();
        if (laser != null)
        {
            laser.isPiercing = hasPiercingShot;
            laser.isHoming = hasHomingShot;
        }

        Rigidbody projRb = proj.GetComponent<Rigidbody>();
        if (projRb != null)
        {
            projRb.linearVelocity = (rotation * Vector3.forward) * bulletSpeed;
        }

        ScoreManager.Instance?.RegisterShotFired();
        Destroy(proj, 3f);
    }

    public void RefundAmmo()
    {
        if (currentAmmo < currentMaxAmmo)
        {
            currentAmmo++;
            isLocked = false;
            UpdateAmmoLeds();
        }
    }

    public void RechargeWeapon()
    {
        currentMaxAmmo = baseMagazineSize;
        currentAmmo = currentMaxAmmo;
        isLocked = false;
        RechargesCount++;

        hasTripleShot = false;
        hasPiercingShot = false;
        hasHomingShot = false;

        PlaySound(rechargeClip, 1f);
        SendHaptic(0.8f, 0.2f);
        UpdateAmmoLeds();
    }

    public void ActivateExpandedMagazine()
    {
        currentMaxAmmo = 10;
        currentAmmo = 10;
        isLocked = false;
        UpdateAmmoLeds();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ChargingStation"))
        {
            RechargeWeapon();
        }
    }

    private void UpdateAmmoLeds()
    {
        bool showExtraRow = currentMaxAmmo > baseMagazineSize;

        if (ammoLeds != null)
        {
            for (int i = 0; i < ammoLeds.Length; i++)
            {
                if (ammoLeds[i] == null) continue;
                bool lit = currentAmmo > i;
                ammoLeds[i].material.color = lit ? availableColor : emptyColor;
            }
        }

        if (extraAmmoLeds != null)
        {
            for (int i = 0; i < extraAmmoLeds.Length; i++)
            {
                if (extraAmmoLeds[i] == null) continue;
                extraAmmoLeds[i].gameObject.SetActive(showExtraRow);
                if (showExtraRow)
                {
                    bool lit = currentAmmo > (baseMagazineSize + i);
                    extraAmmoLeds[i].material.color = lit ? availableColor : emptyColor;
                }
            }
        }
    }

    private void PlaySound(AudioClip clip, float pitch)
    {
        if (weaponAudioSource != null && clip != null)
        {
            weaponAudioSource.pitch = pitch;
            weaponAudioSource.PlayOneShot(clip);
        }
    }

    private void SendHaptic(float amplitude, float duration)
    {
        if (grabInteractable != null && grabInteractable.isSelected &&
            grabInteractable.firstInteractorSelecting is XRBaseInputInteractor inputInteractor)
        {
            inputInteractor.SendHapticImpulse(amplitude, duration);
        }
    }
}