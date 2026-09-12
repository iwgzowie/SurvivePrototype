using System.Collections;
using UnityEngine;

public class EnemyAttack : EnemyBaseController
{
    [Header("Ranged Setup")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    protected override void Start()
    {
        moveSpeed = 2.5f;
        detectionRange = 12f;
        attackRange = 10f;
        attackCooldown = 2.5f;

        useRandomPatrol = true;
        randomPatrolRadius = 6f;

        base.Start();
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
