using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

namespace Survive.Editor
{
    /// <summary>Integra los modelos de Synty sin reemplazar la configuración de IA de cada prefab.</summary>
    public static class EnemyZombieSetup
    {
        const string Folder = "Assets/Animations/Enemies";
        const string Models = "Assets/Synty/SidekickCharacters/Characters/ApocalypseZombies";
        const string Motions = "Assets/Animations/Player/Clips";

        [MenuItem("Survive/Enemies/Integrar modelos ApocalypseZombies")]
        public static void Integrate()
        {
            EnsureFolder(Folder);
            var model = Model(1);
            var idle = EnemyZombieAnimationClips.Create(Folder, "Idle", model);
            var walk = Locomotion("Zombie_Walk", Motions + "/Walk/Walking.fbx");
            var run = Locomotion("Zombie_Run", Motions + "/Run/Running.fbx");
            ConfigureFootstepEvents();
            var melee = EnemyZombieAnimationClips.Create(Folder, "MeleeAttack", model);
            var spit = EnemyZombieAnimationClips.Create(Folder, "SpitAttack", model);
            var death = EnemyZombieAnimationClips.Create(Folder, "Death", model);
            var meleeController = Controller("Zombie_Melee", idle, walk, run, melee, death);
            var rangedController = Controller("Zombie_Ranged", idle, walk, run, spit, death);
            IntegratePrefab("EnemyChasing", 1, meleeController, false);
            IntegratePrefab("EnemyChasingRanged", 2, rangedController, true);
            IntegratePrefab("EnemyPatrol", 3, meleeController, false);
            IntegratePrefab("EnemyAttack", 4, rangedController, true);
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_ZOMBIES_SETUP=PASS (4 prefabs, 2 controllers)");
        }

        static GameObject Model(int index)
        {
            string name = $"ApocalypseZombie_{index:00}";
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{name}/{name}.prefab")
                ?? throw new InvalidOperationException("No se encontró " + name);
        }

        [MenuItem("Survive/Audio/Conectar eventos de pasos de zombies")]
        public static void ConfigureFootstepEvents()
        {
            foreach (string name in new[] { "Zombie_Walk", "Zombie_Run" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/{name}.anim");
                if (clip == null) throw new InvalidOperationException("Falta " + name);
                var events = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName != "Footstep").ToList();
                foreach (float phase in new[] { .15f, .65f }) events.Add(new AnimationEvent { time = clip.length * phase, functionName = "Footstep" });
                AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());EditorUtility.SetDirty(clip);
            }
            ConfigureBossFootsteps();
            AssetDatabase.SaveAssets();
        }

        static void ConfigureBossFootsteps()
        {
            const string path = "Assets/Prefabs/Enemies/Boss_Zombie.prefab";
            if (!File.Exists(path)) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<ZombieFootstepAudio>() != null) return;
                var audio = root.AddComponent<ZombieFootstepAudio>();
                var source = root.GetComponents<AudioSource>().Last();
                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/AudioMixer.mixer");
                var data = new SerializedObject(audio);
                data.FindProperty("footstepType").enumValueIndex = 2;
                data.FindProperty("health").objectReferenceValue = root.GetComponent<EnemyHealth>();
                data.FindProperty("footstepSource").objectReferenceValue = source;
                data.FindProperty("sfxGroup").objectReferenceValue = mixer.FindMatchingGroups("").First(g => g.name == "SFX");
                var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/SFXEnemy" }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.Contains("BOSSWALK")).ToArray();
                var surfaces = data.FindProperty("surfaces");surfaces.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    string folder = i == 0 ? "Concrete" : i == 1 ? "Tile" : "Mud";
                    var selected = clips.Where(p => p.Contains("/" + folder + "/")).ToArray();
                    if (selected.Length == 0) selected = clips.Where(p => p.Contains("/Concrete/")).ToArray();
                    if (selected.Length == 0) throw new InvalidOperationException("Faltan clips de pasos del boss");
                    var entry = surfaces.GetArrayElementAtIndex(i);entry.FindPropertyRelative("surface").enumValueIndex = i == 0 ? 2 : i == 1 ? 1 : 0;
                    var array = entry.FindPropertyRelative("clips");array.arraySize = selected.Length;
                    for (int j = 0; j < selected.Length; j++) array.GetArrayElementAtIndex(j).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(selected[j]);
                }
                data.ApplyModifiedPropertiesWithoutUndo();source.playOnAwake = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static AnimationClip Locomotion(string name, string source)
        {
            string path = $"{Folder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            var original = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__"));
            clip = UnityEngine.Object.Instantiate(original);
            clip.name = name;
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.loopBlendOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AnimatorController Controller(string name, AnimationClip idle, AnimationClip walk,
            AnimationClip run, AnimationClip attack, AnimationClip death)
        {
            string path = $"{Folder}/{name}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var movement = machine.AddState("Locomotion", new Vector3(250, 80));
            var attackState = machine.AddState("Attack", new Vector3(500, 80));
            var dead = machine.AddState("Death", new Vector3(500, 230));
            machine.defaultState = movement;
            var tree = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1.7f);
            tree.AddChild(run, 5f);
            AssetDatabase.AddObjectToAsset(tree, controller);
            movement.motion = tree;
            attackState.motion = attack;
            attackState.speedParameter = "AttackSpeed";
            attackState.speedParameterActive = true;
            dead.motion = death;
            movement.writeDefaultValues = attackState.writeDefaultValues = dead.writeDefaultValues = true;
            var die = machine.AddAnyStateTransition(dead);
            die.AddCondition(AnimatorConditionMode.If, 0f, "Die");
            die.hasExitTime = false;
            die.duration = .08f;
            die.hasFixedDuration = true;
            die.canTransitionToSelf = false;
            var start = movement.AddTransition(attackState);
            start.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            start.hasExitTime = false;
            start.duration = .04f;
            start.hasFixedDuration = true;
            var finish = attackState.AddTransition(movement);
            finish.hasExitTime = true;
            finish.exitTime = .98f;
            finish.duration = .04f;
            finish.hasFixedDuration = true;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void IntegratePrefab(string name, int modelIndex, RuntimeAnimatorController controller, bool ranged)
        {
            string path = $"Assets/Players/{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<EnemyBaseController>() == null || root.GetComponent<EnemyHealth>() == null)
                    throw new InvalidOperationException("Falta la IA o la salud en " + path);
                if (root.transform.Find("ZombieVisual") != null)
                {
                    Debug.Log("Modelo ya integrado: " + path);
                    return;
                }
                // Se mantienen root, collider, NavMeshAgent y valores de combate/patrullaje.
                var holder = new GameObject("ZombieVisual").transform;
                holder.SetParent(root.transform, false);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(Model(modelIndex), holder);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                var animator = visual.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                    throw new InvalidOperationException("El modelo no tiene Avatar Humanoid válido: " + name);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                var capsule = root.GetComponent<CapsuleCollider>();
                Bounds bounds = VisualBounds(holder, root.transform);
                holder.localScale = Vector3.one * (capsule.height / bounds.size.y);
                bounds = VisualBounds(holder, root.transform);
                holder.localPosition = Vector3.up * (capsule.center.y - capsule.height * .5f - bounds.min.y);
                var placeholder = root.GetComponent<MeshRenderer>();
                if (placeholder != null) UnityEngine.Object.DestroyImmediate(placeholder);
                var filter = root.GetComponent<MeshFilter>();
                if (filter != null) UnityEngine.Object.DestroyImmediate(filter);
                var health = new SerializedObject(root.GetComponent<EnemyHealth>());
                health.FindProperty("meshRenderer").objectReferenceValue = visual.GetComponentInChildren<SkinnedMeshRenderer>();
                health.ApplyModifiedPropertiesWithoutUndo();
                if (ranged)
                {
                    var firePoint = root.transform.Find("FirePoint");
                    if (firePoint == null) firePoint = new GameObject("FirePoint").transform;
                    var head = animator.GetBoneTransform(HumanBodyBones.Jaw) ?? animator.GetBoneTransform(HumanBodyBones.Head);
                    firePoint.SetParent(head, false);
                    // Se alinea con el frente del modelo, no con los ejes particulares del hueso Synty.
                    firePoint.rotation = root.transform.rotation;
                    firePoint.position = head.position + root.transform.forward * .10f;
                    var particles = CreateSpitEffect(firePoint);
                    var events = visual.AddComponent<EnemyAnimationEvents>();
                    var eventData = new SerializedObject(events);
                    eventData.FindProperty("spitEffect").objectReferenceValue = particles;
                    eventData.ApplyModifiedPropertiesWithoutUndo();
                    var data = new SerializedObject(root.GetComponent<EnemyBaseController>());
                    data.FindProperty("firePoint").objectReferenceValue = firePoint;
                    data.FindProperty("attackAnimationImpactTime").floatValue = .4f;
                    var projectile = data.FindProperty("projectilePrefab");
                    // La referencia heredada apunta a un prefab ausente. Conserva el daño directo existente.
                    if (projectile.objectReferenceValue == null) projectile.objectReferenceValue = null;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"ZOMBIE_PREFAB={name}; model={modelIndex}; height={VisualBounds(holder, root.transform).size.y:F3}; feet={VisualBounds(holder, root.transform).min.y:F3}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Bounds VisualBounds(Transform visual, Transform reference)
        {
            bool first = true;
            Bounds total = default;
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                {
                    var point = reference.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if (first) { total = new Bounds(point, Vector3.zero); first = false; }
                    else total.Encapsulate(point);
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            if (first || total.size.y < .1f) throw new InvalidOperationException("Modelo sin geometría válida.");
            return total;
        }

        static ParticleSystem CreateSpitEffect(Transform mouth)
        {
            string materialPath = $"{Folder}/Zombie_Spit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.name = "Zombie_Spit";
                material.SetColor("_BaseColor", new Color(.35f, .65f, .05f, 1f));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var go = new GameObject("SpitEffect");
            go.transform.SetParent(mouth, false);
            var effect = go.AddComponent<ParticleSystem>();
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = effect.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = .15f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .65f);
            main.startSpeed = 15f;
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .07f);
            main.startColor = new Color(.55f, .8f, .1f, 1f);
            main.gravityModifier = .35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;
            var emission = effect.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4, 4, .025f) });
            var shape = effect.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 2f;
            shape.radius = .025f;
            var collision = effect.collision;
            collision.enabled = false;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.lifetimeLoss = 1f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            renderer.mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(sphere);
            return effect;
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
