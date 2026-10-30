using Survive.Items;
using UnityEngine;

namespace Survive.Turrets
{
    public sealed class TurretItem : MasterItem, IWorldInteractable
    {
        [SerializeField] private TurretDefinition definition;
        private bool collected;
        public TurretDefinition Definition => definition;
        public string InteractionPrompt => "[E] Guardar torreta";
        protected override void Awake() { base.Awake(); SetItemType(ItemType.Turret); }
        protected override void Reset() { base.Reset(); SetItemType(ItemType.Turret); }
        public bool CanInteract(GameObject player) => isActiveAndEnabled && !collected && !IsEquipped && definition != null && player != null
            && player.TryGetComponent(out PlayerTurretController controller) && controller.CanReceive(definition);
        public bool Interact(GameObject player)
        {
            if (!CanInteract(player)) return false;
            collected = true;
            if (!player.GetComponent<PlayerTurretController>().TryAdd(definition)) { collected = false; return false; }
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
