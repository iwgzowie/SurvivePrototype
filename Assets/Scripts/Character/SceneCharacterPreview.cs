using System;
using UnityEngine;

namespace Survive.Character
{
    /// <summary>Maniquí Sidekick del escenario; el borrador nunca escribe el perfil guardado.</summary>
    [DisallowMultipleComponent]
    public sealed class SceneCharacterPreview : MonoBehaviour
    {
        [SerializeField] private SidekickCharacterCatalog catalog;
        private GameObject rig;
        private SidekickCharacterAppearance appearance;

        public SidekickCharacterAppearance Appearance => appearance;
        public GameObject Rig => rig;

        private void Start() => RestoreSavedAppearance();

        public CharacterProfile LoadProfile()
        {
            catalog ??= SidekickCharacterCatalog.Load();
            if (catalog == null) throw new InvalidOperationException("Falta el catálogo Sidekick del agente.");
            return CharacterProfileStore.TryLoad(out CharacterProfile saved) && catalog.Validate(saved, out _)
                ? saved : catalog.CreateDefault();
        }

        public void RestoreSavedAppearance()
        {
            Apply(LoadProfile());
            Rotate(0);
        }

        public bool Apply(CharacterProfile profile)
        {
            EnsureRig();
            if (!appearance.Apply(profile)) return false;
            AlignFeet();
            return true;
        }

        private void EnsureRig()
        {
            if (rig != null) return;
            catalog ??= SidekickCharacterCatalog.Load();
            if (catalog == null || catalog.previewRig == null)
                throw new InvalidOperationException("Falta el rig Sidekick del agente.");
            rig = Instantiate(catalog.previewRig, transform, false);
            rig.name = "AgentPreviewRig";
            foreach (Animator animator in rig.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (AudioListener listener in rig.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (Collider collider in rig.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            appearance = rig.GetComponent<SidekickCharacterAppearance>() ?? rig.AddComponent<SidekickCharacterAppearance>();
            appearance.Configure(catalog, false);
        }

        public void Rotate(float degrees)
        {
            if (rig != null) rig.transform.localRotation = Quaternion.Euler(0, degrees, 0);
        }

        private void AlignFeet()
        {
            var baked = new Mesh();
            float minimumY = float.PositiveInfinity;
            try
            {
                foreach (SkinnedMeshRenderer renderer in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!renderer.enabled || renderer.sharedMesh == null) continue;
                    if (!renderer.name.StartsWith("FootLeft/", StringComparison.Ordinal)
                        && !renderer.name.StartsWith("FootRight/", StringComparison.Ordinal)) continue;
                    baked.Clear();
                    renderer.BakeMesh(baked);
                    foreach (Vector3 vertex in baked.vertices)
                        minimumY = Mathf.Min(minimumY, transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)).y);
                }
                if (!float.IsInfinity(minimumY)) rig.transform.localPosition -= Vector3.up * minimumY;
            }
            finally { Destroy(baked); }
        }
    }
}
