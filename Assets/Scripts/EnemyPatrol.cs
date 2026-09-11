using UnityEngine;

public class EnemyPatrol : EnemyBaseController
{
    protected override void Start()
    {
        moveSpeed = 4.5f;
        attackRange = 1.5f;
        detectionRange = 8f;
        attackDamage = 8f;

        useRandomPatrol = true;
        randomPatrolRadius = 12f;

        base.Start();
    }

    protected override void ExecuteIdleBehavior()
    {
        base.ExecuteIdleBehavior();
    }
}
