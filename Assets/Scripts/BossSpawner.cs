using System;
using UnityEngine;
using UnityEngine.AI;

public class BossSpawner : MonoBehaviour
{
    [Header("Configuración del Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 3f;

    public event Action<EnemyHealth> OnBossSpawned;
    public event Action OnBossDefeated;

    private EnemyHealth currentBossHealth;

    public bool SpawnBoss(float healthMultiplier, float damageMultiplier, float speedMultiplier, Transform player = null)
    {
        if (currentBossHealth != null && !currentBossHealth.IsDead) return false;
        if (bossPrefab == null || bossPrefab.GetComponent<EnemyHealth>() == null
            || bossPrefab.GetComponent<BossBase>() == null || bossPrefab.GetComponent<NavMeshAgent>() == null)
        {
            Debug.LogError("BossSpawner: Asigná un prefab con BossBase, EnemyHealth y NavMeshAgent.", this);
            return false;
        }
        var templateAgent = bossPrefab.GetComponent<NavMeshAgent>();
        Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;
        Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.rotation : transform.rotation;
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (!EnemyNavigation.TryPosition(templateAgent, spawnPos, player, navMeshSampleDistance, out var position)
            && !EnemyNavigation.TryNearPlayer(templateAgent, player, out position))
        {
            Debug.LogError("BossSpawner: No hay una posición libre desde la que el jefe pueda llegar al jugador.", this);
            return false;
        }
        GameObject bossInstance = Instantiate(bossPrefab, position, spawnRot);
        var agent = bossInstance.GetComponent<NavMeshAgent>();
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            bossInstance.SetActive(false);
            Destroy(bossInstance);
            Debug.LogError("BossSpawner: El jefe no pudo vincularse a su NavMesh.", this);
            return false;
        }
        EnemyHealth health = bossInstance.GetComponent<EnemyHealth>();
        health.ApplyWaveMultipliers(healthMultiplier);
        bossInstance.GetComponent<BossBase>().ApplyWaveMultipliers(damageMultiplier, speedMultiplier);
        currentBossHealth = health;
        health.OnDeath += HandleBossDeath;
        EnemyReachability.Track(bossInstance, player);

        OnBossSpawned?.Invoke(health);
        return true;
    }

    private void OnDestroy()
    {
        if (currentBossHealth != null) currentBossHealth.OnDeath -= HandleBossDeath;
    }

    private void HandleBossDeath()
    {
        if (currentBossHealth != null)
        {
            currentBossHealth.OnDeath -= HandleBossDeath;
            currentBossHealth = null;
        }

        OnBossDefeated?.Invoke();
    }
}
