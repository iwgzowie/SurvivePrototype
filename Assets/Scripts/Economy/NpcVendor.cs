using Survive.Items;
using UnityEngine;

namespace Survive.Economy
{
    [DisallowMultipleComponent]
    public sealed class NpcVendor : MonoBehaviour, IWorldInteractable
    {
        [SerializeField] private string displayName = "Armero";
        [SerializeField] private VendorCatalog catalog;
        [SerializeField, Min(.1f)] private float tradeDistance = 3f;
        public string DisplayName => displayName;
        public VendorCatalog Catalog => catalog;
        public string InteractionPrompt => $"[E] Comerciar · {displayName}";

        public bool CanInteract(GameObject player)
        {
            return player != null && isActiveAndEnabled && catalog != null
                && player.TryGetComponent(out PlayerVendorController controller) && controller.CanOpen;
        }

        // La tienda revalida esto en cada compra y mientras permanece abierta.
        public bool IsInReach(GameObject player)
        {
            if (!isActiveAndEnabled || player == null
                || Vector3.Distance(player.transform.position, transform.position) > tradeDistance) return false;
            Vector3 origin = player.transform.position + Vector3.up;
            Vector3 destination = transform.position + Vector3.up;
            foreach (Collider body in Physics.OverlapSphere(origin, .02f, ~0, QueryTriggerInteraction.Ignore))
                if (!body.transform.IsChildOf(player.transform) && !body.transform.IsChildOf(transform)) return false;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, destination - origin,
                         Vector3.Distance(origin, destination), ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(player.transform) && !hit.transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        public bool Interact(GameObject player) => CanInteract(player) && IsInReach(player)
            && player.GetComponent<PlayerVendorController>().TryOpen(this);
    }
}
