using UnityEngine;

namespace Survive.Combat
{
    public enum WeaponImpactSurfaceKind
    {
        Concrete,
        Metal,
        Wood,
        Dirt,
        Flesh,
        Glass
    }

    /// <summary>Declara una respuesta visual de impacto sin cambiar las propiedades físicas del collider.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Survive/Combate/Superficie de impacto de arma")]
    public sealed class WeaponImpactSurface : MonoBehaviour
    {
        [SerializeField] private WeaponImpactSurfaceKind surface = WeaponImpactSurfaceKind.Concrete;

        public WeaponImpactSurfaceKind Surface => surface;
    }
}
