using UnityEngine;
using UnityEngine.AI;

/// <summary>Consultas del tipo de agente real, con destino en el jugador y barreras físicas.</summary>
public static class EnemyNavigation
{
    public static NavMeshQueryFilter Filter(NavMeshAgent agent) => new NavMeshQueryFilter
    { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

    public static bool CanReach(NavMeshAgent agent, Vector3 groundPosition, Transform player)
    {
        if (agent == null || player == null) return false;
        var filter = Filter(agent);
        if (!NavMesh.SamplePosition(player.position, out var target, 4f, filter)) return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(groundPosition, target.position, filter, path)
            || path.status != NavMeshPathStatus.PathComplete) return false;
        Vector3[] corners = path.corners;
        // El NavMesh horneado puede atravesar colliders añadidos después (puertas, vallas, etc.).
        float radius = Mathf.Max(.1f, agent.radius * .85f);
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 delta = corners[i] - corners[i - 1]; float length = delta.magnitude;
            if (length < .01f) continue;
            Vector3 bottom = corners[i - 1] + Vector3.up * (radius + .2f);
            Vector3 top = corners[i - 1] + Vector3.up * Mathf.Max(radius + .2f, agent.height - radius);
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, delta / length, length, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null || hit.normal.y > .5f || Ignore(hit.collider, agent, player)) continue;
                return false;
            }
        }
        return true;
    }

    static bool Ignore(Collider collider, NavMeshAgent agent, Transform player) =>
        collider.transform.IsChildOf(agent.transform) || collider.transform.IsChildOf(player)
        || collider.GetComponentInParent<EnemyHealth>() != null;

    public static bool TryPosition(NavMeshAgent agent, Vector3 desired, Transform player, float sampleDistance, out Vector3 rootPosition)
    {
        rootPosition = default;
        if (agent == null || player == null
            || !NavMesh.SamplePosition(desired, out var hit, sampleDistance, Filter(agent))
            || !CanReach(agent, hit.position, player)) return false;
        float radius = Mathf.Max(.1f, agent.radius);
        Vector3 bottom = hit.position + Vector3.up * (radius + .25f);
        Vector3 top = hit.position + Vector3.up * Mathf.Max(radius + .25f, agent.height - radius);
        foreach (var body in Physics.OverlapCapsule(bottom, top, radius * .9f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (body.transform.IsChildOf(agent.transform)) continue;
            // La malla de terreno puede englobar todo el mapa en sus bounds.
            if (body is TerrainCollider || body.bounds.max.y <= hit.position.y + .5f) continue;
            return false;
        }
        rootPosition = hit.position + Vector3.up * agent.baseOffset;
        return true;
    }

    public static bool TryNearPlayer(NavMeshAgent agent, Transform player, out Vector3 position)
    {
        position = default;
        if (player == null || agent == null) return false;
        // Primero intenta mantener distancia; si la zona habilitada es pequeña, reduce el radio.
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32f;
                float radius = pass == 0 ? Mathf.Lerp(10, 20, (i % 4) / 3f) : Mathf.Lerp(4, 9, (i % 4) / 3f);
                Vector3 desired = player.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (TryPosition(agent, desired, player, 3, out position)
                    && Vector3.Distance(position, player.position) >= 3f) return true;
            }
        return false;
    }
}
