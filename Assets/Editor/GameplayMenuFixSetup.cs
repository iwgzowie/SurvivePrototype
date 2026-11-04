using System;
using System.IO;
using System.Linq;
using Survive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class GameplayMenuFixSetup
    {
        public const string Folder = "Logs/GameplayMenuFix", PausePrefab = "Assets/Prefabs/UI/SyntyPauseMenu.prefab";
        const string Main = "Assets/Scenes/SceneAlejandro.unity", Level = "Assets/Scenes/Level 2.unity";
        static VendorUITheme theme;
        [MenuItem("Survive/Gameplay/Integrar pausa y destino Level 2")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(".utmp/GameplayMenuFixBackup");
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(Main, OpenSceneMode.Single);
            BuildPause();
            foreach (string path in new[] { Main, Level })
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                File.Copy(path, ".utmp/GameplayMenuFixBackup/" + scene.name + "-disk.unity", true);
                string copy = "Assets/Scenes/" + scene.name + "-BeforeMenuFix.temp.unity";
                EditorSceneManager.SaveScene(scene, copy, true);
                File.Copy(copy, ".utmp/GameplayMenuFixBackup/" + scene.name + "-editor.unity", true);
                AssetDatabase.DeleteAsset(copy);
                if (path == Main)
                {
                    foreach (var menu in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuBehaviour>(true)))
                    {
                        var data = new SerializedObject(menu); data.FindProperty("gameplayScene").stringValue = "Level 2";
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                else
                {
                    var menus = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuBehaviour>(true)).ToArray();
                    if (menus.Length == 0)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefab), scene);
                        instance.name = "SyntyPauseMenu";
                    }
                    else if (menus.Length != 1 || menus[0].container == null)
                        throw new Exception("Level 2 tiene una configuración de menú inesperada; revisar antes de duplicar gestores.");
                    if (!scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Any())
                    {
                        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                        SceneManager.MoveGameObjectToScene(go, scene); go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == Level)) buildScenes.Add(new EditorBuildSettingsScene(Level, true));
            else buildScenes.Single(s => s.path == Level).enabled = true;
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Folder + "/setup.txt", "Play de SceneAlejandro -> Level 2. Level 2 habilitada en Build Settings. SyntyPauseMenu + EventSystem/InputSystemUIInputModule en Level 2. ESC/Continuar/Reiniciar/Menú principal/Salir enlazados a MenuBehaviour.\n");
            Debug.Log("GAMEPLAY_MENU_FIX_SETUP=PASS");
        }
        public static void Validate() => GameplayMenuFixValidation.Run();
        public static void SetupAndValidate() { Run(); Validate(); }
        static void BuildPause()
        {
            theme = VendorUITheme.Load(); if (theme == null) throw new Exception("Falta el tema Synty.");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("SyntyPauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
                var menu = root.AddComponent<MenuBehaviour>();
                var shade = Rect("PausePanel", root.transform, Vector2.zero, Vector2.zero, Vector2.zero);
                shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
                shade.gameObject.AddComponent<Image>().color = new Color(.012f,.022f,.035f,.82f);
                var panel = Rect("Menu", shade, new Vector2(650,690), Vector2.zero, new Vector2(.5f,.5f));
                var image = panel.gameObject.AddComponent<Image>(); image.sprite = theme.panel; image.type = Image.Type.Sliced; image.color = theme.background;
                Label(panel, "Heading", "PARTIDA EN PAUSA", new Vector2(0,240), new Vector2(560,70), 44, theme.accent);
                Label(panel, "Subheading", "LEVEL 2 · SUPERVIVENCIA", new Vector2(0,183), new Vector2(560,38), 20, theme.muted);
                Button(panel, "Continue", "CONTINUAR", 90, menu.ResumeGame);
                Button(panel, "Restart", "REINICIAR", 0, menu.ReloadCurrentScene);
                Button(panel, "MainMenu", "MENÚ PRINCIPAL", -90, menu.BackToMainMenu);
                Button(panel, "Exit", "SALIR DEL JUEGO", -180, menu.ExitGame);
                Label(panel, "Hint", "ESC · VOLVER A LA PARTIDA", new Vector2(0,-280), new Vector2(560,36), 19, theme.muted);
                menu.container = shade.gameObject; shade.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PausePrefab);
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos, Vector2 anchor)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f,.5f); rect.sizeDelta = size; rect.anchoredPosition = pos; return rect;
        }
        static void Label(Transform parent, string name, string value, Vector2 pos, Vector2 size, float fontSize, Color color)
        {
            var text = Rect(name,parent,size,pos,new Vector2(.5f,.5f)).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = theme.font; text.text = value; text.fontSize = fontSize; text.color = color;
            text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        }
        static void Button(Transform parent, string name, string label, float y, UnityAction action)
        {
            var rect = Rect(name,parent,new Vector2(490,70),new Vector2(0,y),new Vector2(.5f,.5f));
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = theme.button; image.type = Image.Type.Sliced; image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = theme.cardColor; colors.highlightedColor = new Color(.25f,.31f,.32f); colors.selectedColor = colors.highlightedColor; colors.pressedColor = theme.accent; button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            Label(rect,"Label",label,Vector2.zero,new Vector2(460,60),26,Color.white);
        }
    }
}
