using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DiegeticWeapon : MonoBehaviour
{
    public enum PowerUpType
    {
        TripleShot,
        ExpandedMagazine,
        PiercingShot,
        HomingShot
    }

    [Header("Configuración de Munición")]
    public int baseMagazineSize = 5;
    private int currentMaxAmmo;
    private int currentAmmo;
    private bool isLocked = false;

    [Header("Acoplamiento y Recarga Automática en Base")]
    public bool returnToDockWhenReleased = true;
    public bool autoRechargeOnDockReturn = true;
    public float returnSpeed = 8f;
    public float dockSnapDistance = 0.035f;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    [Header("Disparo y Proyectiles")]
    public GameObject bulletPrefab;
    public Transform muzzlePoint;
    public float muzzleForwardOffset = 1.1f;
    public float bulletSpeed = 160f;
    public float fireCooldown = 0.15f;
    private float lastFireTime;
    private float lastRechargeTime = -2f;

    [Header("Asistencia de Apuntado (Aim Assist)")]
    [Tooltip("Activa una corrección suave dentro de un cono estrecho para facilitar acertar a los núcleos del Boss.")]
    public bool enableAimAssist = true;
    [Range(0f, 15f)]
    public float aimAssistConeAngle = 6.5f;
    public float aimAssistRange = 95f;
    [Range(0f, 1f)]
    public float aimAssistStrength = 0.75f;

    [Header("Potenciadores Activos (Solo lectura en ejecución)")]
    public bool hasTripleShot = false;
    public bool hasPiercingShot = false;
    public bool hasHomingShot = false;
    public float powerUpDurationSeconds = 10f;
    private float powerUpExpireTime = 0f;

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

    public bool HasExpandedMagazine => currentMaxAmmo > baseMagazineSize;
    public bool HasAnyPowerUpActive => hasTripleShot || hasPiercingShot || hasHomingShot || HasExpandedMagazine;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        shipFlight = GetComponentInParent<ShipFlightController>();
        if (shipFlight == null) shipFlight = FindFirstObjectByType<ShipFlightController>();

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;

        hasTripleShot = false;
        hasPiercingShot = false;
        hasHomingShot = false;

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

            // Al soltar la pistola y llegar de vuelta a su base, se recarga automáticamente
            if (autoRechargeOnDockReturn && Vector3.Distance(transform.localPosition, initialLocalPosition) <= dockSnapDistance)
            {
                RechargeWeapon();
            }
        }

        if (HasAnyPowerUpActive && Time.time >= powerUpExpireTime)
        {
            ClearAllPowerUps("¡POTENCIADOR AGOTADO (10s)!");
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
            if (HasAnyPowerUpActive)
            {
                ClearAllPowerUps("¡SIN BALAS! POTENCIADOR PERDIDO");
            }
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

        Vector3 rawAimDir = rotation * Vector3.forward;
        Vector3 spawnPos = muzzlePoint.position + (rawAimDir * muzzleForwardOffset);

        Transform assistedTarget = null;
        Vector3 finalAimDir = rawAimDir;

        if (enableAimAssist && aimAssistConeAngle > 0f && aimAssistStrength > 0f)
        {
            assistedTarget = FindAimAssistTarget(spawnPos, rawAimDir);
            if (assistedTarget != null)
            {
                Vector3 dirToTarget = (assistedTarget.position - spawnPos).normalized;
                finalAimDir = Vector3.Slerp(rawAimDir, dirToTarget, aimAssistStrength).normalized;
            }
        }

        Quaternion finalRotation = Quaternion.LookRotation(finalAimDir);
        GameObject proj = Instantiate(bulletPrefab, spawnPos, finalRotation);

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
            laser.softAssistTarget = (!hasHomingShot) ? assistedTarget : null;
        }

        float shipSpeedZ = shipFlight != null ? shipFlight.CurrentForwardSpeed : 0f;
        Vector3 finalVelocity = (finalAimDir * bulletSpeed) + (Vector3.forward * shipSpeedZ);

        Rigidbody projRb = proj.GetComponent<Rigidbody>();
        if (projRb != null)
        {
            projRb.linearVelocity = finalVelocity;
        }

        ScoreManager.Instance?.RegisterShotFired();
        Destroy(proj, 2.5f);
    }

    private Transform FindAimAssistTarget(Vector3 origin, Vector3 forwardDir)
    {
        Transform best = null;
        float bestAngle = aimAssistConeAngle;

        // 1. Prioriza núcleos rojos activos del Dreadnought MK-IV
        foreach (BossCore core in FindObjectsByType<BossCore>(FindObjectsSortMode.None))
        {
            if (core == null || core.IsDestroyed) continue;
            Vector3 toCore = core.transform.position - origin;
            float dist = toCore.magnitude;
            if (dist > aimAssistRange || dist < 0.5f) continue;

            float angle = Vector3.Angle(forwardDir, toCore / dist);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                best = core.transform;
            }
        }

        if (best != null) return best;

        // 2. Dianas y minas destruibles (ignora asteroides indestructibles)
        foreach (TargetObjective target in FindObjectsByType<TargetObjective>(FindObjectsSortMode.None))
        {
            if (target == null || target.objectiveType == TargetObjective.ObjectiveType.Asteroid) continue;
            Vector3 toTarget = target.transform.position - origin;
            float dist = toTarget.magnitude;
            if (dist > aimAssistRange || dist < 0.5f) continue;

            float angle = Vector3.Angle(forwardDir, toTarget / dist);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                best = target.transform;
            }
        }

        return best;
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

        bool needsRecharge = isLocked || currentAmmo < currentMaxAmmo || HasAnyPowerUpActive;
        if (!needsRecharge) return;

        lastRechargeTime = Time.time;
        RechargesCount++;

        ClearAllPowerUps(null);

        currentMaxAmmo = baseMagazineSize;
        currentAmmo = currentMaxAmmo;
        isLocked = false;

        PlaySound(rechargeClip, 1f);
        SendHaptic(0.8f, 0.2f);
        UpdateAmmoLeds();
    }

    public void ActivatePowerUp(PowerUpType type)
    {
        powerUpExpireTime = Time.time + powerUpDurationSeconds;

        switch (type)
        {
            case PowerUpType.TripleShot:
                hasTripleShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO TRIPLE (10s)!", Color.cyan);
                break;
            case PowerUpType.ExpandedMagazine:
                currentMaxAmmo = 10;
                currentAmmo = 10;
                isLocked = false;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: CARGADOR 10 BALAS (10s)!", Color.cyan);
                break;
            case PowerUpType.PiercingShot:
                hasPiercingShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO PERFORANTE (10s)!", Color.cyan);
                break;
            case PowerUpType.HomingShot:
                hasHomingShot = true;
                ScoreManager.Instance?.ShowBanner("¡POTENCIADOR: DISPARO TELEDIRIGIDO (10s)!", Color.cyan);
                break;
        }

        UpdateAmmoLeds();
    }

    public void ClearAllPowerUps(string bannerReason)
    {
        hasTripleShot = false;
        hasPiercingShot = false;
        hasHomingShot = false;

        if (currentMaxAmmo > baseMagazineSize)
        {
            currentMaxAmmo = baseMagazineSize;
            currentAmmo = Mathf.Min(currentAmmo, baseMagazineSize);
        }

        UpdateAmmoLeds();

        if (!string.IsNullOrEmpty(bannerReason))
        {
            ScoreManager.Instance?.ShowBanner(bannerReason, new Color(1f, 0.55f, 0f), 2.5f);
        }
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