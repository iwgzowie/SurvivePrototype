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
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Conexión con Leaderboard")]
    [SerializeField] private LeaderboardManager leaderboardManager;
    [SerializeField] private LeaderboardUI leaderboardUI;

    private bool gameOverTriggered = false;

    protected virtual bool MonitorPlayerDeath => true;
    protected virtual bool PauseWhenShown => true;
    protected virtual GameObject DisplayPanel => GameOverPanel;

    protected virtual void Update()
    {
        if (!MonitorPlayerDeath) return;
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
            float accuracy = GameStatsManager.Instance.shotsFired > 0
                ? (float)GameStatsManager.Instance.shotsHit / GameStatsManager.Instance.shotsFired * 100f
                : 0f;
            if (accuracyText != null) accuracyText.text = $"Precisión: {accuracy:F1}%";
            if (scoreText != null) scoreText.text = "Puntaje Total: " + finalScore.ToString("N0");
        }

        GameObject panel = DisplayPanel;
        if (panel != null)
        {
            panel.SetActive(true);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
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

        if (PauseWhenShown) Time.timeScale = 0f;
    }
}
