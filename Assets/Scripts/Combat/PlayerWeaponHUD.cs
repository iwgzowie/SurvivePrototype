using Survive.Items;
using TMPro;
using UnityEngine;

namespace Survive.Combat
{
    /// <summary>Presenta el arma y la munición mediante los eventos del controlador existente.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Survive/Combate/HUD de arma")]
    public sealed class PlayerWeaponHUD : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponController weaponController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private CanvasGroup hudCanvasGroup;

        private PlayerWeaponController subscribedController;
        private PlayerHealth subscribedHealth;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Start()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            if (weaponController == null) weaponController = GetComponentInParent<PlayerWeaponController>();
            // La escena puede sobrescribir la salud del controlador; se usa esa misma instancia.
            if (weaponController != null && weaponController.Health != null) playerHealth = weaponController.Health;
            if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
            subscribedController = weaponController;
            subscribedHealth = playerHealth;
            if (subscribedController != null)
            {
                subscribedController.WeaponChanged += RefreshWeapon;
                subscribedController.WeaponStateChanged += RefreshWeapon;
            }
            if (subscribedHealth != null) subscribedHealth.OnDeath += Refresh;
        }

        private void Unsubscribe()
        {
            if (subscribedController != null)
            {
                subscribedController.WeaponChanged -= RefreshWeapon;
                subscribedController.WeaponStateChanged -= RefreshWeapon;
            }
            if (subscribedHealth != null) subscribedHealth.OnDeath -= Refresh;
            subscribedController = null;
            subscribedHealth = null;
        }

        private void RefreshWeapon(WeaponItem weapon) => Refresh();

        public void Refresh()
        {
            bool visible = weaponController != null && weaponController.isActiveAndEnabled
                && (playerHealth == null || !playerHealth.IsDead);
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = visible ? 1f : 0f;
                hudCanvasGroup.interactable = false;
                hudCanvasGroup.blocksRaycasts = false;
            }
            WeaponItem weapon = visible ? weaponController.EquippedWeapon : null;
            if (weaponNameText != null) weaponNameText.text = weapon != null ? weapon.DisplayName : "Sin arma";
            if (ammoText != null)
                ammoText.text = weapon != null ? $"{weapon.AmmoInMagazine} / {weapon.ReserveAmmo}" : "-- / --";
            if (statusText != null)
                statusText.text = weapon == null ? "1–6 · Elegir    E · Recoger"
                    : weapon.IsReloading ? "Recargando…"
                    : !weapon.HasAmmo ? (weapon.ReserveAmmo > 0 ? "R · Recargar" : "Sin munición · 1–6 cambiar")
                    : "R · Recargar    G · Soltar";
        }
    }
}
