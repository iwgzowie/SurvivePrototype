using System;
using Survive.Combat;
using Survive.Items;
using UnityEditor;
using UnityEngine;
using UPP.ThirdPersonController;

namespace Survive.Editor
{
    /// <summary>Calibra los prefabs propios; preserva las fuentes importadas de Synty.</summary>
    public static class WeaponGripSetup
    {
        private const string PlayerPath = "Assets/Prefabs/Player/UPPPlayer.prefab";

        [MenuItem("Survive/Weapons/Calibrar agarres e integrar pose")]
        public static void ConfigureGripsAndPlayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("La calibración se ejecuta fuera de Play Mode.");
            ConfigureWeapon("Pistol", true, new Vector3(-.06f, -.025f, -.10f));
            ConfigureWeapon("Revolver", true, new Vector3(-.06f, -.025f, -.085f));
            ConfigureWeapon("SMG", true, new Vector3(-.015f, .012f, .08f));
            ConfigureWeapon("AssaultRifle", true, new Vector3(-.012f, .025f, .18f));
            ConfigureWeapon("Shotgun", true, new Vector3(-.012f, .005f, .15f));
            ConfigureWeapon("HuntingRifle", true, new Vector3(-.012f, .005f, .20f));
            ConfigureWeapon("SniperRifle", true, new Vector3(-.012f, .012f, .17f));
            ConfigureWeapon("MachineGun", true, new Vector3(-.018f, .012f, .20f));
            IntegratePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_WEAPON_GRIPS=PASS presets=8 pose=integrated");
        }

        private static void ConfigureWeapon(string id, bool twoHanded, Vector3 gripPosition)
        {
            string path = $"{WeaponSystemSetup.PrefabsFolder}/Weapon_{id}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                WeaponItem weapon = root.GetComponent<WeaponItem>();
                if (weapon == null) throw new InvalidOperationException($"Falta WeaponItem en {path}.");
                Transform grip = root.transform.Find("LeftHandGrip");
                if (grip == null)
                {
                    grip = new GameObject("LeftHandGrip").transform;
                    grip.SetParent(root.transform, false);
                }
                grip.localPosition = gripPosition;
                // El eje -X de los dedos de la mano izquierda sigue el cañón.
                // La muñeca queda detrás del punto de contacto de la palma.
                bool pistolSupport = id == "Pistol" || id == "Revolver";
                grip.localRotation = Quaternion.Euler(pistolSupport ? 90f : 0f, 90f, 0f);
                grip.localScale = Vector3.one;
                var data = new SerializedObject(weapon);
                Property(data, "leftHandGrip").objectReferenceValue = grip;
                Property(data, "twoHanded").boolValue = twoHanded;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void IntegratePlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                PlayerWeaponController controller = root.GetComponent<PlayerWeaponController>();
                UPPCharacterMovementComponent movement = root.GetComponent<UPPCharacterMovementComponent>();
                if (animator == null || !animator.isHuman || controller == null || movement == null)
                    throw new InvalidOperationException("Falta el contrato humanoide/armas en UPPPlayer.");
                PlayerWeaponPose pose = animator.GetComponent<PlayerWeaponPose>();
                if (pose == null) pose = animator.gameObject.AddComponent<PlayerWeaponPose>();
                var data = new SerializedObject(pose);
                Property(data, "animator").objectReferenceValue = animator;
                Property(data, "movement").objectReferenceValue = movement;
                Property(data, "weaponController").objectReferenceValue = controller;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static SerializedProperty Property(SerializedObject data, string name) => data.FindProperty(name)
            ?? throw new InvalidOperationException($"Falta {name} en {data.targetObject.GetType().Name}.");
    }
}
