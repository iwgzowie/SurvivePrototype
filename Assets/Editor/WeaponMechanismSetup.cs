using System;
using System.Collections.Generic;
using System.Linq;
using Survive.Combat;
using Survive.Items;
using UnityEditor;
using UnityEngine;

namespace Survive.Editor
{
    /// <summary>Autoría de clips locales propios, sin modificar los modelos o prefabs originales de Synty.</summary>
    public static class WeaponMechanismSetup
    {
        private const string Folder = "Assets/Animations/Weapons";
        private static readonly string[] Ids = { "Pistol", "Revolver", "SMG", "AssaultRifle", "Shotgun", "HuntingRifle", "SniperRifle", "MachineGun" };

        private readonly struct Pose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public Pose(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
        }

        private sealed class Part
        {
            public Transform Transform, Root;
            public Vector3 Position, RootPosition;
            public Quaternion Rotation;
            public Func<float, Pose> Fire, Reload;
            public Pose Rest => new Pose(Position, Rotation);

            public Pose Move(Vector3 offset, float angle = 0f, Vector3 axis = default, Vector3? pivot = null)
            {
                Vector3 parentOffset = Transform.parent.InverseTransformVector(Root.TransformVector(offset));
                Vector3 parentAxis = Transform.parent.InverseTransformDirection(Root.TransformDirection(axis));
                Vector3 parentPivot = Transform.parent.InverseTransformPoint(Root.TransformPoint(pivot ?? RootPosition));
                Quaternion delta = axis.sqrMagnitude > 0f ? Quaternion.AngleAxis(angle, parentAxis) : Quaternion.identity;
                return new Pose(parentPivot + delta * (Position - parentPivot) + parentOffset, delta * Rotation);
            }
        }

        [MenuItem("Survive/Weapons/Animar mecanismos de los ocho presets")]
        public static void CreateAndIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de crear clips de mecanismos.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations", "Weapons");
            foreach (string id in Ids)
                Configure($"Assets/Prefabs/Items/Weapons/Weapon_{id}.prefab", id, true);
            Configure("Assets/Prefabs/Items/Weapon_Rifle.prefab", "AssaultRifle", true, "AssaultRifle_Legacy");
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_WEAPON_MECHANISMS=PASS presets=8 legacy=integrated clips=18");
        }

        private static void Configure(string path, string id, bool writeClips, string clipId = null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                WeaponItem item = root.GetComponent<WeaponItem>();
                if (item == null) throw new InvalidOperationException($"Falta WeaponItem en {path}.");
                var parts = new List<Part>();
                Part Find(string suffix, bool required = true)
                {
                    Transform target = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t =>
                        t.name.EndsWith(suffix, StringComparison.Ordinal) && t.GetComponent<MeshFilter>() != null);
                    if (target == null)
                    {
                        if (required) throw new InvalidOperationException($"Falta la pieza {suffix} en {path}.");
                        return null;
                    }
                    var p = new Part { Transform = target, Root = root.transform,
                        Position = target.localPosition, Rotation = target.localRotation,
                        RootPosition = root.transform.InverseTransformPoint(target.position) };
                    parts.Add(p);
                    MeshFilter filter = target.GetComponent<MeshFilter>();
                    Bounds b = filter.sharedMesh.bounds;
                    Debug.Log($"WEAPON_PART {id} {target.name} pivot={p.RootPosition:F4} meshCenter={b.center:F4} meshSize={b.size:F4}");
                    return p;
                }

                Part trigger = Find("_Trigger_01");
                trigger.Fire = t => trigger.Move(Vector3.zero, -12f * Curve(t, 0, 0, .12f, 1, .45f, 1, 1, 0), Vector3.right);
                Part magazine = Find("_Magazine_01", false);
                if (magazine != null)
                {
                    float distance = id == "Pistol" ? .16f : id == "SniperRifle" ? .15f : .24f;
                    magazine.Reload = t => magazine.Move(new Vector3(0, -distance, -.025f)
                        * Curve(t, 0, 0, .12f, 0, .28f, 1, .54f, 1, .70f, 0, 1, 0));
                }
                Part slide = Find("_Slide_01", false);
                if (slide != null)
                {
                    float travel = id == "Pistol" ? .035f : id == "SMG" ? .035f : .055f;
                    slide.Fire = t => slide.Move(Vector3.back * travel * Curve(t, 0, 0, .12f, 1, .35f, 1, 1, 0));
                    slide.Reload = t => slide.Move(Vector3.back * travel * Curve(t, 0, 0, .73f, 0, .80f, 1, .87f, 1, .94f, 0, 1, 0));
                }
                if (id == "Shotgun")
                {
                    // Este modelo es de palanca: Handle es el aro bajo el
                    // gatillo, con pivote delantero. No es un guardamanos móvil.
                    Part lever = Find("_Handle_01");
                    lever.Fire = t => lever.Move(Vector3.zero,
                        -52f * Curve(t, 0, 0, .15f, 0, .45f, 1, .63f, 1, 1, 0), Vector3.right);
                    lever.Reload = t => lever.Move(Vector3.zero,
                        -52f * Curve(t, 0, 0, .16f, 1, .72f, 1, .88f, 0, 1, 0), Vector3.right);
                    // La mano izquierda conserva su apoyo sobre la madera fija.
                    if (item.LeftHandGrip != null && item.LeftHandGrip.parent != root.transform)
                    {
                        item.LeftHandGrip.SetParent(root.transform, true);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(item.LeftHandGrip);
                    }
                    // Retira sólo marcadores propios vacíos de la preparación
                    // anterior; nunca elimina geometría importada de Synty.
                    foreach (Transform marker in root.GetComponentsInChildren<Transform>(true))
                        if (marker != item.LeftHandGrip && marker.name == "LeftHandGrip"
                            && marker.childCount == 0 && marker.GetComponents<Component>().Length == 1)
                            UnityEngine.Object.DestroyImmediate(marker.gameObject);
                }
                if (id == "HuntingRifle" || id == "SniperRifle")
                {
                    Part bolt = Find("_Bolt_01");
                    bolt.Fire = t => bolt.Move(Vector3.back * .075f * Curve(t, 0, 0, .23f, 0, .42f, 1, .65f, 1, .82f, 0, 1, 0),
                        65f * Curve(t, 0, 0, .12f, 0, .23f, 1, .82f, 1, 1, 0), Vector3.forward);
                    bolt.Reload = t => bolt.Move(Vector3.back * .075f * Curve(t, 0, 0, .12f, 0, .24f, 1, .68f, 1, .82f, 0, 1, 0),
                        65f * Curve(t, 0, 0, .12f, 1, .82f, 1, .94f, 0, 1, 0), Vector3.forward);
                }
                if (id == "Revolver")
                {
                    Part cylinder = Find("_Cylinder_01");
                    Part spin = Find("_Cylinder_Spin_01");
                    Part hammer = Find("_Hammer_01");
                    Vector3 hinge = cylinder.RootPosition;
                    cylinder.Reload = t => cylinder.Move(Vector3.zero, 70f * Swing(t), Vector3.forward, hinge);
                    spin.Fire = t => spin.Move(Vector3.zero, 60f * Curve(t, 0, 0, .15f, 0, .50f, 1, 1, 1), Vector3.forward);
                    spin.Reload = t =>
                    {
                        Pose swing = spin.Move(Vector3.zero, 70f * Swing(t), Vector3.forward, hinge);
                        Vector3 axis = spin.Transform.parent.InverseTransformDirection(root.transform.forward);
                        float turns = 720f * Curve(t, 0, 0, .30f, 0, .62f, 1, 1, 1);
                        return new Pose(swing.Position, swing.Rotation * Quaternion.AngleAxis(turns, axis));
                    };
                    hammer.Fire = t => hammer.Move(Vector3.zero, -35f * Curve(t, 0, 0, .12f, 1, .30f, 0, 1, 0), Vector3.right);
                    for (int bullet = 1; bullet <= 6; bullet++)
                    {
                        Part round = Find($"_Cylinder_Bullet_{bullet:00}");
                        round.Reload = t => round.Move(Vector3.back * .055f
                            * Curve(t, 0, 0, .29f, 0, .39f, 1, .52f, 1, .62f, 0, 1, 0));
                    }
                }

                string assetId = clipId ?? id;
                AnimationClip fire = writeClips ? WriteClip(root, assetId, "Fire", FireDuration(id), parts, false)
                    : AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/AM_{id}_Fire.anim");
                AnimationClip reload = writeClips ? WriteClip(root, assetId, "Reload", 1f, parts, true)
                    : AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/AM_{id}_Reload.anim");
                WeaponMechanismAnimator mechanism = root.GetComponent<WeaponMechanismAnimator>();
                if (mechanism == null) mechanism = root.AddComponent<WeaponMechanismAnimator>();
                var data = new SerializedObject(mechanism);
                data.FindProperty("weapon").objectReferenceValue = item;
                data.FindProperty("fireClip").objectReferenceValue = fire;
                data.FindProperty("reloadClip").objectReferenceValue = reload;
                SerializedProperty animated = data.FindProperty("animatedParts");
                animated.arraySize = parts.Count;
                for (int i = 0; i < parts.Count; i++) animated.GetArrayElementAtIndex(i).objectReferenceValue = parts[i].Transform;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static AnimationClip WriteClip(GameObject root, string id, string action, float duration,
            List<Part> parts, bool reload)
        {
            string path = $"{Folder}/AM_{id}_{action}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
            clip.name = $"AM_{id}_{action}";
            clip.legacy = true;
            clip.frameRate = 60f;
            clip.wrapMode = WrapMode.ClampForever;
            int steps = Mathf.Max(12, Mathf.CeilToInt(duration * 120));
            foreach (Part part in parts)
            {
                string transformPath = AnimationUtility.CalculateTransformPath(part.Transform, root.transform);
                var channels = new Keyframe[7][];
                for (int c = 0; c < channels.Length; c++) channels[c] = new Keyframe[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Pose pose = (reload ? part.Reload : part.Fire)?.Invoke(t) ?? part.Rest;
                    float[] values = { pose.Position.x, pose.Position.y, pose.Position.z,
                        pose.Rotation.x, pose.Rotation.y, pose.Rotation.z, pose.Rotation.w };
                    for (int c = 0; c < channels.Length; c++) channels[c][i] = new Keyframe(t * duration, values[c]);
                }
                string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                    "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int c = 0; c < channels.Length; c++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(transformPath, typeof(Transform), properties[c]),
                        new AnimationCurve(channels[c]));
            }
            clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static float Swing(float t) => Curve(t, 0, 0, .12f, 0, .24f, 1, .72f, 1, .88f, 0, 1, 0);

        private static float FireDuration(string id) => id == "Pistol" ? .15f : id == "Revolver" ? .32f
            : id == "SMG" ? .065f : id == "AssaultRifle" ? .10f : id == "MachineGun" ? .08f
            : id == "Shotgun" ? .55f : id == "HuntingRifle" ? .65f : .75f;

        private static float Curve(float time, params float[] keys)
        {
            for (int i = 2; i < keys.Length; i += 2)
                if (time <= keys[i]) return Mathf.Lerp(keys[i - 1], keys[i + 1],
                    Mathf.InverseLerp(keys[i - 2], keys[i], time));
            return keys[keys.Length - 1];
        }
    }
}
