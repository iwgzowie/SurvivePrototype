using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BossBase : MonoBehaviour
{
    [Header("Pruebas / Comportamiento")]
    [SerializeField] private bool stayStaticForTesting = false;

    [Header("Referencias")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private GameObject minionPrefab;

    [Header("Datos de Visualización UI")]
    [SerializeField] private string bossName = "Guardián Ancestral";

    [Header("Estadísticas de Ataque")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float areaAttackRadius = 6f;
    [SerializeField] private float areaAttackDamage = 25f;
    [SerializeField] private float areaAttackCooldown = 8f;

    [Header("Invocación de Esbirros")]
    [SerializeField] private int minionsToSpawn = 2;
    [SerializeField] private float summonCooldown = 12f;

    [Header("Fase de Furia")]
    [SerializeField] private float rageHealthThreshold = 0.5f;
    [SerializeField] private float rageSpeedMultiplier = 1.35f;

    [Header("Drops de Botín")]
    [Tooltip("La llave que dropea sí o sí al 100%")]
    [SerializeField] private GameObject guaranteedKeyPrefab;
    [SerializeField] private GameObject doorBlueprintPrefab;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int minCoins = 5;
    [SerializeField] private int maxCoins = 10;
    [SerializeField] private GameObject healthPotionPrefab;
    [Range(0f, 1f)][SerializeField] private float potionDropChance = 0.75f;
    [SerializeField] private int maxPotions = 2;
    [SerializeField] private float dropScatterRadius = 2f;

    [Header("Recompensa Directa de Victoria")]
    
    [SerializeField] private int directGoldReward = 150;

    private NavMeshAgent navAgent;
    private EnemyHealth healthComponent;
    private bool isEnraged = false;
    private float nextAreaAttackTime;
    private float nextSummonTime;
    private float minionSpeedMultiplier = 1f;

    
    public static event Action<BossBase> OnBossSpawnedUI;
    public static event Action OnBossDefeatedUI;

    public EnemyHealth Health => healthComponent;
    public string BossName => bossName;
    public int DirectGoldReward => Mathf.Max(0, directGoldReward);
    private Animator animator;

    public void ApplyWaveMultipliers(float damage, float speed)
    {
        minionSpeedMultiplier = Mathf.Max(.01f, speed);
        areaAttackDamage *= damage;
        if (navAgent != null) navAgent.speed *= speed;
    }

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        healthComponent = GetComponent<EnemyHealth>();
        animator = GetComponentInChildren<Animator>();
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

        if (animator != null) animator.SetFloat("Speed", navAgent != null && navAgent.enabled && navAgent.isOnNavMesh ? navAgent.velocity.magnitude : 0f);
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
        Debug.Log("¡EL BOSS ENTRÓ EN FASE DE FURIA!");

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

        if (distanceToPlayer <= attackRange)
        {
            
        }

        if (distanceToPlayer <= areaAttackRadius && Time.time >= nextAreaAttackTime)
        {
            ExecuteAreaAttack();
            nextAreaAttackTime = Time.time + areaAttackCooldown;
        }

        if (Time.time >= nextSummonTime)
        {
            SummonMinions();
            nextSummonTime = Time.time + summonCooldown;
        }
    }

    private void ExecuteAreaAttack()
    {
        if (animator != null) animator.SetTrigger("Attack");
        var damaged = new System.Collections.Generic.HashSet<PlayerHealth>();
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, areaAttackRadius);
        foreach (var hit in hitColliders)
        {
            PlayerHealth pHealth = hit.GetComponentInParent<PlayerHealth>();
            if (pHealth != null && damaged.Add(pHealth)) pHealth.TakeDamage(areaAttackDamage);
        }
    }

    private void SummonMinions()
    {
        if (minionPrefab == null) return;

        for (int i = 0; i < minionsToSpawn; i++)
        {
            Vector3 spawnOffset = UnityEngine.Random.insideUnitSphere * 3f;
            spawnOffset.y = 0;
            Vector3 spawnPos = transform.position + spawnOffset;

            var agent = minionPrefab.GetComponent<NavMeshAgent>();
            if (EnemyNavigation.TryPosition(agent, spawnPos, targetPlayer, 2f, out var position))
            {
                var minion = Instantiate(minionPrefab, position, Quaternion.identity);
                if (minion.TryGetComponent<EnemyBaseController>(out var controller))
                    controller.ApplyWaveMultipliers(1f, minionSpeedMultiplier);
                minion.GetComponent<EnemyHealth>()?.MarkAsBossMinion();
                EnemyReachability.Track(minion, targetPlayer);
            }
        }
    }

    private void HandleBossDeath()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= HandleBossDeath;
        }

        SpawnLoot();
        

        OnBossDefeatedUI?.Invoke();

        // EnemyHealth conserva el cadáver para la animación o el ragdoll.
    }

    private void OnDestroy()
    {
        if (healthComponent != null) healthComponent.OnDeath -= HandleBossDeath;
    }

    private void SpawnLoot()
    {
        Vector3 basePosition = transform.position - Vector3.up * (navAgent != null ? navAgent.baseOffset : 0f);

       
        if (guaranteedKeyPrefab != null)
        {
            Instantiate(guaranteedKeyPrefab, basePosition + Vector3.up * 0.5f, Quaternion.identity);
        }
        if (doorBlueprintPrefab != null)
            Instantiate(doorBlueprintPrefab, basePosition + transform.right * 1.1f + Vector3.up * .65f, Quaternion.identity);

        
        if (coinPrefab != null)
        {
            int coinCount = UnityEngine.Random.Range(minCoins, maxCoins + 1);
            for (int i = 0; i < coinCount; i++)
            {
                Instantiate(coinPrefab, GetRandomDropPosition(basePosition), Quaternion.identity);
            }
        }

        
        if (healthPotionPrefab != null)
        {
            for (int i = 0; i < maxPotions; i++)
            {
                if (UnityEngine.Random.value <= potionDropChance)
                {
                    Instantiate(healthPotionPrefab, GetRandomDropPosition(basePosition), Quaternion.identity);
                }
            }
        }
    }

    private Vector3 GetRandomDropPosition(Vector3 center)
    {
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * dropScatterRadius;
        Vector3 candidatePos = center + new Vector3(randomCircle.x, 0.3f, randomCircle.y);

        if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            return hit.position + Vector3.up * 0.2f;
        }

        return candidatePos;
    }

  

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, areaAttackRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dropScatterRadius);
    }
}
