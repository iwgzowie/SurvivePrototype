using System;
using System.IO;
using System.Linq;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UPP.ThirdPersonController;
using UPP.ThirdPersonController.Internal;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class HealingExplosiveSetup
    {
        public const string HealingPath = "Assets/Data/Consumables/HD_Bandages.asset";
        public const string PickupPath = "Assets/Prefabs/Consumables/BandagePickup.prefab";
        public const string AmmoPath = "Assets/Data/Vendor/AD_ExplosiveMagazine.asset";
        public const string Folder = "Logs/HealingExplosive";

        [MenuItem("Survive/Gameplay/Integrar vendas y cargador explosivo")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(Folder);
            foreach (string path in new[] { "Assets/Data/Consumables", "Assets/Prefabs/Consumables", "Assets/Materials/Consumables", "Assets/Resources/Gameplay" }) EnsureFolder(path);
            HealingDefinition healing = AssetDatabase.LoadAssetAtPath<HealingDefinition>(HealingPath);
            if (healing == null) { healing = ScriptableObject.CreateInstance<HealingDefinition>(); AssetDatabase.CreateAsset(healing, HealingPath); }
            healing.icon = Icon("ICON_SM_Prop_Bandage_01_BattleRoyale"); EditorUtility.SetDirty(healing);
            MedicalBandageModelSetup.PrepareAssets();
            Scene preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("BandagePickup"); SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var pickup = root.AddComponent<HealingPickup>(); VendorTurretSetup.Set(pickup, "definition", healing);
                VendorTurretSetup.Set(pickup, "hoverHeightCentimeters", 90f); VendorTurretSetup.Set(pickup, "rotationDegreesPerSecond", 55f);
                root.GetComponent<SphereCollider>().radius = .4f;
                MedicalBandageModelSetup.InstallVisual(root);
                PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            healing.worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath).GetComponent<HealingPickup>(); EditorUtility.SetDirty(healing);
            ExplosiveAmmoDefinition ammo = AssetDatabase.LoadAssetAtPath<ExplosiveAmmoDefinition>(AmmoPath);
            if (ammo == null) { ammo = ScriptableObject.CreateInstance<ExplosiveAmmoDefinition>(); AssetDatabase.CreateAsset(ammo, AmmoPath); }
            ammo.icon = Icon("ICON_SM_Wep_Bullet_Ammo_Box_02_BattleRoyale"); EditorUtility.SetDirty(ammo);
            VendorCatalog catalog = AssetDatabase.LoadAssetAtPath<VendorCatalog>(VendorTurretSetup.CatalogPath);
            var data = new SerializedObject(catalog); var offers = data.FindProperty("offers");
            if (!Enumerable.Range(0, offers.arraySize).Any(i => offers.GetArrayElementAtIndex(i).FindPropertyRelative("explosiveMagazine").objectReferenceValue == ammo))
            {
                int index = offers.arraySize++; var offer = offers.GetArrayElementAtIndex(index);
                offer.FindPropertyRelative("weaponPrefab").objectReferenceValue = null; offer.FindPropertyRelative("turret").objectReferenceValue = null;
                offer.FindPropertyRelative("explosiveMagazine").objectReferenceValue = ammo; offer.FindPropertyRelative("price").intValue = 90;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            BuildFlashMaterial(); AssetDatabase.SaveAssets();
            foreach (string path in new[] { "Assets/Scenes/Level 2 UI.unity", "Assets/Scenes/Level 2.unity" }) Integrate(path, healing.worldPrefab);
            Debug.Log("HEALING_EXPLOSIVE_SETUP_PASS");
        }

        static void Integrate(string path, HealingPickup prefab)
        {
            string backups = ".utmp/HealingExplosive-20261103"; Directory.CreateDirectory(backups);
            string backup = backups + "/" + Path.GetFileName(path); if (!File.Exists(backup)) File.Copy(path, backup);
            Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (UPPCharacterMovementComponent movement in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UPPCharacterMovementComponent>(true)))
            {
                movement.AimMode = UPPCharacterMovementCore.PressAimMode.HoldToAim;
                PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
            }
            if (!scene.GetRootGameObjects().Any(r => r.name == "HealingSupplies"))
            {
                PlayerWeaponInventory inventory = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerWeaponInventory>(true)).FirstOrDefault();
                if (inventory == null) throw new InvalidOperationException("Falta jugador en " + path);
                Transform player = inventory.transform; var root = new GameObject("HealingSupplies"); SceneManager.MoveGameObjectToScene(root, scene);
                for (int i = 0; i < 3; i++)
                {
                    GameObject item = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, scene); item.transform.SetParent(root.transform, false);
                    item.transform.position = player.position + player.forward * (1.7f + i * .55f) + player.right * 1.5f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SyntyGameplayHudSetup.Source + "Sprites/Icons_Resources/" + name + ".png")
            ?? throw new InvalidOperationException("Falta icono " + name);
        static void EnsureFolder(string path) { string parent = Path.GetDirectoryName(path).Replace('\\', '/'); if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        static void BuildFlashMaterial()
        {
            const string texturePath = "Assets/Resources/Gameplay/BlastParticle.png", path = "Assets/Resources/Gameplay/ExplosiveFlash.mat";
            if (!File.Exists(texturePath))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                { float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude; texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 1.5f))); }
                texture.Apply(); File.WriteAllBytes(texturePath, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(texturePath);
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 2); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One); material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = (int)RenderQueue.Transparent; EditorUtility.SetDirty(material);
        }
    }
}
