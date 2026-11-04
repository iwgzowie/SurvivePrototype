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
        [SerializeField] private Transform barrelPitch;
        [SerializeField] private Transform alternateMuzzle;
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private AudioSource shotAudio;
        [SerializeField] private WeaponDefinition presentation;
        private readonly Collider[] candidates = new Collider[64];
        private GameObject owner;
        public void InitializeOwner(GameObject player) => owner = player;
        private EnemyHealth target;
        private Collider targetCollider;
        private float nextScan, nextShot, tracerUntil, activatedAt;
        public TurretDefinition Definition => definition;
        public EnemyHealth CurrentTarget => target;
        public int ShotsFired { get; private set; }
        public Vector3 LastShotOrigin { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth => definition != null ? definition.MaxHealth : 150;
        public bool IsDepleted => CurrentHealth <= 0;
        public event System.Action HealthChanged;
        private bool healthInitialized;

        private void OnEnable()
        {
            if (!healthInitialized) { CurrentHealth = MaxHealth; healthInitialized = true; }
            activatedAt = Time.time;
            nextScan = nextShot = Time.time;
            if (tracer != null) tracer.enabled = false;
        }

        private void OnDisable() { if (tracer != null) tracer.enabled = false; }

        private void Update()
        {
            if (definition == null || IsDepleted || Time.timeScale <= 0) return;
            if (definition.LifetimeSeconds > 0 && Time.time - activatedAt >= definition.LifetimeSeconds)
            { Destroy(gameObject); return; }
            if (tracer != null && Time.time >= tracerUntil) tracer.enabled = false;
            if (Time.time >= nextScan)
            {
                AcquireTarget();
                nextScan = Time.time + .15f;
            }
            if (!ValidTarget(target, targetCollider)) return;
            AimHead(targetCollider.bounds.center);
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
                Vector3 origin = barrelPitch != null ? barrelPitch.position : head.position;
                if (distance >= best || !HasLineOfSight(origin, hits[i].bounds.center, enemy, out _)) continue;
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
            if (!isActiveAndEnabled || IsDepleted || Time.timeScale <= 0 || Time.time < nextShot
                || !ValidTarget(target, targetCollider) || muzzle == null || head == null) return false;
            AimHead(targetCollider.bounds.center);
            Transform firingMuzzle = alternateMuzzle != null && ShotsFired % 2 == 1 ? alternateMuzzle : muzzle;
            if (!HasLineOfSight(firingMuzzle.position, targetCollider.bounds.center, target, out RaycastHit hit)) return false;
            nextShot = Time.time + 1f / definition.ShotsPerSecond;
            LastShotOrigin = firingMuzzle.position;
            ShotsFired++;
            if (tracer != null)
            {
                tracer.SetPosition(0, firingMuzzle.position);
                tracer.SetPosition(1, hit.point);
                tracer.enabled = true;
                tracerUntil = Time.time + .055f;
            }
            if (shotAudio != null && presentation != null && presentation.ShotSound != null)
                shotAudio.PlayOneShot(presentation.ShotSound, presentation.ShotVolume * .6f);
            WeaponVfxProfile profile = presentation != null ? presentation.VfxProfile : null;
            if (profile != null && profile.MuzzlePrefab != null)
                WeaponVfxPool.GetOrCreate(gameObject.scene)?.Spawn(profile.MuzzlePrefab, firingMuzzle.position,
                    firingMuzzle.rotation, profile.MuzzleScale, profile.MuzzleLifetime, firingMuzzle);
            target.TakeDamage(definition.Damage, hit.point, owner);
            TakeDamage(definition.HealthCostPerShot);
            return true;
        }

        public void TakeDamage(float damage)
        {
            if (IsDepleted || Time.timeScale <= 0 || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            HealthChanged?.Invoke();
            if (!IsDepleted) return;
            target = null; targetCollider = null;
            foreach (var obstacle in GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) obstacle.enabled = false;
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            Destroy(gameObject, .15f);
        }

        private void AimHead(Vector3 point)
        {
            if (head == null) return;
            Vector3 aim = point - head.position;
            if (aim.sqrMagnitude <= .001f) return;
            if (barrelPitch == null) { head.rotation = Quaternion.LookRotation(aim); return; }
            Vector3 horizontal = Vector3.ProjectOnPlane(aim, Vector3.up);
            if (horizontal.sqrMagnitude > .001f) head.rotation = Quaternion.LookRotation(horizontal);
            Vector3 local = head.InverseTransformDirection(point - barrelPitch.position);
            barrelPitch.localRotation = Quaternion.Euler(-Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg, 0, 0);
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, definition.Range);
        }
    }
}
