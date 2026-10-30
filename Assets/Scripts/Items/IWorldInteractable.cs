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
}
