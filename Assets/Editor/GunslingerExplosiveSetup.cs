using System;
using System.IO;
using System.Linq;
using System.Text;
using Survive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class GunslingerExplosiveSetup
    {
        public const string Source = "Assets/Hovl Studio/Realistic Gunslinger effects/Prefabs/Bullet explosion.prefab";
        public const string PrefabPath = "Assets/Prefabs/VFX/GunslingerMiniBulletExplosion.prefab";
        public const string Folder = "Logs/GunslingerExplosive";
        [MenuItem("Survive/Gameplay/Vincular mini explosión Gunslinger")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/VFX")) AssetDatabase.CreateFolder("Assets/Prefabs", "VFX");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null) throw new InvalidOperationException("No se importó Bullet explosion del paquete.");
            GameObject copy = Object.Instantiate(source); copy.name = "GunslingerMiniBulletExplosion";
            var report = new StringBuilder();
            try
            {
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // La aparición y el fin los administra el pool del juego; el prefab conserva sólo la presentación.
                foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (MonoBehaviour component in copy.GetComponentsInChildren<MonoBehaviour>(true)) if (component != null) Object.DestroyImmediate(component);
                foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                foreach (ParticleSystem ps in copy.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); var main = ps.main;
                    main.loop = false; main.playOnAwake = false; main.stopAction = ParticleSystemStopAction.None;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.useUnscaledTime = false;
                    report.AppendLine(ps.name + " duration=" + main.duration + " lifetime=" + main.startLifetime.constantMax + " delay=" + main.startDelay.constantMax);
                }
                foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader == null || !material.shader.isSupported) throw new InvalidOperationException("Material no compatible en " + renderer.name);
                        report.AppendLine("Material " + material.name + " shader=" + material.shader.name);
                    }
                PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
            }
            finally { Object.DestroyImmediate(copy); }
            ExplosiveAmmoDefinition ammo = AssetDatabase.LoadAssetAtPath<ExplosiveAmmoDefinition>(HealingExplosiveSetup.AmmoPath);
            ammo.impactEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); ammo.impactEffectScale = .3f; ammo.impactEffectLifetime = 2.5f;
            EditorUtility.SetDirty(ammo); AssetDatabase.SaveAssets();
            report.AppendLine("Assigned to " + HealingExplosiveSetup.AmmoPath + "; scale=0.3; lifetime=2.5 seconds.");
            File.WriteAllText(Folder + "/effect-setup.txt", report.ToString());
            RenderSamples(ammo); Debug.Log("GUNSLINGER_EXPLOSIVE_SETUP_PASS");
        }
        public static void RunAndValidate() { Run(); HealingExplosiveValidation.Run(); }
        static void RenderSamples(ExplosiveAmmoDefinition ammo)
        {
            Scene scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("GunslingerSample"); SceneManager.MoveGameObjectToScene(root, scene);
            var rt = new RenderTexture(960, 720, 24); rt.Create(); RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform); camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.11f, .13f, .16f); camera.transform.position = new Vector3(2.4f, 1.7f, -3.2f);
                camera.transform.LookAt(new Vector3(0, .4f, 0)); camera.fieldOfView = 45; camera.targetTexture = rt;
                Light light = new GameObject("Light").AddComponent<Light>(); light.transform.SetParent(root.transform); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(45, -30, 0);
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform, false); floor.transform.localPosition = new Vector3(0, -.1f, 0); floor.transform.localScale = new Vector3(8, .2f, 8);
                for (int index = 0; index < 3; index++)
                {
                    GameObject effect = Object.Instantiate(ammo.impactEffectPrefab, root.transform); effect.transform.localPosition = new Vector3(0, .08f, 0); effect.transform.localScale *= ammo.impactEffectScale;
                    uint seed = 17; foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>(true)) { ps.useAutoRandomSeed = false; ps.randomSeed = seed++; ps.Simulate(new[] { .08f, .2f, .5f }[index], false, true, true); }
                    camera.Render(); RenderTexture.active = rt; var photo = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    photo.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); photo.Apply(); File.WriteAllBytes(Folder + "/0" + (index + 1) + "-mini-explosion.png", photo.EncodeToPNG());
                    Object.DestroyImmediate(photo); Object.DestroyImmediate(effect);
                }
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
