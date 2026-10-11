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

    public bool SpawnBoss(float healthMultiplier, float damageMultiplier, float speedMultiplier)
    {
        if (bossPrefab == null)
        {
            Debug.LogError("BossSpawner: No hay ningún prefab de jefe asignado.", this);
            return false;
        }

        Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;
        Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.rotation : transform.rotation;

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject bossInstance = Instantiate(bossPrefab, spawnPos, spawnRot);

        EnemyHealth health = bossInstance.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.ApplyWaveMultipliers(healthMultiplier);
            currentBossHealth = health;
            health.OnDeath += HandleBossDeath;
        }

        var controller = bossInstance.GetComponent<EnemyBaseController>();
        if (controller != null)
        {
            controller.ApplyWaveMultipliers(damageMultiplier, speedMultiplier);
        }

        OnBossSpawned?.Invoke(health);
        return true;
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