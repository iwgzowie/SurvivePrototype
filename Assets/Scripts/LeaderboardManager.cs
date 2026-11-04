using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ScoreEntry
{
    public string playerName;
    public int score;
    public ScoreEntry(string name, int newScore) { playerName = name; score = newScore; }
}
public class LeaderboardManager : MonoBehaviour
{
    [SerializeField, Range(1, 10)] protected int maxEntries = 10;
    [SerializeField] private List<ScoreEntry> entries = new List<ScoreEntry>();
#if UNITY_EDITOR
    public static string StoragePrefixOverride { get; set; }
#endif
    private static string Prefix
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(StoragePrefixOverride)) return StoragePrefixOverride;
#endif
            return "LB_";
        }
    }
    public int MaxEntries => Mathf.Clamp(maxEntries, 1, 10);
    protected virtual void Awake() => LoadScores();
    public bool IsHighScore(int score) => score > 0 && (entries.Count < MaxEntries || score > entries[entries.Count - 1].score);
    public void AddScore(string playerName, int score)
    {
        if (score <= 0) return;
        string name = string.IsNullOrWhiteSpace(playerName) ? "AAA" : playerName.Trim().ToUpperInvariant();
        if (name.Length > 24) name = name.Substring(0, 24);
        entries.Add(new ScoreEntry(name, score)); Normalize(); SaveScores();
    }
    public List<ScoreEntry> GetScores() => new List<ScoreEntry>(entries);
    private void Normalize()
    {
        entries = entries.Where(e => e != null && e.score >= 0).OrderByDescending(e => e.score).Take(MaxEntries).ToList();
    }
    private void SaveScores()
    {
        int previous = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Count", 0), 0, 1000);
        PlayerPrefs.SetInt(Prefix + "Count", entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            PlayerPrefs.SetString(Prefix + "Name_" + i, entries[i].playerName);
            PlayerPrefs.SetInt(Prefix + "Score_" + i, entries[i].score);
        }
        for (int i = entries.Count; i < previous; i++) { PlayerPrefs.DeleteKey(Prefix + "Name_" + i); PlayerPrefs.DeleteKey(Prefix + "Score_" + i); }
        PlayerPrefs.Save();
    }
    private void LoadScores()
    {
        entries.Clear();
        if (!PlayerPrefs.HasKey(Prefix + "Count"))
        {
            string[] names = { "DEV", "PRO", "CPU", "BOT", "MAX", "LEO", "ANA", "SOL", "LUZ", "NEW" };
            for (int i = 0; i < names.Length; i++) entries.Add(new ScoreEntry(names[i], 5000 - i * 500));
            Normalize(); SaveScores(); return;
        }
        int count = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Count", 0), 0, 1000);
        for (int i = 0; i < count; i++) entries.Add(new ScoreEntry(PlayerPrefs.GetString(Prefix + "Name_" + i, "AAA"), PlayerPrefs.GetInt(Prefix + "Score_" + i, 0)));
        Normalize();
    }
    public void ClearLeaderboard()
    {
        // Borrar la tabla nunca borra controles, opciones u otros datos de PlayerPrefs.
        entries.Clear(); SaveScores();
    }
}
