using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,      // Patrullando
        Chasing,   // Persiguiendo al jugador
        Attack     // Detenido atacando
    }

    [Header("State")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    [Header("Target & Patrol")]
    [SerializeField] private Transform player;
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();
    [SerializeField] private float waitTimeAtPoint = 1.5f;

    [Header("Range Configuration")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("Attack Settings")]
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackCooldown = 1.2f;

    // Variables privadas de control
    private NavMeshAgent agent;
    private PlayerHealth playerHealth;
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private float lastAttackTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.stoppingDistance = attackRange * 0.8f;
    }

    private void Start()
    {
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (patrolPoints.Count > 0 && agent.isOnNavMesh)
        {
            SetNextPatrolDestination();
        }
    }

    private void Update()
    {
        if (player == null || (playerHealth != null && playerHealth.IsDead))
        {
            currentState = EnemyState.Idle;
            PatrolBehavior();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        CheckStateTransitions(distanceToPlayer);

        switch (currentState)
        {
            case EnemyState.Idle:
                PatrolBehavior();
                break;

            case EnemyState.Chasing:
                ChaseBehavior();
                break;

            case EnemyState.Attack:
                AttackBehavior();
                break;
        }
    }

    private void CheckStateTransitions(float distanceToPlayer)
    {
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
    }

    private void PatrolBehavior()
    {
        if (patrolPoints.Count == 0 || !agent.isOnNavMesh) return;

        // Si está esperando en un punto
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

        // Comprobación por distancia directa al transform objetivo
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
        agent.SetDestination(player.position);
    }

    private void AttackBehavior()
    {
        if (!agent.isOnNavMesh) return;

        agent.isStopped = true;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            PerformAttack();
        }
    }

    private void PerformAttack()
    {
        Debug.Log("¡Enemigo atacando!");
        if (playerHealth != null && !playerHealth.IsDead)
        {
            playerHealth.TakeDamage(attackDamage);
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