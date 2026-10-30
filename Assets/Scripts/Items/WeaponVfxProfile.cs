using Survive.Combat;
using UnityEngine;

namespace Survive.Items
{
    /// <summary>Referencias y escalas de presentación; no modifica disparos, daño ni munición.</summary>
    [CreateAssetMenu(fileName = "WeaponVfxProfile", menuName = "Survive/Items/Weapon VFX Profile")]
    public sealed class WeaponVfxProfile : ScriptableObject
    {
        [Header("Boca del arma")]
        [SerializeField] private GameObject muzzlePrefab;
        [SerializeField, Min(.001f)] private float muzzleScale = 1f;
        [SerializeField, Min(.02f)] private float muzzleLifetime = .12f;

        [Header("Casquillos")]
        [SerializeField] private GameObject shellPrefab;
        [SerializeField, Min(.001f)] private float shellScale = 1f;
        [SerializeField, Min(.02f)] private float shellLifetime = 3f;
        [SerializeField, Min(0f)] private float ejectionDelaySeconds;
        [SerializeField, Tooltip("El revólver conserva las vainas al disparar y las expulsa durante la recarga.")]
        private bool ejectCasingsOnReload;
        [SerializeField, Range(0f, 1f)] private float reloadEjectionProgress = .39f;

        [Header("Impactos por superficie")]
        [SerializeField] private GameObject concreteImpactPrefab;
        [SerializeField] private GameObject metalImpactPrefab;
        [SerializeField] private GameObject woodImpactPrefab;
        [SerializeField] private GameObject dirtImpactPrefab;
        [SerializeField] private GameObject fleshImpactPrefab;
        [SerializeField] private GameObject glassImpactPrefab;
        [SerializeField, Min(.001f)] private float impactScale = 1f;
        [SerializeField, Min(.02f)] private float impactLifetime = .9f;

        public GameObject MuzzlePrefab => muzzlePrefab;
        public float MuzzleScale => Mathf.Max(.001f, muzzleScale);
        public float MuzzleLifetime => Mathf.Max(.02f, muzzleLifetime);
        public GameObject ShellPrefab => shellPrefab;
        public float ShellScale => Mathf.Max(.001f, shellScale);
        public float ShellLifetime => Mathf.Max(.02f, shellLifetime);
        public float EjectionDelaySeconds => Mathf.Max(0f, ejectionDelaySeconds);
        public bool EjectCasingsOnReload => ejectCasingsOnReload;
        public float ReloadEjectionProgress => Mathf.Clamp01(reloadEjectionProgress);
        public float ImpactScale => Mathf.Max(.001f, impactScale);
        public float ImpactLifetime => Mathf.Max(.02f, impactLifetime);

        public GameObject GetImpactPrefab(WeaponImpactSurfaceKind surface)
        {
            switch (surface)
            {
                case WeaponImpactSurfaceKind.Metal: return metalImpactPrefab != null ? metalImpactPrefab : concreteImpactPrefab;
                case WeaponImpactSurfaceKind.Wood: return woodImpactPrefab != null ? woodImpactPrefab : concreteImpactPrefab;
                case WeaponImpactSurfaceKind.Dirt: return dirtImpactPrefab != null ? dirtImpactPrefab : concreteImpactPrefab;
                case WeaponImpactSurfaceKind.Glass: return glassImpactPrefab != null ? glassImpactPrefab : concreteImpactPrefab;
                // Una superficie orgánica sin efecto propio no debe producir polvo de hormigón.
                case WeaponImpactSurfaceKind.Flesh: return fleshImpactPrefab;
                default: return concreteImpactPrefab;
            }
        }
    }
}
