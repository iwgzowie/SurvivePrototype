using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class BossBase : MonoBehaviour
{
    [Header("Pruebas / Comportamiento")]
    [SerializeField] private bool stayStaticForTesting = false;

    [Header("Referencias")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private GameObject minionPrefab;

    [Header("Estadisticas de Ataque")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float areaAttackRadius = 6f;
    [SerializeField] private float areaAttackDamage = 25f;
    [SerializeField] private float areaAttackCooldown = 8f;

    [Header("Invocacion de Esbirros")]
    [SerializeField] private int minionsToSpawn = 2;
    [SerializeField] private float summonCooldown = 12f;

    [Header("Fase de Furia")]
    [SerializeField] private float rageHealthThreshold = 0.5f;
    [SerializeField] private float rageSpeedMultiplier = 1.35f;

    private NavMeshAgent navAgent;
    private EnemyHealth healthComponent;
    private bool isEnraged = false;
    private float nextAreaAttackTime;
    private float nextSummonTime;

    // Eventos para UI y Sistemas de Recompensas
    public static event Action<BossBase> OnBossSpawnedUI;
    public static event Action OnBossDefeatedUI;

    public EnemyHealth Health => healthComponent;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        healthComponent = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        FindPlayer();

        OnBossSpawnedUI?.Invoke(this);

        if (healthComponent != null)
        {
            healthComponent.OnDeath += HandleBossDeath;
        }

        nextAreaAttackTime = Time.time + 3f;
        nextSummonTime = Time.time + 5f;
    }

    private void Update()
    {
        if (healthComponent == null || healthComponent.IsDead) return;

        CheckRagePhase();

        if (stayStaticForTesting) return;

        MoveTowardsPlayer();
        HandleAttacks();
    }

    private void FindPlayer()
    {
        if (targetPlayer == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) targetPlayer = playerObj.transform;
        }
    }

    private void MoveTowardsPlayer()
    {
        if (targetPlayer != null && navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(targetPlayer.position);
        }
    }

    private void CheckRagePhase()
    {
        if (!isEnraged && healthComponent != null)
        {
            float healthRatio = healthComponent.CurrentHealth / healthComponent.MaxHealth;
            if (healthRatio <= rageHealthThreshold)
            {
                EnterRagePhase();
            }
        }
    }

    private void EnterRagePhase()
    {
        isEnraged = true;
        Debug.Log("¡EL BOSS ENTRO EN FASE DE FURIA!");

        if (navAgent != null)
        {
            navAgent.speed *= rageSpeedMultiplier;
        }

        areaAttackCooldown /= 1.5f;
        summonCooldown /= 1.5f;
    }

    private void HandleAttacks()
    {
        if (targetPlayer == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // Ataque Cuerpo a Cuerpo Básico (utilizando 'attackRange')
        if (distanceToPlayer <= attackRange)
        {
        }

        // Ataque en Área
        if (distanceToPlayer <= areaAttackRadius && Time.time >= nextAreaAttackTime)
        {
            ExecuteAreaAttack();
            nextAreaAttackTime = Time.time + areaAttackCooldown;
        }

        // Invocación de Esbirros
        if (Time.time >= nextSummonTime)
        {
            SummonMinions();
            nextSummonTime = Time.time + summonCooldown;
        }
    }

    private void ExecuteAreaAttack()
    {
        Debug.Log("¡Boss ejecuta Ataque en Área!");
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, areaAttackRadius);
        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerHealth pHealth = hit.GetComponent<PlayerHealth>();
                if (pHealth != null)
                {
                    pHealth.TakeDamage(areaAttackDamage);
                }
            }
        }
    }

    private void SummonMinions()
    {
        if (minionPrefab == null) return;

        Debug.Log("¡Boss invocando esbirros!");
        for (int i = 0; i < minionsToSpawn; i++)
        {
            Vector3 spawnOffset = UnityEngine.Random.insideUnitSphere * 3f;
            spawnOffset.y = 0;
            Vector3 spawnPos = transform.position + spawnOffset;

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Instantiate(minionPrefab, hit.position, Quaternion.identity);
            }
        }
    }

    private void HandleBossDeath()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= HandleBossDeath;
        }

        OnBossDefeatedUI?.Invoke();

        GrantBossRewards();

        Destroy(gameObject, 0.2f);
    }

    private void GrantBossRewards()
    {
        Debug.Log("¡Recompensas otorgadas al jugador!");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, areaAttackRadius);
    }
}
