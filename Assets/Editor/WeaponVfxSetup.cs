using System;
using System.Linq;
using Survive.Combat;
using Survive.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Survive.Editor
{
    /// <summary>Adapta GunFX URP a disparos individuales sin alterar los prefabs originales.</summary>
    public static class WeaponVfxSetup
    {
        const string Output = "Assets/Prefabs/VFX/Weapons";
        const string Profiles = "Assets/Data/Weapons/VFX";
        public static readonly string[] RequiredSourceAssets =
        {
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/MuzzleFlashes/Pistol/NS_Muzzle_Pistol_01.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/MuzzleFlashes/AR/NS_Muzzle_AR_01.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/MuzzleFlashes/SMG/NS_Muzzle_SMG_01.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/MuzzleFlashes/Shotgun/NS_Muzzle_SG_01 .prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/MuzzleFlashes/Sniper/NS_Muzzle_Sniper_01.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/Ejections/Loop/NS_Ejection_HandGun_Loop.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/Ejections/Loop/NS_Ejection_SMG_Loop.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/Ejections/Loop/NS_Ejection_AR_Loop.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/Ejections/Loop/NS_Ejection_SG_Loop.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/Ejections/Loop/NS_Ejection_SR_Loop.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/Ejections/Loop/NS_Ejection_MG_Loop.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/ImpactFX/Concrete/NS_Impact_Concrete_Med.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/ImpactFX/Metal/NS_Impact_Metal_Med.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/ImpactFX/NaturalWood/NS_Impact_NaturalWood_Med.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/ImpactFX/Dirt/NS_Impact_Dirt_Med.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/ImpactFX/Flesh/NS_Impact_Flesh_Med.prefab",
            "Assets/HIVEMIND/GunFX/URP/Art/Prefabs/ImpactFX/Glass/NS_Impact_Glass_Med.prefab"
        };
        static readonly string[] Names = { "Muzzle_Pistol", "Muzzle_AR", "Muzzle_SMG", "Muzzle_Shotgun", "Muzzle_Sniper",
            "Case_Handgun", "Case_SMG", "Case_AR", "Case_Shotgun", "Case_Sniper", "Case_MG",
            "Impact_Concrete", "Impact_Metal", "Impact_Wood", "Impact_Dirt", "Impact_Flesh", "Impact_Glass" };
        readonly struct Configuration
        {
            public readonly string Id;
            public readonly int Muzzle, Shell;
            public readonly float FlashScale, ShellScale, ImpactScale, Delay;
            public readonly Vector3 Port;
            public Configuration(string id, int muzzle, int shell, float flash, float shellScale, float impact,
                float delay, Vector3 port)
            { Id = id; Muzzle = muzzle; Shell = shell; FlashScale = flash; ShellScale = shellScale;
                ImpactScale = impact; Delay = delay; Port = port; }
        }
        static readonly Configuration[] Configurations =
        {
            new Configuration("Pistol", 0, 5, .45f, 1, .35f, .025f, new Vector3(.035f,.107f,.085f)),
            new Configuration("Revolver", 0, 5, .65f, 1.2f, .45f, 0, new Vector3(-.055f,.105f,.104f)),
            new Configuration("SMG", 2, 6, .40f, .85f, .3f, .015f, new Vector3(.035f,.09f,.005f)),
            new Configuration("AssaultRifle", 1, 7, .6f, 1, .4f, .02f, new Vector3(.045f,.15f,.185f)),
            new Configuration("Shotgun", 3, 8, .8f, 1, .55f, .24f, new Vector3(.035f,.11f,.145f)),
            new Configuration("HuntingRifle", 4, 9, .7f, .8f, .5f, .28f, new Vector3(.035f,.092f,.07f)),
            new Configuration("SniperRifle", 4, 9, .95f, 1, .65f, .32f, new Vector3(.035f,.105f,.1f)),
            new Configuration("MachineGun", 1, 10, .7f, 1, .4f, .015f, new Vector3(.07f,.125f,.065f))
        };

        [MenuItem("Survive/Weapons/Integrar GunFX URP en las ocho armas")]
        public static void CreateAndIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de integrar efectos.");
            foreach (string source in RequiredSourceAssets)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
                    throw new InvalidOperationException("Falta importar GunFX URP: " + source);
            Folder(Output); Folder(Profiles);
            var prefabs = new GameObject[Names.Length];
            for (int index = 0; index < Names.Length; index++)
                prefabs[index] = Adapt(RequiredSourceAssets[index], Names[index], index >= 5 && index <= 10, index < 5);
            foreach (Configuration configuration in Configurations)
            {
                string definitionPath = $"Assets/Data/Weapons/WD_{configuration.Id}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(definitionPath);
                if (definition == null) throw new InvalidOperationException("Falta " + definitionPath);
                string profilePath = $"{Profiles}/WVFX_{configuration.Id}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<WeaponVfxProfile>(profilePath);
                if (profile == null)
                { profile = ScriptableObject.CreateInstance<WeaponVfxProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
                var data = new SerializedObject(profile);
                Set(data,"muzzlePrefab",prefabs[configuration.Muzzle]); Set(data,"shellPrefab",prefabs[configuration.Shell]);
                Set(data,"muzzleScale",configuration.FlashScale); Set(data,"muzzleLifetime",.28f);
                Set(data,"shellScale",configuration.ShellScale); Set(data,"shellLifetime",3f);
                Set(data,"ejectionDelaySeconds",configuration.Delay); Set(data,"reloadEjectionProgress",.39f);
                data.FindProperty("ejectCasingsOnReload").boolValue = configuration.Id == "Revolver";
                string[] surfaces = { "concrete", "metal", "wood", "dirt", "flesh", "glass" };
                for (int surface = 0; surface < surfaces.Length; surface++) Set(data,surfaces[surface]+"ImpactPrefab",prefabs[11+surface]);
                Set(data,"impactScale",configuration.ImpactScale); Set(data,"impactLifetime",2.4f);
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(profile);
                data = new SerializedObject(definition); Set(data,"vfxProfile",profile);
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(definition);
                BindWeapon($"Assets/Prefabs/Items/Weapons/Weapon_{configuration.Id}.prefab", configuration);
            }
            BindWeapon("Assets/Prefabs/Items/Weapon_Rifle.prefab", Configurations.First(c => c.Id == "AssaultRifle"));
            EnsureDecals("Assets/Settings/PC_Renderer.asset"); EnsureDecals("Assets/Settings/Mobile_Renderer.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_WEAPON_VFX_SETUP=PASS profiles=8 legacy=true effects=17 surfaces=6 oneshot=true decals=true");
        }

        public static void IntegrateAndAudit()
        {
            CreateAndIntegrate();
            AssetAudioVfxAudit.ScanOnly();
        }

        static GameObject Adapt(string source, string name, bool casing, bool muzzle)
        {
            string output = $"{Output}/{name}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child != root.transform && PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject))
                        PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = name; root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (script != null && script.GetType().Namespace != "UnityEngine.Rendering.Universal")
                        UnityEngine.Object.DestroyImmediate(script);
                foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(audio);
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    main.loop = false; main.playOnAwake = false; main.useUnscaledTime = false;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    if (casing) main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var collision = system.collision; collision.colliderForce = 0; collision.sendCollisionMessages = false;
                    if (muzzle || casing)
                    {
                        var emission = system.emission;
                        if (emission.enabled || casing)
                        {
                            ParticleSystem.Burst burst = emission.burstCount > 0 ? emission.GetBurst(0) : new ParticleSystem.Burst(0, (short)1);
                            burst.time = 0; burst.cycleCount = 1; burst.probability = 1;
                            if (casing) burst.count = new ParticleSystem.MinMaxCurve(1);
                            emission.enabled = true; emission.rateOverTime = 0; emission.rateOverDistance = 0;
                            emission.SetBursts(new[] { burst }); main.startDelay = 0;
                        }
                    }
                }
                return PrefabUtility.SaveAsPrefabAsset(root, output);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void BindWeapon(string path, Configuration configuration)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                WeaponItem weapon = root.GetComponent<WeaponItem>();
                var itemData = new SerializedObject(weapon);
                GameObject placeholder = itemData.FindProperty("muzzleFlash").objectReferenceValue as GameObject;
                Set(itemData,"muzzleFlash",(UnityEngine.Object)null); itemData.ApplyModifiedPropertiesWithoutUndo();
                if (placeholder != null && placeholder.transform.IsChildOf(root.transform) && placeholder.name == "MuzzleFlash")
                    UnityEngine.Object.DestroyImmediate(placeholder);
                Transform port = root.transform.Find("ShellEjectionPort");
                if (port == null) { port = new GameObject("ShellEjectionPort").transform; port.SetParent(root.transform,false); }
                port.localPosition = configuration.Port;
                port.localRotation = Quaternion.Euler(0, configuration.Id == "Revolver" ? 180 : 90, 0);
                var presenter = root.GetComponent<WeaponVfxPresenter>();
                if (presenter == null) presenter = root.AddComponent<WeaponVfxPresenter>();
                var data = new SerializedObject(presenter); Set(data,"weapon",weapon); Set(data,"shellEjectionPort",port);
                data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void EnsureDecals(string path)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (renderer == null) throw new InvalidOperationException("Falta el renderer URP: " + path);
            if (renderer.rendererFeatures.Any(feature => feature is DecalRendererFeature)) return;
            var decal = ScriptableObject.CreateInstance<DecalRendererFeature>(); decal.name = "Weapon Impact Decals";
            AssetDatabase.AddObjectToAsset(decal,renderer); renderer.rendererFeatures.Add(decal); renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
        static void Set(SerializedObject data, string field, UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue = value;
        static void Set(SerializedObject data, string field, float value) => data.FindProperty(field).floatValue = value;
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent);
            AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));
        }
    }
}
