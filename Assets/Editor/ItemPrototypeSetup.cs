using System;
using System.IO;
using System.Linq;
using Survive.Combat;
using Survive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Survive.Editor
{
    /// <summary>Prepara el prototipo sin reemplazar los assets configurados previamente.</summary>
    public static class ItemPrototypeSetup
    {
        const string Items = "Assets/Prefabs/Items";
        const string Materials = "Assets/Materials/Items";
        const string Player = "Assets/Prefabs/Player/UPPPlayer.prefab";
        const string Scene = "Assets/StarterAssets/ThirdPersonController/Scenes/Playground.unity";

        [MenuItem("Survive/Items/Crear e integrar prototipo")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Folder(Items);
            Folder(Materials);
            var body = MakeMaterial("Weapon_Body", new Color(.065f, .09f, .13f), false);
            var accent = MakeMaterial("Weapon_Accent", new Color(.12f, .8f, .95f), false);
            var tracer = MakeMaterial("Weapon_Tracer", new Color(1f, .77f, .2f), true);
            MakeItem(false, body, accent, tracer);
            MakeItem(true, body, accent, tracer);
            IntegratePlayer();
            IntegrateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_ITEMS_SETUP=PASS");
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        static Material MakeMaterial(string name, Color color, bool unlit)
        {
            string path = $"{Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("No se encontró el shader URP del arma.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (!unlit) { material.SetFloat("_Metallic", .45f); material.SetFloat("_Smoothness", .35f); }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void MakeItem(bool isWeapon, Material body, Material accent, Material tracer)
        {
            string name = isWeapon ? "Weapon_Rifle" : "MasterItem";
            string path = $"{Items}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var root = new GameObject(name);
            try
            {
                MasterItem item = isWeapon ? root.AddComponent<WeaponItem>() : root.AddComponent<MasterItem>();
                var trigger = root.GetComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = .65f;
                var rigidbody = root.GetComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                if (!isWeapon)
                    Part(root.transform, "Visual", Vector3.zero, Vector3.one * .2f, body);
                else
                {
                    // El origen coincide con la empuñadura; +Z local apunta hacia el cañón.
                    Part(root.transform, "Receiver", new Vector3(0,.065f,.12f), new Vector3(.11f,.13f,.4f), body);
                    Part(root.transform, "Barrel", new Vector3(0,.085f,.45f), new Vector3(.055f,.055f,.32f), body);
                    Part(root.transform, "Stock", new Vector3(0,.05f,-.18f), new Vector3(.09f,.11f,.24f), body);
                    Part(root.transform, "Grip", new Vector3(0,-.07f,0), new Vector3(.065f,.2f,.075f), body);
                    Part(root.transform, "Magazine", new Vector3(0,-.085f,.18f), new Vector3(.07f,.2f,.11f), body);
                    Part(root.transform, "Sight", new Vector3(0,.155f,.17f), new Vector3(.065f,.045f,.16f), accent);
                    Part(root.transform, "Accent", new Vector3(0,.1f,.3f), new Vector3(.12f,.025f,.12f), accent);
                    var muzzle = new GameObject("Muzzle").transform;
                    muzzle.SetParent(root.transform, false);
                    muzzle.localPosition = new Vector3(0,.085f,.625f);
                    var flash = Part(muzzle, "MuzzleFlash", new Vector3(0,0,.045f), new Vector3(.09f,.09f,.11f), tracer);
                    flash.SetActive(false);
                    var data = new SerializedObject(item);
                    data.FindProperty("itemType").enumValueIndex = (int)ItemType.WeaponType;
                    data.FindProperty("muzzle").objectReferenceValue = muzzle;
                    data.FindProperty("tracerMaterial").objectReferenceValue = tracer;
                    data.FindProperty("muzzleFlash").objectReferenceValue = flash;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    // El centro de Grip coincide con el origen del atache.
                    foreach (Transform child in root.transform)
                        child.localPosition += Vector3.up * .07f;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static GameObject Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        static void IntegratePlayer()
        {
            var root = PrefabUtility.LoadPrefabContents(Player);
            try
            {
                var controller = root.GetComponent<PlayerWeaponController>();
                if (controller == null) controller = root.AddComponent<PlayerWeaponController>();
                var data = new SerializedObject(controller);
                if (data.FindProperty("weaponSocket").objectReferenceValue == null)
                    data.FindProperty("weaponSocket").objectReferenceValue = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WeaponSocket");
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Player);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void IntegrateScene()
        {
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(root => root.name == "Weapon_Rifle_Pickup")) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Items}/Weapon_Rifle.prefab");
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            item.name = "Weapon_Rifle_Pickup";
            item.transform.position = new Vector3(1.6f, 0, 1.6f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
