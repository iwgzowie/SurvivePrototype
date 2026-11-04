using System.Collections.Generic;
using Survive.Character;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Survive.UI
{
    public sealed class SyntyResultsUI : MonoBehaviour
    {
        [SerializeField] private MenuBehaviour menu;
        [SerializeField] private LeaderboardManager leaderboard;
        [SerializeField] private GameObject defeatPanel, leaderboardPanel, mainOpenButton;
        [SerializeField] private TMP_Text scoreText, killsText, waveText, timeText, accuracyText, saveStatus;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button saveButton, leaderboardBack;
        [SerializeField] private Transform rows;
        [SerializeField] private ScoreRowUI rowTemplate;
        readonly List<GameObject> visibleRows = new List<GameObject>();
        bool shown, submitted;
        int pendingScore;
        public bool IsLeaderboardOpen => leaderboardPanel != null && leaderboardPanel.activeSelf;
        public int PendingScore => pendingScore;
        public bool ScoreSubmitted => submitted;
        void Update()
        {
            if (mainOpenButton != null) mainOpenButton.SetActive(menu != null && menu.IsMainMenu && !menu.IsCreatingCharacter && !IsLeaderboardOpen);
        }
        public void ShowDefeat()
        {
            if (shown) return;
            shown = true; submitted = false;
            var stats = GameStatsManager.GetOrCreate(); stats.FinalizeGameStats(); pendingScore = stats.totalScore;
            scoreText.text = pendingScore.ToString("N0"); killsText.text = stats.enemiesKilled.ToString("N0");
            waveText.text = stats.waveReached.ToString(); int time = Mathf.FloorToInt(stats.survivalTime);
            timeText.text = $"{time / 60:00}:{time % 60:00}"; accuracyText.text = $"{stats.Accuracy:0.#}%";
            nameInput.text = CharacterProfileStore.TryLoad(out var profile) ? profile.displayName : "SUPERVIVIENTE";
            saveButton.interactable = leaderboard.IsHighScore(pendingScore);
            saveStatus.text = saveButton.interactable ? "Guardá tu marca en la clasificación local." : "Tu puntaje no entra entre las mejores marcas.";
            leaderboardPanel.SetActive(false); defeatPanel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(saveButton.interactable ? saveButton.gameObject : null);
        }
        public void SaveScore()
        {
            if (!shown || submitted || menu == null || !menu.IsGameOver || !leaderboard.IsHighScore(pendingScore)) return;
            leaderboard.AddScore(nameInput.text, pendingScore); submitted = true; saveButton.interactable = false;
            saveStatus.text = "PUNTAJE GUARDADO";
            OpenLeaderboard();
        }
        public void OpenLeaderboard()
        {
            if (menu == null || menu.IsCreatingCharacter) return;
            if (!menu.IsMainMenu && !menu.IsFinished && !menu.IsPaused) menu.PauseGame();
            if (menu.container != null) menu.container.SetActive(false);
            defeatPanel.SetActive(false); leaderboardPanel.SetActive(true);
            foreach (var row in visibleRows) if (row != null) { row.SetActive(false); Destroy(row); }
            visibleRows.Clear(); var entries = leaderboard.GetScores();
            for (int i = 0; i < entries.Count; i++)
            {
                var row = Instantiate(rowTemplate, rows); row.name = "ScoreRow_" + (i + 1); row.gameObject.SetActive(true);
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0, 218 - i * 52);
                row.Setup(i + 1, entries[i].playerName, entries[i].score); visibleRows.Add(row.gameObject);
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            EventSystem.current?.SetSelectedGameObject(leaderboardBack.gameObject);
        }
        public void CloseLeaderboard()
        {
            if (!IsLeaderboardOpen) return;
            leaderboardPanel.SetActive(false);
            if (menu.IsGameOver) defeatPanel.SetActive(true);
            else if (menu.IsPaused && menu.container != null) menu.container.SetActive(true);
        }
    }
}
