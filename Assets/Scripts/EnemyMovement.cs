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

    [Header("Range Configuration")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("Attack Settings")]
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackCooldown = 1.2f;
    private float lastAttackTime;
    private int currentPatrolIndex = 0;

    private NavMeshAgent agent;
    private PlayerHealth playerHealth;

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
        if (patrolPoints.Count > 0)
        {
            SetNextPatrolDestination();
        }
    }

    private void Update()
    {
        if (player == null || (playerHealth != null && playerHealth.IsDead))
        {
            // Si el jugador no existe o ya murió, vuelve a patrullar
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
        if (patrolPoints.Count == 0) return;

        agent.isStopped = false;


        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            SetNextPatrolDestination();
        }
    }

    private void SetNextPatrolDestination()
    {
        if (patrolPoints[currentPatrolIndex] != null)
        {
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }

    private void ChaseBehavior()
    {
        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    private void AttackBehavior()
    {

        agent.isStopped = true;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Debug.Log("¡Enemigo atacando!");
            lastAttackTime = Time.time;
            // Aca iria la animación de ataque o daño
        }
    }

    private void PerformAttack()
    {
        if (playerHealth != null && !playerHealth.IsDead)
        {
            playerHealth.TakeDamage(attackDamage);

            // Si se usa Animator:
            // GetComponent<Animator>()?.SetTrigger("Attack");
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