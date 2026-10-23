using System.Collections.Generic;
using System.IO;
using UnityEngine;


[System.Serializable]
public class ScoreEntry
{
    public string playerName;
    public int score;

    public ScoreEntry(string name, int newScore)
    {
        playerName = name;
        score = newScore;
    }
}

public class LeaderboardManager : MonoBehaviour
{
    [SerializeField] private int maxEntries = 5;


    [SerializeField] private List<ScoreEntry> entries = new List<ScoreEntry>();

    private void Awake()
    {
        LoadScores();
    }

    public bool IsHighScore(int newScore)
    {
        if (newScore <= 0)
            return false;

        if (entries.Count < maxEntries)
            return true;

        int lowestTopScore = entries[entries.Count - 1].score;
        return newScore > lowestTopScore;
    }

    public void AddScore(string playerName, int score)
    {
        entries.Add(new ScoreEntry(playerName, score));

        // Ordena de mayor a menor
        entries.Sort((a, b) => b.score.CompareTo(a.score));

        
        if (entries.Count > maxEntries)
        {
            entries.RemoveRange(maxEntries, entries.Count - maxEntries);
        }

        SaveScores();
    }

    public List<ScoreEntry> GetScores()
    {
        return entries;
    }

    private void SaveScores()
    {
        
        PlayerPrefs.SetInt("LB_Count", entries.Count);

       
        for (int i = 0; i < entries.Count; i++)
        {
            PlayerPrefs.SetString("LB_Name_" + i, entries[i].playerName);
            PlayerPrefs.SetInt("LB_Score_" + i, entries[i].score);
        }

        
        PlayerPrefs.Save();
    }

    private void LoadScores()
    {
        entries.Clear();

        
        int count = PlayerPrefs.GetInt("LB_Count", 0);

        if (count == 0)
        {
            
            CreateDefaultScores();
        }
        else
        {
            
            for (int i = 0; i < count; i++)
            {
                string name = PlayerPrefs.GetString("LB_Name_" + i, "AAA");
                int score = PlayerPrefs.GetInt("LB_Score_" + i, 0);

                entries.Add(new ScoreEntry(name, score));
            }
        }
    }

    private void CreateDefaultScores()
    {
        
        entries.Add(new ScoreEntry("DEV", 5000));
        entries.Add(new ScoreEntry("PRO", 4500));
        entries.Add(new ScoreEntry("CPU", 4000));
        entries.Add(new ScoreEntry("BOT", 3500));
        entries.Add(new ScoreEntry("MAX", 3000));
        entries.Add(new ScoreEntry("LEO", 2500));
        entries.Add(new ScoreEntry("ANA", 2000));
        entries.Add(new ScoreEntry("SOL", 1500));
        entries.Add(new ScoreEntry("LUZ", 1000));
        entries.Add(new ScoreEntry("NEW", 500));

        
        entries.Sort((a, b) => b.score.CompareTo(a.score));

        
        SaveScores();
    }


    public void ClearLeaderboard()
    {
        entries.Clear();
        PlayerPrefs.DeleteAll(); 
    }
}

