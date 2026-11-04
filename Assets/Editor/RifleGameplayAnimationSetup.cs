using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Survive.Editor
{
    public static class RifleGameplayAnimationSetup
    {
        public const string Controller = "Assets/Animations/Player/Controllers/UPPAnimatorTPS.controller";
        public const string Folder = "Logs/RifleGameplayAnimations";
        const string Loops = "Assets/Rifle_27A2_Pro/Animation/IPC/Loops/";
        const string Jumps = "Assets/Rifle_27A2_Pro/Animation/IPC/Split_Jumps/";
        static readonly Dictionary<string, string> Directions = new Dictionary<string, string> {
            {"right", "R"}, {"left", "L"}, {"forward", "F"}, {"forward right", "FR"}, {"forward left", "FL"},
            {"backward", "B"}, {"backward right", "BR_BkPd"}, {"backward left", "BL_BkPd"}
        };
        static readonly List<string> changes = new List<string>();
        static readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();

        [MenuItem("Survive/Animaciones/Integrar RIFLE PRO en acciones actuales")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(".utmp/RifleGameplayAnimations");
            if (!File.Exists(".utmp/RifleGameplayAnimations/UPPAnimatorTPS.controller"))
                File.Copy(Controller, ".utmp/RifleGameplayAnimations/UPPAnimatorTPS.controller");
            changes.Clear(); clips.Clear();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(Controller).OfType<BlendTree>()) ReplaceTree(tree);
            for (int i = 0; i < controller.layers.Length; i++) Traverse(controller.layers[i].stateMachine, i, controller.layers[i].name);
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            var sources = AssetDatabase.GetDependencies(Controller, true).Where(p => p.StartsWith("Assets/Rifle_27A2_Pro/Animation/")).OrderBy(p => p).ToArray();
            File.WriteAllLines(Folder + "/changes.txt", changes);
            File.WriteAllLines(Folder + "/used-sources.txt", sources);
            Debug.Log($"RIFLE_GAMEPLAY_ANIMATIONS_SETUP=PASS bindings={changes.Count} sources={sources.Length}");
        }

        public static void SetupAndValidate() { Run(); RifleGameplayAnimationValidation.Run(); }

        static AnimationClip Load(string name, bool jump = false)
        {
            string path = (jump ? Jumps : Loops) + name + ".fbx";
            if (clips.TryGetValue(path, out var clip)) return clip;
            clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            if (clip == null || !clip.humanMotion || clip.length <= 0) throw new Exception("Clip Humanoid inválido: " + path);
            if (!jump && !clip.isLooping) throw new Exception("El clip de locomoción no está en loop: " + path);
            clips[path] = clip; return clip;
        }

        static AnimationClip Replacement(AnimationClip old)
        {
            string path = AssetDatabase.GetAssetPath(old).Replace('\\', '/');
            string leaf = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            string prefix = null, direction = null;
            if (path.Contains("/Walk/Armed/")) { prefix = "W2_Walk_Aim_"; direction = leaf.Replace("walk ", ""); }
            if (path.Contains("/Run/Armed/")) { prefix = "W2_Jog_Aim_"; direction = leaf.Replace("run ", ""); }
            if (path.Contains("/Crouched Armed/")) { prefix = "W2_CrouchWalk_Aim_"; direction = leaf.Replace("walk crouching ", ""); }
            if (prefix != null && Directions.TryGetValue(direction, out var suffix)) return Load(prefix + suffix + "_Loop_IPC");
            if (leaf == "rifle idle aiming") return Load("W2_Stand_Aim_Idle_IPC");
            if (leaf == "rifle crouch left turn") return Load("W2_Crouch_Aim_Turn_In_Place_L_Loop_IPC");
            if (leaf == "rifle crouch right turn") return Load("W2_Crouch_Aim_Turn_In_Place_R_Loop_IPC");
            return null;
        }

        static void ReplaceTree(BlendTree tree)
        {
            var children = tree.children;
            bool crouchedArmed = children.Any(c => c.motion is AnimationClip && AssetDatabase.GetAssetPath(c.motion).Contains("/Crouched Armed/"));
            bool standingArmed = children.Any(c => c.motion is AnimationClip && (AssetDatabase.GetAssetPath(c.motion).EndsWith("/Rifle Idle Aiming.anim") || c.motion.name == "W2_Stand_Aim_Idle_IPC"));
            for (int i = 0; i < children.Length; i++)
            {
                if (!(children[i].motion is AnimationClip old)) continue;
                var replacement = Replacement(old);
                string oldPath = AssetDatabase.GetAssetPath(old);
                if (standingArmed && oldPath.EndsWith("/Left Turn.fbx")) replacement = Load("W2_Stand_Aim_Turn_In_Place_L_Loop_IPC");
                if (standingArmed && oldPath.EndsWith("/Right Turn.fbx")) replacement = Load("W2_Stand_Aim_Turn_In_Place_R_Loop_IPC");
                if (crouchedArmed && AssetDatabase.GetAssetPath(old).EndsWith("/Crouched Idle.anim")) replacement = Load("W2_Crouch_Aim_Idle_IPC");
                if (replacement == null || replacement == old) continue;
                // Mantiene la cadencia existente: duración nueva / velocidad nueva = duración anterior / velocidad anterior.
                children[i].timeScale *= replacement.length / Mathf.Max(.01f, old.length);
                children[i].motion = replacement;
                changes.Add(tree.name + " | " + old.name + " -> " + replacement.name + " | cadence=" + children[i].timeScale);
            }
            tree.children = children; EditorUtility.SetDirty(tree);
        }

        static void Traverse(AnimatorStateMachine machine, int layer, string path)
        {
            foreach (var entry in machine.states)
            {
                var state = entry.state;
                if (state.motion is AnimationClip clip)
                {
                    var replacement = Replacement(clip);
                    if (replacement != null && replacement != clip) { state.motion = replacement; changes.Add(path + "/" + state.name + " -> " + replacement.name); }
                }
                // La capa de piernas sólo pesa en FireMode. El salto sin arma conserva su animación y el motor físico.
                if (layer == 1 && (state.name == "Jump" || state.name == "Jumping" || state.name == "EndJump" || state.name == "End Jump"))
                {
                    string phase = state.name == "Jump" ? "Start" : state.name == "Jumping" ? "Air" : "End";
                    if (state.motion is BlendTree jumpTree)
                    {
                        var children = jumpTree.children;
                        for (int i = 0; i < children.Length; i++)
                        {
                            children[i].motion = Load((i == 0 ? "W2_Stand_Aim_Jump_" : "W2_Jog_Aim_F_Jump_RU_") + phase + "_IPC", true);
                            children[i].timeScale = 1;
                        }
                        jumpTree.children = children; EditorUtility.SetDirty(jumpTree);
                    }
                    else state.motion = Load("W2_Stand_Aim_Jump_" + phase + "_IPC", true);
                    changes.Add(path + "/" + state.name + " -> RIFLE split " + phase);
                }
                EditorUtility.SetDirty(state);
            }
            foreach (var child in machine.stateMachines) Traverse(child.stateMachine, layer, path + "/" + child.stateMachine.name);
        }
    }
}
