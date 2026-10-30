using Survive.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survive.Combat
{
    /// <summary>Presenta los seis slots del inventario existente sin administrar el equipamiento.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Survive/Combate/Hotbar de armas")]
    public sealed class PlayerWeaponHotbar : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponInventory inventory;
        [SerializeField] private PlayerWeaponController weaponController;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image[] slotBackgrounds;
        [SerializeField] private Image[] selectionBars;
        [SerializeField] private Image[] iconImages;
        [SerializeField] private TMP_Text[] numberLabels;
        [SerializeField] private TMP_Text[] nameLabels;
        [SerializeField] private TMP_Text[] ammoLabels;

        private PlayerWeaponInventory subscribedInventory;
        private PlayerWeaponController subscribedController;
        private PlayerHealth subscribedHealth;
        private static readonly Color Accent = new Color(.28f, .85f, .94f, 1f);
        private static readonly Color Muted = new Color(.48f, .57f, .63f, 1f);
        private static readonly Color Reloading = new Color(1f, .76f, .30f, 1f);

        private void OnEnable() { Subscribe(); Refresh(); }
        private void Start() { Subscribe(); Refresh(); }
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            Unsubscribe();
            inventory ??= GetComponentInParent<PlayerWeaponInventory>();
            weaponController ??= GetComponentInParent<PlayerWeaponController>();
            subscribedInventory = inventory;
            subscribedController = weaponController;
            subscribedHealth = weaponController != null ? weaponController.Health : GetComponentInParent<PlayerHealth>();
            if (subscribedInventory != null) subscribedInventory.Changed += Refresh;
            if (subscribedController != null)
            {
                subscribedController.WeaponChanged += OnWeaponState;
                subscribedController.WeaponStateChanged += OnWeaponState;
            }
            if (subscribedHealth != null) subscribedHealth.OnDeath += Refresh;
        }

        private void Unsubscribe()
        {
            if (subscribedInventory != null) subscribedInventory.Changed -= Refresh;
            if (subscribedController != null)
            {
                subscribedController.WeaponChanged -= OnWeaponState;
                subscribedController.WeaponStateChanged -= OnWeaponState;
            }
            if (subscribedHealth != null) subscribedHealth.OnDeath -= Refresh;
            subscribedInventory = null;
            subscribedController = null;
            subscribedHealth = null;
        }

        private void OnWeaponState(WeaponItem ignored) => Refresh();

        public void Refresh()
        {
            bool visible = inventory != null && inventory.isActiveAndEnabled
                && weaponController != null && weaponController.isActiveAndEnabled
                && (weaponController.Health == null || !weaponController.Health.IsDead);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            if (inventory == null) return;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                WeaponItem weapon = inventory.GetWeapon(i);
                bool selected = inventory.SelectedIndex == i;
                if (slotBackgrounds != null && i < slotBackgrounds.Length && slotBackgrounds[i] != null)
                    slotBackgrounds[i].color = selected ? new Color(.055f, .19f, .23f, .96f)
                        : weapon != null ? new Color(.035f, .055f, .072f, .92f) : new Color(.025f, .04f, .055f, .76f);
                if (selectionBars != null && i < selectionBars.Length && selectionBars[i] != null)
                {
                    selectionBars[i].enabled = selected;
                    selectionBars[i].color = Accent;
                }
                if (numberLabels != null && i < numberLabels.Length && numberLabels[i] != null)
                    numberLabels[i].color = selected ? Accent : Muted;
                if (iconImages != null && i < iconImages.Length && iconImages[i] != null)
                {
                    Sprite icon = weapon != null && weapon.Definition != null ? weapon.Definition.Icon : null;
                    iconImages[i].sprite = icon;
                    iconImages[i].enabled = icon != null;
                    iconImages[i].color = Color.white;
                }
                if (nameLabels != null && i < nameLabels.Length && nameLabels[i] != null)
                {
                    nameLabels[i].text = weapon != null ? weapon.DisplayName : "Vacío";
                    nameLabels[i].color = weapon != null ? Color.white : Muted;
                }
                if (ammoLabels != null && i < ammoLabels.Length && ammoLabels[i] != null)
                {
                    ammoLabels[i].text = weapon == null ? "—" : weapon.IsReloading ? "Recargando…"
                        : $"{weapon.AmmoInMagazine} / {weapon.ReserveAmmo}";
                    ammoLabels[i].color = weapon == null ? Muted : weapon.IsReloading ? Reloading
                        : !weapon.HasAmmo ? new Color(1f, .48f, .38f, 1f) : new Color(.76f, .85f, .89f, 1f);
                }
            }
        }
    }
}
