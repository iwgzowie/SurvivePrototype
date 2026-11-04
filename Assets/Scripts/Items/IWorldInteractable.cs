using UnityEngine;

namespace Survive.Items
{
    /// <summary>Extiende el raycast y las comprobaciones de alcance existentes.</summary>
    public interface IWorldInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(GameObject player);
        bool Interact(GameObject player);
    }

    /// <summary>Colliders de un mismo objeto lógico que no deben bloquear su propia interacción.</summary>
    public interface IWorldInteractionColliderGroup
    {
        bool OwnsCollider(Collider collider);
    }

    /// <summary>Aviso que depende del inventario del jugador que está mirando el objeto.</summary>
    public interface IPlayerInteractionPrompt
    {
        string GetInteractionPrompt(GameObject player);
    }
}
