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
    public sealed class PlayerVendorController : MonoBehaviour
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
            hud.gameObject.SetActive(alive && Time.timeScale > 0 && !IsOpen);
            hud.text = $"MONEDAS  {wallet.Coins}\nTORRETAS  {turrets.Count}/{turrets.Capacity}   [T] Colocar";
            placementHelp.gameObject.SetActive(turrets.IsPlacing && alive);
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
            try
            {
                paid = wallet.TrySpend(offer.price);
                if (!paid) return false;
                if (offer.IsWeapon)
                {
                    delivery = Instantiate(offer.weaponPrefab, transform.position, Quaternion.identity);
                    delivered = inventory.TryStorePurchase(delivery);
                }
                else delivered = turrets.TryAdd(offer.turret);
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
            balance.text = $"Monedas: {wallet.Coins}     |     Torretas: {turrets.Count}/{turrets.Capacity}";
            for (int index = 0; index < buyButtons.Length; index++)
            {
                string problem = PurchaseProblem(index);
                buyButtons[index].interactable = problem == null;
                buyLabels[index].text = problem == null ? "COMPRAR" : problem;
            }
        }

        private void EnsureUi()
        {
            if (uiRoot != null) return;
            uiRoot = new GameObject("VendorAndTurretUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot.transform.SetParent(transform, false);
            Canvas canvas = uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 900);
            scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("VendorEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(uiRoot.transform, false);
            }
            hud = Label(uiRoot.transform, "EconomyHUD", "", new Vector2(-300, -50), new Vector2(580, 68), 22);
            RectTransform hudRect = hud.rectTransform;
            hudRect.anchorMin = hudRect.anchorMax = new Vector2(1, 1);
            hud.alignment = TextAlignmentOptions.TopRight;
            placementHelp = Label(uiRoot.transform, "PlacementHelp", "", new Vector2(0, -120), new Vector2(1000, 60), 23);
            placementHelp.alignment = TextAlignmentOptions.Center;
            placementHelp.gameObject.SetActive(false);
            var shade = Rect(uiRoot.transform, "Shop", Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.gameObject.AddComponent<Image>().color = new Color(.015f, .025f, .04f, .88f);
            panel = shade.gameObject;
            RectTransform window = Rect(shade, "Window", Vector2.zero, new Vector2(960, 800));
            window.gameObject.AddComponent<Image>().color = new Color(.04f, .065f, .085f, 1);
            title = Label(window, "Title", "ARMERO", new Vector2(-55, 354), new Vector2(790, 45), 30);
            title.fontStyle = FontStyles.Bold;
            Button close = MakeButton(window, "Cerrar", new Vector2(405, 350), new Vector2(100, 42), out _);
            close.onClick.AddListener(Close);
            balance = Label(window, "Balance", "", new Vector2(0, 304), new Vector2(900, 34), 22);
            balance.color = new Color(1f, .82f, .34f);
            RectTransform list = Rect(window, "ProductScroll", new Vector2(0, -3), new Vector2(920, 550));
            list.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            rows = Rect(list, "Products", Vector2.zero, new Vector2(920, 550));
            rows.anchorMin = new Vector2(.5f, 1); rows.anchorMax = new Vector2(.5f, 1); rows.pivot = new Vector2(.5f, 1);
            scroll.viewport = list; scroll.content = rows;
            message = Label(window, "Message", "", new Vector2(0, -322), new Vector2(900, 60), 20);
            message.enableAutoSizing = true; message.fontSizeMin = 16; message.fontSizeMax = 20;
            Label(window, "Footer", "[E] Abrir vendedor   ·   [Esc] Cerrar   ·   La partida continúa durante la compra", new Vector2(0, -373), new Vector2(900, 28), 17);
            panel.SetActive(false);
        }

        private void BuildRows()
        {
            foreach (Transform child in rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            int count = vendor.Catalog.Count;
            buyButtons = new Button[count]; buyLabels = new TMP_Text[count];
            rows.sizeDelta = new Vector2(920, Mathf.Max(550, count * 61));
            rows.anchoredPosition = Vector2.zero;
            for (int index = 0; index < count; index++)
            {
                VendorOffer offer = vendor.Catalog.GetOffer(index);
                RectTransform row = Rect(rows, "Product_" + index, new Vector2(0, -29 - index * 61), new Vector2(910, 56));
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1);
                row.gameObject.AddComponent<Image>().color = new Color(.085f, .12f, .15f);
                if (offer != null && offer.Icon != null)
                {
                    Image icon = Rect(row, "Icon", new Vector2(-411, 0), new Vector2(66, 48)).gameObject.AddComponent<Image>();
                    icon.sprite = offer.Icon; icon.preserveAspect = true; icon.raycastTarget = false;
                }
                Label(row, "Name", offer != null ? offer.DisplayName : "Sin configurar", new Vector2(-145, 13), new Vector2(450, 28), 21);
                Label(row, "Description", offer != null ? offer.Description : "", new Vector2(-145, -14), new Vector2(450, 24), 15).color = new Color(.62f, .73f, .8f);
                Label(row, "Price", offer != null ? offer.price + " M" : "--", new Vector2(165, 0), new Vector2(105, 40), 23).color = new Color(1f, .82f, .34f);
                int offerIndex = index;
                buyButtons[index] = MakeButton(row, "COMPRAR", new Vector2(333, 0), new Vector2(210, 40), out buyLabels[index]);
                buyButtons[index].onClick.AddListener(() => TryBuy(offerIndex));
            }
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = fontSize;
            label.color = Color.white; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static Button MakeButton(Transform parent, string text, Vector2 position, Vector2 size, out TMP_Text label)
        {
            RectTransform rect = Rect(parent, text, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.12f, .4f, .43f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors; colors.disabledColor = new Color(.32f, .36f, .4f); button.colors = colors;
            label = Label(rect, "Label", text, Vector2.zero, size - new Vector2(10, 4), 18);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private void OnDisable()
        {
            Close();
            if (uiRoot != null) uiRoot.SetActive(false);
        }
        private void OnEnable() { if (uiRoot != null) uiRoot.SetActive(true); }
    }
}
