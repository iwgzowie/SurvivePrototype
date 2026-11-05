using UnityEngine;
using UnityEngine.AI;

/// <summary>Recupera enemigos aislados; el boss sólo recalcula su ruta, sin teletransportarse durante el combate.</summary>
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public sealed class EnemyReachability : MonoBehaviour
{
    NavMeshAgent agent;
    EnemyHealth health;
    bool isBoss;
    Transform player;
    float nextCheck, blockedSince = -1, stalledSince = -1;
    Vector3 lastPosition;
    public int Recoveries { get; private set; }
    public int RouteRetries { get; private set; }

    public static void Track(GameObject enemy, Transform target)
    {
        if (enemy.GetComponent<NavMeshAgent>() == null || enemy.GetComponent<EnemyHealth>() == null) return;
        var tracker = enemy.GetComponent<EnemyReachability>();
        if (tracker == null) tracker = enemy.AddComponent<EnemyReachability>();
        tracker.player = target;
        if (enemy.TryGetComponent<EnemyBaseController>(out var controller)) controller.PursuePlayer();
    }
    void Awake() { agent = GetComponent<NavMeshAgent>(); health = GetComponent<EnemyHealth>(); isBoss = GetComponent<BossBase>() != null; lastPosition = transform.position; }
    void Update()
    {
        if (health.IsDead || !agent.enabled || Time.timeScale <= 0 || Time.time < nextCheck) return;
        nextCheck = Time.time + 1.5f;
        if (player == null) return;
        // Saltar o salir momentáneamente del NavMesh no debe provocar teletransportes.
        if (!NavMesh.SamplePosition(player.position, out var target, 4, EnemyNavigation.Filter(agent)))
        { ResetTimers(); return; }
        if (agent.isOnNavMesh && agent.isOnOffMeshLink)
        { ResetTimers(); return; }
        if (agent.isOnNavMesh && agent.pathPending)
        { stalledSince = -1; lastPosition = transform.position; return; }
        bool reachable = agent.isOnNavMesh && EnemyNavigation.CanReach(agent, transform.position - Vector3.up * agent.baseOffset, player);
        if (reachable) blockedSince = -1;
        else if (blockedSince < 0) blockedSince = Time.time;
        bool wantsToMove = agent.isOnNavMesh && !agent.isStopped && !agent.pathPending && agent.hasPath
            && agent.remainingDistance > Mathf.Max(2.5f, agent.stoppingDistance + 1);
        if (!wantsToMove || Vector3.Distance(lastPosition, transform.position) > .15f) stalledSince = -1;
        else if (stalledSince < 0) stalledSince = Time.time;
        lastPosition = transform.position;
        if ((blockedSince < 0 || Time.time - blockedSince < 3f) && (stalledSince < 0 || Time.time - stalledSince < 6f)) return;
        if (isBoss)
        {
            // Una puerta, un atasco o un recálculo transitorio no deben cambiar de lugar al jefe.
            if (agent.isOnNavMesh)
            {
                agent.ResetPath(); agent.isStopped = false;
                if (agent.SetDestination(target.position)) RouteRetries++;
            }
            ResetTimers(); return;
        }
        if (!EnemyNavigation.TryNearPlayer(agent, player, out var position) || !agent.Warp(position)) return;
        agent.isStopped = false;
        agent.SetDestination(player.position);
        Recoveries++; ResetTimers();
    }
    void ResetTimers() { blockedSince = stalledSince = -1; lastPosition = transform.position; }
}
