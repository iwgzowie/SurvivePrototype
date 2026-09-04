using UnityEngine;
using UnityEngine.AI;

public class EnemyBaseController : MonoBehaviour

{
    //Referencias base
    protected GameObject player;
    protected NavMeshAgent agent;
    protected Animator animator;

    //Equipamiento
    [SerializeField] protected GameObject weaponEquip;

    //Estadisticas Base
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected float attackDamage = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackCooldown = 1.5f;
    protected float currentHealth;
    protected float lastAttackTime = 0f;

    //Estados
    protected bool isDead = false;
    protected bool isAttacking = false;

    //Detectar bloqueos
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    [SerializeField] private float stuckCheckInterval = 1f;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }
    protected virtual void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        currentHealth = maxHealth;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange;
        }

        lastPosition = transform.position;
    }

    protected virtual void Update()
    {
        if (isDead || player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (distanceToPlayer <= attackRange)
        {
            StopAndAttack();
        }
        else
        {
            ChasePlayer();
            CheckIsStuck();
        }

        UpdateAnimator();
    }

    protected virtual void ChasePlayer()
    {
        if (agent.isStopped) agent.isStopped = false;
        agent.SetDestination(player.transform.position);
        isAttacking = false;
    }

    protected virtual void StopAndAttack()
    {
        agent.isStopped = true;

        Vector3 direction = (player.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
        }

        if (Time.time >= lastAttackTime + attackCooldown) 
        {
            PerformAttack();
            lastAttackTime = Time.time;
        }
    }

    protected virtual void PerformAttack()
    {
        isAttacking = true;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        else
        {
            Debug.Log("Hook de attack ejecutado, pero no hay animator asignado.");
        }
    }

    protected virtual void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }
    }

    public virtual void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        
        if(animator != null)
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

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
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
    protected virtual void CheckIsStuck()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer >= stuckCheckInterval)
        {
            if (agent.hasPath && Vector3.Distance(transform.position, lastPosition) < 0.2f)
            {
                agent.ResetPath();
                agent.SetDestination(player.transform.position);
            }

            lastPosition = transform.position;
            stuckTimer = 0f;
        }
    }
}
