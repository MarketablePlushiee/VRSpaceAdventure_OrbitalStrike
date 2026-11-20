using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Guarda la dificultad, velocidad y multiplicador de puntos elegidos en el Hangar.
/// </summary>
public static class GameSettingsData
{
    public static string difficultyName = "NORMAL";
    public static float selectedForwardSpeed = 15f;
    public static float scoreMultiplier = 1.0f;
}

/// <summary>
/// Controla el avance constante sobre el riel (eje Z) y la esquiva lateral/vertical (ejes X/Y)
/// mediante movimiento kinemático físico (rb.MovePosition).
/// </summary>
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

    [Header("Referencias Visuales de Velocidad (Prototipo)")]
    public bool spawnTunnelMarkers = true;
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

        Quaternion targetRot = Quaternion.Euler(smoothedInput.y * maxTiltAngle, 0f, -smoothedInput.x * maxTiltAngle);
        joystickPivot.localRotation = Quaternion.Slerp(joystickPivot.localRotation, targetRot, Time.deltaTime * 15f);
    }

    private void UpdateTunnelMarkers()
    {
        if (!spawnTunnelMarkers) return;

        float activeSpeed = Mathf.Max(CurrentForwardSpeed, 2f);
        float dynamicLifetime = (markerLookAheadZ + 30f) / activeSpeed;

        while (transform.position.z + markerLookAheadZ > nextMarkerZ)
        {
            CreatePillar(new Vector3(startPosition.x - 14f, startPosition.y + 2f, nextMarkerZ), dynamicLifetime);
            CreatePillar(new Vector3(startPosition.x + 14f, startPosition.y + 2f, nextMarkerZ), dynamicLifetime);
            nextMarkerZ += markerSpacingZ;
        }
    }

    private void CreatePillar(Vector3 pos, float lifetime)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(pillar.GetComponent<Collider>());
        pillar.transform.position = pos;
        pillar.transform.localScale = new Vector3(1.2f, 18f, 1.2f);
        Destroy(pillar, lifetime);
    }
}