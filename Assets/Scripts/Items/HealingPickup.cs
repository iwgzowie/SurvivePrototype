using Survive.Combat;
using UnityEngine;

namespace Survive.Items
{
    /// <summary>Reutiliza la flotación y rotación de los objetos del mundo. E guarda, nunca cura al tocar.</summary>
    public sealed class HealingPickup : MasterItem, IWorldInteractable
    {
        [SerializeField] private HealingDefinition definition;
        [SerializeField, Min(1)] private int quantity = 1;
        private bool collected;
        public HealingDefinition Definition => definition;
        public int Quantity => quantity;
        public string InteractionPrompt => "[E] Guardar " + (definition != null ? definition.displayName : "curación") + " x" + quantity;
        public void Initialize(HealingDefinition value, int count) { definition = value; quantity = Mathf.Max(1, count); }
        public bool CanInteract(GameObject player) => !collected && isActiveAndEnabled && !IsEquipped && definition != null
            && player != null && player.TryGetComponent(out PlayerWeaponInventory inventory)
            && inventory.CanManageInventory && inventory.CanAddHealing(definition, quantity);
        public bool Interact(GameObject player)
        {
            if (!CanInteract(player)) return false;
            collected = true;
            if (!player.GetComponent<PlayerWeaponInventory>().TryAddHealing(definition, quantity)) { collected = false; return false; }
            gameObject.SetActive(false); Destroy(gameObject); return true;
        }
    }
}
