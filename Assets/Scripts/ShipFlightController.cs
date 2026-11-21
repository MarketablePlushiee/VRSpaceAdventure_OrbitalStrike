using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class GameSettingsData
{
    public static string difficultyName = "NORMAL";
    public static float selectedForwardSpeed = 15f;
    public static float scoreMultiplier = 1.0f;

    /// <summary>
    /// 0 = Fácil, 1 = Normal, 2 = Difícil, 3 = Imposible
    /// </summary>
    public static int DifficultyTier
    {
        get
        {
            if (selectedForwardSpeed <= 10f) return 0;
            if (selectedForwardSpeed <= 17f) return 1;
            if (selectedForwardSpeed <= 24f) return 2;
            return 3;
        }
    }
}

[RequireComponent(typeof(Rigidbody))]
public class ShipFlightController : MonoBehaviour
{
    [Header("Avance en Riel (Eje Z)")]
    public bool useSelectedDifficultySpeed = true;
    public float forwardSpeed = 15f;
    public bool isBossPhase = false;

    [Header("Maniobra Evasiva (Ejes X / Y)")]
    public float strafeSpeed = 14f;
    public Vector2 movementBoundsX = new Vector2(-12f, 12f);
    public Vector2 movementBoundsY = new Vector2(-6f, 10f);

    [Header("Referencias del Joystick VR (Mano Izquierda)")]
    public XRBaseInteractable joystickInteractable;
    public Transform joystickPivot;
    public float maxTiltAngle = 28f;
    public float handSensitivityRadius = 0.08f;
    public float deadzone = 0.012f;

    [Header("Pilares / Arcos de Referencia en el Camino")]
    public bool spawnTunnelMarkers = true;
    [Tooltip("Opcional: arrastra aquí el Prefab 3D del pilar o arco orbital. Si está vacío, usa cubos de prototipo.")]
    public GameObject tunnelPillarPrefab;
    [Tooltip("Si es true, crea un pilar a la izquierda y otro a la derecha. Desmárcalo si el prefab es un arco completo centrado.")]
    public bool mirrorPillarBothSides = true;
    public Vector3 fallbackPillarScale = new Vector3(1.2f, 18f, 1.2f);
    public float markerSpacingZ = 25f;
    public float markerLookAheadZ = 120f;
    public bool enableKeyboardDebug = true;

    private Rigidbody rb;
    private Vector2 targetInput;
    private Vector2 smoothedInput;
    private Vector3 startPosition;
    private Vector3 grabOriginLocal;
    private bool wasGrabbed = false;
    private float nextMarkerZ = 20f;
    private Quaternion initialPivotLocalRotation = Quaternion.identity;

    public float CurrentForwardSpeed => isBossPhase ? forwardSpeed * 0.5f : forwardSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startPosition = transform.position;
    }

    private void Start()
    {
        if (useSelectedDifficultySpeed)
        {
            forwardSpeed = GameSettingsData.selectedForwardSpeed;
        }

        if (joystickInteractable != null)
        {
            if (joystickPivot == null || !joystickInteractable.transform.IsChildOf(joystickPivot))
            {
                joystickPivot = joystickInteractable.transform.parent != null && joystickInteractable.transform.parent != transform
                    ? joystickInteractable.transform.parent
                    : joystickInteractable.transform;
            }
        }

        if (joystickPivot != null)
        {
            initialPivotLocalRotation = joystickPivot.localRotation;
        }
    }

    private void Update()
    {
        ReadFlightInput();
        UpdateJoystickVisual();
        UpdateTunnelMarkers();
    }

    private void FixedUpdate()
    {
        smoothedInput = Vector2.Lerp(smoothedInput, targetInput, Time.fixedDeltaTime * 10f);

        Vector3 displacement = new Vector3(
            smoothedInput.x * strafeSpeed * Time.fixedDeltaTime,
            smoothedInput.y * strafeSpeed * Time.fixedDeltaTime,
            CurrentForwardSpeed * Time.fixedDeltaTime
        );

        Vector3 nextPos = rb.position + displacement;
        nextPos.x = Mathf.Clamp(nextPos.x, startPosition.x + movementBoundsX.x, startPosition.x + movementBoundsX.y);
        nextPos.y = Mathf.Clamp(nextPos.y, startPosition.y + movementBoundsY.x, startPosition.y + movementBoundsY.y);

        rb.MovePosition(nextPos);
    }

    private void ReadFlightInput()
    {
        targetInput = Vector2.zero;

        if (joystickInteractable != null && joystickInteractable.isSelected && joystickInteractable.firstInteractorSelecting != null)
        {
            Transform handTransform = joystickInteractable.firstInteractorSelecting.transform;
            Vector3 currentHandLocal = transform.InverseTransformPoint(handTransform.position);

            if (!wasGrabbed)
            {
                grabOriginLocal = currentHandLocal;
                wasGrabbed = true;
            }

            Vector3 handDelta = currentHandLocal - grabOriginLocal;

            float rawX = Mathf.Abs(handDelta.x) < deadzone ? 0f : handDelta.x;
            targetInput.x = Mathf.Clamp(rawX / handSensitivityRadius, -1f, 1f);

            float rawZ = Mathf.Abs(handDelta.z) < deadzone ? 0f : handDelta.z;
            targetInput.y = Mathf.Clamp(rawZ / handSensitivityRadius, -1f, 1f);

            if (joystickInteractable.firstInteractorSelecting is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(0.08f, Time.deltaTime);
            }
        }
        else
        {
            wasGrabbed = false;

            if (enableKeyboardDebug && Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) targetInput.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) targetInput.x += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) targetInput.y -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) targetInput.y += 1f;
            }
        }
    }

    private void UpdateJoystickVisual()
    {
        if (joystickPivot == null) return;

        Quaternion tiltOffset = Quaternion.Euler(smoothedInput.y * maxTiltAngle, 0f, -smoothedInput.x * maxTiltAngle);
        joystickPivot.localRotation = Quaternion.Slerp(joystickPivot.localRotation, initialPivotLocalRotation * tiltOffset, Time.deltaTime * 15f);
    }

    private void UpdateTunnelMarkers()
    {
        if (!spawnTunnelMarkers) return;

        float activeSpeed = Mathf.Max(CurrentForwardSpeed, 2f);
        float dynamicLifetime = (markerLookAheadZ + 30f) / activeSpeed;

        while (transform.position.z + markerLookAheadZ > nextMarkerZ)
        {
            if (mirrorPillarBothSides)
            {
                SpawnPillarInstance(new Vector3(startPosition.x - 14f, startPosition.y + 2f, nextMarkerZ), dynamicLifetime);
                SpawnPillarInstance(new Vector3(startPosition.x + 14f, startPosition.y + 2f, nextMarkerZ), dynamicLifetime);
            }
            else
            {
                SpawnPillarInstance(new Vector3(startPosition.x, startPosition.y + 2f, nextMarkerZ), dynamicLifetime);
            }

            nextMarkerZ += markerSpacingZ;
        }
    }

    private void SpawnPillarInstance(Vector3 pos, float lifetime)
    {
        GameObject pillar;
        if (tunnelPillarPrefab != null)
        {
            pillar = Instantiate(tunnelPillarPrefab, pos, tunnelPillarPrefab.transform.rotation);
            foreach (Collider col in pillar.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }
        }
        else
        {
            pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(pillar.GetComponent<Collider>());
            pillar.transform.position = pos;
            pillar.transform.localScale = fallbackPillarScale;
        }

        Destroy(pillar, lifetime);
    }
}