using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Gestiona el disparo 6DOF con salida adelantada fuera de la cabina,
/// cargador base de 5 balas, fila superior temporal de 5 LEDs y recarga física.
/// </summary>
public class DiegeticWeapon : MonoBehaviour
{
    [Header("Configuración de Munición")]
    public int baseMagazineSize = 5;
    private int currentMaxAmmo;
    private int currentAmmo;
    private bool isLocked = false;

    [Header("Acoplamiento Automático en Base")]
    public bool returnToDockWhenReleased = true;
    public float returnSpeed = 8f;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    [Header("Disparo y Proyectiles")]
    public GameObject bulletPrefab;
    public Transform muzzlePoint;
    [Tooltip("Distancia adelantada desde el cañón para que la bala nazca fuera de la cabina.")]
    public float muzzleForwardOffset = 1.1f;
    public float bulletSpeed = 160f;
    public float fireCooldown = 0.15f;
    private float lastFireTime;
    private float lastRechargeTime = -2f;

    [Header("Potenciadores Activos")]
    public bool hasTripleShot = false;
    public bool hasPiercingShot = false;
    public bool hasHomingShot = false;

    [Header("Indicadores LED Físicos")]
    public Renderer[] ammoLeds;
    public Renderer[] extraAmmoLeds;
    public Color availableColor = Color.green;
    public Color emptyColor = Color.red;

    [Header("Audio y VR")]
    public AudioSource weaponAudioSource;
    public AudioClip shootClip;
    public AudioClip emptyLockClip;
    public AudioClip rechargeClip;
    private XRGrabInteractable grabInteractable;
    private ShipFlightController shipFlight;

    [Header("Prueba en Editor (Sin Visor VR)")]
    public bool enableMouseDebug = true;

    public int RechargesCount { get; private set; } = 0;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        shipFlight = GetComponentInParent<ShipFlightController>();
        if (shipFlight == null) shipFlight = FindFirstObjectByType<ShipFlightController>();

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;

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
        if (returnToDockWhenReleased && grabInteractable != null && !grabInteractable.isSelected)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, initialLocalPosition, Time.deltaTime * returnSpeed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, initialLocalRotation, Time.deltaTime * returnSpeed);
        }

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
            SpawnProjectile(muzzlePoint.rotation * Quaternion.Euler(0f, -5f, 0f));
            SpawnProjectile(muzzlePoint.rotation * Quaternion.Euler(0f, 5f, 0f));
        }

        PlaySound(shootClip, Random.Range(0.95f, 1.05f));
        SendHaptic(0.6f, 0.08f);
        UpdateAmmoLeds();
    }

    private void SpawnProjectile(Quaternion rotation)
    {
        if (bulletPrefab == null || muzzlePoint == null) return;

        Vector3 aimDir = rotation * Vector3.forward;
        // Nace 1.10 m por delante del cañón para salir despejada de la cabina a cualquier velocidad
        Vector3 spawnPos = muzzlePoint.position + (aimDir * muzzleForwardOffset);

        GameObject proj = Instantiate(bulletPrefab, spawnPos, rotation);

        // Ignorar colisiones físicas entre la bala y cualquier pieza de la nave/cabina
        Collider bulletCol = proj.GetComponent<Collider>();
        if (bulletCol != null)
        {
            bulletCol.isTrigger = true;
            if (shipFlight != null)
            {
                foreach (Collider shipCol in shipFlight.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(bulletCol, shipCol, true);
                }
            }
        }

        LaserProjectile laser = proj.GetComponent<LaserProjectile>();
        if (laser != null)
        {
            laser.ownerWeapon = this;
            laser.ignoredRoot = shipFlight != null ? shipFlight.transform : null;
            laser.isPiercing = hasPiercingShot;
            laser.isHoming = hasHomingShot;
        }

        float shipSpeedZ = shipFlight != null ? shipFlight.CurrentForwardSpeed : 0f;
        Vector3 finalVelocity = (aimDir * bulletSpeed) + (Vector3.forward * shipSpeedZ);

        Rigidbody projRb = proj.GetComponent<Rigidbody>();
        if (projRb != null)
        {
            projRb.linearVelocity = finalVelocity;
        }

        ScoreManager.Instance?.RegisterShotFired();
        Destroy(proj, 2.5f);
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
        if (Time.time - lastRechargeTime < 0.8f) return;

        bool needsRecharge = isLocked || currentAmmo < currentMaxAmmo;
        if (!needsRecharge) return;

        lastRechargeTime = Time.time;
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
        if (other.CompareTag("ChargingStation") && (grabInteractable == null || grabInteractable.isSelected))
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