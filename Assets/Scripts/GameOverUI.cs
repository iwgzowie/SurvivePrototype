using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("Panel Visual de Game Over")]
    [SerializeField] private GameObject GameOverPanel;

    [Header("Textos de Estadísticas (TMP)")]
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Conexión con Leaderboard")]
    [SerializeField] private LeaderboardManager leaderboardManager;
    [SerializeField] private LeaderboardUI leaderboardUI;

    private bool gameOverTriggered = false;

    private void Update()
    {
        if (GameStatsManager.Instance != null)
        {
            if (GameStatsManager.Instance.survivalTime < 0.2f)
            {
                gameOverTriggered = false;
            }
        }

        if (!gameOverTriggered)
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null && playerHealth.IsDead)
            {
                gameOverTriggered = true;
                ShowGameOverScreen();
            }
        }
    }

    public void ShowGameOverScreen()
    {
        int finalScore = 0;

        if (GameStatsManager.Instance != null)
        {
            GameStatsManager.Instance.FinalizeGameStats();

            int kills = GameStatsManager.Instance.enemiesKilled;
            int wave = GameStatsManager.Instance.waveReached;
            float time = GameStatsManager.Instance.survivalTime;
            finalScore = GameStatsManager.Instance.totalScore;

            int minutes = Mathf.FloorToInt(time / 60);
            int seconds = Mathf.FloorToInt(time % 60);

            if (enemiesText != null) enemiesText.text = "Enemigos eliminados: " + kills;
            if (waveText != null) waveText.text = "Oleada alcanzada: " + wave;
            if (timeText != null) timeText.text = $"Tiempo sobrevivido: {minutes:00}:{seconds:00}";
            if (scoreText != null) scoreText.text = "Puntaje Total: " + finalScore.ToString("N0");
        }

        if (GameOverPanel != null)
        {
            GameOverPanel.SetActive(true);

            RectTransform panelRect = GameOverPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
        }

        if (leaderboardManager != null && leaderboardUI != null)
        {
            if (leaderboardManager.IsHighScore(finalScore))
            {
                leaderboardUI.EvaluateGameOverScore(finalScore);
            }
        }

        Canvas.ForceUpdateCanvases();

        Time.timeScale = 0f;
    }
}