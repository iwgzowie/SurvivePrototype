using UnityEngine;

/// <summary>Eventos de presentación del Animator; el daño sigue a cargo de la IA.</summary>
public sealed class EnemyAnimationEvents : MonoBehaviour
{
    [SerializeField] private ParticleSystem spitEffect;
    [SerializeField] private LayerMask spitObstructionMask = ~0;
    private EnemyHealth health;
    private Transform player;

    private void Awake()
    {
        health = GetComponentInParent<EnemyHealth>();
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }

    public void PlaySpit()
    {
        if (spitEffect == null || health == null || health.IsDead) return;
        Vector3 origin = spitEffect.transform.position;
        Vector3 direction = health.transform.forward;
        float distance = 10f;
        if (player != null)
        {
            var collider = player.GetComponentInChildren<Collider>();
            Vector3 target = collider != null ? collider.bounds.center : player.position;
            Vector3 offset = target - origin;
            if (offset.sqrMagnitude > .001f)
            {
                distance = offset.magnitude;
                direction = offset.normalized;
            }
        }
        // La boca acompaña al hueso; el chorro apunta al objetivo, independientemente del pitch del gesto.
        spitEffect.transform.rotation = Quaternion.LookRotation(direction);
        foreach (var hit in Physics.RaycastAll(origin, direction, distance, spitObstructionMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(health.transform))
                distance = Mathf.Min(distance, hit.distance);
        }
        var main = spitEffect.main;
        main.startLifetime = Mathf.Clamp(distance / 15f, .03f, .7f);
        spitEffect.Play(true);
    }
}