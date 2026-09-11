using System.Collections;
using UnityEngine;

public class EnemyChasingRanged : EnemyBaseController
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    protected override void Start()
    {
        detectionRange = 9999f;

        moveSpeed = 4.0f;
        attackRange = 10f; 
        attackCooldown = 2.0f;
        attackWindupTime = 0.4f;

        base.Start();

        currentState = EnemyState.Chasing;
    }

    protected override void ExecuteChasingBehavior()
    {
        base.ExecuteChasingBehavior();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
        }
    }

    protected override IEnumerator AttackRoutine()
    {
        isAttacking = true;

        if (animator != null) animator.SetTrigger("Attack");

        yield return new WaitForSeconds(attackWindupTime);

        if (player != null && playerHealth != null && !playerHealth.IsDead)
        {
            if (projectilePrefab != null && firePoint != null)
            {
                Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            }
            else
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }

        isAttacking = false;
        yield return null;
    }
}