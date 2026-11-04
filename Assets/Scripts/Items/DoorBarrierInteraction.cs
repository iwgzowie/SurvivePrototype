using UnityEngine;

namespace Survive.Items
{
    /// <summary>Permite usar la misma puerta al apuntar a su barrera física desde el otro lado.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorBarrierInteraction : MonoBehaviour, IWorldInteractable, IWorldInteractionColliderGroup, IPlayerInteractionPrompt
    {
        [SerializeField] private PuertaConLlave door;
        public PuertaConLlave Door => door;
        public string InteractionPrompt => door != null ? door.InteractionPrompt : "";
        public string GetInteractionPrompt(GameObject player) => door != null ? door.GetInteractionPrompt(player) : "";
        public bool CanInteract(GameObject player) => isActiveAndEnabled && door != null && door.CanInteract(player);
        public bool Interact(GameObject player) => CanInteract(player) && door.Interact(player);
        public bool OwnsCollider(Collider collider) => door != null && door.OwnsCollider(collider);
    }
}
