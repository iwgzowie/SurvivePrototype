using UnityEngine;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("Textos de Estadísticas (TMP)")]
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Conexión con Leaderboard")]
    [SerializeField] private LeaderboardManager leaderboardManager;
    [SerializeField] private LeaderboardUI leaderboardUI;

    
    public void ShowGameOverScreen()
    {
        int finalScore = 0;

        if (GameStatsManager.Instance != null)
        {
            // Calculamos el puntaje total y bonificaciones
            GameStatsManager.Instance.FinalizeGameStats();

            int kills = GameStatsManager.Instance.enemiesKilled;
            int wave = GameStatsManager.Instance.waveReached;
            float time = GameStatsManager.Instance.survivalTime;
            finalScore = GameStatsManager.Instance.totalScore;

            int fired = GameStatsManager.Instance.shotsFired;
            int hit = GameStatsManager.Instance.shotsHit;
            float accuracy = fired > 0 ? ((float)hit / fired) * 100f : 0f;

            int minutes = Mathf.FloorToInt(time / 60);
            int seconds = Mathf.FloorToInt(time % 60);

            // Actualizamos los textos del panel
            if (enemiesText != null) enemiesText.text = "Enemigos eliminados: " + kills;
            if (waveText != null) waveText.text = "Oleada alcanzada: " + wave;
            if (timeText != null) timeText.text = $"Tiempo sobrevivido: {minutes:00}:{seconds:00}";
            if (accuracyText != null) accuracyText.text = $"Precisión: {accuracy:F1}%";
            if (scoreText != null) scoreText.text = "Puntaje Total: " + finalScore.ToString("N0");
        }

        // Mostramos el GameOverPanel con tus estadísticas y botones
        gameObject.SetActive(true);

        //  Si además entró en el Top 10, abrimos encima el panel para poner su nombre
        if (leaderboardManager != null && leaderboardUI != null)
        {
            if (leaderboardManager.IsHighScore(finalScore))
            {
                leaderboardUI.EvaluateGameOverScore(finalScore);
            }
        }
    }
}