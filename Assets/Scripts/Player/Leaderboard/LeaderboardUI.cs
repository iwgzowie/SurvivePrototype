using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LeaderboardUI : MonoBehaviour
{
    [Header("Gestor de Datos")]
    [SerializeField] private LeaderboardManager leaderboardManager;

    [Header("Paneles del Canvas")]
    [SerializeField] private GameObject inputPanel;
    [SerializeField] private GameObject leaderboardPanel;

    [Header("Referencias de Input")]
    [SerializeField] private TMP_InputField nameInputField; 

    [Header("Generación de Tabla")]
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private ScoreRowUI rowPrefab;

    private int pendingScore;

    public void EvaluateGameOverScore(int finalScore)
    {
        pendingScore = finalScore;

        if (leaderboardManager.IsHighScore(finalScore))
        {
            inputPanel.SetActive(true);
            leaderboardPanel.SetActive(false);

            nameInputField.text = string.Empty;
            nameInputField.Select();
        }
        else
        {
            inputPanel.SetActive(false);
            ShowLeaderboard();
        }
    }

    public void OnSubmitScoreButton()
    {
        string playerName = string.IsNullOrWhiteSpace(nameInputField.text)
            ? "AAA"
            : nameInputField.text.Trim().ToUpper();

        leaderboardManager.AddScore(playerName, pendingScore);

        inputPanel.SetActive(false);
        ShowLeaderboard();
    }

    public void ShowLeaderboard()
    {
        leaderboardPanel.SetActive(true);

        foreach (Transform child in rowsContainer)
        {
            Destroy(child.gameObject);
        }

        List<ScoreEntry> scores = leaderboardManager.GetScores();

        for (int i = 0; i < scores.Count; i++)
        {
            ScoreRowUI newRow = Instantiate(rowPrefab, rowsContainer);
            newRow.Setup(i + 1, scores[i].playerName, scores[i].score);
        }
    }
}