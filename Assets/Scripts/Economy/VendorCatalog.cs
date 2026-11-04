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
        public ExplosiveAmmoDefinition explosiveMagazine;
        public AmmoBoxDefinition commonAmmo;
        [Min(1)] public int price = 100;
        public bool IsWeapon => weaponPrefab != null && turret == null && explosiveMagazine == null && commonAmmo == null;
        public bool IsTurret => turret != null && weaponPrefab == null && explosiveMagazine == null && commonAmmo == null;
        public bool IsExplosiveMagazine => explosiveMagazine != null && weaponPrefab == null && turret == null && commonAmmo == null;
        public bool IsCommonAmmo => commonAmmo != null && weaponPrefab == null && turret == null && explosiveMagazine == null;
        public bool IsValid => price > 0 && (IsWeapon || IsTurret && turret.DeployedPrefab != null || IsExplosiveMagazine || IsCommonAmmo && commonAmmo.rounds > 0);
        public string DisplayName => IsWeapon ? weaponPrefab.DisplayName : IsTurret ? turret.DisplayName : IsExplosiveMagazine ? explosiveMagazine.displayName : IsCommonAmmo ? commonAmmo.displayName : "Sin configurar";
        public Sprite Icon => IsWeapon && weaponPrefab.Definition != null ? weaponPrefab.Definition.Icon : IsTurret ? turret.Icon : IsExplosiveMagazine ? explosiveMagazine.icon : IsCommonAmmo ? commonAmmo.icon : null;
        public string Description => IsWeapon
            ? $"Daño {weaponPrefab.Damage:0} · Cargador {weaponPrefab.MagazineSize} · Alcance {weaponPrefab.Range:0} m"
            : IsTurret ? $"Vida {turret.MaxHealth:0} · Desgaste {turret.HealthCostPerShot:0}/tiro\nDaño {turret.Damage:0} · {turret.ShotsPerSecond:0.#} tiros/s · {turret.Range:0} m"
            : IsExplosiveMagazine ? $"Carga el arma en mano · Explosión {explosiveMagazine.radius:0.#} m · Daño {explosiveMagazine.damage:0}"
            : IsCommonAmmo ? $"+{commonAmmo.rounds} balas comunes · Reserva del arma equipada" : "";
    }

    [CreateAssetMenu(menuName = "Survive/Vendedor/Catálogo", fileName = "VendorCatalog")]
    public sealed class VendorCatalog : ScriptableObject
    {
        [SerializeField] private VendorOffer[] offers = Array.Empty<VendorOffer>();
        public int Count => offers != null ? offers.Length : 0;
        public VendorOffer GetOffer(int index) => index >= 0 && index < Count ? offers[index] : null;
    }
}
