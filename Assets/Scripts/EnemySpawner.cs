using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum WaveRunState
{
    Idle,
    Waiting,
    Spawning,
    WaitingForEnemies,
    Completed,
    Cancelled,
    Failed
}

/// <summary>Genera oleadas configurables y termina la partida al eliminar la última.</summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs predeterminados")]
    [SerializeField, InspectorName("Prefab melee")] private GameObject chasingPrefab;
    [SerializeField, InspectorName("Prefab a distancia")] private GameObject chasingRangedPrefab;
    [SerializeField, InspectorName("Prefab patrullero")] private GameObject patrolPrefab;

    [Header("Oleadas")]
    [SerializeField] private List<EnemyWave> waves = new List<EnemyWave>();
    [SerializeField] private bool autoStart = true;

    [Header("Partida")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private MenuBehaviour menu;

    [Header("Aparición en el mapa")]
    [SerializeField] private List<Transform> defaultSpawnPoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 2f;
    [SerializeField, Min(1)] private int maxSpawnAttempts = 32;

    private readonly Dictionary<EnemyHealth, Action> aliveEnemies = new Dictionary<EnemyHealth, Action>();
    private readonly List<EnemyHealth> removedEnemies = new List<EnemyHealth>();
    private Coroutine waveRoutine;
    private List<EnemyWave> runningWaves;
    private float runningSampleDistance;
    private int runningSpawnAttempts;
    private bool healthSubscribed;
    private int nextSpawnPoint;

    public WaveRunState State { get; private set; } = WaveRunState.Idle;
    public int CurrentWaveIndex { get; private set; } = -1;
    public int CurrentWaveNumber => CurrentWaveIndex + 1;
    public int WaveCount => runningWaves != null ? runningWaves.Count : (waves != null ? waves.Count : 0);
    public int AliveEnemyCount => aliveEnemies.Count;
    public int SpawnedInCurrentWave { get; private set; }
    public IReadOnlyCollection<EnemyHealth> AliveEnemies => aliveEnemies.Keys;
    public string LastError { get; private set; }
    public bool IsRunning => State == WaveRunState.Waiting || State == WaveRunState.Spawning
        || State == WaveRunState.WaitingForEnemies;

    public event Action<int, EnemyWave> OnWaveStarted;
    public event Action<int> OnWaveCompleted;
    public event Action<EnemyHealth> OnEnemySpawned;
    public event Action OnAllWavesCompleted;

    private void Start()
    {
        if (autoStart) StartWaves();
    }

    private void OnDisable()
    {
        if (IsRunning) CancelWaves();
        UnsubscribeHealth();
    }

    private void OnDestroy()
    {
        ClearTrackedEnemies();
        UnsubscribeHealth();
    }

    /// <summary>Inicia una sola ejecución. Reintentar la partida recarga la escena y sus oleadas.</summary>
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
        if (!ValidateWaves(out string error))
        {
            Fail(error);
            return false;
        }

        // La partida conserva su configuración; los cambios del Inspector se aplican al reiniciar.
        runningSampleDistance = navMeshSampleDistance;
        runningSpawnAttempts = maxSpawnAttempts;
        runningWaves = new List<EnemyWave>(waves.Count);
        foreach (EnemyWave wave in waves)
        {
            runningWaves.Add(new EnemyWave
            {
                waveName = wave.waveName, chasingCount = wave.chasingCount,
                chasingRangedCount = wave.chasingRangedCount, patrolCount = wave.patrolCount,
                chasingPrefab = wave.chasingPrefab != null ? wave.chasingPrefab : chasingPrefab,
                chasingRangedPrefab = wave.chasingRangedPrefab != null ? wave.chasingRangedPrefab : chasingRangedPrefab,
                patrolPrefab = wave.patrolPrefab != null ? wave.patrolPrefab : patrolPrefab,
                delayBeforeWave = wave.delayBeforeWave,
                spawnInterval = wave.spawnInterval, spawnRadius = wave.spawnRadius,
                spawnPoints = wave.spawnPoints != null && wave.spawnPoints.Count > 0
                    ? new List<Transform>(wave.spawnPoints)
                    : defaultSpawnPoints != null && defaultSpawnPoints.Count > 0
                        ? new List<Transform>(defaultSpawnPoints) : new List<Transform> { transform }
            });
        }
        playerHealth.OnDeath += CancelWaves;
        healthSubscribed = true;
        State = WaveRunState.Waiting;
        waveRoutine = StartCoroutine(RunWaves());
        return true;
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

    private bool ValidateWaves(out string error)
    {
        error = null;
        if (waves == null || waves.Count == 0)
        {
            error = "La lista de oleadas está vacía.";
            return false;
        }
        bool anyEnemies = false;
        for (int i = 0; i < waves.Count; i++)
        {
            EnemyWave wave = waves[i];
            if (wave == null) { error = $"La oleada {i + 1} no tiene configuración."; return false; }
            if (wave.chasingCount < 0 || wave.chasingRangedCount < 0 || wave.patrolCount < 0
                || wave.TotalEnemies > int.MaxValue || !ValidTime(wave.delayBeforeWave)
                || !ValidTime(wave.spawnInterval) || !ValidTime(wave.spawnRadius))
            {
                error = $"La oleada {i + 1} tiene cantidades o tiempos inválidos.";
                return false;
            }
            anyEnemies |= wave.TotalEnemies > 0;
            if (!ValidatePrefab(wave.chasingPrefab != null ? wave.chasingPrefab : chasingPrefab, wave.chasingCount, out error)
                || !ValidatePrefab(wave.chasingRangedPrefab != null ? wave.chasingRangedPrefab : chasingRangedPrefab, wave.chasingRangedCount, out error)
                || !ValidatePrefab(wave.patrolPrefab != null ? wave.patrolPrefab : patrolPrefab, wave.patrolCount, out error))
            {
                error = $"Oleada {i + 1}: {error}";
                return false;
            }
        }
        if (!anyEnemies) { error = "Configurá al menos un enemigo; una lista vacía de enemigos no otorga victoria."; return false; }
        return true;
    }

    private static bool ValidTime(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool ValidatePrefab(GameObject prefab, int count, out string error)
    {
        error = null;
        if (count == 0) return true;
        if (prefab == null) { error = "Falta el prefab de un tipo con cantidad mayor a cero."; return false; }
        if (!prefab.activeSelf || prefab.GetComponent<EnemyHealth>() == null
            || !prefab.TryGetComponent(out NavMeshAgent agent) || !agent.enabled)
        {
            error = $"El prefab {prefab.name} necesita estar activo y tener EnemyHealth y NavMeshAgent habilitado en su raíz.";
            return false;
        }
        return true;
    }

    private IEnumerator RunWaves()
    {
        // Permite terminar Awake/Start y evita ganar dentro de una notificación de muerte.
        yield return null;
        for (int i = 0; i < runningWaves.Count && CanContinue(); i++)
        {
            CurrentWaveIndex = i;
            SpawnedInCurrentWave = 0;
            nextSpawnPoint = 0;
            EnemyWave wave = runningWaves[i];
            State = WaveRunState.Waiting;
            yield return WaitWhileAlive(wave.delayBeforeWave);
            if (!CanContinue()) yield break;

            State = WaveRunState.Spawning;
            OnWaveStarted?.Invoke(i, wave);
            int melee = wave.chasingCount, ranged = wave.chasingRangedCount, patrol = wave.patrolCount;
            while ((long)melee + ranged + patrol > 0 && CanContinue())
            {
                // Alterna los tipos disponibles para distribuir la composición durante la generación.
                for (int type = 0; type < 3 && CanContinue(); type++)
                {
                    if ((type == 0 && melee == 0) || (type == 1 && ranged == 0) || (type == 2 && patrol == 0)) continue;
                    GameObject prefab = type == 0 ? wave.chasingPrefab
                        : type == 1 ? wave.chasingRangedPrefab : wave.patrolPrefab;
                    if (!SpawnEnemy(prefab, wave)) yield break;
                    if (type == 0) melee--; else if (type == 1) ranged--; else patrol--;
                    if ((long)melee + ranged + patrol > 0) yield return WaitWhileAlive(wave.spawnInterval);
                }
            }
            if (!CanContinue()) yield break;
            State = WaveRunState.WaitingForEnemies;
            do
            {
                yield return null;
                RemoveDestroyedEnemies();
            } while ((aliveEnemies.Count > 0 || Time.timeScale <= 0f) && CanContinue());
            if (!CanContinue()) yield break;
            OnWaveCompleted?.Invoke(i);
        }
        if (!CanContinue()) yield break;
        State = WaveRunState.Completed;
        waveRoutine = null;
        UnsubscribeHealth();
        if (menu != null) menu.ShowVictory();
        else { Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        OnAllWavesCompleted?.Invoke();
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
            return false;
        }
        return true;
    }

    private bool SpawnEnemy(GameObject prefab, EnemyWave wave)
    {
        if (!TrySpawnPosition(prefab, wave, out Vector3 position, out Quaternion rotation))
        {
            Fail($"Oleada {CurrentWaveNumber}: no se encontró un punto libre en el NavMesh para {prefab.name}. Revisá los puntos y el radio de aparición.");
            return false;
        }
        GameObject instance = Instantiate(prefab, position, rotation);
        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (!agent.isOnNavMesh)
        {
            Destroy(instance);
            Fail($"Oleada {CurrentWaveNumber}: {prefab.name} no pudo colocarse sobre el NavMesh.");
            return false;
        }
        EnemyHealth health = instance.GetComponent<EnemyHealth>();
        if (!health.IsDead)
        {
            Action onDeath = () => RemoveTrackedEnemy(health);
            aliveEnemies.Add(health, onDeath);
            health.OnDeath += onDeath;
        }
        SpawnedInCurrentWave++;
        OnEnemySpawned?.Invoke(health);
        return true;
    }

    private bool TrySpawnPosition(GameObject prefab, EnemyWave wave, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = transform.rotation;
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        List<Transform> points = wave.spawnPoints;
        var path = new NavMeshPath();
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
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, Mathf.Max(.1f, runningSampleDistance), filter)) continue;
            // La dispersión no debe saltar a un techo o isla desconectada del punto configurado.
            if (!NavMesh.SamplePosition(point.position, out NavMeshHit anchor, Mathf.Max(.1f, runningSampleDistance), filter)
                || !NavMesh.CalculatePath(anchor.position, hit.position, filter, path)
                || path.status != NavMeshPathStatus.PathComplete) continue;
            Vector3 rootPosition = hit.position + Vector3.up * agent.baseOffset;
            bool occupied = false;
            foreach (EnemyHealth other in aliveEnemies.Keys)
            {
                if (other == null || other.IsDead) continue;
                NavMeshAgent otherAgent = other.GetComponent<NavMeshAgent>();
                float separation = agent.radius + (otherAgent != null ? otherAgent.radius : .5f);
                if ((other.transform.position - rootPosition).sqrMagnitude < separation * separation) { occupied = true; break; }
            }
            if (occupied) continue;
            position = rootPosition;
            rotation = point.rotation;
            return true;
        }
        return false;
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

    public void CancelWaves()
    {
        if (!IsRunning) return;
        State = WaveRunState.Cancelled;
        if (waveRoutine != null) StopCoroutine(waveRoutine);
        waveRoutine = null;
        ClearTrackedEnemies();
        UnsubscribeHealth();
    }

    private void Fail(string error)
    {
        State = WaveRunState.Failed;
        LastError = error;
        ClearTrackedEnemies();
        UnsubscribeHealth();
        Debug.LogError($"EnemySpawner: {error}", this);
    }
}
