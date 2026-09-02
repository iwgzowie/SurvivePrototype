using System.Collections;
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
    [SerializeField] private float attackWindupTime = 0.4f; // Tiempo de aviso/preparación
    [SerializeField] private float hitTolerance = 0.5f;     // Margen de tolerancia al impactar

    // Variables privadas de control
    private NavMeshAgent agent;
    private PlayerHealth playerHealth;
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private float lastAttackTime;
    private bool isAttacking = false;
    private Coroutine attackCoroutine;

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
        // Cancelar ataque si el jugador no existe o muere
        if (player == null || (playerHealth != null && playerHealth.IsDead))
        {
            CancelCurrentAttack();
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
        // Si acaba de salir de persecución o ataque y vuelve a patrullar
        if (previousState != EnemyState.Idle && currentState == EnemyState.Idle)
        {
            CancelCurrentAttack();
            isWaiting = false;
            waitTimer = 0f;
            SetNextPatrolDestination(); // <-- Reasigna la ruta hacia el punto de patrulla
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

    private void SetClosestPatrolDestination()
    {
        if (patrolPoints.Count == 0 || !agent.isOnNavMesh) return;

        int closestIndex = 0;
        float minDistance = Mathf.Infinity;

        for (int i = 0; i < patrolPoints.Count; i++)
        {
            if (patrolPoints[i] == null) continue;
            float dist = Vector3.Distance(transform.position, patrolPoints[i].position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestIndex = i;
            }
        }

        currentPatrolIndex = closestIndex;
        SetNextPatrolDestination();
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

        if (!isAttacking && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            attackCoroutine = StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        // Preparación / Anticipación
        Debug.Log("¡Enemigo cargando ataque!");
        // GetComponent<Animator>()?.SetTrigger("Attack");

        yield return new WaitForSeconds(attackWindupTime);

        // Validación de rango al momento de impactar
        if (player != null && playerHealth != null && !playerHealth.IsDead)
        {
            float currentDistance = Vector3.Distance(transform.position, player.position);

            if (currentDistance <= attackRange + hitTolerance)
            {
                Debug.Log("¡Impacto conectado!");
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

    private void CancelCurrentAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        isAttacking = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}