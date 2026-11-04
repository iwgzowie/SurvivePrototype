using System;
using Survive.Combat;
using Survive.Items;
using Survive.Turrets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UPP.ThirdPersonController;

namespace Survive.Economy
{
    [DefaultExecutionOrder(1050)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerWallet), typeof(PlayerTurretController), typeof(PlayerWeaponInventory))]
    public sealed partial class PlayerVendorController : MonoBehaviour
    {
        [SerializeField] private bool requireLockedCursor = true;
        private PlayerWallet wallet;
        private PlayerTurretController turrets;
        private PlayerWeaponInventory inventory;
        private PlayerHealth health;
        private UPPCharacterMovementComponent movement;
        private NpcVendor vendor;
        private bool transaction;
        private bool previousMoveDisabled;
        private float preventReopenUntil;
        private GameObject uiRoot, panel;
        private RectTransform rows;
        private TMP_Text balance, message, title, hud, placementHelp;
        public bool ExternalGameplayHud { get; set; }
        private Button[] buyButtons = Array.Empty<Button>();
        private TMP_Text[] buyLabels = Array.Empty<TMP_Text>();
        public bool IsOpen => vendor != null;
        public NpcVendor CurrentVendor => vendor;
        public bool CanOpen => isActiveAndEnabled && !IsOpen && Time.timeScale > 0
            && Time.unscaledTime >= preventReopenUntil && (health == null || !health.IsDead)
            && movement != null && movement.isActiveAndEnabled && !movement.DisableAllMove && !movement.IsRolling
            && (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked) && !turrets.BlocksWeaponInput;

        private void Awake()
        {
            wallet = GetComponent<PlayerWallet>();
            turrets = GetComponent<PlayerTurretController>();
            inventory = GetComponent<PlayerWeaponInventory>();
            health = GetComponent<PlayerHealth>();
            movement = GetComponent<UPPCharacterMovementComponent>();
        }

        private void Start() => EnsureUi();

        public bool TryOpen(NpcVendor npc)
        {
            if (!CanOpen || npc == null || npc.Catalog == null || !npc.IsInReach(gameObject)) return false;
            EnsureUi();
            vendor = npc;
            previousMoveDisabled = movement.DisableAllMove;
            movement.DisableAllMove = true;
            // Detener el desplazamiento acumulado al entrar en el menú.
            if (TryGetComponent(out Rigidbody body) && !body.isKinematic)
                body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
            GetComponent<PlayerWeaponController>()?.EquippedWeapon?.CancelReload();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            panel.SetActive(true);
            hud.gameObject.SetActive(false);
            title.text = npc.DisplayName + "  /  SUMINISTROS";
            message.text = "Las armas se guardan en un espacio libre. Las torretas se colocan con T.";
            BuildRows();
            RefreshUi();
            return true;
        }

        public void Close()
        {
            if (!IsOpen && (panel == null || !panel.activeSelf)) return;
            vendor = null;
            if (panel != null) panel.SetActive(false);
            if (movement != null && movement.isActiveAndEnabled && (health == null || !health.IsDead))
                movement.DisableAllMove = previousMoveDisabled;
            preventReopenUntil = Time.unscaledTime + .25f;
            // También protege el arma del clic que cierra el panel.
            turrets?.CancelPlacement();
            if (Time.timeScale > 0 && (health == null || !health.IsDead))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void LateUpdate()
        {
            EnsureUi();
            bool alive = health == null || !health.IsDead;
            if (IsOpen && (!alive || Time.timeScale <= 0 || !vendor.IsInReach(gameObject))) Close();
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            if (panel.activeSelf && !IsOpen) Close(); // NPC destruido o desactivado.
            if (IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshUi();
            }
            hud.gameObject.SetActive(!ExternalGameplayHud && alive && Time.timeScale > 0 && !IsOpen);
            hud.text = $"MONEDAS  {wallet.Coins}\nTORRETAS  {turrets.Count}/{turrets.Capacity}   [T] Colocar";
            placementHelp.gameObject.SetActive(!ExternalGameplayHud && turrets.IsPlacing && alive);
            placementHelp.text = turrets.PlacementMessage;
        }

        public string PurchaseProblem(int index)
        {
            if (!isActiveAndEnabled || !IsOpen || vendor.Catalog == null || !vendor.IsInReach(gameObject)
                || Time.timeScale <= 0 || health != null && health.IsDead) return "Comercio no disponible";
            VendorOffer offer = vendor.Catalog.GetOffer(index);
            if (offer == null || !offer.IsValid) return "Producto no disponible";
            if (!wallet.CanAfford(offer.price)) return "Faltan monedas";
            if (offer.IsWeapon && !inventory.HasFreeSlot) return "Inventario lleno";
            if (offer.IsTurret && !turrets.CanReceive(offer.turret)) return "Reserva de torretas llena";
            if (offer.IsExplosiveMagazine)
            {
                WeaponItem weapon = GetComponent<PlayerWeaponController>().EquippedWeapon;
                if (weapon == null) return "Equipá un arma";
                if (!weapon.CanLoadExplosiveMagazine) return "Cargador ya activo";
            }
            if (offer.IsCommonAmmo)
            {
                WeaponItem weapon = GetComponent<PlayerWeaponController>().EquippedWeapon;
                if (weapon == null) return "Equipá un arma";
                if (weapon.ReserveAmmo > int.MaxValue - offer.commonAmmo.rounds) return "Reserva llena";
            }
            return null;
        }

        public bool TryBuy(int index)
        {
            if (transaction) return false;
            string problem = PurchaseProblem(index);
            if (problem != null) { if (message != null) message.text = problem; return false; }
            VendorOffer offer = vendor.Catalog.GetOffer(index);
            transaction = true;
            WeaponItem delivery = null;
            bool paid = false, delivered = false;
            WeaponItem ammoWeapon = offer.IsCommonAmmo ? GetComponent<PlayerWeaponController>().EquippedWeapon : null;
            try
            {
                paid = wallet.TrySpend(offer.price);
                if (!paid) return false;
                if (offer.IsWeapon)
                {
                    delivery = Instantiate(offer.weaponPrefab, transform.position, Quaternion.identity);
                    delivered = inventory.TryStorePurchase(delivery);
                }
                else if (offer.IsTurret) delivered = turrets.TryAdd(offer.turret);
                else if (offer.IsExplosiveMagazine)
                {
                    WeaponItem weapon = GetComponent<PlayerWeaponController>().EquippedWeapon;
                    delivered = weapon != null && weapon.TryLoadExplosiveMagazine(offer.explosiveMagazine);
                }
                else if (offer.IsCommonAmmo)
                    delivered = ammoWeapon != null && ammoWeapon.ReserveAmmo <= int.MaxValue - offer.commonAmmo.rounds
                        && ammoWeapon.AddReserveAmmo(offer.commonAmmo.rounds) == offer.commonAmmo.rounds;
                if (message != null) message.text = delivered ? $"Compraste: {offer.DisplayName}" : "No se pudo entregar. Monedas devueltas.";
                return delivered;
            }
            finally
            {
                if (!delivered)
                {
                    if (delivery != null) { delivery.gameObject.SetActive(false); Destroy(delivery.gameObject); }
                    if (paid) wallet.AddCoins(offer.price);
                }
                transaction = false;
                RefreshUi();
            }
        }

        private void RefreshUi()
        {
            if (balance == null) return;
            balance.text = $"MONEDAS  {wallet.Coins:N0}     |     TORRETAS  {turrets.Count}/{turrets.Capacity}";
            RefreshInventoryStatus();
            for (int index = 0; index < buyButtons.Length; index++)
            {
                string problem = PurchaseProblem(index);
                buyButtons[index].interactable = problem == null;
                buyLabels[index].text = problem == null ? "COMPRAR" : problem == "Reserva de torretas llena" ? "RESERVA LLENA" : problem;
            }
        }

        private void OnDisable()
        {
            Close();
            if (uiRoot != null) uiRoot.SetActive(false);
        }
        private void OnEnable() { if (uiRoot != null) uiRoot.SetActive(true); }
    }
}
