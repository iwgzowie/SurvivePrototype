using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class ZombieRewardBossSetup
    {
        public const string Folder = "Logs/ZombieRewardBoss";
        public const string BossPath = "Assets/Prefabs/Enemies/Boss_Zombie.prefab";
        public const string Level = "Assets/Scenes/Level 2.unity";
        [MenuItem("Survive/Boss/Validar recompensas y aparición Level 2")]
        public static void Validate() => ZombieRewardBossValidation.Run();
        public static void Preview()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            try
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BossPath));
                SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<NavMeshAgent>().enabled = false;
                root.transform.position = Vector3.up * 1.5f;
                var anim = root.GetComponentInChildren<Animator>();
                anim.runtimeAnimatorController.animationClips.First(c => c.name.Contains("Idle")).SampleAnimation(anim.gameObject, .4f);
                var light = new GameObject("KeyLight").AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(35, -35, 0);
                var fill = new GameObject("FillLight").AddComponent<Light>(); SceneManager.MoveGameObjectToScene(fill.gameObject, scene);
                fill.type = LightType.Directional; fill.intensity = .7f; fill.transform.rotation = Quaternion.Euler(25, 140, 0);
                var camera = new GameObject("BossPreviewCamera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.transform.position = new Vector3(3, 2.2f, 5.5f); camera.transform.LookAt(new Vector3(0, 1.5f, 0));
                camera.fieldOfView = 35; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f,.13f,.17f);
                camera.scene = scene; var target = new RenderTexture(900, 1000, 24); camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                var png = new Texture2D(900,1000,TextureFormat.RGB24,false); png.ReadPixels(new Rect(0,0,900,1000),0,0); png.Apply();
                File.WriteAllBytes(Folder + "/boss-model.png", png.EncodeToPNG());
                RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(png);
            }
            finally { RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static void Set(Object target, string field, Object value) => VendorTurretSetup.Set(target, field, value);
        [MenuItem("Survive/Boss/Configurar recompensas y modelo")]
        public static void Setup()
        {
            Directory.CreateDirectory(Folder);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Rewards/ZombieKillReward.wav");
            if (clip == null) throw new Exception("No se importó el sonido de recompensa.");
            foreach (string name in new[] { "EnemyChasing", "EnemyChasingRanged", "EnemyPatrol", "EnemyAttack" })
            {
                string path = "Assets/Players/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var data = new SerializedObject(root.GetComponent<EnemyHealth>());
                    data.FindProperty("killCoins").intValue = 5;
                    data.FindProperty("rewardSound").objectReferenceValue = clip;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Directory.CreateDirectory("Assets/Prefabs/Enemies"); AssetDatabase.Refresh();
            var boss = new GameObject("Boss_Zombie"); boss.tag = "Enemy";
            try
            {
                var collider = boss.AddComponent<CapsuleCollider>(); collider.height = 3; collider.radius = .8f;
                var agent = boss.AddComponent<NavMeshAgent>(); agent.agentTypeID = -902729914;
                agent.height = 3; agent.radius = .8f; agent.baseOffset = 1.5f; agent.speed = 3.5f;
                agent.acceleration = 10; agent.angularSpeed = 180; agent.stoppingDistance = 2;
                var health = boss.AddComponent<EnemyHealth>(); var data = new SerializedObject(health);
                data.FindProperty("maxHealth").floatValue = 700;
                data.FindProperty("destroyDelay").floatValue = 4;
                data.FindProperty("rewardSound").objectReferenceValue = clip;
                data.ApplyModifiedPropertiesWithoutUndo();
                var holder = new GameObject("BossVisual").transform; holder.SetParent(boss.transform, false);
                string model = "Assets/Synty/SidekickCharacters/Characters/ApocalypseZombies/ApocalypseZombie_05/ApocalypseZombie_05.prefab";
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model), holder);
                var animator = visual.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Enemies/Zombie_Melee.controller");
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Bounds bounds = BoundsOf(holder, boss.transform);
                holder.localScale = Vector3.one * (3f / bounds.size.y);
                bounds = BoundsOf(holder, boss.transform); holder.localPosition = Vector3.up * (-1.5f - bounds.min.y);
                Set(health, "meshRenderer", visual.GetComponentInChildren<SkinnedMeshRenderer>());
                var brain = boss.AddComponent<BossBase>();
                Set(brain, "minionPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Players/EnemyChasing.prefab"));
                Set(brain, "guaranteedKeyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosskey.prefab"));
                Set(brain, "coinPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/coin.prefab"));
                // La curación usa el consumible de vendas ya integrado.
                var healing = AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.StartsWith("Assets/Prefabs/")).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                    .FirstOrDefault(p => p != null && p.GetComponent<Survive.Items.HealingPickup>() != null);
                Set(brain, "healthPotionPrefab", healing);
                PrefabUtility.SaveAsPrefabAsset(boss, BossPath);
            }
            finally { Object.DestroyImmediate(boss); }
            AssetDatabase.SaveAssets();
            Audit();
        }
        static Bounds BoundsOf(Transform holder, Transform root)
        {
            Bounds bounds = default; bool first = true;
            foreach (var renderer in holder.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 p = root.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
                Object.DestroyImmediate(mesh);
            }
            if (first) throw new Exception("Boss sin geometría."); return bounds;
        }
        public static void Audit()
        {
            Directory.CreateDirectory(Folder);
            string report = "OPEN SCENES\n";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            { var s = SceneManager.GetSceneAt(i); report += s.path + " dirty=" + s.isDirty + "\n"; }
            foreach (string path in Directory.GetFiles("Assets/Scenes/Level 2", "*.asset"))
            {
                var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                report += path + " loaded=" + (data != null) + "\n";
            }
            File.WriteAllText(Folder + "/audit.txt", report);
            Debug.Log("ZOMBIE_REWARD_BOSS_SETUP=PASS");
        }
        [MenuItem("Survive/Boss/Asignar boss en Level 2 y comprobar navegación")]
        public static void ConfigureLevel()
        {
            Scene scene = SceneManager.GetSceneByPath(Level);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(Level, OpenSceneMode.Additive);
            Directory.CreateDirectory(".utmp/ZombieRewardBossBackup");
            File.Copy(Level, ".utmp/ZombieRewardBossBackup/Level2-disk.unity", true);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level2-BeforeBoss.temp.unity", true);
            File.Copy("Assets/Scenes/Level2-BeforeBoss.temp.unity", ".utmp/ZombieRewardBossBackup/Level2-editor.unity", true);
            AssetDatabase.DeleteAsset("Assets/Scenes/Level2-BeforeBoss.temp.unity");
            var spawner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossSpawner>(true)).Single();
            Set(spawner, "bossPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(BossPath));
            var point = (Transform)new SerializedObject(spawner).FindProperty("bossSpawnPoint").objectReferenceValue;
            string report = "BossSpawnPoint=" + point.position + "\n";
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerHealth>(true)).FirstOrDefault();
            foreach (var surface in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NavMeshSurface>(true)))
            {
                var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
                bool spawn = NavMesh.SamplePosition(point.position, out var hit, 3, filter);
                report += surface.name + " type=" + surface.agentTypeID + " data=" + AssetDatabase.GetAssetPath(surface.navMeshData) + " spawn=" + spawn + " hit=" + hit.position;
                if (player != null)
                {
                    bool target = NavMesh.SamplePosition(player.transform.position, out var targetHit, 5, filter);
                    var path = new NavMeshPath(); bool route = spawn && target && NavMesh.CalculatePath(hit.position, targetHit.position, filter, path);
                    report += " player=" + player.transform.position + " target=" + target + " route=" + route + " status=" + path.status;
                }
                report += "\n";
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Folder + "/level-navigation.txt", report);
            Debug.Log("ZOMBIE_BOSS_LEVEL_CONFIG=PASS");
        }
    }
}
