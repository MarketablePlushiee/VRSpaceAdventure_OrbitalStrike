using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using TMPro;

public class VRConsoleButton : MonoBehaviour
{
    public enum ButtonAction
    {
        LoadScene,
        QuitApplication,
        SetDifficulty
    }

    [Header("Acción del Botón")]
    public ButtonAction action = ButtonAction.LoadScene;
    public string targetSceneName = "VRSpaceAdventure";

    [Header("Alcance y Respuesta Táctil")]
    public float interactionPadding = 0.05f;
    public float maxPointerReach = 30f;
    public float pressSinkDepth = 0.025f;

    [Header("Configuración de Dificultad (Solo si Action = SetDifficulty)")]
    public string difficultyName = "NORMAL";
    public float difficultySpeed = 15f;
    public float difficultyScoreMultiplier = 1.0f;
    public TMP_Text difficultyFeedbackText;

    private XRBaseInteractable interactable;
    private Collider buttonCollider;
    private float lastTriggerTime = -2f;
    private bool wasTriggerPressed = false;
    private Vector3 initialLocalPos;
    private Vector3 targetLocalPos;
    private static bool pointerRangeBoosted = false;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        buttonCollider = GetComponent<Collider>();
        initialLocalPos = transform.localPosition;
        targetLocalPos = initialLocalPos;
        pointerRangeBoosted = false;

        BoxCollider box = buttonCollider as BoxCollider;
        if (box != null)
        {
            box.size += Vector3.one * interactionPadding;
        }
        else if (buttonCollider is SphereCollider sphere)
        {
            sphere.radius += interactionPadding * 0.5f;
        }
    }

    private void Start()
    {
        BoostSceneInteractorRanges();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelected);
            interactable.activated.AddListener(OnActivated);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.activated.RemoveListener(OnActivated);
        }
    }

    private void Update()
    {
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, Time.deltaTime * 16f);
        if (Vector3.Distance(transform.localPosition, targetLocalPos) < 0.002f)
        {
            targetLocalPos = initialLocalPos;
        }

        bool triggerJustPressed = CheckAnyTriggerJustPressed();
        if (triggerJustPressed && IsPointedOrHovered())
        {
            ExecuteAction();
        }
    }

    private bool IsPointedOrHovered()
    {
        if (interactable != null && interactable.isHovered) return true;
        if (buttonCollider == null) return false;

        // Asistencia de rayo amplio desde los interactores de las manos VR (alcance 30 m)
        XRBaseInteractor[] interactors = FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None);
        foreach (var interactor in interactors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled) continue;
            Ray ray = new Ray(interactor.transform.position, interactor.transform.forward);
            if (Physics.SphereCast(ray, 0.06f, out RaycastHit hit, maxPointerReach, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.collider == buttonCollider || hit.collider.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void BoostSceneInteractorRanges()
    {
        if (pointerRangeBoosted) return;
        pointerRangeBoosted = true;

        // Amplía automáticamente el alcance del rayo en NearFarInteractor / XRRayInteractor (Unity 6 XRI)
        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in allBehaviours)
        {
            if (mb == null) continue;
            var type = mb.GetType();
            PropertyInfo maxDistProp = type.GetProperty("maxRaycastDistance") ?? type.GetProperty("castDistance");
            if (maxDistProp != null && maxDistProp.CanWrite && maxDistProp.PropertyType == typeof(float))
            {
                float current = (float)maxDistProp.GetValue(mb);
                if (current < maxPointerReach)
                {
                    maxDistProp.SetValue(mb, maxPointerReach);
                }
            }
        }
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        ExecuteAction();
    }

    private void OnActivated(ActivateEventArgs args)
    {
        ExecuteAction();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerBullet") ||
            other.GetComponentInParent<XRDirectInteractor>() != null ||
            other.GetComponentInParent<XRPokeInteractor>() != null)
        {
            ExecuteAction();
        }
    }

    private bool CheckAnyTriggerJustPressed()
    {
        bool isDownNow = false;

        var xrDevices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, xrDevices);
        foreach (var dev in xrDevices)
        {
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float val) && val > 0.55f)
            {
                isDownNow = true;
                break;
            }
        }

        if (!isDownNow)
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is UnityEngine.InputSystem.XR.XRController)
                {
                    AxisControl trigger = device.TryGetChildControl<AxisControl>("trigger");
                    if (trigger != null && trigger.ReadValue() > 0.55f)
                    {
                        isDownNow = true;
                        break;
                    }
                }
            }
        }

        bool justPressed = isDownNow && !wasTriggerPressed;
        wasTriggerPressed = isDownNow;
        return justPressed;
    }

    public void ExecuteAction()
    {
        if (Time.time - lastTriggerTime < 0.35f) return;
        lastTriggerTime = Time.time;

        targetLocalPos = initialLocalPos - (transform.parent != null
            ? transform.parent.InverseTransformDirection(transform.forward) * pressSinkDepth
            : Vector3.forward * pressSinkDepth);

        if (action == ButtonAction.SetDifficulty)
        {
            GameSettingsData.difficultyName = difficultyName;
            GameSettingsData.selectedForwardSpeed = difficultySpeed;
            GameSettingsData.scoreMultiplier = difficultyScoreMultiplier;

            if (difficultyFeedbackText != null)
            {
                difficultyFeedbackText.text = $"DIFICULTAD: {difficultyName} (x{difficultyScoreMultiplier:0.##} PTS)";
            }
            return;
        }

        if (action == ButtonAction.LoadScene && !string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else if (action == ButtonAction.QuitApplication)
        {
            Application.Quit();
        }
    }
}