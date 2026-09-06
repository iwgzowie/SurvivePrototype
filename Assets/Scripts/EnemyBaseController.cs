using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static EnemyAI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBaseController : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    // Referencias base
    protected GameObject player;
    protected PlayerHealth playerHealth;
    protected NavMeshAgent agent;
    protected Animator animator;

    // Equipamiento
    [SerializeField] protected GameObject weaponEquip;

    // Estadísticas Base
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected float attackDamage = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackCooldown = 1.5f;
    [SerializeField] protected float attackWindupTime = 0.5f;
    [SerializeField] protected float hitTolerance = 0.5f;

    protected float currentHealth;
    protected float lastAttackTime = 0f;

    // Estados
    protected bool isDead = false;
    protected bool isAttacking = false;

    // Detectar bloqueos
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    [SerializeField] private float stuckCheckInterval = 1f;

    [Header("Target & Patrol")]
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();
    [SerializeField] private float waitTimeAtPoint = 1.5f;
    [SerializeField] private Transform Player;

    [Header("Range Configuration")]
    [SerializeField] private float detectionRange = 6f;

    // Variables privadas de control
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private Coroutine attackCoroutine;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    protected virtual void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        currentHealth = maxHealth;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange;
        }

        lastPosition = transform.position;
    }

    protected virtual void OnEnable()
    {
        isDead = false;
        isAttacking = false;
        currentHealth = maxHealth;

        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }
    }

    protected virtual void Update()
    {
        if (isDead) return;

        UpdateAnimator();

        // Si el jugador no existe o ya murió, volver a patrulla
        bool playerDead = (playerHealth != null && playerHealth.IsDead);
        if (player == null || playerDead)
        {
            CancelCurrentAttack();
            currentState = EnemyState.Idle;
            PatrolBehavior();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        CheckStateTransitions(distanceToPlayer);

        switch (currentState)
        {
            case EnemyState.Idle:
                PatrolBehavior();
                break;

            case EnemyState.Chasing:
                ChaseBehavior();
                CheckIsStuck();
                break;

            case EnemyState.Attack:
                AttackBehavior();
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

    private void PatrolBehavior()
    {
        if (patrolPoints.Count == 0 || !agent.isOnNavMesh) return;

        if (isWaiting)
        {
            agent.isStopped = true;
            waitTimer += Time.deltaTime;

            if (waitTimer >= waitTimeAtPoint)
            {
                isWaiting = false;
                waitTimer = 0f;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
                SetNextPatrolDestination();
            }
            return;
        }

        agent.isStopped = false;

        Transform targetPoint = patrolPoints[currentPatrolIndex];
        if (targetPoint != null)
        {
            float distanceToPoint = Vector3.Distance(transform.position, targetPoint.position);
            if (distanceToPoint <= agent.stoppingDistance + 0.5f)
            {
                isWaiting = true;
            }
        }
    }

    private void SetNextPatrolDestination()
    {
        if (patrolPoints.Count > 0 && patrolPoints[currentPatrolIndex] != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }

    private void ChaseBehavior()
    {
        if (!agent.isOnNavMesh) return;

        isWaiting = false;
        waitTimer = 0f;
        agent.isStopped = false;
        agent.SetDestination(player.transform.position);
    }

    private void AttackBehavior()
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

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        // Tiempo de anticipación/viento antes de impactar
        yield return new WaitForSeconds(attackWindupTime);

        // Validación de daño
        if (player != null && playerHealth != null && !playerHealth.IsDead)
        {
            float currentDistance = Vector3.Distance(transform.position, player.transform.position);

            if (currentDistance <= attackRange + hitTolerance)
            {
                playerHealth.TakeDamage(attackDamage);
            }
            else
            {
                Debug.Log("¡El jugador esquivó el ataque!");
            }
        }

        isAttacking = false;
        attackCoroutine = null;
    }

    public virtual void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;

        if (animator != null)
        {
            animator.SetTrigger("GetHit");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public virtual void Die()
    {
        isDead = true;
        CancelCurrentAttack();

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
    }

    private void CancelCurrentAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        isAttacking = false;
    }

    protected virtual void UpdateAnimator()
    {
        if (animator != null && agent != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
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