using System;
using System.IO;
using System.Linq;
using Survive.Combat;
using Survive.Items;
using UnityEditor;
using UnityEngine;

namespace Survive.Editor
{
    /// <summary>Integra las poses, recarga y respuesta del disparo sobre los prefabs existentes.</summary>
    public static class WeaponFeelSetup
    {
        private const string PlayerPath = "Assets/Prefabs/Player/UPPPlayer.prefab";
        private const string LegacyPath = "Assets/Prefabs/Items/Weapon_Rifle.prefab";

        [MenuItem("Survive/Weapons/Completar agarres, recarga y disparo")]
        public static void CreateAndIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de integrar las armas.");
            WeaponSystemSetup.CreatePresets();
            WeaponAnimationSetup.CreateAndIntegrate();
            WeaponGripSetup.ConfigureGripsAndPlayer();
            ConfigureDefinition("Pistol", 1.7f, .022f, 15f);
            ConfigureDefinition("Revolver", 3f, .04f, 11f);
            ConfigureDefinition("SMG", .55f, .012f, 18f);
            ConfigureDefinition("AssaultRifle", 1.1f, .025f, 16f);
            ConfigureDefinition("Shotgun", 3.2f, .055f, 10f);
            ConfigureDefinition("HuntingRifle", 2.6f, .04f, 11f);
            ConfigureDefinition("SniperRifle", 3f, .05f, 10f);
            ConfigureDefinition("MachineGun", .9f, .023f, 16f);
            ConfigureController();
            UpgradeLegacyRifle();
            WeaponPresentationSetup.CreateAndIntegrate();
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_WEAPON_FEEL_SETUP=PASS presets=8 heldFire=true autoReload=true animation=true handIK=true");
        }

        private static void ConfigureDefinition(string id, float recoil, float back, float recovery)
        {
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponSystemSetup.DefinitionsFolder}/WD_{id}.asset");
            if (definition == null) throw new InvalidOperationException("Falta el preset " + id);
            var data = new SerializedObject(definition);
            data.FindProperty("automatic").boolValue = true;
            data.FindProperty("recoilDegrees").floatValue = recoil;
            data.FindProperty("recoilBackDistance").floatValue = back;
            data.FindProperty("recoilRecoverySpeed").floatValue = recovery;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureController()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                PlayerWeaponController controller = root.GetComponent<PlayerWeaponController>();
                // El jugador local sigue resolviendo manos y recarga aunque una
                // pared o la cámara oculten momentáneamente el modelo.
                root.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var data = new SerializedObject(controller);
                data.FindProperty("weaponAnimator").objectReferenceValue = root.GetComponent<Animator>();
                var animatorController = root.GetComponent<Animator>().runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                AnimationClip reloadClip = animatorController.layers.First(l => l.name == "Weapon Actions")
                    .stateMachine.states.First(s => s.state.name == "Reload").state.motion as AnimationClip;
                if (reloadClip == null) throw new InvalidOperationException("Falta la animación real de recarga.");
                data.FindProperty("reloadAnimationClip").objectReferenceValue = reloadClip;
                data.FindProperty("automaticReloadOnEmpty").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                // El interruptor de diagnóstico no debe tapar la presentación de combate.
                var debug = root.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "UPPGizmosDebugComponent");
                if (debug != null)
                {
                    var debugData = new SerializedObject(debug);
                    var master = debugData.FindProperty("DrawDebug");
                    if (master != null) { master.boolValue = false; debugData.ApplyModifiedPropertiesWithoutUndo(); }
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpgradeLegacyRifle()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LegacyPath);
            try
            {
                WeaponItem weapon = root.GetComponent<WeaponItem>();
                Transform visual = root.transform.Find("SyntyVisual");
                if (visual == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonApocalypse/Prefabs/Weapons/Guns/SM_Wep_AssaultRifle_01.prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                    instance.name = "SyntyVisual";
                    visual = instance.transform;
                    foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                        UnityEngine.Object.DestroyImmediate(collider);
                    var generated = AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponSystemSetup.PrefabsFolder}/Weapon_AssaultRifle.prefab");
                    Material material = generated.GetComponentsInChildren<Renderer>(true).First(r => r.sharedMaterial != null).sharedMaterial;
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                }
                visual.localPosition = Vector3.up * .05f;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                foreach (string name in new[] { "Receiver", "Barrel", "Stock", "Grip", "Magazine", "Sight", "Accent" })
                {
                    Transform primitive = root.transform.Find(name);
                    if (primitive != null) primitive.gameObject.SetActive(false);
                }
                weapon.Muzzle.localPosition = new Vector3(0, .1327f, .728f);
                Transform grip = root.transform.Find("LeftHandGrip");
                if (grip == null)
                {
                    grip = new GameObject("LeftHandGrip").transform;
                    grip.SetParent(root.transform, false);
                }
                grip.localPosition = new Vector3(-.012f, .025f, .18f);
                grip.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var data = new SerializedObject(weapon);
                data.FindProperty("leftHandGrip").objectReferenceValue = grip;
                data.FindProperty("twoHanded").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, LegacyPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
