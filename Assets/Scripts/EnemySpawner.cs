using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public enum WaveRunState
{
    Idle,
    Waiting,
    Spawning,
    WaitingForEnemies,
    Resting,
    Completed,
    Cancelled,
    Failed
}

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs predeterminados")]
    [SerializeField, InspectorName("Prefab melee")] private GameObject chasingPrefab;
    [SerializeField, InspectorName("Prefab a distancia")] private GameObject chasingRangedPrefab;
    [SerializeField, InspectorName("Prefab patrullero")] private GameObject patrolPrefab;

    [Header("Oleadas e Infinito")]
    [FormerlySerializedAs("waves")]
    [SerializeField] private List<EnemyWave> baseWaveTemplates = new List<EnemyWave>();
    [SerializeField] private BossSpawner bossSpawner;
    [SerializeField] private float restDurationAfterBoss = 120f;
    [SerializeField] private bool autoStart = false;

    [Header("Velocidad por oleada")]
    [Tooltip("Aumento lineal sobre el multiplicador de velocidad de la plantilla. 0.04 = +4 % por oleada. No se reinicia al matar al boss.")]
    [SerializeField, Min(0.001f)] private float speedIncreasePerWave = .04f;
    public float SpeedMultiplierForWave(float baseMultiplier, int waveIndex) =>
        Mathf.Max(.01f, baseMultiplier) * (1f + Mathf.Max(0, waveIndex) * Mathf.Max(.001f, speedIncreasePerWave));

    [Header("Configuracion del Jefe Aleatoria")]
    [SerializeField, Min(1)] private int minWavesBeforeBoss = 2;
    [SerializeField, Min(1)] private int maxWavesBeforeBoss = 5;

    [Header("Partida")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private MenuBehaviour menu;

    [Header("Aparición en el mapa")]
    [SerializeField] private List<Transform> defaultSpawnPoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 2f;
    [SerializeField, Min(1)] private int maxSpawnAttempts = 32;

    [Header("Limites de Enemigos Simultaneos")]
    [SerializeField, Min(1)] private int baseSimultaneousEnemies = 8;
    [SerializeField, Min(1)] private int maxSimultaneousLimit = 20;
    [SerializeField, Min(1)] private int enemiesIncreasePerWave = 1;

    private readonly Dictionary<EnemyHealth, Action> aliveEnemies = new Dictionary<EnemyHealth, Action>();
    private readonly List<EnemyHealth> removedEnemies = new List<EnemyHealth>();
    private Coroutine waveRoutine;
    private float runningSampleDistance;
    private int runningSpawnAttempts;
    private bool healthSubscribed;
    private int nextSpawnPoint;
    private bool waitingForBossDefeat = false;
    private float previousWaveSpeedMultiplier;

    // Variables internas para el ciclo de oleadas y jefes
    private int wavesForCurrentBossCycle;
    private int wavesPassedInCurrentCycle = 0;

    // Declaración recuperada para evitar errores de contexto
    public int SpawnedInCurrentWave { get; private set; }

    // Propiedades públicas de estado
    public WaveRunState State { get; private set; } = WaveRunState.Idle;
    public int CurrentWaveIndex { get; private set; } = -1;
    public int CurrentWaveNumber => CurrentWaveIndex + 1;

    // Cantidad de oleadas que faltan para el jefe
    public int WavesUntilBoss => Mathf.Max(0, wavesForCurrentBossCycle - wavesPassedInCurrentCycle);

    // Límite activo de enemigos simultáneos calculado según la oleada actual
    public int CurrentMaxSimultaneousEnemies => Mathf.Min(maxSimultaneousLimit, baseSimultaneousEnemies + (CurrentWaveIndex * enemiesIncreasePerWave));

    public float RestTimeRemaining { get; private set; } = 0f;
    public int AliveEnemyCount => aliveEnemies.Count;
    public IReadOnlyCollection<EnemyHealth> AliveEnemies => aliveEnemies.Keys;
    public string LastError { get; private set; }
    public bool IsRunning => State == WaveRunState.Waiting || State == WaveRunState.Spawning
        || State == WaveRunState.WaitingForEnemies || State == WaveRunState.Resting;

    // Eventos públicos para UI / Leaderboard
    public event Action<int, EnemyWave> OnWaveStarted;
    public event Action<int> OnWaveCompleted;
    public event Action<int> OnBossWaveApproaching;
    public event Action OnBossWaveStarted;
    public event Action<float> OnRestTimerUpdated;
    public event Action OnRestStarted;
    public event Action OnRestFinished;
    public event Action<EnemyHealth> OnEnemySpawned;

    private void Start()
    {
        if (bossSpawner == null)
        {
            bossSpawner = GetComponent<BossSpawner>();
            if (bossSpawner == null)
            {
                Debug.LogWarning("EnemySpawner: No se asignó ningún BossSpawner en el inspector ni en el GameObject.");
            }
        }

        if (autoStart) StartWaves();
    }

    private void OnDisable()
    {
        if (IsRunning) CancelWaves();
        UnsubscribeHealth();
        UnsubscribeBossEvents();
    }

    private void OnDestroy()
    {
        ClearTrackedEnemies();
        UnsubscribeHealth();
        UnsubscribeBossEvents();
    }

    public bool StartWaves()
    {

        if (!isActiveAndEnabled || State != WaveRunState.Idle) return false;
        ResolveGameReferences();
        if (playerHealth == null)
        {
            Fail("Falta un jugador con PlayerHealth en la escena.");
            return false;
        }
        if (playerHealth.IsDead || (menu != null && menu.IsFinished))
        {
            State = WaveRunState.Cancelled;
            return false;
        }
        if (baseWaveTemplates == null || baseWaveTemplates.Count == 0)
        {
            Fail("La lista de plantillas de oleadas está vacía.");
            return false;
        }

        GameStatsManager.GetOrCreate().ResetStats();
        runningSampleDistance = navMeshSampleDistance;
        runningSpawnAttempts = maxSpawnAttempts;
        CurrentWaveIndex = -1;
        previousWaveSpeedMultiplier = 0f;

        GenerateNewBossCycle();

        playerHealth.OnDeath += CancelWaves;
        healthSubscribed = true;
        State = WaveRunState.Waiting;
        waveRoutine = StartCoroutine(RunInfiniteWaves());
        return true;
    }

    private void GenerateNewBossCycle()
    {
        wavesPassedInCurrentCycle = 0;
        int minRange = Mathf.Min(minWavesBeforeBoss, maxWavesBeforeBoss);
        int maxRange = Mathf.Max(minWavesBeforeBoss, maxWavesBeforeBoss);
        wavesForCurrentBossCycle = UnityEngine.Random.Range(minRange, maxRange + 1);
    }

    private IEnumerator RunInfiniteWaves()
    {
        yield return null;

        while (CanContinue())
        {
            CurrentWaveIndex++;
            wavesPassedInCurrentCycle++;
            SpawnedInCurrentWave = 0;
            nextSpawnPoint = 0;

            if (GameStatsManager.Instance != null)
            {
                GameStatsManager.Instance.waveReached = CurrentWaveNumber;
            }

            bool isBossWave = (wavesPassedInCurrentCycle >= wavesForCurrentBossCycle);

            if (!isBossWave)
            {
                OnBossWaveApproaching?.Invoke(WavesUntilBoss);
            }

            EnemyWave currentWave = GenerateProceduralWave(CurrentWaveIndex, isBossWave);

            State = WaveRunState.Waiting;
            yield return WaitWhileAlive(currentWave.delayBeforeWave);
            if (!CanContinue()) yield break;

            State = WaveRunState.Spawning;
            OnWaveStarted?.Invoke(CurrentWaveIndex, currentWave);

            if (isBossWave)
            {
                OnBossWaveStarted?.Invoke();

                if (bossSpawner != null)
                {
                    waitingForBossDefeat = true;
                    bossSpawner.OnBossSpawned += TrackBossHealth;
                    bossSpawner.OnBossDefeated += HandleBossDefeatedExternal;

                    if (!bossSpawner.SpawnBoss(currentWave.healthMultiplier, currentWave.damageMultiplier, currentWave.speedMultiplier, playerHealth.transform))
                    {
                        waitingForBossDefeat = false;
                        UnsubscribeBossEvents();
                        Fail("No se pudo generar el jefe sobre su NavMesh; revisá el prefab y el punto de aparición.");
                        yield break;
                    }
                }
                else
                {
                    Fail("Es oleada de jefe pero falta asignar BossSpawner.");
                    yield break;
                }

                State = WaveRunState.WaitingForEnemies;
                while (waitingForBossDefeat && CanContinue())
                {
                    yield return null;
                }
            }
            else
            {
                int melee = currentWave.chasingCount, ranged = currentWave.chasingRangedCount, patrol = currentWave.patrolCount;
                while ((long)melee + ranged + patrol > 0 && CanContinue())
                {
                    while (aliveEnemies.Count >= CurrentMaxSimultaneousEnemies && CanContinue())
                    {
                        yield return new WaitForSeconds(0.5f);
                    }

                    if (!CanContinue()) yield break;

                    for (int type = 0; type < 3 && CanContinue(); type++)
                    {
                        if ((type == 0 && melee == 0) || (type == 1 && ranged == 0) || (type == 2 && patrol == 0)) continue;
                        GameObject prefab = type == 0 ? currentWave.chasingPrefab
                            : type == 1 ? currentWave.chasingRangedPrefab : currentWave.patrolPrefab;
                        if (!SpawnEnemy(prefab, currentWave)) yield break;
                        if (type == 0) melee--; else if (type == 1) ranged--; else patrol--;
                        if ((long)melee + ranged + patrol > 0) yield return WaitWhileAlive(currentWave.spawnInterval);
                    }
                }

                if (!CanContinue()) yield break;
                State = WaveRunState.WaitingForEnemies;

                do
                {
                    yield return null;
                    RemoveDestroyedEnemies();
                } while ((aliveEnemies.Count > 0 || Time.timeScale <= 0f) && CanContinue());
            }

            if (!CanContinue()) yield break;
            OnWaveCompleted?.Invoke(CurrentWaveIndex);

            if (isBossWave && CanContinue())
            {
                GenerateNewBossCycle();

                State = WaveRunState.Resting;
                OnRestStarted?.Invoke();
                RestTimeRemaining = restDurationAfterBoss;

                while (RestTimeRemaining > 0f && CanContinue())
                {
                    if (Time.timeScale > 0f)
                    {
                        RestTimeRemaining -= Time.deltaTime;
                        OnRestTimerUpdated?.Invoke(RestTimeRemaining);
                    }
                    yield return null;
                }

                if (!CanContinue()) yield break;
                OnRestFinished?.Invoke();
            }
        }
    }

    private EnemyWave GenerateProceduralWave(int waveIndex, bool isBossWave)
    {
        EnemyWave template = baseWaveTemplates[waveIndex % baseWaveTemplates.Count];
        float difficultyFactor = 1f + (waveIndex * 0.15f);
        float speed = SpeedMultiplierForWave(template.speedMultiplier, waveIndex);
        if (waveIndex > 0)
            speed = Mathf.Max(speed, previousWaveSpeedMultiplier
                + Mathf.Max(.01f, baseWaveTemplates[0].speedMultiplier) * Mathf.Max(.001f, speedIncreasePerWave));
        previousWaveSpeedMultiplier = speed;

        return new EnemyWave
        {
            waveName = isBossWave ? $"¡Jefe en Oleada {waveIndex + 1}!" : $"Oleada {waveIndex + 1}",
            chasingCount = isBossWave ? 0 : Mathf.RoundToInt(template.chasingCount * difficultyFactor),
            chasingPrefab = template.chasingPrefab != null ? template.chasingPrefab : chasingPrefab,
            chasingRangedCount = isBossWave ? 0 : Mathf.RoundToInt(template.chasingRangedCount * difficultyFactor),
            chasingRangedPrefab = template.chasingRangedPrefab != null ? template.chasingRangedPrefab : chasingRangedPrefab,
            patrolCount = isBossWave ? 0 : Mathf.RoundToInt(template.patrolCount * difficultyFactor),
            patrolPrefab = template.patrolPrefab != null ? template.patrolPrefab : patrolPrefab,
            delayBeforeWave = template.delayBeforeWave,
            spawnInterval = Mathf.Max(0.1f, template.spawnInterval / Mathf.Sqrt(difficultyFactor)),
            spawnRadius = template.spawnRadius,
            healthMultiplier = template.healthMultiplier * difficultyFactor * (isBossWave ? 2.5f : 1f),
            speedMultiplier = speed,
            damageMultiplier = template.damageMultiplier * difficultyFactor * (isBossWave ? 1.5f : 1f),
            spawnPoints = template.spawnPoints != null && template.spawnPoints.Count > 0
                ? new List<Transform>(template.spawnPoints)
                : (defaultSpawnPoints != null && defaultSpawnPoints.Count > 0 ? new List<Transform>(defaultSpawnPoints) : new List<Transform> { transform })
        };
    }

    // Sobrecargas para adaptarse al tipo de argumento del BossSpawner (sea GameObject o EnemyHealth)
    private void TrackBossHealth(EnemyHealth bossHealth) { }
    private void TrackBossHealth(GameObject bossObject) { }

    private void HandleBossDefeatedExternal()
    {
        UnsubscribeBossEvents();
        waitingForBossDefeat = false;
    }

    private void UnsubscribeBossEvents()
    {
        if (bossSpawner != null)
        {
            bossSpawner.OnBossSpawned -= TrackBossHealth;
            bossSpawner.OnBossDefeated -= HandleBossDefeatedExternal;
        }
    }

    private IEnumerator WaitWhileAlive(float duration)
    {
        float elapsed = 0f;
        while ((elapsed < duration || Time.timeScale <= 0f) && CanContinue())
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    private bool CanContinue()
    {
        if (!IsRunning) return false;
        if (playerHealth == null || playerHealth.IsDead || (menu != null && menu.IsFinished))
        {
            State = WaveRunState.Cancelled;
            ClearTrackedEnemies();
            UnsubscribeHealth();
            UnsubscribeBossEvents();
            return false;
        }
        return true;
    }

    private bool SpawnEnemy(GameObject prefab, EnemyWave wave)
    {
        if (prefab == null) return false;
        if (!TrySpawnPosition(prefab, wave, out Vector3 position, out Quaternion rotation))
        {
            Fail($"Oleada {CurrentWaveNumber}: no se encontró un punto libre en el NavMesh para {prefab.name}.");
            return false;
        }
        GameObject instance = Instantiate(prefab, position, rotation);
        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null && !agent.isOnNavMesh)
        {
            Destroy(instance);
            Fail($"Oleada {CurrentWaveNumber}: {prefab.name} no pudo colocarse sobre el NavMesh.");
            return false;
        }

        EnemyHealth health = instance.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.ApplyWaveMultipliers(wave.healthMultiplier);

            if (!health.IsDead)
            {
                Action onDeath = () => RemoveTrackedEnemy(health);
                aliveEnemies.Add(health, onDeath);
                health.OnDeath += onDeath;
            }
        }

        var controller = instance.GetComponent<EnemyBaseController>();
        if (controller != null)
        {
            controller.ApplyWaveMultipliers(wave.damageMultiplier, wave.speedMultiplier);
        }

        EnemyReachability.Track(instance, playerHealth.transform);

        SpawnedInCurrentWave++;
        OnEnemySpawned?.Invoke(health);
        return true;
    }

    private bool TrySpawnPosition(GameObject prefab, EnemyWave wave, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = transform.rotation;
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
        if (agent == null || playerHealth == null) return false;
        List<Transform> points = wave.spawnPoints;
        for (int attempt = 0; attempt < Mathf.Max(1, runningSpawnAttempts); attempt++)
        {
            Transform point = transform;
            if (points != null && points.Count > 0)
            {
                point = points[nextSpawnPoint % points.Count];
                nextSpawnPoint++;
                if (point == null) continue;
            }
            Vector2 offset = UnityEngine.Random.insideUnitCircle * wave.spawnRadius;
            Vector3 desired = point.position + new Vector3(offset.x, 0f, offset.y);
            if (!EnemyNavigation.TryPosition(agent, desired, playerHealth.transform, Mathf.Max(.1f, runningSampleDistance), out var rootPosition)) continue;
            bool occupied = false;
            foreach (EnemyHealth other in aliveEnemies.Keys)
            {
                if (other == null || other.IsDead) continue;
                NavMeshAgent otherAgent = other.GetComponent<NavMeshAgent>();
                float otherRadius = otherAgent != null ? otherAgent.radius : 0.5f;
                float currentRadius = agent != null ? agent.radius : 0.5f;
                float separation = currentRadius + otherRadius;
                if ((other.transform.position - rootPosition).sqrMagnitude < separation * separation) { occupied = true; break; }
            }
            if (occupied) continue;
            position = rootPosition;
            rotation = point.rotation;
            return true;
        }
        // Un punto fijo puede quedar al otro lado de una puerta. Buscar en la zona habilitada del jugador.
        return EnemyNavigation.TryNearPlayer(agent, playerHealth.transform, out position);
    }

    private void RemoveDestroyedEnemies()
    {
        removedEnemies.Clear();
        foreach (EnemyHealth health in aliveEnemies.Keys) if (health == null || health.IsDead) removedEnemies.Add(health);
        foreach (EnemyHealth health in removedEnemies) RemoveTrackedEnemy(health);
    }

    private void RemoveTrackedEnemy(EnemyHealth health)
    {
        if (!aliveEnemies.TryGetValue(health, out Action callback)) return;
        if (health != null) health.OnDeath -= callback;
        aliveEnemies.Remove(health);
    }

    private void ClearTrackedEnemies()
    {
        foreach (var tracked in aliveEnemies) if (tracked.Key != null) tracked.Key.OnDeath -= tracked.Value;
        aliveEnemies.Clear();
    }

    private void UnsubscribeHealth()
    {
        if (!healthSubscribed) return;
        if (playerHealth != null) playerHealth.OnDeath -= CancelWaves;
        healthSubscribed = false;
    }

    private void ResolveGameReferences()
    {
        if (playerHealth == null)
        {
            foreach (PlayerHealth candidate in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == gameObject.scene && candidate.CompareTag("Player"))
                {
                    playerHealth = candidate;
                    break;
                }
            }
        }
        if (menu == null)
        {
            foreach (MenuBehaviour candidate in FindObjectsByType<MenuBehaviour>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == gameObject.scene && !candidate.IsMainMenu)
                {
                    menu = candidate;
                    break;
                }
            }
        }
    }

    public void CancelWaves()
    {
        if (!IsRunning) return;
        State = WaveRunState.Cancelled;
        if (waveRoutine != null) StopCoroutine(waveRoutine);
        waveRoutine = null;
        ClearTrackedEnemies();
        UnsubscribeHealth();
        UnsubscribeBossEvents();
    }

    public void SkipRest()
    {
        if (State == WaveRunState.Resting)
        {
            RestTimeRemaining = 0f;
            Debug.Log("EnemySpawner: ¡Descanso salteado por comando de depuración!");
        }
    }

    private void Fail(string error)
    {
        State = WaveRunState.Failed;
        LastError = error;
        ClearTrackedEnemies();
        UnsubscribeHealth();
        UnsubscribeBossEvents();
        Debug.LogError($"EnemySpawner: {error}", this);
    }

    public bool ResetAndRestartWaves()
    {
        CancelWaves();
        State = WaveRunState.Idle;
        CurrentWaveIndex = -1;
        return StartWaves();
    }

    public void KillAllActiveEnemies()
    {
        List<EnemyHealth> currentAlive = new List<EnemyHealth>(aliveEnemies.Keys);
        foreach (EnemyHealth enemy in currentAlive)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(999999f);
            }
        }
    }

    public void ForceNextWave()
    {
        if (!IsRunning) return;
        if (waveRoutine != null) StopCoroutine(waveRoutine);
        KillAllActiveEnemies();
        State = WaveRunState.Waiting;
        waveRoutine = StartCoroutine(RunInfiniteWaves());
    }
}
