using Survive.Combat;
using Survive.Items;
using UnityEngine;

namespace Survive.Turrets
{
    /// <summary>El daño pasa por EnemyHealth; los obstáculos físicos bloquean adquisición y disparo.</summary>
    public sealed class AutoTurret : MonoBehaviour
    {
        [SerializeField] private TurretDefinition definition;
        [SerializeField] private Transform head;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private AudioSource shotAudio;
        [SerializeField] private WeaponDefinition presentation;
        private readonly Collider[] candidates = new Collider[64];
        private EnemyHealth target;
        private Collider targetCollider;
        private float nextScan, nextShot, tracerUntil, activatedAt;
        public TurretDefinition Definition => definition;
        public EnemyHealth CurrentTarget => target;
        public int ShotsFired { get; private set; }

        private void OnEnable()
        {
            activatedAt = Time.time;
            nextScan = nextShot = Time.time;
            if (tracer != null) tracer.enabled = false;
        }

        private void OnDisable() { if (tracer != null) tracer.enabled = false; }

        private void Update()
        {
            if (definition == null || Time.timeScale <= 0) return;
            if (definition.LifetimeSeconds > 0 && Time.time - activatedAt >= definition.LifetimeSeconds)
            { Destroy(gameObject); return; }
            if (tracer != null && Time.time >= tracerUntil) tracer.enabled = false;
            if (Time.time >= nextScan)
            {
                AcquireTarget();
                nextScan = Time.time + .15f;
            }
            if (!ValidTarget(target, targetCollider)) return;
            Vector3 point = targetCollider.bounds.center;
            Vector3 aim = point - head.position;
            if (aim.sqrMagnitude > .001f) head.rotation = Quaternion.LookRotation(aim);
            if (Time.time >= nextShot) TryFire();
        }

        public void AcquireTarget()
        {
            target = null;
            targetCollider = null;
            if (definition == null || head == null || muzzle == null) return;
            int count = Physics.OverlapSphereNonAlloc(transform.position, definition.Range, candidates,
                ~0, QueryTriggerInteraction.Ignore);
            Collider[] hits = count == candidates.Length
                ? Physics.OverlapSphere(transform.position, definition.Range, ~0, QueryTriggerInteraction.Ignore) : candidates;
            if (hits != candidates) count = hits.Length;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
                if (!ValidTarget(enemy, hits[i])) continue;
                float distance = (hits[i].bounds.center - transform.position).sqrMagnitude;
                if (distance >= best || !HasLineOfSight(head.position, hits[i].bounds.center, enemy, out _)) continue;
                best = distance;
                target = enemy;
                targetCollider = hits[i];
            }
        }

        private bool ValidTarget(EnemyHealth enemy, Collider body) => definition != null
            && enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead && body != null && body.enabled
            && (body.bounds.center - transform.position).sqrMagnitude <= definition.Range * definition.Range;

        private bool HasLineOfSight(Vector3 origin, Vector3 point, EnemyHealth enemy, out RaycastHit nearest)
        {
            nearest = default;
            Vector3 delta = point - origin;
            float best = float.PositiveInfinity;
            // Impide disparar desde dentro de un obstáculo aunque un rayo no detecte su cara de salida.
            foreach (Collider body in Physics.OverlapSphere(origin, .02f, ~0, QueryTriggerInteraction.Ignore))
                if (!body.transform.IsChildOf(transform) && body.GetComponentInParent<EnemyHealth>() != enemy) return false;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude + .03f,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= best) continue;
                best = hit.distance;
                nearest = hit;
            }
            return nearest.collider != null && nearest.collider.GetComponentInParent<EnemyHealth>() == enemy;
        }

        public bool TryFire()
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || Time.time < nextShot
                || !ValidTarget(target, targetCollider) || muzzle == null
                || !HasLineOfSight(muzzle.position, targetCollider.bounds.center, target, out RaycastHit hit)) return false;
            nextShot = Time.time + 1f / definition.ShotsPerSecond;
            ShotsFired++;
            if (tracer != null)
            {
                tracer.SetPosition(0, muzzle.position);
                tracer.SetPosition(1, hit.point);
                tracer.enabled = true;
                tracerUntil = Time.time + .055f;
            }
            if (shotAudio != null && presentation != null && presentation.ShotSound != null)
                shotAudio.PlayOneShot(presentation.ShotSound, presentation.ShotVolume * .6f);
            WeaponVfxProfile profile = presentation != null ? presentation.VfxProfile : null;
            if (profile != null && profile.MuzzlePrefab != null)
                WeaponVfxPool.GetOrCreate(gameObject.scene)?.Spawn(profile.MuzzlePrefab, muzzle.position,
                    muzzle.rotation, profile.MuzzleScale, profile.MuzzleLifetime, muzzle);
            target.TakeDamage(definition.Damage, hit.point);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, definition.Range);
        }
    }
}
