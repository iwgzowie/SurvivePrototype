using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyBaseController : MonoBehaviour
{
    //State
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;

    // Referencias base
    protected GameObject player;
    protected PlayerHealth playerHealth;
    protected NavMeshAgent agent;
    protected Animator animator;
    protected EnemyHealth enemyHealth;

    // Equipamiento
    [SerializeField] protected GameObject weaponEquip;

    // Estadísticas Base
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected float attackDamage = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackCooldown = 1.5f;
    [SerializeField] protected float attackWindupTime = 0.5f;
    [SerializeField] protected float hitTolerance = 0.5f;
    [SerializeField] protected float detectionRange = 6f;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] protected float attackAnimationImpactTime = 0.5f;

    protected float lastAttackTime = 0f;
    protected bool isAttacking = false;

    //Config Patrol
    [SerializeField] protected List<Transform> patrolPoints = new List<Transform>();
    [SerializeField] protected float waitTimeAtPoint = 1.5f;

    //Random Patrol Settings
    [SerializeField] protected bool useRandomPatrol = true;
    [SerializeField] protected float randomPatrolRadius = 5f;

    //Control Interno Patrol
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private Coroutine attackCoroutine;

    // Detectar bloqueos
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    [SerializeField] private float stuckCheckInterval = 1f;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        enemyHealth = GetComponent<EnemyHealth>();
    }

    protected virtual void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleDeath;

        isAttacking = false;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
    }

    protected virtual void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleDeath;
    }

    protected virtual void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = 0.2f;
        }

        lastPosition = transform.position;

        if (patrolPoints.Count > 0 && agent.isOnNavMesh)
        {
            SetNextPatrolDestination();
        }
    }

    protected virtual void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;
        
        UpdateAnimator();

        bool playerDead = (playerHealth != null && playerHealth.IsDead);
        if (player == null || playerDead)
        {
            CancelCurrentAttack();
            currentState = EnemyState.Idle;
            ExecuteIdleBehavior();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        CheckStateTransitions(distanceToPlayer);

        switch (currentState)
        {
            case EnemyState.Idle:
                ExecuteIdleBehavior();
                break;

            case EnemyState.Chasing:
                ExecuteChasingBehavior();
                CheckIsStuck();
                break;

            case EnemyState.Attack:
                ExecuteAttackBehavior();
                break;
        }
    }

    private void CheckStateTransitions(float distanceToPlayer)
    {
        EnemyState previousState = currentState;

        if (distanceToPlayer <= attackRange)
        {
            currentState = EnemyState.Attack;
        }
        else if (distanceToPlayer <= detectionRange)
        {
            currentState = EnemyState.Chasing;
        }
        else
        {
            currentState = EnemyState.Idle;
        }

        if (previousState != EnemyState.Idle && currentState == EnemyState.Idle)
        {
            CancelCurrentAttack();
            isWaiting = false;
            waitTimer = 0f;
            SetNextPatrolDestination();
        }
    }

    protected Vector3 GetRandomNavMeshPoint(Vector3 center, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += center;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return center;
    }

    //Comportamientos Virtuales Overrideables
    protected virtual void ExecuteIdleBehavior()
    {
        if (!agent.isOnNavMesh) return;

        if (isWaiting)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= waitTimeAtPoint)
            {
                isWaiting = false;
                waitTimer = 0f;

                if (!useRandomPatrol && patrolPoints.Count > 0)
                {
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
                }

                SetNextPatrolDestination();
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= 0.8f)
        {
            isWaiting = true;
            agent.isStopped = true;
        }
    }
    protected virtual void ExecuteChasingBehavior()
    {
        if (!agent.isOnNavMesh) return;

        isWaiting = false;
        waitTimer = 0f;
        agent.isStopped = false;
        agent.SetDestination(player.transform.position);
    }
    protected virtual void ExecuteAttackBehavior()
    {
        if (!agent.isOnNavMesh) return;

        agent.isStopped = true;

        Vector3 direction = (player.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
        }

        if (!isAttacking && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            attackCoroutine = StartCoroutine(AttackRoutine());
        }
    }
    protected virtual IEnumerator AttackRoutine()
    {
        isAttacking = true;

        if (animator != null) animator.SetTrigger("Attack");

        yield return new WaitForSeconds(attackWindupTime);

        if (player != null && playerHealth != null && !playerHealth.IsDead)
        {
            float currentDistance = Vector3.Distance(transform.position, player.transform.position);
            if (currentDistance <= attackRange + hitTolerance)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }

        isAttacking = false;
        attackCoroutine = null;
    }

    protected virtual void HandleDeath()
    {
        CancelCurrentAttack();
        
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
    }
    protected void CancelCurrentAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        isAttacking = false;
    }

    protected virtual void SetNextPatrolDestination()
    {
        if (!agent.isOnNavMesh) return;

        if (useRandomPatrol)
        {
            agent.isStopped = false;
            Vector3 newDestination = GetRandomNavMeshPoint(transform.position, randomPatrolRadius);
            agent.SetDestination(newDestination);
        }
        else if (patrolPoints.Count > 0 && patrolPoints[currentPatrolIndex] != null)
        {
            agent.isStopped = false;
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }
    protected virtual void UpdateAnimator()
    {
        if (animator != null && agent != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
            // El gesto y sus eventos acompañan al windup configurado por cada variante.
            animator.SetFloat("AttackSpeed", attackAnimationImpactTime / Mathf.Max(.01f, attackWindupTime));
        }
    }

    protected virtual void CheckIsStuck()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer >= stuckCheckInterval)
        {
            if (agent.hasPath && Vector3.Distance(transform.position, lastPosition) < 0.2f)
            {
                agent.ResetPath();
                if (player != null)
                {
                    agent.SetDestination(player.transform.position);
                }
            }

            lastPosition = transform.position;
            stuckTimer = 0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}