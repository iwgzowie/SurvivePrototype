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
    private Collider playerCollider;

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

    [Header("Obstrucciones del ataque a distancia")]
    [SerializeField] protected LayerMask attackObstructionMask = ~0;
    [SerializeField, Min(0.001f)] protected float rangedOriginClearanceRadius = 0.025f;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] protected float attackAnimationImpactTime = 0.5f;
    [SerializeField, Min(0.1f)] protected float animationRunSpeed = 2.32f;

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
        CancelCurrentAttack();
    }

    protected virtual void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerCollider = player.GetComponent<Collider>();
        }

        if (agent != null)
        {
            agent.speed = moveSpeed;
        }

        lastPosition = transform.position;

        if (agent != null && agent.isOnNavMesh)
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

        if (distanceToPlayer <= attackRange && CanStartAttack())
        {
            currentState = EnemyState.Attack;
        }
        else if (distanceToPlayer <= Mathf.Max(detectionRange, attackRange))
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

        if (!isAttacking && !IsAttackAnimationPlaying() && CanStartAttack()
            && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            attackCoroutine = StartCoroutine(AttackRoutine());
        }
    }
    private bool IsAttackAnimationPlaying()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || animator.layerCount == 0)
            return false;

        // Espera la recuperación del gesto para no aplicar otro golpe durante el mismo clip.
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")) return true;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Attack");
    }

    protected virtual bool CanStartAttack()
    {
        return player != null && playerHealth != null && !playerHealth.IsDead
            && Vector3.Distance(transform.position, player.transform.position) <= attackRange;
    }

    protected bool CanHitRangedTarget(Transform firePoint)
    {
        if (player == null || playerHealth == null || playerHealth.IsDead
            || (enemyHealth != null && enemyHealth.IsDead)
            || Vector3.Distance(transform.position, player.transform.position) > attackRange)
            return false;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Vector3 target = GetRangedAimPoint();
        Vector3 direction = target - origin;

        // Un raycast no informa el collider que contiene su origen. Revisar la boca
        // evita que un enemigo pegado a una pared dispare desde dentro de ella.
        foreach (Collider overlap in Physics.OverlapSphere(origin,
            Mathf.Max(0.001f, rangedOriginClearanceRadius), attackObstructionMask,
            QueryTriggerInteraction.Ignore))
        {
            if (!IsAttackColliderIgnored(overlap)) return false;
        }

        float distance = direction.magnitude;
        if (distance <= 0.001f) return true;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction / distance,
            distance, attackObstructionMask, QueryTriggerInteraction.Ignore))
        {
            if (!IsAttackColliderIgnored(hit.collider)) return false;
        }
        return true;
    }

    private bool IsAttackColliderIgnored(Collider candidate)
    {
        return candidate == null || candidate.transform.IsChildOf(transform)
            || (player != null && candidate.transform.IsChildOf(player.transform));
    }

    private Vector3 GetRangedAimPoint()
    {
        return playerCollider != null && playerCollider.enabled
            ? playerCollider.bounds.center
            : player.transform.position;
    }

    protected IEnumerator RangedAttackRoutine(GameObject projectilePrefab, Transform firePoint)
    {
        if (!CanHitRangedTarget(firePoint)) yield break;
        isAttacking = true;
        if (animator != null) animator.SetTrigger("Attack");

        yield return new WaitForSeconds(attackWindupTime);

        // La preparación no garantiza el impacto: el jugador puede alejarse
        // o ponerse a cubierto antes de que termine la animación.
        if (CanHitRangedTarget(firePoint))
        {
            if (projectilePrefab != null)
            {
                Vector3 origin = firePoint != null ? firePoint.position : transform.position;
                Vector3 direction = GetRangedAimPoint() - origin;
                Quaternion rotation = direction.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(direction.normalized)
                    : transform.rotation;
                Instantiate(projectilePrefab, origin, rotation);
            }
            else
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }

        isAttacking = false;
        attackCoroutine = null;
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
        else
        {
            // Se pueden editar los puntos por instancia; las entradas vacías
            // no interrumpen una ruta que todavía tiene destinos válidos.
            for (int checkedPoints = 0; checkedPoints < patrolPoints.Count; checkedPoints++)
            {
                currentPatrolIndex %= patrolPoints.Count;
                Transform point = patrolPoints[currentPatrolIndex];
                if (point != null)
                {
                    agent.isStopped = false;
                    agent.SetDestination(point.position);
                    return;
                }
                currentPatrolIndex++;
            }
            agent.ResetPath();
            agent.isStopped = true;
        }
    }
    protected virtual void UpdateAnimator()
    {
        if (animator != null && agent != null)
        {
            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
            // Mantiene la cadencia de los pasos cuando el agente supera la velocidad nativa del clip.
            animator.SetFloat("LocomotionSpeed", Mathf.Max(1f, speed / Mathf.Max(.1f, animationRunSpeed)));
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