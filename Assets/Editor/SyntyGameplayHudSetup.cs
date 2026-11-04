using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Survive.Editor
{
    public static partial class SyntyGameplayHudSetup
    {
        public const string Source = "Assets/Synty/InterfaceMilitaryCombatHUD/";
        public const string Output = "Logs/SyntyGameplayHUD";

        [MenuItem("Survive/UI/Auditar assets Synty y canvases")]
        public static void Audit()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var text = new StringBuilder();
            foreach (string path in new[]
            {
                "Prefabs/_PreMadeHUDs/Screen_MilitaryCombat_HUD_3rdPerson_01.prefab",
                "Prefabs/Player_Health_Equipment/HUD_MilitaryCombat_HealthBar_01.prefab",
                "Prefabs/Player_Health_Equipment/HUD_MilitaryCombat_HealthStats_03.prefab",
                "Prefabs/Player_Health_Equipment/HUD_MilitaryCombat_CurrentWeapon_02.prefab",
                "Prefabs/Player_Health_Equipment/HUD_MilitaryCombat_CurrentWeapon_01.prefab",
                "Prefabs/ActionBar/HUD_MilitaryCombat_HotBar_05.prefab",
                "Prefabs/Input_Interactions/HUD_MilitaryCombat_Stat_Base_01.prefab",
                "Prefabs/Objectives_Story_Location/_Parts/HUD_MilitaryCombat_Objective_Header_01.prefab"
            })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source + path);
                text.AppendLine("ASSET " + path);
                if (prefab == null) { text.AppendLine("MISSING"); continue; }
                foreach (Transform item in prefab.GetComponentsInChildren<Transform>(true))
                {
                    string location = AnimationUtility.CalculateTransformPath(item, prefab.transform);
                    string detail = "";
                    if (item.TryGetComponent(out TMP_Text label)) detail += " TEXT=" + label.text.Replace("\n", "|");
                    if (item.TryGetComponent(out Slider slider)) detail += $" SLIDER={slider.value} fill={(slider.fillRect != null ? slider.fillRect.name : "none")}";
                    if (item.TryGetComponent(out Image image)) detail += " SPRITE=" + (image.sprite != null ? image.sprite.name : "none");
                    if (item is RectTransform rect) detail += $" size={rect.sizeDelta} anchor={rect.anchorMin}/{rect.anchorMax}";
                    text.AppendLine(location + detail);
                }
            }
            File.WriteAllText(Output + "/package-prefabs.txt", text.ToString());
            foreach (string scenePath in new[] { "Assets/Scenes/Level 2 UI.unity", "Assets/Scenes/Level 2.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(scenePath);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                text.Clear();
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                    {
                        text.AppendLine($"CANVAS {AnimationUtility.CalculateTransformPath(canvas.transform, null)} active={canvas.gameObject.activeInHierarchy} id={GlobalObjectId.GetGlobalObjectIdSlow(canvas.gameObject)}");
                        foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
                            text.AppendLine("  " + AnimationUtility.CalculateTransformPath(label.transform, canvas.transform) + " = " + label.text.Replace("\n", "|"));
                    }
                File.WriteAllText(Output + "/" + scene.name.Replace(' ', '-') + "-ui.txt", text.ToString());
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("SYNTY_HUD_AUDIT_PASS");
        }
    }
}
