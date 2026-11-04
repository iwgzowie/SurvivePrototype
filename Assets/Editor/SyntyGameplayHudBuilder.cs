using System;
using System.IO;
using System.Linq;
using Survive.Items;
using Survive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static partial class SyntyGameplayHudSetup
    {
        public const string PrefabPath = "Assets/Prefabs/UI/SyntyGameplayHUD.prefab";
        private static readonly Color Accent = new Color(.94f, .74f, .36f);
        private static readonly Color Muted = new Color(.64f, .73f, .78f);
        private static TMP_FontAsset hudFont;

        [MenuItem("Survive/UI/Integrar HUD Synty en Level 2")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(Output); Directory.CreateDirectory(".utmp/SyntyHUD-20261103/before-scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            hudFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Source + "Fonts/Oxanium/Oxanium-Medium SDF.asset");
            Scene preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("SyntyGameplayHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 95;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0;
                CanvasGroup group = root.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
                SyntyGameplayHUD hud = root.AddComponent<SyntyGameplayHUD>();
                Ref(hud, "canvasGroup", group);

                RectTransform wave = Panel("WavePanel", root.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -34), new Vector2(390, 130));
                Icon(wave, "WaveIcon", "Icons_Status/ICON_MilitaryCombat_Status_Attack_01_Clean.png", new Vector2(40, 88), new Vector2(48, 48));
                Ref(hud, "waveText", Label(wave, "WaveValue", "OLEADA 01", new Vector2(88, 77), new Vector2(280, 44), 32, Accent));
                Ref(hud, "enemiesText", Label(wave, "EnemiesValue", "ENEMIGOS  0", new Vector2(88, 46), new Vector2(280, 30), 21, Color.white));
                Ref(hud, "phaseText", Label(wave, "WavePhase", "PREPARATE PARA SOBREVIVIR", new Vector2(22, 7), new Vector2(355, 30), 16, Muted));
                RectTransform stats = Panel("MissionStats", root.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -177), new Vector2(390, 52));
                Ref(hud, "scoreText", Label(stats, "ScoreValue", "PUNTAJE  0", new Vector2(18, 8), new Vector2(240, 36), 18, Color.white));
                Ref(hud, "timeText", Label(stats, "SurvivalTime", "00:00", new Vector2(279, 8), new Vector2(90, 36), 18, Muted, TextAlignmentOptions.Right));

                RectTransform economy = Panel("EconomyPanel", root.transform, Vector2.one, Vector2.one, new Vector2(-44, -34), new Vector2(330, 130));
                Icon(economy, "CurrencyIcon", "Icons_Inventory/ICON_MilitaryCombat_Inventory_Currency_02_Clean.png", new Vector2(44, 88), new Vector2(44, 44));
                Label(economy, "CoinsHeading", "MONEDAS", new Vector2(80, 93), new Vector2(200, 25), 16, Muted);
                Ref(hud, "coinsText", Label(economy, "CoinsValue", "0", new Vector2(80, 57), new Vector2(215, 40), 34, Accent));
                Icon(economy, "TurretIcon", "Icons_Special/ICON_MilitaryCombat_Special_Drone_01_Clean.png", new Vector2(42, 30), new Vector2(40, 34));
                Label(economy, "TurretHeading", "TORRETAS [T]", new Vector2(80, 31), new Vector2(140, 25), 16, Muted);
                Ref(hud, "turretText", Label(economy, "TurretValue", "0 / 10", new Vector2(220, 26), new Vector2(100, 30), 21, Color.white));
                RectTransform keyPanel = Panel("KeyPanel", root.transform, Vector2.one, Vector2.one, new Vector2(-44, -177), new Vector2(330, 44));
                Ref(hud, "keyText", Label(keyPanel, "KeyStatus", "SIN LLAVE", new Vector2(18, 7), new Vector2(294, 30), 18, Muted, TextAlignmentOptions.Right));

                RectTransform vitals = Panel("VitalsPanel", root.transform, Vector2.zero, Vector2.zero, new Vector2(44, 44), new Vector2(420, 132));
                Label(vitals, "HealthHeading", "ESTADO DEL AGENTE", new Vector2(24, 93), new Vector2(280, 26), 17, Muted);
                Ref(hud, "healthText", Label(vitals, "HealthValue", "100 <size=65%>/ 100</size>", new Vector2(227, 86), new Vector2(165, 40), 28, Color.white, TextAlignmentOptions.Right));
                RectTransform hp = Part("Prefabs/Player_Health_Equipment/HUD_MilitaryCombat_HealthBar_01.prefab", vitals, "SyntyHealthBar");
                Place(hp, new Vector2(.5f, 0), new Vector2(.5f, .5f), new Vector2(0, 49), new Vector2(760, 80)); hp.localScale = Vector3.one * .46f;
                Slider health = hp.GetComponentInChildren<Slider>(); health.interactable = false; health.minValue = 0; health.maxValue = 1; health.value = 1;
                if (health.handleRect != null) health.handleRect.gameObject.SetActive(false);
                Ref(hud, "healthBar", health); Ref(hud, "healthFill", health.fillRect.GetComponent<Image>());

                RectTransform weapon = Part("Prefabs/Player_Health_Equipment/HUD_MilitaryCombat_CurrentWeapon_02.prefab", root.transform, "CurrentWeaponPanel");
                Place(weapon, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 44), new Vector2(390, 168));
                weapon.Find("Input_Hotkey").gameObject.SetActive(false); weapon.Find("Item/Ammo").gameObject.SetActive(false);
                Image weaponBackground = weapon.Find("Item/SPR_Background").GetComponent<Image>();
                weaponBackground.sprite = Sprite("Sprites/HUD/SPR_HUD_MilitaryCombat_Background_DogEar_01.png");
                weaponBackground.type = Image.Type.Sliced; weaponBackground.color = new Color(.025f, .045f, .065f, .94f);
                Image weaponImage = weapon.Find("Item/ICON").GetComponent<Image>();
                Place(weaponImage.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24, 51), new Vector2(145, 75)); weaponImage.preserveAspect = true;
                Ref(hud, "weaponIcon", weaponImage);
                Ref(hud, "weaponName", Label(weapon, "WeaponName", "PISTOLA", new Vector2(24, 129), new Vector2(340, 30), 23, Accent));
                Ref(hud, "ammoText", Label(weapon, "MagazineValue", "12", new Vector2(188, 63), new Vector2(95, 67), 54, Color.white));
                Ref(hud, "reserveText", Label(weapon, "ReserveValue", "/ 72", new Vector2(278, 70), new Vector2(95, 45), 25, Muted));
                Ref(hud, "weaponStatus", Label(weapon, "WeaponStatus", "R RECARGAR · G SOLTAR", new Vector2(24, 14), new Vector2(350, 30), 16, Muted));
                Slider reload = Bar(weapon, "ReloadProgress", new Vector2(25, 49), new Vector2(340, 7)); Ref(hud, "reloadBar", reload); reload.gameObject.SetActive(false);
                BuildHotbar(root.transform, hud);

                RectTransform prompt = Panel("InteractionPanel", root.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -125), new Vector2(560, 66));
                Ref(hud, "promptRoot", prompt.gameObject);
                Ref(hud, "promptText", Label(prompt, "InteractionText", "[E] INTERACTUAR", new Vector2(18, 9), new Vector2(524, 48), 23, Color.white, TextAlignmentOptions.Center)); prompt.gameObject.SetActive(false);
                RectTransform boss = Panel("BossPanel", root.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -34), new Vector2(620, 90));
                Ref(hud, "bossRoot", boss.gameObject); Ref(hud, "bossName", Label(boss, "BossName", "JEFE", new Vector2(25, 47), new Vector2(570, 34), 23, Accent, TextAlignmentOptions.Center));
                Ref(hud, "bossBar", Bar(boss, "BossHealth", new Vector2(30, 20), new Vector2(560, 14))); boss.gameObject.SetActive(false);

                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                ConfigureWeaponIcons(hud);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssets();
            foreach (string path in new[] { "Assets/Scenes/Level 2 UI.unity", "Assets/Scenes/Level 2.unity" }) IntegrateScene(path);
            Debug.Log("SYNTY_HUD_BUILD_PASS " + PrefabPath);
        }

        private static void BuildHotbar(Transform parent, SyntyGameplayHUD hud)
        {
            RectTransform bar = Part("Prefabs/ActionBar/HUD_MilitaryCombat_HotBar_05.prefab", parent, "WeaponSlots");
            foreach (string path in new[] { "XPBar", "SPR_Shadow", "SPR_Greeble_Scope" }) if (bar.Find(path) != null) bar.Find(path).gameObject.SetActive(false);
            Place(bar, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 80), new Vector2(900, 130)); bar.localScale = Vector3.one * .75f;
            Transform content = bar.Find("ActionBar");
            foreach (LayoutGroup layout in content.GetComponents<LayoutGroup>()) Object.DestroyImmediate(layout);
            foreach (ContentSizeFitter fit in content.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fit);
            GameObject template = content.GetChild(0).gameObject;
            while (content.childCount < 6) Object.Instantiate(template, content);
            var serialized = new SerializedObject(hud); var slots = serialized.FindProperty("slots"); slots.arraySize = 6;
            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform item = (RectTransform)content.GetChild(i); item.name = "Slot" + (i + 1);
                if (i >= 6) { item.gameObject.SetActive(false); continue; }
                Place(item, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2((i - 2.5f) * 146, 0), new Vector2(130, 130));
                foreach (Selectable control in item.GetComponentsInChildren<Selectable>(true)) Object.DestroyImmediate(control);
                item.Find("Item/Highlighted").gameObject.SetActive(false);
                var selected = item.Find("Item/Selected").gameObject; selected.SetActive(i == 0);
                item.Find("Item/Input_Hotkey/Icon").gameObject.SetActive(false);
                item.Find("Item/Input_Hotkey").GetComponent<Image>().color = Accent;
                item.Find("Item/Input_Hotkey/Input").GetComponent<TMP_Text>().text = (i + 1).ToString();
                item.Find("Item/Input_Hotkey/Input").GetComponent<TMP_Text>().color = new Color(.025f, .045f, .065f);
                Image icon = item.Find("Item/Icon/ICON").GetComponent<Image>(); icon.preserveAspect = true;
                TMP_Text ammo = Label(item, "SlotAmmo", i == 0 ? "12 / 72" : "VACÍO", new Vector2(0, 4), new Vector2(130, 27), 22, Muted, TextAlignmentOptions.Center);
                var slot = slots.GetArrayElementAtIndex(i); slot.FindPropertyRelative("selected").objectReferenceValue = selected;
                slot.FindPropertyRelative("icon").objectReferenceValue = icon; slot.FindPropertyRelative("ammo").objectReferenceValue = ammo;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Label(parent, "InventoryControls", "1–6 / RUEDA · CAMBIAR ARMA", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 191), new Vector2(670, 28), 16, Muted, TextAlignmentOptions.Center);
        }

        private static void ConfigureWeaponIcons(SyntyGameplayHUD hud)
        {
            SyntyWeaponIconSetup.Assign();
            SyntyWeaponIconSetup.ApplyPreview(hud);
        }

        private static void IntegrateScene(string path)
        {
            string backup = ".utmp/SyntyHUD-20261103/before-scenes/" + Path.GetFileName(path);
            if (!File.Exists(backup)) File.Copy(path, backup);
            Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (!scene.GetRootGameObjects().Any(r => r.GetComponent<SyntyGameplayHUD>() != null)) PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
                if (canvas.name == "PlayerInteractionHUD" || canvas.name == "WaveHUD") { canvas.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(canvas); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        private static RectTransform Part(string path, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source + path), parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); instance.name = name;
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            foreach (MonoBehaviour script in instance.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null && script.GetType().Namespace != null && script.GetType().Namespace.Contains(".Samples")) Object.DestroyImmediate(script);
            return (RectTransform)instance.transform;
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent); Place(rect, anchor, pivot, position, size);
            Image image = rect.gameObject.AddComponent<Image>(); image.sprite = Sprite("Sprites/HUD/SPR_HUD_MilitaryCombat_Background_DogEar_01.png"); image.type = Image.Type.Sliced; image.color = new Color(.025f, .045f, .065f, .94f);
            return rect;
        }
        private static void Icon(Transform parent, string name, string path, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent); Place(rect, Vector2.zero, new Vector2(.5f, .5f), position, size);
            Image image = rect.gameObject.AddComponent<Image>(); image.sprite = Sprite("Sprites/" + path); image.preserveAspect = true; image.color = Accent;
        }
        private static Slider Bar(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent); Place(rect, Vector2.zero, Vector2.zero, position, size);
            Image bg = rect.gameObject.AddComponent<Image>(); bg.sprite = Sprite("Sprites/HUD/SPR_HUD_MilitaryCombat_Box_White_01.png"); bg.color = new Color(.12f, .18f, .22f);
            RectTransform fill = Rect("Fill", rect); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            Image image = fill.gameObject.AddComponent<Image>(); image.sprite = bg.sprite; image.color = Accent;
            Slider slider = rect.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.minValue = 0; slider.maxValue = 1; slider.value = 1; slider.interactable = false;
            return slider;
        }
        private static TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
            => Label(parent, name, value, Vector2.zero, Vector2.zero, position, size, fontSize, color, alignment);
        private static TMP_Text Label(Transform parent, string name, string value, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            RectTransform rect = Rect(name, parent); Place(rect, anchor, pivot, position, size);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = hudFont; text.fontSize = Mathf.Max(18, fontSize); text.color = color; text.alignment = alignment; text.text = value;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
            return text;
        }
        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Source + path) ?? throw new InvalidOperationException("Falta sprite " + path);
        private static RectTransform Rect(string name, Transform parent) { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size) { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; }
        private static void Ref(Object target, string field, Object value) { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
