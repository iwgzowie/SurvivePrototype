using System;
using Survive.Items;
using Survive.Turrets;
using UnityEngine;

namespace Survive.Economy
{
    [Serializable]
    public sealed class VendorOffer
    {
        public WeaponItem weaponPrefab;
        public TurretDefinition turret;
        [Min(1)] public int price = 100;
        public bool IsWeapon => weaponPrefab != null && turret == null;
        public bool IsTurret => turret != null && weaponPrefab == null;
        public bool IsValid => price > 0 && (IsWeapon || IsTurret && turret.DeployedPrefab != null);
        public string DisplayName => IsWeapon ? weaponPrefab.DisplayName : turret != null ? turret.DisplayName : "Sin configurar";
        public Sprite Icon => IsWeapon && weaponPrefab.Definition != null ? weaponPrefab.Definition.Icon : turret != null ? turret.Icon : null;
        public string Description => IsWeapon
            ? $"Daño {weaponPrefab.Damage:0} · Cargador {weaponPrefab.MagazineSize} · Alcance {weaponPrefab.Range:0} m"
            : turret != null ? $"Daño {turret.Damage:0} · {turret.ShotsPerSecond:0.#} tiros/s · Alcance {turret.Range:0} m" : "";
    }

    [CreateAssetMenu(menuName = "Survive/Vendedor/Catálogo", fileName = "VendorCatalog")]
    public sealed class VendorCatalog : ScriptableObject
    {
        [SerializeField] private VendorOffer[] offers = Array.Empty<VendorOffer>();
        public int Count => offers != null ? offers.Length : 0;
        public VendorOffer GetOffer(int index) => index >= 0 && index < Count ? offers[index] : null;
    }
}
