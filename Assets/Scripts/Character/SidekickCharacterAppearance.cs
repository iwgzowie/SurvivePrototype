using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Character
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class SidekickCharacterAppearance : MonoBehaviour
    {
        [SerializeField] private SidekickCharacterCatalog catalog;
        [SerializeField] private bool loadSavedProfile = true;
        private GameObject visualRoot;
        private readonly List<Renderer> originals = new List<Renderer>();
        public string LastError { get; private set; }
        public CharacterProfile AppliedProfile { get; private set; }

        public void Configure(SidekickCharacterCatalog characterCatalog, bool shouldLoadSavedProfile)
        {
            catalog = characterCatalog;
            loadSavedProfile = shouldLoadSavedProfile;
        }

        private void Start()
        {
            if (!loadSavedProfile) return;
            catalog ??= SidekickCharacterCatalog.Load();
            if (catalog != null && CharacterProfileStore.TryLoad(out CharacterProfile profile)) Apply(profile);
        }

        public bool Apply(CharacterProfile profile)
        {
            catalog ??= SidekickCharacterCatalog.Load();
            if (catalog == null) { LastError = "No se encontró el catálogo Sidekick."; return false; }
            if (!catalog.Validate(profile, out string error)) { LastError = error; return false; }
            if (visualRoot != null && SameParts(AppliedProfile, profile))
            {
                foreach (SkinnedMeshRenderer renderer in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    ApplyBody(renderer, profile);
                AppliedProfile = profile.Clone();
                LastError = null;
                return true;
            }
            var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
                if ((visualRoot == null || !bone.IsChildOf(visualRoot.transform)) && !bones.ContainsKey(bone.name))
                    bones.Add(bone.name, bone);
            var nextRoot = new GameObject("SidekickAppearance");
            nextRoot.transform.SetParent(transform, false);
            nextRoot.SetActive(false);
            var createdAuxiliaryBones = new List<GameObject>();
            try
            {
                foreach (CharacterPartSelection selection in profile.parts)
                {
                    SidekickCharacterCatalog.Part part = catalog.FindPart(selection.partId);
                    SkinnedMeshRenderer[] sources = part.prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (sources.Length == 0) throw new InvalidOperationException("El módulo no tiene malla: " + part.id);
                    foreach (SkinnedMeshRenderer source in sources)
                    {
                        if (source.sharedMesh == null) continue;
                        var child = new GameObject(selection.slot + "/" + part.id);
                        child.layer = gameObject.layer;
                        child.transform.SetParent(nextRoot.transform, false);
                        Matrix4x4 relative = part.prefab.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                        child.transform.localPosition = relative.GetColumn(3);
                        child.transform.localRotation = relative.rotation;
                        child.transform.localScale = relative.lossyScale;
                        var renderer = child.AddComponent<SkinnedMeshRenderer>();
                        renderer.sharedMesh = source.sharedMesh;
                        var remapped = new Transform[source.bones.Length];
                        for (int i = 0; i < remapped.Length; i++)
                            remapped[i] = ResolveBone(source.bones[i], part.prefab.transform, bones, createdAuxiliaryBones);
                        renderer.bones = remapped;
                        renderer.rootBone = source.rootBone != null ? ResolveBone(source.rootBone, part.prefab.transform, bones, createdAuxiliaryBones)
                            : (bones.TryGetValue("pelvis", out Transform pelvis) ? pelvis : transform);
                        Material[] materials = source.sharedMaterials;
                        if (part.material != null)
                        {
                            materials = new Material[Mathf.Max(1, source.sharedMesh.subMeshCount)];
                            for (int i = 0; i < materials.Length; i++) materials[i] = part.material;
                        }
                        renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = source.shadowCastingMode;
                        renderer.receiveShadows = true;
                        renderer.updateWhenOffscreen = true;
                        renderer.localBounds = new Bounds(new Vector3(0, 0.95f, 0), new Vector3(3, 3, 3));
                        ApplyBody(renderer, profile);
                    }
                }
                if (visualRoot == null)
                    foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (!renderer.transform.IsChildOf(nextRoot.transform)) originals.Add(renderer);
                foreach (Renderer renderer in originals) if (renderer != null) renderer.enabled = false;
                if (visualRoot != null) { visualRoot.SetActive(false); ReleaseVisualObject(visualRoot); }
                visualRoot = nextRoot;
                visualRoot.SetActive(true);
                AppliedProfile = profile.Clone();
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                ReleaseVisualObject(nextRoot);
                for (int i = createdAuxiliaryBones.Count - 1; i >= 0; i--)
                    if (createdAuxiliaryBones[i] != null) ReleaseVisualObject(createdAuxiliaryBones[i]);
                LastError = exception.Message;
                Debug.LogError("No se pudo aplicar el personaje Sidekick: " + LastError, this);
                return false;
            }
        }

        private Transform ResolveBone(Transform source, Transform sourceRoot, Dictionary<string, Transform> bones,
            List<GameObject> createdAuxiliaryBones)
        {
            if (source == null) throw new InvalidOperationException("El módulo contiene un hueso vacío.");
            if (bones.TryGetValue(source.name, out Transform target)) return target;
            // Auxiliary attachment bones are optional in the gameplay rig; add only the missing chain, never another skeleton.
            Transform parent = source.parent != null && source != sourceRoot
                ? ResolveBone(source.parent, sourceRoot, bones, createdAuxiliaryBones) : transform;
            var auxiliary = new GameObject(source.name);
            createdAuxiliaryBones.Add(auxiliary);
            auxiliary.transform.SetParent(parent, false);
            auxiliary.transform.localPosition = source.localPosition;
            auxiliary.transform.localRotation = source.localRotation;
            auxiliary.transform.localScale = source.localScale;
            bones[source.name] = auxiliary.transform;
            return auxiliary.transform;
        }

        private static bool SameParts(CharacterProfile left, CharacterProfile right)
        {
            if (left?.parts == null || right?.parts == null || left.parts.Count != right.parts.Count) return false;
            for (int i = 0; i < left.parts.Count; i++)
                if (left.parts[i].slot != right.parts[i].slot || left.parts[i].partId != right.parts[i].partId) return false;
            return true;
        }

        public static void ApplyBody(SkinnedMeshRenderer renderer, CharacterProfile profile)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null) return;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                if (name.EndsWith("masculineFeminine", StringComparison.OrdinalIgnoreCase))
                    renderer.SetBlendShapeWeight(i, profile.gender == CharacterGender.Feminine ? 100 : 0);
                else if (name.EndsWith("defaultSkinny", StringComparison.OrdinalIgnoreCase))
                    renderer.SetBlendShapeWeight(i, Mathf.Max(0, -profile.bodySize));
                else if (name.EndsWith("defaultHeavy", StringComparison.OrdinalIgnoreCase))
                    renderer.SetBlendShapeWeight(i, Mathf.Max(0, profile.bodySize));
                else if (name.EndsWith("defaultBuff", StringComparison.OrdinalIgnoreCase))
                    renderer.SetBlendShapeWeight(i, Mathf.Clamp(profile.muscle, 0, 100));
            }
        }

        private static void ReleaseVisualObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
