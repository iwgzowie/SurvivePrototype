using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survive.Editor
{
    public static class BossLootSetup
    {
        public const string Level = "Assets/Scenes/Level 2.unity";
        public const string Evidence = "Logs/BossLoot";

        [MenuItem("Survive/Boss/Preparar materiales y drops")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(Evidence);
            EnsureFolder("Assets/Materials/Loot");
            Material coin = Material("CoinGold", new Color(1f, .65f, .10f), .65f);
            Material key = Material("BossKeyCyan", new Color(.1f, .8f, 1f), .45f);
            foreach (string path in new[] { "Assets/Prefabs/coin.prefab", "Assets/Prefabs/coin 1.prefab" }) Configure(path, coin, false);
            foreach (string path in new[] { "Assets/Prefabs/Bosskey.prefab", "Assets/Prefabs/Bosskey 1.prefab" }) Configure(path, key, true);
            AssetDatabase.SaveAssets();
            AuditLevel();
        }

        private static Material Material(string name, Color color, float metallic)
        {
            string path = "Assets/Materials/Loot/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", .45f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * .18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Configure(string path, Material material, bool key)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) != 0)
                    throw new InvalidOperationException("Script faltante en " + path);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Sin modelo 3D: " + path);
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    renderer.enabled = true;
                    renderer.gameObject.SetActive(true);
                    bounds.Encapsulate(renderer.bounds);
                    int count = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh.subMeshCount
                        : renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, count)).ToArray();
                }
                float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (size <= .0001f) throw new InvalidOperationException("Malla vacía: " + path);
                float scale = (key ? .65f : .35f) / size;
                root.transform.localScale = Vector3.one * scale;
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                SphereCollider trigger = root.GetComponent<SphereCollider>();
                if (trigger == null) trigger = root.AddComponent<SphereCollider>();
                trigger.enabled = true;
                trigger.isTrigger = true;
                trigger.center = root.transform.InverseTransformPoint(renderers[0].bounds.center);
                trigger.radius = .32f / scale;
                if (key)
                {
                    KeyPickup pickup = root.GetComponent<KeyPickup>();
                    if (pickup == null) pickup = root.AddComponent<KeyPickup>();
                    var serialized = new SerializedObject(pickup);
                    serialized.FindProperty("floatAmplitude").floatValue = .1f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    CoinPickup pickup = root.GetComponent<CoinPickup>();
                    if (pickup == null) pickup = root.AddComponent<CoinPickup>();
                    var serialized = new SerializedObject(pickup);
                    serialized.FindProperty("floatAmplitude").floatValue = .08f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"BOSS_LOOT_ASSET path={path} size={size:F5} scale={scale:F3} material={material.name} renderers={renderers.Length}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void AuditLevel()
        {
            Scene scene = EditorSceneManager.OpenScene(Level);
            var lines = new System.Collections.Generic.List<string>();
            foreach (Transform item in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            {
                if (!item.name.Contains("puerta", StringComparison.OrdinalIgnoreCase) && item.GetComponent<BossSpawner>() == null) continue;
                lines.Add($"OBJECT {item.name} pos={item.position} renderers={item.GetComponentsInChildren<Renderer>(true).Length} colliders={item.GetComponentsInChildren<Collider>(true).Length}");
                foreach (Transform child in item)
                    lines.Add($"  CHILD {child.name} pos={child.position} missing={PrefabUtility.IsPrefabAssetMissing(child.gameObject)} renderers={child.GetComponentsInChildren<Renderer>(true).Length}");
                if (item.TryGetComponent(out BossSpawner boss))
                    lines.Add("  BOSS_PREFAB=" + new SerializedObject(boss).FindProperty("bossPrefab").objectReferenceValue);
            }
            File.WriteAllLines(Evidence + "/level-audit.txt", lines);
            Transform gates = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "puertas boss");
            Transform barriers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "puertas niveles");
            var links = new System.Collections.Generic.List<string>();
            foreach (PuertaConLlave door in gates.GetComponentsInChildren<PuertaConLlave>(true))
            {
                Bounds bounds = door.GetComponent<Collider>().bounds;
                foreach (Collider collider in barriers.GetComponentsInChildren<Collider>(true))
                    if (bounds.Intersects(collider.bounds))
                        links.Add(GlobalObjectId.GetGlobalObjectIdSlow(door.gameObject).targetObjectId + "|"
                            + GlobalObjectId.GetGlobalObjectIdSlow(collider.gameObject).targetObjectId + "|" + door.name + "|" + collider.name);
            }
            File.WriteAllLines(Evidence + "/gate-links.txt", links);
            Debug.Log("BOSS_LOOT_SETUP_PASS");
        }

        private static void EnsureFolder(string path)
        {
            string parent = "Assets";
            foreach (string name in path.Split('/').Skip(1))
            {
                if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
                parent += "/" + name;
            }
        }
    }
}
