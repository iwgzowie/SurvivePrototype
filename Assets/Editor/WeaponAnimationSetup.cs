using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Survive.Editor
{
    /// <summary>Integra la recarga Humanoid de RIFLE PRO sin cambiar los estados de locomoción.</summary>
    public static class WeaponAnimationSetup
    {
        public const string LayerName = "Weapon Actions";
        public const string ReloadParameter = "WeaponReloading";
        public const string SpeedParameter = "WeaponReloadSpeed";
        public const string ReloadClipPath = "Assets/Rifle_27A2_Pro/Animation/IPC/W2_Stand_Aim_Reload_IPC.fbx";
        public const string MaskPath = "Assets/Animations/Player/Masks/WeaponUpperBody.mask";
        const string ControllerPath = "Assets/Animations/Player/Controllers/UPPAnimatorTPS.controller";

        public static AnimationClip SourceReloadClip => LoadSourceReloadClip();

        [MenuItem("Survive/Weapons/Crear recarga e integrar Animator")]
        public static void CreateAndIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de integrar el Animator.");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Falta el Animator Controller canónico.");
            AnimationClip clip = SourceReloadClip;
            ConfigureAimPose(controller);
            AvatarMask mask = CreateMask();
            EnsureParameter(controller, ReloadParameter, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, SpeedParameter, AnimatorControllerParameterType.Float);

            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = Array.FindIndex(layers, layer => layer.name == LayerName);
            if (layerIndex < 0)
            {
                controller.AddLayer(LayerName);
                layers = controller.layers;
                layerIndex = layers.Length - 1;
            }
            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = mask;
            layer.defaultWeight = 0f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.iKPass = true;
            layer.syncedLayerIndex = -1;
            AnimatorStateMachine machine = layer.stateMachine;
            AnimatorState empty = FindOrCreateState(machine, "Empty", new Vector3(240f, 60f));
            AnimatorState reload = FindOrCreateState(machine, "Reload", new Vector3(490f, 60f));
            empty.motion = null;
            empty.writeDefaultValues = false;
            reload.motion = clip;
            reload.tag = "WeaponReload";
            reload.writeDefaultValues = false;
            reload.speed = 1f;
            reload.speedParameter = SpeedParameter;
            reload.speedParameterActive = true;
            machine.defaultState = empty;
            EnsureTransition(empty, reload, AnimatorConditionMode.If, .08f);
            EnsureTransition(reload, empty, AnimatorConditionMode.IfNot, .1f);
            layers[layerIndex] = layer;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(empty);
            EditorUtility.SetDirty(reload);
            AssetDatabase.SaveAssets();
            Debug.Log($"SURVIVE_WEAPON_ANIMATION_SETUP=PASS layer={layerIndex} clip={clip.name} duration={clip.length:F2} humanoid={clip.humanMotion}");
        }

        static AnimationClip LoadSourceReloadClip()
        {
            // Se referencia el subasset original. El paquete conserva sus curvas,
            // Avatar y GUID; la máscara evita que la recarga controle raíz o piernas.
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ReloadClipPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == "W2_Stand_Aim_Reload_IPC");
            if (clip == null || !clip.humanMotion || clip.length <= 0f)
                throw new InvalidOperationException("Importar RIFLE PRO V2.7A2 con su Avatar MotusMan_v55 antes de integrar la recarga: " + ReloadClipPath);
            return clip;
        }

        static void ConfigureAimPose(AnimatorController controller)
        {
            const string path = "Assets/Rifle_27A2_Pro/Animation/IPC/Loops/W2_Stand_Aim_Idle_IPC.fbx";
            AnimationClip aim = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == "W2_Stand_Aim_Idle_IPC");
            if (aim == null || !aim.humanMotion) throw new InvalidOperationException("Falta la pose de rifle RIFLE PRO.");
            ConfigureAimMachine(controller.layers[0].stateMachine, aim);
        }

        static void ConfigureAimMachine(AnimatorStateMachine machine, AnimationClip aim)
        {
            foreach (ChildAnimatorState entry in machine.states)
            {
                if (entry.state.name != "Strafe Torso Pose" || !(entry.state.motion is BlendTree tree)) continue;
                ChildMotion[] children = tree.children;
                for (int i = 0; i < children.Length; i++)
                {
                    children[i].motion = aim;
                    children[i].timeScale = 1f;
                }
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines) ConfigureAimMachine(child.stateMachine, aim);
        }

        static AvatarMask CreateMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask != null) return mask;
            mask = new AvatarMask { name = "WeaponUpperBody" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (AvatarMaskBodyPart part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.LeftArm,
                AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
                AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK })
                mask.SetHumanoidBodyPartActive(part, true);
            EnsureFolder(Path.GetDirectoryName(MaskPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(mask, MaskPath);
            return mask;
        }

        static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(item => item.name == name);
            if (parameter != null)
            {
                if (parameter.type != type) throw new InvalidOperationException("Tipo de parámetro incompatible: " + name);
                return;
            }
            controller.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = 1f });
        }

        static AnimatorState FindOrCreateState(AnimatorStateMachine machine, string name, Vector3 position)
        {
            AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(item => item.name == name);
            return state != null ? state : machine.AddState(name, position);
        }

        static void EnsureTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode condition, float duration)
        {
            AnimatorStateTransition transition = from.transitions.FirstOrDefault(item => item.destinationState == to);
            if (transition == null) transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            transition.conditions = new[] { new AnimatorCondition { mode = condition, parameter = ReloadParameter } };
            EditorUtility.SetDirty(transition);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
