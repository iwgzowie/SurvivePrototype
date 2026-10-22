using UnityEngine;
using TMPro;

public class GameScoreHUD : MonoBehaviour
{
    [Header("Textos en Pantalla (HUD)")]
    [SerializeField] private TMP_Text liveScoreText;
    [SerializeField] private TMP_Text liveWaveText;

    private void Update()
    {
        if (GameStatsManager.Instance != null)
        {
            // Actualiza el puntaje actual en vivo
            if (liveScoreText != null)
            {
                liveScoreText.text = "Puntaje: " + GameStatsManager.Instance.totalScore.ToString("N0");
            }

            // Actualiza la oleada actual en vivo
            if (liveWaveText != null)
            {
                liveWaveText.text = "Oleada: " + GameStatsManager.Instance.waveReached;
            }
        }
    }
}