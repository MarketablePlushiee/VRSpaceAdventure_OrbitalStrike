using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Controla el avance constante sobre el riel (eje Z) y la esquiva lateral/vertical (ejes X/Y)
/// mediante movimiento kinemático físico (rb.MovePosition) para evitar bloqueos con objetos hijos.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShipFlightController : MonoBehaviour
{
    [Header("Avance en Riel (Eje Z)")]
    public float forwardSpeed = 30f;
    public bool isBossPhase = false;

    [Header("Maniobra Evasiva (Ejes X / Y)")]
    public float strafeSpeed = 14f;
    public Vector2 movementBoundsX = new Vector2(-12f, 12f);
    public Vector2 movementBoundsY = new Vector2(-6f, 10f);

    [Header("Referencias del Joystick VR (Mano Izquierda)")]
    public XRBaseInteractable joystickInteractable;
    public Transform joystickPivot;
    public float maxTiltAngle = 28f;
    [Tooltip("Distancia en metros desde el agarre para alcanzar el 100% de movimiento.")]
    public float handSensitivityRadius = 0.08f;
    public float deadzone = 0.012f;

    [Header("Referencias Visuales de Velocidad (Prototipo)")]
    public bool spawnTunnelMarkers = true;
    public bool enableKeyboardDebug = true;

    private Rigidbody rb;
    private Vector2 targetInput;   // X = Izquierda/Derecha, Y = Subir/Bajar
    private Vector2 smoothedInput;
    private Vector3 startPosition;
    private Vector3 grabOriginLocal;
    private bool wasGrabbed = false;
    private float nextMarkerZ = 40f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startPosition = transform.position;
    }

    // Update lee únicamente los inputs del mando VR y actualiza la rotación visual del joystick
    private void Update()
    {
        ReadFlightInput();
        UpdateJoystickVisual();
        UpdateTunnelMarkers();
    }

    // FixedUpdate desplaza la nave sobre el riel de forma determinista y sin bloqueos físicos
    private void FixedUpdate()
    {
        smoothedInput = Vector2.Lerp(smoothedInput, targetInput, Time.fixedDeltaTime * 10f);

        float currentZSpeed = isBossPhase ? forwardSpeed * 0.5f : forwardSpeed;

        Vector3 displacement = new Vector3(
            smoothedInput.x * strafeSpeed * Time.fixedDeltaTime,
            smoothedInput.y * strafeSpeed * Time.fixedDeltaTime,
            currentZSpeed * Time.fixedDeltaTime
        );

        Vector3 nextPos = rb.position + displacement;
        nextPos.x = Mathf.Clamp(nextPos.x, startPosition.x + movementBoundsX.x, startPosition.x + movementBoundsX.y);
        nextPos.y = Mathf.Clamp(nextPos.y, startPosition.y + movementBoundsY.x, startPosition.y + movementBoundsY.y);

        rb.MovePosition(nextPos);
    }

    private void ReadFlightInput()
    {
        targetInput = Vector2.zero;

        // 1. Lectura relativa al punto exacto donde la mano izquierda agarra el joystick
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

            // Izquierda / Derecha (eje X)
            float rawX = Mathf.Abs(handDelta.x) < deadzone ? 0f : handDelta.x;
            targetInput.x = Mathf.Clamp(rawX / handSensitivityRadius, -1f, 1f);

            // Empujar adelante (+Z) = Subir (+Y) | Tirar hacia atrás (-Z) = Bajar (-Y)
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

            // 2. Respaldo con teclado (Unity 6 Input System)
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

        while (transform.position.z + 240f > nextMarkerZ)
        {
            CreatePillar(new Vector3(startPosition.x - 15f, startPosition.y + 2f, nextMarkerZ));
            CreatePillar(new Vector3(startPosition.x + 15f, startPosition.y + 2f, nextMarkerZ));
            nextMarkerZ += 40f;
        }
    }

    private void CreatePillar(Vector3 pos)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(pillar.GetComponent<Collider>());
        pillar.transform.position = pos;
        pillar.transform.localScale = new Vector3(1.2f, 18f, 1.2f);
        Destroy(pillar, 12f);
    }
}