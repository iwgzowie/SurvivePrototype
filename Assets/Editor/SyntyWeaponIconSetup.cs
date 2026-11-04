using System;
using Survive.Items;
using Survive.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Survive.Editor
{
    /// <summary>Asignación explícita de iconos Synty a color, compartidos por todas las vistas.</summary>
    public static class SyntyWeaponIconSetup
    {
        public const string ThemePath = "Assets/Resources/UI/SyntyVendorTheme.asset";
        public static readonly string[] WeaponIds = { "Pistol", "Revolver", "SMG", "AssaultRifle", "MachineGun", "Shotgun", "HuntingRifle", "SniperRifle" };
        public static readonly string[] SpriteNames =
        {
            "ICON_SM_Wep_Pistol_01_Military", "ICON_SM_Wep_Pistol_Revolver_01_BattleRoyale",
            "ICON_SM_Wep_SubMGun_Lite_01_BattleRoyale", "ICON_SM_Wep_Preset_A_Rifle_02_Military",
            "ICON_SM_Wep_MachineGun_USA_01_Military", "ICON_SM_Wep_Shotgun_01_Military",
            "ICON_SM_Wep_Rifle_01_Military", "ICON_SM_Wep_Sniper_01_Military"
        };
        public static void Assign()
        {
            for (int i = 0; i < WeaponIds.Length; i++)
            {
                WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/WD_" + WeaponIds[i] + ".asset");
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SyntyGameplayHudSetup.Source + "Sprites/Icons_Weapons/" + SpriteNames[i] + ".png");
                if (definition == null || sprite == null) throw new InvalidOperationException("Falta preset o icono: " + WeaponIds[i]);
                var serialized = new SerializedObject(definition); serialized.FindProperty("icon").objectReferenceValue = sprite; serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }
        }

        [MenuItem("Survive/UI/Asignar iconos a color y tema Synty del vendedor")]
        public static void Run()
        {
            Assign();
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");
            VendorUITheme theme = AssetDatabase.LoadAssetAtPath<VendorUITheme>(ThemePath);
            if (theme == null) { theme = ScriptableObject.CreateInstance<VendorUITheme>(); AssetDatabase.CreateAsset(theme, ThemePath); }
            theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SyntyGameplayHudSetup.Source + "Fonts/Oxanium/Oxanium-Medium SDF.asset");
            theme.panel = Sprite("HUD/SPR_HUD_MilitaryCombat_Background_DogEar_01");
            theme.card = Sprite("HUD/SPR_HUD_MilitaryCombat_Box_White_01");
            theme.frame = Sprite("MilitaryCombat/SPR_MilitaryCombat_Frame_Small_03");
            theme.button = Sprite("HUD/SPR_HUD_MilitaryCombat_Background_Trapezoid_01_Clean");
            theme.currency = Sprite("Icons_Inventory/ICON_MilitaryCombat_Inventory_Currency_02_Clean");
            EditorUtility.SetDirty(theme);
            GameObject prefab = PrefabUtility.LoadPrefabContents(SyntyGameplayHudSetup.PrefabPath);
            try
            {
                ApplyPreview(prefab.GetComponent<SyntyGameplayHUD>());
                PrefabUtility.SaveAsPrefabAsset(prefab, SyntyGameplayHudSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets(); Debug.Log("SYNTY_VENDOR_ICON_SETUP_PASS count=8");
        }
        public static void ApplyPreview(SyntyGameplayHUD hud)
        {
            var serialized = new SerializedObject(hud);
            Image equipped = (Image)serialized.FindProperty("weaponIcon").objectReferenceValue;
            WeaponDefinition pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/WD_Pistol.asset");
            equipped.sprite = pistol.Icon; equipped.color = Color.white;
            var slots = serialized.FindProperty("slots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                Image icon = (Image)slots.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue;
                icon.sprite = i == 0 ? pistol.Icon : null; icon.enabled = i == 0; icon.color = Color.white;
            }
        }
        private static Sprite Sprite(string path)
        {
            Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(SyntyGameplayHudSetup.Source + "Sprites/" + path + ".png");
            if (result == null) throw new InvalidOperationException("Falta sprite Synty " + path);
            return result;
        }
    }
}
