using System;
using Survive.Items;
using UnityEngine;

namespace Survive.Combat
{
    /// <summary>Presenta boca, casquillos e impactos a partir del resultado del disparo existente.</summary>
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponItem))]
    [AddComponentMenu("Survive/Combate/Efectos visuales del arma")]
    public sealed class WeaponVfxPresenter : MonoBehaviour
    {
        [SerializeField] private WeaponItem weapon;
        [SerializeField, Tooltip("Salida de vainas calibrada sobre la recámara de este modelo, independiente del Muzzle.")]
        private Transform shellEjectionPort;

        private readonly float[] pendingEjections = new float[8];
        private int pendingEjectionCount;
        private PlayerWeaponController ownerController;
        private PlayerHealth ownerHealth;
        private WeaponVfxPool pool;
        private bool observedReload;
        private bool reloadCasesEmitted;
        private int reloadStartedWithAmmo;
        private int pendingReloadCases;
        private int retainedCases;
        private int previousAmmo = -1;

        private WeaponVfxProfile Profile => weapon != null && weapon.Definition != null
            ? weapon.Definition.VfxProfile : null;
        private bool CanPresent => weapon != null && weapon.isActiveAndEnabled && weapon.IsEquipped
            && (ownerController == null || (ownerController.isActiveAndEnabled && ownerController.EquippedWeapon == weapon))
            && (ownerHealth == null || !ownerHealth.IsDead);

        private void Awake()
        {
            weapon ??= GetComponent<WeaponItem>();
            ResolveOwner();
        }

        private void OnEnable()
        {
            weapon ??= GetComponent<WeaponItem>();
            ResolveOwner();
            weapon.ShotPresented += OnShot;
            weapon.ImpactPresented += OnImpact;
            weapon.StateChanged += OnWeaponState;
            ObserveReload();
        }

        private void OnDisable()
        {
            if (weapon != null)
            {
                weapon.ShotPresented -= OnShot;
                weapon.ImpactPresented -= OnImpact;
                weapon.StateChanged -= OnWeaponState;
            }
            CancelAttachedPresentation();
        }

        private void OnTransformParentChanged() => ResolveOwner();

        private void ResolveOwner()
        {
            ownerController = GetComponentInParent<PlayerWeaponController>();
            ownerHealth = ownerController != null ? ownerController.Health : GetComponentInParent<PlayerHealth>();
        }

        private WeaponVfxPool ResolvePool()
        {
            if (pool == null || pool.gameObject.scene != gameObject.scene)
                pool = WeaponVfxPool.GetOrCreate(gameObject.scene);
            return pool;
        }

        private void OnShot(WeaponItem source)
        {
            WeaponVfxProfile profile = Profile;
            if (source != weapon || !CanPresent || profile == null || Time.timeScale <= 0f) return;
            WeaponVfxPool effects = ResolvePool();
            if (effects == null) return;
            Transform muzzle = weapon.Muzzle;
            effects.Spawn(profile.MuzzlePrefab, muzzle.position, muzzle.rotation, profile.MuzzleScale,
                profile.MuzzleLifetime, muzzle);

            if (profile.EjectCasingsOnReload)
            {
                // Las vainas se conservan incluso al guardar o soltar esta misma instancia.
                retainedCases = Mathf.Min(weapon.MagazineSize, retainedCases + 1);
                return;
            }
            if (profile.ShellPrefab == null || shellEjectionPort == null) return;
            if (profile.EjectionDelaySeconds <= 0f) SpawnCases(1);
            else if (pendingEjectionCount < pendingEjections.Length)
                pendingEjections[pendingEjectionCount++] = Time.time + profile.EjectionDelaySeconds;
        }

        private void OnImpact(WeaponItem source, RaycastHit hit)
        {
            WeaponVfxProfile profile = Profile;
            if (source != weapon || !CanPresent || profile == null || hit.collider == null || Time.timeScale <= 0f) return;
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            WeaponImpactSurfaceKind surface = ResolveSurface(hit.collider, enemy);
            // EnemyHealth conserva la responsabilidad de su efecto de impacto propio.
            if (surface == WeaponImpactSurfaceKind.Flesh && enemy != null && enemy.HasHitParticleEffect) return;
            GameObject prefab = profile.GetImpactPrefab(surface);
            if (prefab == null) return;
            Vector3 normal = hit.normal.sqrMagnitude > .0001f ? hit.normal.normalized : -weapon.Muzzle.forward;
            ResolvePool()?.Spawn(prefab, hit.point + normal * .005f, Quaternion.LookRotation(normal),
                profile.ImpactScale, profile.ImpactLifetime);
        }

        private static WeaponImpactSurfaceKind ResolveSurface(Collider collider, EnemyHealth enemy)
        {
            WeaponImpactSurface explicitSurface = collider.GetComponentInParent<WeaponImpactSurface>();
            if (explicitSurface != null) return explicitSurface.Surface;
            if (enemy != null) return WeaponImpactSurfaceKind.Flesh;
            if (collider is TerrainCollider) return WeaponImpactSurfaceKind.Dirt;
            // Sólo consulta un material físico explícito; no instancia materiales de render.
            string materialName = collider.sharedMaterial != null ? collider.sharedMaterial.name : null;
            if (!string.IsNullOrEmpty(materialName))
            {
                if (Contains(materialName, "metal") || Contains(materialName, "steel")
                    || string.Equals(materialName, "Iron", StringComparison.OrdinalIgnoreCase))
                    return WeaponImpactSurfaceKind.Metal;
                if (Contains(materialName, "wood")) return WeaponImpactSurfaceKind.Wood;
                if (Contains(materialName, "glass")) return WeaponImpactSurfaceKind.Glass;
                if (Contains(materialName, "dirt") || Contains(materialName, "soil") || Contains(materialName, "sand"))
                    return WeaponImpactSurfaceKind.Dirt;
            }
            return WeaponImpactSurfaceKind.Concrete;
        }

        private static bool Contains(string value, string part) => value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnWeaponState(WeaponItem ignored) => ObserveReload();

        private void ObserveReload()
        {
            if (weapon == null) return;
            int currentAmmo = weapon.AmmoInMagazine;
            bool ammoIncreased = previousAmmo >= 0 && currentAmmo > previousAmmo;
            previousAmmo = currentAmmo;
            WeaponVfxProfile profile = Profile;
            if (profile == null || !profile.EjectCasingsOnReload)
            {
                observedReload = false;
                pendingReloadCases = 0;
                retainedCases = 0;
                return;
            }

            if (observedReload && !weapon.IsReloading)
            {
                bool completed = currentAmmo > reloadStartedWithAmmo;
                // Emitir antes de limpiar también cubre un cuadro que completa toda la recarga.
                if (completed && !reloadCasesEmitted && CanPresent) EmitReloadCases();
                if (completed) retainedCases = 0;
                observedReload = false;
                pendingReloadCases = 0;
            }
            // ResetAmmunition repone el cargador; agregar reserva no cambia este valor.
            if (ammoIncreased && !weapon.IsReloading) retainedCases = 0;
            if (!CanPresent)
            {
                observedReload = false;
                pendingReloadCases = 0;
                return;
            }

            if (weapon.IsReloading && !observedReload)
            {
                observedReload = true;
                reloadCasesEmitted = false;
                reloadStartedWithAmmo = currentAmmo;
                pendingReloadCases = Mathf.Clamp(retainedCases, 0, 64);
            }
        }

        private void LateUpdate()
        {
            if (!CanPresent)
            {
                CancelAttachedPresentation();
                return;
            }
            if (Time.timeScale <= 0f) return;
            ObserveReload();
            for (int index = pendingEjectionCount - 1; index >= 0; index--)
            {
                if (Time.time < pendingEjections[index]) continue;
                SpawnCases(1);
                pendingEjections[index] = pendingEjections[--pendingEjectionCount];
            }
            WeaponVfxProfile profile = Profile;
            if (observedReload && !reloadCasesEmitted && profile != null
                && weapon.ReloadProgress >= profile.ReloadEjectionProgress)
            {
                EmitReloadCases();
            }
        }

        private void EmitReloadCases()
        {
            SpawnCases(pendingReloadCases);
            retainedCases = Mathf.Max(0, retainedCases - pendingReloadCases);
            reloadCasesEmitted = true;
        }

        private void SpawnCases(int count)
        {
            WeaponVfxProfile profile = Profile;
            if (!CanPresent || profile == null || profile.ShellPrefab == null || shellEjectionPort == null) return;
            WeaponVfxPool effects = ResolvePool();
            if (effects == null) return;
            for (int index = 0; index < Mathf.Clamp(count, 0, 64); index++)
                effects.Spawn(profile.ShellPrefab, shellEjectionPort.position, shellEjectionPort.rotation,
                    profile.ShellScale, profile.ShellLifetime);
        }

        private void CancelAttachedPresentation()
        {
            pendingEjectionCount = 0;
            observedReload = false;
            pendingReloadCases = 0;
            if (pool != null && weapon != null) pool.ReleaseAttached(weapon.Muzzle);
        }
    }
}
