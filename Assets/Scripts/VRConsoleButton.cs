using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// Botón físico 3D diegético para menús en VR: se activa exclusivamente cuando
/// el jugador lo apunta/toca y pulsa el Gatillo (Trigger) o el botón Grip.
/// </summary>
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

    [Header("Configuración de Dificultad (Solo si Action = SetDifficulty)")]
    public string difficultyName = "NORMAL";
    public float difficultySpeed = 15f;
    public float difficultyScoreMultiplier = 1.0f;
    public TMP_Text difficultyFeedbackText;

    private XRBaseInteractable interactable;
    private float lastTriggerTime = -2f;
    private bool wasTriggerPressed = false;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelected);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelected);
        }
    }

    private void Update()
    {
        bool triggerJustPressed = CheckXRTriggerJustPressed();

        // Solo activa el botón si el rayo o la mano están apuntando/tocando ESTE botón y se pulsa el Gatillo
        if (interactable != null && interactable.isHovered && triggerJustPressed)
        {
            ExecuteAction();
        }
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        // También permite activarlo con el botón Grip (botón lateral de interacción)
        ExecuteAction();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Mantiene compatibilidad si le disparas con la pistola láser
        if (other.CompareTag("PlayerBullet"))
        {
            ExecuteAction();
        }
    }

    private bool CheckXRTriggerJustPressed()
    {
        bool isDownNow = false;

        foreach (var device in InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.XR.XRController)
            {
                AxisControl trigger = device.TryGetChildControl<AxisControl>("trigger");
                if (trigger != null && trigger.ReadValue() > 0.7f)
                {
                    isDownNow = true;
                    break;
                }
            }
        }

        bool justPressed = isDownNow && !wasTriggerPressed;
        wasTriggerPressed = isDownNow;
        return justPressed;
    }

    public void ExecuteAction()
    {
        if (Time.time - lastTriggerTime < 0.25f) return;
        lastTriggerTime = Time.time;

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