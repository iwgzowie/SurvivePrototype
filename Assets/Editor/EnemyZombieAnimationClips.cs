using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Survive.Editor
{
    /// <summary>
    /// Crea poses humanoides simples para el prototipo. Son clips editables propios,
    /// no animaciones capturadas ni eventos que apliquen daño.
    /// </summary>
    public static class EnemyZombieAnimationClips
    {
        sealed class PoseKey
        {
            public float time;
            public float[] muscles;
            public Vector3 bodyPosition;
            public Quaternion bodyRotation;
        }

        static readonly Dictionary<string, int> MuscleIndices = BuildMuscleIndices();

        /// <summary>
        /// Genera una vez cada clip; preserva los clips existentes para permitir ajustes manuales.
        /// </summary>
        public static AnimationClip Create(string folder, string kind, GameObject sourceModel, bool overwrite = false)
        {
            if (sourceModel == null) throw new ArgumentNullException(nameof(sourceModel));
            if (kind != "Idle" && kind != "MeleeAttack" && kind != "SpitAttack" && kind != "Death")
                throw new ArgumentException("Tipo de animación zombie desconocido: " + kind, nameof(kind));
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("La carpeta de clips debe estar dentro de Assets.", nameof(folder));
            EnsureFolder(folder);
            string assetPath = folder + "/Zombie_" + kind + ".anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing != null && !overwrite) return existing;

            HumanPose rest = ReadRestPose(sourceModel);
            var frames = BuildFrames(kind, rest);
            var clip = new AnimationClip
            {
                name = "Zombie_" + kind,
                frameRate = 60f,
                wrapMode = kind == "Idle" ? WrapMode.Loop : WrapMode.ClampForever
            };

            for (int muscle = 0; muscle < HumanTrait.MuscleCount; muscle++)
            {
                int index = muscle;
                AddCurve(clip, HumanTrait.MuscleName[muscle], frames, frame => frame.muscles[index]);
            }
            AddCurve(clip, "RootT.x", frames, frame => frame.bodyPosition.x);
            AddCurve(clip, "RootT.y", frames, frame => frame.bodyPosition.y);
            AddCurve(clip, "RootT.z", frames, frame => frame.bodyPosition.z);
            AddCurve(clip, "RootQ.x", frames, frame => frame.bodyRotation.x);
            AddCurve(clip, "RootQ.y", frames, frame => frame.bodyRotation.y);
            AddCurve(clip, "RootQ.z", frames, frame => frame.bodyRotation.z);
            AddCurve(clip, "RootQ.w", frames, frame => frame.bodyRotation.w);
            clip.EnsureQuaternionContinuity();

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime = frames[frames.Count - 1].time;
            settings.loopTime = kind == "Idle";
            settings.loopBlend = false;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.heightFromFeet = false;
            // El NavMeshAgent mueve la raíz. La caída queda horneada en la pose del cuerpo.
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (kind == "SpitAttack")
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = .4f, functionName = "PlaySpit" } });
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                UnityEngine.Object.DestroyImmediate(clip);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        static HumanPose ReadRestPose(GameObject sourceModel)
        {
            const string idlePath = "Assets/Animations/Player/Clips/Idle/Neutral Idle.fbx";
            AnimationClip idle = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(idlePath))
            {
                if (asset is AnimationClip candidate && !candidate.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    idle = candidate;
                    break;
                }
            }
            if (idle == null) throw new InvalidOperationException("No se encontró el clip humanoide Neutral Idle.");

            GameObject copy = UnityEngine.Object.Instantiate(sourceModel);
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.transform.localScale = Vector3.one;
            try
            {
                Animator animator = copy.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("El modelo zombie necesita un Avatar Humanoid válido.");
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;

                // Cero en todos los músculos no equivale a estar de pie: parte del idle retargeteado.
                idle.SampleAnimation(animator.gameObject, Mathf.Min(.5f, idle.length));
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                float supportHeight = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
                using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
                {
                    var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                    handler.GetHumanPose(ref pose);
                    // Se conservan piernas y cadera del idle; los pequeños offsets afectan sólo el torso y brazos.
                    foreach (string side in new[] { "Left", "Right" })
                    {
                        int arm = MuscleIndices[side + " Arm Down-Up"];
                        pose.muscles[arm] = Mathf.Clamp(pose.muscles[arm] + .07f, -1f, 1f);
                        int forearm = MuscleIndices[side + " Forearm Stretch"];
                        pose.muscles[forearm] = Mathf.Clamp(pose.muscles[forearm] - .08f, -1f, 1f);
                    }
                    foreach (string muscle in new[] { "Spine Front-Back", "Chest Front-Back", "Head Nod Down-Up" })
                    {
                        int index = MuscleIndices[muscle];
                        pose.muscles[index] = Mathf.Clamp(pose.muscles[index] - .06f, -1f, 1f);
                    }
                    foreach (string side in new[] { "Left", "Right" })
                    foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    for (int joint = 1; joint <= 3; joint++)
                        SetOptional(pose.muscles, side + " " + finger + " " + joint + " Stretched", -.2f);

                    // Al cambiar brazos/torso cambia el centro de masa; mantiene la altura de apoyo del idle.
                    handler.SetHumanPose(ref pose);
                    float adjustedSupportHeight = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
                    pose.bodyPosition.y -= (adjustedSupportHeight - supportHeight) / Mathf.Max(.001f, animator.humanScale);
                    return pose;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        static List<PoseKey> BuildFrames(string kind, HumanPose rest)
        {
            var frames = new List<PoseKey>();
            if (kind == "Idle")
            {
                Frame(frames, rest, 0f);
                PoseKey inhale = Frame(frames, rest, .9f);
                Set(inhale, "Chest Front-Back", -.045f);
                Set(inhale, "Spine Left-Right", .025f);
                Set(inhale, "Head Turn Left-Right", .045f);
                PoseKey exhale = Frame(frames, rest, 1.8f);
                Set(exhale, "Chest Front-Back", -.11f);
                Set(exhale, "Spine Left-Right", -.025f);
                Set(exhale, "Head Turn Left-Right", -.035f);
                Frame(frames, rest, 2.7f);
            }
            else if (kind == "MeleeAttack")
            {
                Frame(frames, rest, 0f);
                PoseKey lift = Frame(frames, rest, .14f);
                Set(lift, "Right Arm Down-Up", -.1f);
                Set(lift, "Right Forearm Stretch", -.55f);
                Set(lift, "Chest Twist Left-Right", .14f);
                PoseKey windup = Frame(frames, rest, .34f);
                Set(windup, "Right Arm Down-Up", .35f);
                Set(windup, "Right Arm Front-Back", -.2f);
                Set(windup, "Right Forearm Stretch", -.65f);
                Set(windup, "Left Arm Down-Up", -.4f);
                Set(windup, "Chest Twist Left-Right", .3f);
                Set(windup, "Chest Front-Back", .12f);
                PoseKey strike = Frame(frames, rest, .5f);
                Set(strike, "Right Arm Down-Up", -.05f);
                Set(strike, "Right Arm Front-Back", .75f);
                Set(strike, "Right Forearm Stretch", -.05f);
                Set(strike, "Left Arm Down-Up", -.45f);
                Set(strike, "Chest Twist Left-Right", -.3f);
                Set(strike, "Spine Front-Back", -.3f);
                Set(strike, "Chest Front-Back", -.3f);
                PoseKey follow = Frame(frames, rest, .6f);
                Set(follow, "Right Arm Down-Up", -.5f);
                Set(follow, "Right Arm Front-Back", .4f);
                Set(follow, "Chest Twist Left-Right", -.2f);
                Set(follow, "Spine Front-Back", -.22f);
                Frame(frames, rest, .75f);
            }
            else if (kind == "SpitAttack")
            {
                Frame(frames, rest, 0f);
                PoseKey draw = Frame(frames, rest, .15f);
                Set(draw, "Chest Front-Back", .16f);
                Set(draw, "Head Nod Down-Up", .25f);
                PoseKey prepare = Frame(frames, rest, .3f);
                Set(prepare, "Chest Front-Back", .26f);
                Set(prepare, "Head Nod Down-Up", .35f);
                Set(prepare, "Jaw Close", -.65f);
                Set(prepare, "Left Arm Down-Up", -.6f);
                Set(prepare, "Right Arm Down-Up", -.6f);
                PoseKey spit = Frame(frames, rest, .4f);
                Set(spit, "Spine Front-Back", -.28f);
                Set(spit, "Chest Front-Back", -.35f);
                Set(spit, "Neck Nod Down-Up", -.2f);
                Set(spit, "Head Nod Down-Up", -.18f);
                Set(spit, "Jaw Close", -.95f);
                Set(spit, "Left Arm Down-Up", -.55f);
                Set(spit, "Right Arm Down-Up", -.55f);
                Set(spit, "Left Arm Front-Back", -.25f);
                Set(spit, "Right Arm Front-Back", -.25f);
                PoseKey settle = Frame(frames, rest, .52f);
                Set(settle, "Chest Front-Back", -.28f);
                Set(settle, "Head Nod Down-Up", -.22f);
                Set(settle, "Jaw Close", -.4f);
                PoseKey recover = Frame(frames, rest, .76f);
                Set(recover, "Chest Front-Back", -.15f);
                Frame(frames, rest, 1.1f);
            }
            else
            {
                Frame(frames, rest, 0f);
                PoseKey stagger = Frame(frames, rest, .22f);
                Set(stagger, "Left Arm Down-Up", -.35f);
                Set(stagger, "Right Arm Down-Up", -.25f);
                Set(stagger, "Spine Front-Back", -.25f);
                Set(stagger, "Head Nod Down-Up", -.35f);
                stagger.bodyRotation = Quaternion.Euler(12f, 0f, -5f) * rest.bodyRotation;
                stagger.bodyPosition.y -= .04f;
                PoseKey collapse = Frame(frames, rest, .6f);
                Set(collapse, "Left Arm Down-Up", -.1f);
                Set(collapse, "Right Arm Down-Up", -.1f);
                Set(collapse, "Left Forearm Stretch", -.3f);
                Set(collapse, "Right Forearm Stretch", -.45f);
                collapse.bodyRotation = Quaternion.Euler(62f, 0f, -8f) * rest.bodyRotation;
                collapse.bodyPosition.y = rest.bodyPosition.y * .49f;
                collapse.bodyPosition.z += .12f;
                PoseKey ground = Frame(frames, rest, .95f);
                SetDeathPose(ground, rest);
                PoseKey final = Frame(frames, rest, 1.5f);
                SetDeathPose(final, rest);
            }
            return frames;
        }

        static void SetDeathPose(PoseKey frame, HumanPose rest)
        {
            frame.bodyRotation = Quaternion.Euler(88f, 0f, -8f) * rest.bodyRotation;
            frame.bodyPosition.y = rest.bodyPosition.y * .2f;
            frame.bodyPosition.z += .18f;
            Set(frame, "Left Arm Down-Up", -.4f);
            Set(frame, "Right Arm Down-Up", -.25f);
            Set(frame, "Left Forearm Stretch", -.35f);
            Set(frame, "Right Forearm Stretch", -.6f);
            Set(frame, "Head Turn Left-Right", .35f);
            Set(frame, "Head Nod Down-Up", .08f);
            Set(frame, "Spine Front-Back", 0f);
            Set(frame, "Chest Front-Back", 0f);
        }

        static PoseKey Frame(List<PoseKey> frames, HumanPose rest, float time)
        {
            var result = new PoseKey
            {
                time = time,
                muscles = (float[])rest.muscles.Clone(),
                bodyPosition = rest.bodyPosition,
                bodyRotation = rest.bodyRotation
            };
            frames.Add(result);
            return result;
        }

        static void AddCurve(AnimationClip clip, string property, List<PoseKey> frames, Func<PoseKey, float> value)
        {
            var keys = new Keyframe[frames.Count];
            for (int i = 0; i < frames.Count; i++) keys[i] = new Keyframe(frames[i].time, value(frames[i]));
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }

        static void Set(PoseKey frame, string muscle, float value) => Set(frame.muscles, muscle, value);

        static void Set(float[] pose, string muscle, float value)
        {
            if (!MuscleIndices.TryGetValue(muscle, out int index))
                throw new InvalidOperationException("No se encontró el músculo humanoide: " + muscle);
            pose[index] = Mathf.Clamp(value, -1f, 1f);
        }

        static void SetOptional(float[] pose, string muscle, float value)
        {
            if (MuscleIndices.TryGetValue(muscle, out int index)) pose[index] = Mathf.Clamp(value, -1f, 1f);
        }

        static Dictionary<string, int> BuildMuscleIndices()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < HumanTrait.MuscleCount; i++) result[HumanTrait.MuscleName[i]] = i;
            return result;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}