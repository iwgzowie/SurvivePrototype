using System;
using System.Linq;
using Survive.Combat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Survive.Editor
{
    /// <summary>Integra el inventario en el jugador y reutiliza su Canvas de gameplay.</summary>
    public static class WeaponInventorySetup
    {
        private const string PlayerPath = "Assets/Prefabs/Player/UPPPlayer.prefab";

        [MenuItem("Survive/Weapons/Integrar inventario y hotbar de seis slots")]
        public static void CreateAndIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de integrar el inventario.");
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var controller = root.GetComponent<PlayerWeaponController>();
                if (controller == null) throw new InvalidOperationException("Falta PlayerWeaponController.");
                var inventory = root.GetComponent<PlayerWeaponInventory>();
                if (inventory == null) inventory = root.AddComponent<PlayerWeaponInventory>();
                BindIfPresent(inventory, "weaponController", controller);
                Transform storage = root.transform.Find("InventoryStorage");
                if (storage == null)
                {
                    storage = new GameObject("InventoryStorage").transform;
                    storage.SetParent(root.transform, false);
                }
                BindIfPresent(inventory, "inventoryStorage", storage);
                NormalizeNumberBindings(inventory);
                BindIfPresent(controller, "inventory", inventory);
                var interaction = root.GetComponent<PlayerInteractionController>();
                if (interaction != null) BindIfPresent(interaction, "weaponInventory", inventory);
                Canvas canvas = root.GetComponentsInChildren<Canvas>(true)
                    .FirstOrDefault(c => c.name == "PlayerInteractionHUD");
                if (canvas == null) throw new InvalidOperationException("Falta el Canvas de gameplay existente.");
                Transform existing = canvas.transform.Find("WeaponHotbar");
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                if (font == null) font = TMP_Settings.defaultFontAsset;
                if (font == null) throw new InvalidOperationException("Falta la fuente del HUD.");
                var panel = new GameObject("WeaponHotbar", typeof(RectTransform), typeof(CanvasGroup));
                panel.layer = canvas.gameObject.layer;
                RectTransform rect = panel.GetComponent<RectTransform>();
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
                rect.pivot = new Vector2(.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 28f);
                rect.sizeDelta = new Vector2(660f, 146f);
                var group = panel.GetComponent<CanvasGroup>();
                group.interactable = group.blocksRaycasts = false;
                TMP_Text hint = Text(rect, "Controls", "1–6 / Rueda · Cambiar arma", new Vector2(0, 124), new Vector2(660, 22), 15, font);
                hint.color = new Color(.70f, .81f, .86f, 1f);
                var backgrounds = new Image[6];
                var bars = new Image[6];
                var icons = new Image[6];
                var numbers = new TMP_Text[6];
                var names = new TMP_Text[6];
                var ammunition = new TMP_Text[6];
                for (int i = 0; i < 6; i++)
                {
                    var card = new GameObject("Slot" + (i + 1), typeof(RectTransform), typeof(Image));
                    card.layer = panel.layer;
                    RectTransform cardRect = card.GetComponent<RectTransform>();
                    cardRect.SetParent(rect, false);
                    cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = Vector2.zero;
                    cardRect.anchoredPosition = new Vector2(10 + i * 108, 0);
                    cardRect.sizeDelta = new Vector2(100, 116);
                    backgrounds[i] = card.GetComponent<Image>();
                    backgrounds[i].raycastTarget = false;
                    backgrounds[i].color = new Color(.025f, .04f, .055f, .76f);
                    numbers[i] = Text(cardRect, "Number", (i + 1).ToString(), new Vector2(8, 96), new Vector2(84, 18), 14, font);
                    numbers[i].alignment = TextAlignmentOptions.MidlineLeft;
                    numbers[i].fontStyle = FontStyles.Bold;
                    var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconObject.layer = panel.layer;
                    RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                    iconRect.SetParent(cardRect, false);
                    iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = Vector2.zero;
                    iconRect.anchoredPosition = new Vector2(5, 42);
                    iconRect.sizeDelta = new Vector2(90, 56);
                    icons[i] = iconObject.GetComponent<Image>();
                    icons[i].preserveAspect = true;
                    icons[i].raycastTarget = false;
                    icons[i].enabled = false;
                    names[i] = Text(cardRect, "WeaponName", "Vacío", new Vector2(5, 21), new Vector2(90, 22), 11.5f, font);
                    names[i].textWrappingMode = TextWrappingModes.Normal;
                    names[i].enableAutoSizing = true;
                    names[i].fontSizeMin = 10;
                    names[i].fontSizeMax = 11.5f;
                    ammunition[i] = Text(cardRect, "Ammo", "—", new Vector2(6, 3), new Vector2(88, 17), 13, font);
                    var bar = new GameObject("Selected", typeof(RectTransform), typeof(Image));
                    bar.layer = panel.layer;
                    RectTransform barRect = bar.GetComponent<RectTransform>();
                    barRect.SetParent(cardRect, false);
                    barRect.anchorMin = new Vector2(0f, 0f);
                    barRect.anchorMax = new Vector2(1f, 0f);
                    barRect.pivot = new Vector2(.5f, 0f);
                    barRect.sizeDelta = new Vector2(0, 3);
                    bars[i] = bar.GetComponent<Image>();
                    bars[i].color = new Color(.28f, .85f, .94f, 1);
                    bars[i].raycastTarget = false;
                    bars[i].enabled = i == 0;
                }
                var hotbar = panel.AddComponent<PlayerWeaponHotbar>();
                var data = new SerializedObject(hotbar);
                data.FindProperty("inventory").objectReferenceValue = inventory;
                data.FindProperty("weaponController").objectReferenceValue = controller;
                data.FindProperty("canvasGroup").objectReferenceValue = group;
                References(data, "slotBackgrounds", backgrounds);
                References(data, "selectionBars", bars);
                References(data, "iconImages", icons);
                References(data, "numberLabels", numbers);
                References(data, "nameLabels", names);
                References(data, "ammoLabels", ammunition);
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            WeaponAudioSetup.CreateAndIntegrate();
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_WEAPON_INVENTORY_SETUP=PASS slots=6 hotbar=integrated reloadPlaceholder=removed");
        }

        private static void BindIfPresent(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (property == null) return;
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void NormalizeNumberBindings(PlayerWeaponInventory inventory)
        {
            var data = new SerializedObject(inventory);
            SerializedProperty actions = data.FindProperty("slotActions");
            for (int index = 0; index < actions.arraySize; index++)
            {
                SerializedProperty bindings = actions.GetArrayElementAtIndex(index).FindPropertyRelative("m_SingletonActionBindings");
                for (int binding = 0; binding < bindings.arraySize; binding++)
                {
                    SerializedProperty path = bindings.GetArrayElementAtIndex(binding).FindPropertyRelative("m_Path");
                    if (path.stringValue == $"<Keyboard>/digit{index + 1}") path.stringValue = $"<Keyboard>/{index + 1}";
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void References<T>(SerializedObject data, string field, T[] values) where T : UnityEngine.Object
        {
            SerializedProperty array = data.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static TMP_Text Text(RectTransform parent, string name, string initial, Vector2 position,
            Vector2 size, float fontSize, TMP_FontAsset font)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = initial;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Midline;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }
    }
}
