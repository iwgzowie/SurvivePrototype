using System;
using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Muestra en la interfaz de usuario la oleada actual, enemigos restantes,
/// cuenta regresiva para el Jefe y el temporizador del descanso/tienda.
/// </summary>
[DisallowMultipleComponent]
public sealed class WaveHUD : MonoBehaviour
{
    // Referencias
    [SerializeField] private EnemySpawner enemySpawner;

    // Elementos UI
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text bossWarningText;
    [SerializeField] private TMP_Text restTimerText;

    // Configuración
    [SerializeField, Min(0.5f)] private float statusDisplayDuration = 2.5f;

    private EnemySpawner subscribedSpawner;
    private Coroutine statusCoroutine;

    private void OnEnable()
    {
        SubscribeToSpawner();
        ClearStatusText();
        RefreshUI();
    }

    private void Start()
    {
        SubscribeToSpawner();
        ClearStatusText();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeSpawner();
    }

    private void Update()
    {
        if (subscribedSpawner != null && subscribedSpawner.IsRunning)
        {
            UpdateEnemiesCount();
        }
    }

    private void ResolveSpawner()
    {
        if (enemySpawner == null)
        {
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
        }
    }

    private void SubscribeToSpawner()
    {
        ResolveSpawner();

        if (enemySpawner == null) return;

        UnsubscribeSpawner();
        subscribedSpawner = enemySpawner;

        if (subscribedSpawner != null)
        {
            subscribedSpawner.OnWaveStarted += OnWaveStarted;
            subscribedSpawner.OnWaveCompleted += OnWaveCompleted;
            subscribedSpawner.OnBossWaveApproaching += OnBossWaveApproaching;
            subscribedSpawner.OnBossWaveStarted += OnBossWaveStarted;
            subscribedSpawner.OnRestStarted += OnRestStarted;
            subscribedSpawner.OnRestTimerUpdated += OnRestTimerUpdated;
            subscribedSpawner.OnRestFinished += OnRestFinished;
        }
    }

    private void UnsubscribeSpawner()
    {
        if (subscribedSpawner != null)
        {
            subscribedSpawner.OnWaveStarted -= OnWaveStarted;
            subscribedSpawner.OnWaveCompleted -= OnWaveCompleted;
            subscribedSpawner.OnBossWaveApproaching -= OnBossWaveApproaching;
            subscribedSpawner.OnBossWaveStarted -= OnBossWaveStarted;
            subscribedSpawner.OnRestStarted -= OnRestStarted;
            subscribedSpawner.OnRestTimerUpdated -= OnRestTimerUpdated;
            subscribedSpawner.OnRestFinished -= OnRestFinished;
        }
        subscribedSpawner = null;
    }

    public void RefreshUI()
    {
        ResolveSpawner();

        if (enemySpawner == null)
        {
            if (waveText != null) waveText.text = "Oleada: -";
            if (enemiesText != null) enemiesText.text = "Enemigos: -";
            return;
        }

        UpdateWaveText(enemySpawner.CurrentWaveNumber);
        UpdateEnemiesCount();
    }

    private void OnWaveStarted(int waveIndex, EnemyWave wave)
    {
        UpdateWaveText(waveIndex + 1);
        ClearStatusText();
    }

    private void OnWaveCompleted(int waveIndex)
    {
        ShowTemporaryStatus("¡Oleada Despejada!");
    }

    private void OnBossWaveApproaching(int wavesLeft)
    {
        if (bossWarningText != null)
        {
            bossWarningText.text = $"Faltan {wavesLeft} oleada(s) para el Jefe";
        }
    }

    private void OnBossWaveStarted()
    {
        if (bossWarningText != null)
        {
            bossWarningText.text = "¡¡JEFE EN CAMINO!!";
        }
        ShowTemporaryStatus("¡ALERTA DE JEFE!");
    }

    private void OnRestStarted()
    {
        ShowTemporaryStatus("¡Jefe derrotado! Fase de descanso");
        if (bossWarningText != null) bossWarningText.text = "";
    }

    private void OnRestTimerUpdated(float timeRemaining)
    {
        if (restTimerText != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            restTimerText.text = $"Tiempo de descanso: {minutes:00}:{seconds:00}";
        }
    }

    private void OnRestFinished()
    {
        if (restTimerText != null) restTimerText.text = "";
        ShowTemporaryStatus("¡Fin del descanso! Preparándote para la siguiente oleada...");
    }

    private void ShowTemporaryStatus(string message)
    {
        if (statusText == null) return;

        if (statusCoroutine != null)
        {
            StopCoroutine(statusCoroutine);
        }

        statusCoroutine = StartCoroutine(HideStatusRoutine(message));
    }

    private IEnumerator HideStatusRoutine(string message)
    {
        statusText.text = message;
        yield return new WaitForSeconds(statusDisplayDuration);
        ClearStatusText();
        statusCoroutine = null;
    }

    private void ClearStatusText()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    private void UpdateWaveText(int currentWave)
    {
        if (waveText != null)
        {
            waveText.text = $"Oleada {currentWave}";
        }
    }

    private void UpdateEnemiesCount()
    {
        if (enemiesText == null || subscribedSpawner == null) return;

        enemiesText.text = $"Enemigos restantes: {subscribedSpawner.AliveEnemyCount}";
    }
}
