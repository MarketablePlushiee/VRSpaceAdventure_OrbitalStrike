using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class GameSettingsData
{
    public static string difficultyName = "NORMAL";
    public static float selectedForwardSpeed = 15f;
    public static float scoreMultiplier = 1.0f;

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
    public float strafeSpeed = 18f;
    public Vector2 movementBoundsX = new Vector2(-20f, 20f);
    public Vector2 movementBoundsY = new Vector2(-10f, 15f);

    [Header("Referencias del Joystick VR (Mano Izquierda)")]
    public XRBaseInteractable joystickInteractable;
    public Transform joystickPivot;
    public float maxTiltAngle = 30f;
    public float handSensitivityRadius = 0.1f;
    public float deadzone = 0.015f;

    [Header("Pilares / Arcos de Referencia en el Camino")]
    public bool spawnTunnelMarkers = true;
    public GameObject tunnelPillarPrefab;
    public bool mirrorPillarBothSides = false;
    public float pillarSideOffsetX = 22f;
    public Vector3 tunnelMarkerOffset = new Vector3(0f, 2f, 0f);
    public Vector3 fallbackPillarScale = new Vector3(1.2f, 18f, 1.2f);
    public float markerSpacingZ = 55f;
    public float markerLookAheadZ = 120f;
    public float firstMarkerZ = 35f;

    [Header("Sombras de los Anillos / Arcos")]
    public bool tunnelMarkersCastShadows = false;
    public bool tunnelMarkersReceiveShadows = false;

    [Header("Depuración en Editor")]
    public bool enableKeyboardDebug = true;

    private Rigidbody rb;
    private Vector2 targetInput;
    private Vector2 smoothedInput;
    private Vector3 startPosition;
    private Vector3 grabOriginLocal;
    private bool wasGrabbed = false;
    private float nextMarkerZ;
    private Quaternion initialPivotLocalRotation = Quaternion.identity;
    private readonly List<GameObject> activeTunnelMarkers = new List<GameObject>();

    public float CurrentForwardSpeed => isBossPhase ? forwardSpeed * 0.5f : forwardSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startPosition = transform.position;
        nextMarkerZ = startPosition.z + firstMarkerZ;
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
        float minX = Mathf.Min(movementBoundsX.x, movementBoundsX.y);
        float maxX = Mathf.Max(movementBoundsX.x, movementBoundsX.y);
        float minY = Mathf.Min(movementBoundsY.x, movementBoundsY.y);
        float maxY = Mathf.Max(movementBoundsY.x, movementBoundsY.y);

        nextPos.x = Mathf.Clamp(nextPos.x, startPosition.x + minX, startPosition.x + maxX);
        nextPos.y = Mathf.Clamp(nextPos.y, startPosition.y + minY, startPosition.y + maxY);

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

        float safeSpacing = Mathf.Max(5f, markerSpacingZ);
        float activeSpeed = Mathf.Max(CurrentForwardSpeed, 2f);
        float dynamicLifetime = (markerLookAheadZ + 40f) / activeSpeed;

        while (transform.position.z + markerLookAheadZ > nextMarkerZ)
        {
            Vector3 basePos = new Vector3(
                startPosition.x + tunnelMarkerOffset.x,
                startPosition.y + tunnelMarkerOffset.y,
                nextMarkerZ + tunnelMarkerOffset.z
            );

            if (mirrorPillarBothSides)
            {
                SpawnPillarInstance(basePos + Vector3.left * pillarSideOffsetX, dynamicLifetime);
                SpawnPillarInstance(basePos + Vector3.right * pillarSideOffsetX, dynamicLifetime);
            }
            else
            {
                SpawnPillarInstance(basePos, dynamicLifetime);
            }

            nextMarkerZ += safeSpacing;
        }
    }

    private void SpawnPillarInstance(Vector3 pos, float lifetime)
    {
        GameObject pillar;
        if (tunnelPillarPrefab != null)
        {
            pillar = Instantiate(tunnelPillarPrefab, pos, tunnelPillarPrefab.transform.rotation);
            foreach (Collider col in pillar.GetComponentsInChildren<Collider>(true))
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

        foreach (Renderer rend in pillar.GetComponentsInChildren<Renderer>(true))
        {
            rend.shadowCastingMode = tunnelMarkersCastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            rend.receiveShadows = tunnelMarkersReceiveShadows;
        }

        activeTunnelMarkers.RemoveAll(item => item == null);
        activeTunnelMarkers.Add(pillar);
        Destroy(pillar, lifetime);
    }

    public void ClearAllTunnelMarkers()
    {
        for (int i = 0; i < activeTunnelMarkers.Count; i++)
        {
            if (activeTunnelMarkers[i] != null)
            {
                Destroy(activeTunnelMarkers[i]);
            }
        }
        activeTunnelMarkers.Clear();
    }
}