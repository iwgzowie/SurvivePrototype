using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Survive.Items
{
    /// <summary>Un plano por apertura lógica; sólo guía etapas para las que el jugador tiene una llave.</summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerDoorGuide : MonoBehaviour
    {
        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<Material, Material> highlighted = new Dictionary<Material, Material>();
        PlayerKeyInventory keys;
        Material neon;
        PuertaConLlave lastGuided;
        public int Blueprints { get; private set; }
        public PuertaConLlave Target { get; private set; }
        void Awake() { keys = PlayerKeyInventory.ForPlayer(gameObject, true); neon = Resources.Load<Material>("Gameplay/DoorGuideNeon"); }
        void OnEnable() { keys.Changed += Refresh; PuertaConLlave.Opened += OnDoorOpened; Refresh(); }
        void OnDisable() { keys.Changed -= Refresh; PuertaConLlave.Opened -= OnDoorOpened; Restore(); }
        public bool TryAddBlueprint()
        {
            var health = GetComponent<PlayerHealth>();
            if (!isActiveAndEnabled || health.IsDead || Time.timeScale <= 0 || Blueprints == int.MaxValue || neon == null) return false;
            Blueprints++; Refresh(); return true;
        }
        void OnDoorOpened(PuertaConLlave door)
        {
            if (lastGuided != null && lastGuided.OpensTogether(door))
            { Blueprints = Mathf.Max(0, Blueprints - 1); lastGuided = null; }
            Refresh();
        }
        void Refresh()
        {
            Restore();
            if (Blueprints == 0 || !keys.HasKey || neon == null) return;
            Target = FindObjectsByType<PuertaConLlave>(FindObjectsSortMode.None)
                .Where(d => d.gameObject.scene == gameObject.scene && d.CanUnlockStage).OrderBy(d => d.GuideOrder).ThenBy(d => d.name).FirstOrDefault();
            if (Target == null) return;
            lastGuided = Target;
            foreach (var door in FindObjectsByType<PuertaConLlave>(FindObjectsSortMode.None))
                if (door.gameObject.scene == gameObject.scene && Target.OpensTogether(door))
                    foreach (var renderer in door.GuideRenderers)
                    {
                        if (originals.ContainsKey(renderer)) continue;
                        originals[renderer] = renderer.sharedMaterials;
                        renderer.sharedMaterials = originals[renderer].Select(Highlight).ToArray();
                    }
        }
        Material Highlight(Material original)
        {
            if (original == null) return null;
            if (highlighted.TryGetValue(original, out var cached)) return cached;
            // Una copia por material: conserva albedo, normales, metalicidad y UV sin editar el asset.
            var material = new Material(original) { name = original.name + " (Guía roja)", hideFlags = HideFlags.DontSave };
            if (!material.HasProperty("_EmissionColor")) material.shader = neon.shader;
            string albedo = original.HasProperty("_BaseMap") ? "_BaseMap" : original.HasProperty("_MainTex") ? "_MainTex" : null;
            if (albedo != null)
            {
                if (material.HasProperty("_BaseMap")) CopyTexture(original, albedo, material, "_BaseMap");
                // La emisión sigue la textura de la puerta en lugar de taparla con un color plano.
                if (material.HasProperty("_EmissionMap")) CopyTexture(original, albedo, material, "_EmissionMap");
            }
            Color tint = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor")
                : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
            // El albedo incluye el tinte: la chapa oscura conserva sus detalles bajo el brillo rojo.
            float luminance = Mathf.Clamp(tint.r * .2126f + tint.g * .7152f + tint.b * .0722f, .1f, 1f);
            Color emission = neon.GetColor("_EmissionColor") * luminance; emission.a = 1;
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.EnableKeyword("_EMISSION");
            highlighted.Add(original, material);
            return material;
        }
        static void CopyTexture(Material source, string sourceProperty, Material target, string targetProperty)
        {
            target.SetTexture(targetProperty, source.GetTexture(sourceProperty));
            target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
            target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
        }
        void Restore()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            originals.Clear(); Target = null;
            foreach (var material in highlighted.Values) if (material != null) Destroy(material);
            highlighted.Clear();
        }
    }
}
