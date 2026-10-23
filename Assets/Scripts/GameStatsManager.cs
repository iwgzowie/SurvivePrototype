using UnityEngine;

public class GameStatsManager : MonoBehaviour
{
    public static GameStatsManager Instance;

    [Header("Métricas de Partida")]
    public int enemiesKilled = 0;
    public int waveReached = 1;
    public float survivalTime = 0f;
    public int combatScore = 0;
    public int totalScore = 0;

    [Header("Precisión y Combate")]
    public int shotsFired = 0;
    public int shotsHit = 0;
    public int criticalHits = 0;

    private bool isGameActive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //Reiniciar estadísticas al comenzar una partida
    public void ResetStats()
    {
        enemiesKilled = 0;
        waveReached = 1;
        survivalTime = 0f;
        combatScore = 0;
        totalScore = 0;
        shotsFired = 0;
        shotsHit = 0;
        criticalHits = 0;
        isGameActive = true;
    }

    private void Update()
    {
        if (isGameActive)
        {
            survivalTime += Time.deltaTime;
        }
    }

    // Registra enemigo eliminado
    public void AddEnemyKill()
    {
        enemiesKilled++;
        combatScore += 100;
        totalScore = combatScore;
    }

    public void AddMinionKill()
    {
        enemiesKilled++;
        combatScore += 50;
        totalScore = combatScore;
    }

    public void AddBossKill()
    {
        enemiesKilled++;
        combatScore += 1000;
        totalScore = combatScore;
    }

    // Calcula puntaje total y bonificaciones al llegar al Game Over
    public void FinalizeGameStats()
    {
        if (!isGameActive) return;

        isGameActive = false;

        int criticalBonus = criticalHits * 50;

        // Puntos base por enemigos, oleadas y tiempo
        int timeAndWaveScore = (waveReached * 250) + Mathf.RoundToInt(survivalTime * 5);


        totalScore = combatScore + timeAndWaveScore + criticalBonus;
    }
}