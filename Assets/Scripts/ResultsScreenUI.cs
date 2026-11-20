using UnityEngine;
using TMPro;

/// <summary>
/// Rellena el panel holográfico de la escena de resultados con el Rango (F a SS)
/// y las métricas obtenidas durante la misión.
/// </summary>
public class ResultsScreenUI : MonoBehaviour
{
    public TMP_Text outcomeTitleText;
    public TMP_Text rankLetterText;
    public TMP_Text statsSummaryText;

    private void Start()
    {
        if (outcomeTitleText != null)
            outcomeTitleText.text = MatchResultsData.outcomeTitle;

        if (rankLetterText != null)
            rankLetterText.text = $"RANGO: {MatchResultsData.finalRank}";

        if (statsSummaryText != null)
        {
            statsSummaryText.text =
                $"PUNTAJE FINAL: {MatchResultsData.finalScore:N0}\n" +
                $"DIANAS DESTRUIDAS: {MatchResultsData.targetsDestroyed}\n" +
                $"PRECISIÓN DE TIRO: {MatchResultsData.accuracyPercent:F1}%\n" +
                $"ESCUDO RESTANTE: {MatchResultsData.shieldRemaining}%";
        }
    }
}