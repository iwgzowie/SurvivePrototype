using System;
using System.IO;
using System.Linq;
using Survive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class MusicControlsSetup
    {
        public static void SetupAndValidate() { Run(); MusicControlsAfterValidation.Run(); }
        [MenuItem("Survive/Audio/Integrar volumen de música en opciones y pausa")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SceneAlejandro.unity");
            var options = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuOptions>(true)).Single();
            if (options.musicSlider == null)
            {
                var original = options.volumeSlider.transform.parent;
                var row = Object.Instantiate(original.gameObject, original.parent);
                row.name = "MusicVolume";
                ((RectTransform)row.transform).anchoredPosition = ((RectTransform)original).anchoredPosition + Vector2.down * 65;
                var title = row.GetComponent<TMP_Text>(); title.text = "Música";
                var slider = row.GetComponentInChildren<Slider>(true);
                slider.onValueChanged = new Slider.SliderEvent();
                options.musicSlider = slider;
                var value = Text("MusicPercent", slider.transform, new Vector2(50, -24), new Vector2(60, 24), "100%", 17, TextAlignmentOptions.Right);
                options.musicValueLabel = value;
                StyleSlider(slider);
            }
            EditorUtility.SetDirty(options); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var root = PrefabUtility.LoadPrefabContents(GameplayMenuFixSetup.PausePrefab);
            try { ConfigurePause(root); PrefabUtility.SaveAsPrefabAsset(root, GameplayMenuFixSetup.PausePrefab); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var level = EditorSceneManager.OpenScene("Assets/Scenes/Level 2.unity");
            var menu = level.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuBehaviour>(true)).Single();
            ConfigurePause(menu.gameObject);
            EditorSceneManager.MarkSceneDirty(level); EditorSceneManager.SaveScene(level);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/MusicControls-20261105");
            File.WriteAllText("Logs/MusicControls-20261105/setup.txt", "Música independiente en Panel Options/PanelAudio y SyntyPauseMenu. Preferencia Survive.Options.MusicVolume. Level 2: sólo layout del menú de pausa para conservar clasificación y separar el slider. Mixer sin cambios.\n");
        }
        public static void ConfigurePause(GameObject root)
        {
            var menu = root.GetComponent<MenuBehaviour>();
            var panel = (RectTransform)menu.container.transform.Find("Menu");
            bool hasLeaderboard = panel.Find("Leaderboard") != null;
            panel.sizeDelta = new Vector2(650, hasLeaderboard ? 860 : 800);
            Move(panel, "Heading", hasLeaderboard ? 345 : 285); Move(panel, "Subheading", hasLeaderboard ? 290 : 228);
            Move(panel, "Continue", hasLeaderboard ? 190 : 135); Move(panel, "Restart", hasLeaderboard ? 100 : 45);
            if (hasLeaderboard) Move(panel, "Leaderboard", 10);
            Move(panel, "MainMenu", hasLeaderboard ? -80 : -45); Move(panel, "Exit", hasLeaderboard ? -170 : -135); Move(panel, "Hint", hasLeaderboard ? -380 : -340);
            if (panel.Find("MusicVolume") != null) Move(panel, "MusicVolume", hasLeaderboard ? -280 : -245);
            var options = menu.container.GetComponent<MenuOptions>();
            if (options == null) options = menu.container.AddComponent<MenuOptions>();
            if (options.musicSlider != null) return;
            var row = Rect("MusicVolume", panel, new Vector2(490, 70), new Vector2(0, hasLeaderboard ? -280 : -245));
            Text("Label", row, new Vector2(-50, 24), new Vector2(390, 30), "VOLUMEN DE MÚSICA", 21, TextAlignmentOptions.Left);
            var value = Text("Percent", row, new Vector2(195, 24), new Vector2(100, 30), "100%", 21, TextAlignmentOptions.Right);
            var track = Rect("Slider", row, new Vector2(490, 26), new Vector2(0, -10));
            var background = Rect("Background", track, Vector2.zero, Vector2.zero); Stretch(background, Vector2.zero);
            var image = background.gameObject.AddComponent<Image>(); image.sprite = VendorUITheme.Load().frame; image.type = Image.Type.Sliced; image.color = VendorUITheme.Load().cardColor;
            var area = Rect("Fill Area", track, Vector2.zero, Vector2.zero); Stretch(area, new Vector2(8, 4));
            var fill = Rect("Fill", area, Vector2.zero, Vector2.zero); Stretch(fill, Vector2.zero);
            fill.gameObject.AddComponent<Image>().color = VendorUITheme.Load().accent;
            var handleArea = Rect("Handle Area", track, Vector2.zero, Vector2.zero); Stretch(handleArea, new Vector2(10, 0));
            var handle = Rect("Handle", handleArea, new Vector2(20, 34), Vector2.zero);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.sprite = VendorUITheme.Load().button; handleImage.type = Image.Type.Sliced; handleImage.color = Color.white;
            var slider = track.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage; slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            options.musicSlider = slider; options.musicValueLabel = value;
        }
        static void StyleSlider(Slider slider)
        {
            var theme = VendorUITheme.Load();
            var background = EnsureImage(slider.transform.Find("Background").gameObject); background.sprite = theme.frame; background.type = Image.Type.Sliced; background.color = theme.cardColor;
            EnsureImage(slider.fillRect.gameObject).color = theme.accent;
            if (slider.targetGraphic != null) slider.targetGraphic.color = Color.white;
        }
        static Image EnsureImage(GameObject target)
        {
            var image = target.GetComponent<Image>();
            if (image != null) return image;
            var graphic = target.GetComponent<Graphic>();
            if (graphic != null) Object.DestroyImmediate(graphic);
            return target.AddComponent<Image>();
        }
        static void Move(RectTransform parent, string name, float y)
        {
            var rect = (RectTransform)parent.Find(name); rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        static void Stretch(RectTransform rect, Vector2 inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = inset; rect.offsetMax = -inset;
        }
        static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, string value, float fontSize, TextAlignmentOptions alignment)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            var theme = VendorUITheme.Load(); text.font = theme.font; text.fontSize = fontSize; text.text = value; text.alignment = alignment; text.color = theme.accent; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap; return text;
        }
    }
}
