using UnityEngine;

public class EnemyChasing : EnemyBaseController
{
    protected override void Start()
    {
        detectionRange = 9999f;
        moveSpeed = 5.0f;
        attackRange = 1.8f;
        attackCooldown = 0.8f;

        base.Start();

        currentState = EnemyState.Chasing;
    }

    protected override void ExecuteChasingBehavior()
    {
        base.ExecuteChasingBehavior();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed; //* 1.2f;
        }
    }
}
