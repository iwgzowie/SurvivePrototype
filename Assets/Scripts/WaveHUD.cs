using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 
/// 
/// </summary>
[DisallowMultipleComponent]
public sealed class WaveHUD : MonoBehaviour
{
    //References
    [SerializeField] private EnemySpawner enemySpawner;

    //Elementos UI
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text statusText;

    //Config
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
        if(subscribedSpawner != null && subscribedSpawner.IsRunning)
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
            subscribedSpawner.OnAllWavesCompleted += OnAllWavesCompleted;
        }
    }

    private void UnsubscribeSpawner()
    {
        if (subscribedSpawner != null)
        {
            subscribedSpawner.OnWaveStarted -= OnWaveStarted;
            subscribedSpawner.OnWaveCompleted -= OnWaveCompleted;
            subscribedSpawner.OnAllWavesCompleted -= OnAllWavesCompleted;
        }
        subscribedSpawner = null;
    }
    public void RefreshUI()
    {
        ResolveSpawner();

        if (enemySpawner == null)
        {
            if (waveText != null) waveText.text = "Oleada: - / -";
            if (enemiesText != null) enemiesText.text = "Enemigos: -";
            return;
        }

        UpdateWaveText(enemySpawner.CurrentWaveNumber, enemySpawner.WaveCount);
        UpdateEnemiesCount();
    }

    private void OnWaveStarted(int waveIndex, EnemyWave wave)
    {
        UpdateWaveText(waveIndex + 1, enemySpawner.WaveCount);

        ClearStatusText();
    }

    private void OnWaveCompleted(int waveIndex)
    {
        ShowTemporaryStatus("¡Oleada Despejada!");
    }

    private void OnAllWavesCompleted()
    {
        if (statusCoroutine != null) StopCoroutine(statusCoroutine);
        if (statusText != null) statusText.text = "¡Todas las oleadas completadas!";
        if (enemiesText != null) enemiesText.text = "Enemigos restantes: 0";
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

    private void UpdateWaveText(int currentWave, int totalWaves)
    {
        if (waveText != null)
        {
            waveText.text = $"Oleada {currentWave} / {totalWaves}";
        }
    }

    private void UpdateEnemiesCount()
    {
        if (enemiesText == null || subscribedSpawner == null) return;

        enemiesText.text = $"Enemigos restantes: {subscribedSpawner.AliveEnemyCount}";
    }
}
