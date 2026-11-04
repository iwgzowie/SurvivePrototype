using Survive.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Survive.Economy
{
    /// <summary>Construye la vista Synty; las reglas y transacciones viven en PlayerVendorController.</summary>
    public sealed partial class PlayerVendorController
    {
        private VendorUITheme uiTheme;
        private TMP_Text inventoryStatus;

        private void EnsureUi()
        {
            if (uiRoot != null) return;
            uiTheme = VendorUITheme.Load();
            uiRoot = new GameObject("VendorAndTurretUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot.transform.SetParent(transform, false);
            Canvas canvas = uiRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
            CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0;
            if (EventSystem.current == null)
            {
                var events = new GameObject("VendorEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(uiRoot.transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            hud = Label(uiRoot.transform, "EconomyHUD", "", new Vector2(-340, -65), new Vector2(620, 80), 24);
            hud.rectTransform.anchorMin = hud.rectTransform.anchorMax = Vector2.one; hud.alignment = TextAlignmentOptions.TopRight;
            placementHelp = Label(uiRoot.transform, "PlacementHelp", "", new Vector2(0, -160), new Vector2(1150, 80), 26);
            placementHelp.alignment = TextAlignmentOptions.Center; placementHelp.gameObject.SetActive(false);
            RectTransform shade = Rect(uiRoot.transform, "Shop", Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.gameObject.AddComponent<Image>().color = new Color(.008f, .016f, .025f, .91f); panel = shade.gameObject;
            RectTransform window = Rect(shade, "Window", Vector2.zero, new Vector2(1760, 960));
            Surface(window, uiTheme != null ? uiTheme.panel : null, uiTheme != null ? uiTheme.background : new Color(.025f, .045f, .065f));
            title = Label(window, "Title", "ARMERO / SUMINISTROS", new Vector2(-95, 419), new Vector2(1370, 60), 40);
            title.color = Accent; title.fontStyle = FontStyles.Bold;
            Label(window, "Subtitle", "EQUIPATE PARA LA SIGUIENTE OLEADA", new Vector2(-220, 373), new Vector2(1120, 30), 19).color = Muted;
            Button close = MakeButton(window, "CERRAR  [ESC]", new Vector2(748, 418), new Vector2(180, 52), out _); close.onClick.AddListener(Close);
            RectTransform strip = Rect(window, "BalanceStrip", new Vector2(0, 320), new Vector2(1640, 54));
            Surface(strip, uiTheme != null ? uiTheme.card : null, new Color(.08f, .12f, .16f));
            Image money = Rect(strip, "CurrencyIcon", new Vector2(-783, 0), new Vector2(32, 32)).gameObject.AddComponent<Image>();
            money.sprite = uiTheme != null ? uiTheme.currency : null; money.preserveAspect = true; money.color = Accent; money.raycastTarget = false;
            balance = Label(strip, "Balance", "", new Vector2(-235, 0), new Vector2(1000, 42), 24); balance.color = Accent;
            inventoryStatus = Label(strip, "InventoryStatus", "", new Vector2(584, 0), new Vector2(410, 42), 22); inventoryStatus.alignment = TextAlignmentOptions.Right;
            RectTransform list = Rect(window, "ProductScroll", new Vector2(0, -25), new Vector2(1640, 620));
            list.gameObject.AddComponent<RectMask2D>(); ScrollRect scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            rows = Rect(list, "Products", Vector2.zero, new Vector2(1640, 620));
            rows.anchorMin = rows.anchorMax = new Vector2(.5f, 1); rows.pivot = new Vector2(.5f, 1);
            scroll.viewport = list; scroll.content = rows;
            RectTransform track = Rect(window, "ProductScrollbar", new Vector2(840, -25), new Vector2(12, 620));
            Surface(track, uiTheme != null ? uiTheme.card : null, new Color(.12f, .18f, .23f));
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            RectTransform handle = Rect(track, "Handle", Vector2.zero, new Vector2(12, 100));
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Accent;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            message = Label(window, "Message", "", new Vector2(0, -382), new Vector2(1640, 48), 23);
            message.color = Accent; message.enableAutoSizing = true; message.fontSizeMin = 19; message.fontSizeMax = 23;
            Label(window, "Footer", "RUEDA / BARRA VER MÁS    ·    [ESC] CERRAR    ·    LA PARTIDA CONTINÚA", new Vector2(0, -438), new Vector2(1640, 32), 19).color = Muted;
            panel.SetActive(false);
        }

        private void BuildRows()
        {
            foreach (Transform child in rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            int count = vendor.Catalog.Count; buyButtons = new Button[count]; buyLabels = new TMP_Text[count];
            const int columns = 3; const float width = 532, height = 190, stepX = 548, stepY = 206;
            rows.sizeDelta = new Vector2(1640, Mathf.Max(620, Mathf.CeilToInt(count / 3f) * stepY)); rows.anchoredPosition = Vector2.zero;
            for (int index = 0; index < count; index++)
            {
                VendorOffer offer = vendor.Catalog.GetOffer(index);
                RectTransform row = Rect(rows, "Product_" + index, new Vector2((index % columns - 1) * stepX, -height * .5f - index / columns * stepY), new Vector2(width, height));
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1);
                Surface(row, uiTheme != null ? uiTheme.card : null, uiTheme != null ? uiTheme.cardColor : new Color(.045f, .075f, .10f));
                Image frame = Rect(row, "SyntyFrame", Vector2.zero, new Vector2(width, height)).gameObject.AddComponent<Image>();
                frame.sprite = uiTheme != null ? uiTheme.frame : null; frame.type = Image.Type.Sliced; frame.color = new Color(.34f, .44f, .50f, .55f); frame.raycastTarget = false;
                TMP_Text name = Label(row, "Name", offer != null ? offer.DisplayName.ToUpperInvariant() : "SIN CONFIGURAR", new Vector2(0, 66), new Vector2(480, 34), 25);
                name.fontStyle = FontStyles.Bold;
                Image icon = Rect(row, "Icon", new Vector2(-164, 0), new Vector2(152, 88)).gameObject.AddComponent<Image>();
                icon.sprite = offer != null ? offer.Icon : null; icon.preserveAspect = true; icon.raycastTarget = false; icon.color = Color.white;
                TMP_Text description = Label(row, "Description", offer != null ? offer.Description : "", new Vector2(82, 6), new Vector2(300, 72), 19);
                description.color = Muted;
                TMP_Text price = Label(row, "Price", offer != null ? offer.price.ToString("N0") + " M" : "--", new Vector2(-155, -67), new Vector2(170, 38), 28); price.color = Accent;
                int offerIndex = index;
                buyButtons[index] = MakeButton(row, "COMPRAR", new Vector2(124, -66), new Vector2(224, 42), out buyLabels[index]);
                buyButtons[index].onClick.AddListener(() => TryBuy(offerIndex));
            }
        }

        private void RefreshInventoryStatus()
        {
            if (inventoryStatus == null) return;
            int occupied = 0; for (int i = 0; i < inventory.SlotCount; i++) if (inventory.IsOccupied(i)) occupied++;
            inventoryStatus.text = $"INVENTARIO  {occupied} / {inventory.SlotCount}";
        }
        private Color Accent => uiTheme != null ? uiTheme.accent : new Color(.94f, .74f, .36f);
        private Color Muted => uiTheme != null ? uiTheme.muted : new Color(.66f, .74f, .80f);
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static void Surface(RectTransform rect, Sprite sprite, Color color)
        { Image image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color; }
        private TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            TMP_Text label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (uiTheme != null && uiTheme.font != null) label.font = uiTheme.font;
            label.text = text; label.fontSize = fontSize; label.color = Color.white; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }
        private Button MakeButton(Transform parent, string text, Vector2 position, Vector2 size, out TMP_Text label)
        {
            RectTransform rect = Rect(parent, text, position, size); Surface(rect, uiTheme != null ? uiTheme.button : null, Color.white);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors; colors.normalColor = Accent; colors.highlightedColor = new Color(1, .88f, .60f);
            colors.pressedColor = new Color(.75f, .52f, .19f); colors.selectedColor = colors.highlightedColor; colors.disabledColor = new Color(.30f, .37f, .42f); button.colors = colors;
            label = Label(rect, "Label", text, Vector2.zero, size - new Vector2(16, 4), 19);
            label.color = new Color(.018f, .03f, .04f); label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 19;
            return button;
        }
    }
}
