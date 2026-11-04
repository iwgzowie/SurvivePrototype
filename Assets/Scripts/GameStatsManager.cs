using System;
using UnityEngine;

public class GameStatsManager : MonoBehaviour
{
    public static GameStatsManager Instance;
    [Header("Métricas de partida")]
    public int enemiesKilled;
    public int waveReached = 1;
    public float survivalTime;
    public int combatScore;
    public int totalScore;
    [Header("Precisión y combate")]
    public int shotsFired;
    public int shotsHit;
    public int criticalHits;
    private bool isGameActive;
    public bool IsGameActive => isGameActive;
    public float Accuracy => shotsFired > 0 ? Mathf.Clamp01((float)shotsHit / shotsFired) * 100 : 0;
    public event Action Changed;
    public static GameStatsManager GetOrCreate()
    {
        if (Instance == null) new GameObject("GameStatsManager").AddComponent<GameStatsManager>();
        return Instance;
    }
    protected virtual void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }
    protected virtual void OnDestroy() { if (Instance == this) Instance = null; }
    public void ResetStats()
    {
        enemiesKilled = combatScore = totalScore = shotsFired = shotsHit = criticalHits = 0;
        waveReached = 1; survivalTime = 0; isGameActive = true; RefreshScore();
    }
    protected virtual void Update()
    {
        if (!isGameActive) return;
        survivalTime += Time.deltaTime; RefreshScore();
    }
    public void AddEnemyKill() => AddKill(100);
    public void AddMinionKill() => AddKill(50);
    public void AddBossKill() => AddKill(1000);
    private void AddKill(int score)
    {
        if (!isGameActive) return;
        enemiesKilled = (int)Math.Min(int.MaxValue, (long)enemiesKilled + 1);
        combatScore = (int)Math.Min(int.MaxValue, (long)combatScore + score); RefreshScore();
    }
    public void RegisterShot() { if (isGameActive && shotsFired < int.MaxValue) shotsFired++; }
    public void RegisterHit() { if (isGameActive && shotsHit < shotsFired) { shotsHit++; RefreshScore(); } }
    protected virtual int KillScore => combatScore;
    protected virtual int AccuracyBonus => 0;
    public int CalculateScore() => (int)Math.Min(int.MaxValue, Math.Max(0L,
        (long)KillScore + (long)Mathf.Max(0, waveReached) * 250 + Mathf.RoundToInt(survivalTime * 5)
        + (long)criticalHits * 50 + AccuracyBonus));
    private void RefreshScore()
    {
        int score = CalculateScore(); if (score == totalScore) return;
        totalScore = score; Changed?.Invoke();
    }
    public void FinalizeGameStats()
    {
        if (!isGameActive) return;
        RefreshScore(); isGameActive = false;
    }
}
