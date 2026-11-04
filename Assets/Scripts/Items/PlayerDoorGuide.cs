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
        PlayerKeyInventory keys;
        Material neon;
        PuertaConLlave lastGuided;
        public int Blueprints { get; private set; }
        public PuertaConLlave Target { get; private set; }
        void Awake() { keys = PlayerKeyInventory.ForPlayer(gameObject, true); neon = Resources.Load<Material>("Gameplay/DoorGuideNeon"); }
        void OnEnable() { keys.Changed += Refresh; PuertaConLlave.Opened += OnDoorOpened; }
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
                        renderer.sharedMaterials = Enumerable.Repeat(neon, renderer.sharedMaterials.Length).ToArray();
                    }
        }
        void Restore()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            originals.Clear(); Target = null;
        }
    }
}
