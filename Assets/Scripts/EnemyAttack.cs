using System.Collections;
using UnityEngine;

public class EnemyAttack : EnemyBaseController
{
    [Header("Ataque a distancia")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    protected override bool CanStartAttack()
    {
        return CanHitRangedTarget(firePoint);
    }

    protected override IEnumerator AttackRoutine()
    {
        return RangedAttackRoutine(projectilePrefab, firePoint);
    }
}
