using UnityEngine;

namespace Survive.Items
{
    [RequireComponent(typeof(Collider))]
    public sealed class DoorBlueprintPickup : MonoBehaviour, IWorldInteractable
    {
        bool collected; Vector3 start;
        public string InteractionPrompt => "[E] Recoger plano de la próxima puerta";
        void Awake() { GetComponent<Collider>().isTrigger = true; }
        void Start() => start = transform.position;
        void Update()
        {
            transform.Rotate(Vector3.up, 65 * Time.deltaTime, Space.World);
            transform.position = start + Vector3.up * (Mathf.Sin(Time.time * 2f) * .08f);
        }
        public bool CanInteract(GameObject player)
        {
            var health = player != null ? player.GetComponentInParent<PlayerHealth>() : null;
            return isActiveAndEnabled && !collected && Time.timeScale > 0 && health != null && !health.IsDead;
        }
        public bool Interact(GameObject player)
        {
            if (!CanInteract(player)) return false;
            var health = player.GetComponentInParent<PlayerHealth>();
            var guide = health.GetComponent<PlayerDoorGuide>();
            if (guide == null) guide = health.gameObject.AddComponent<PlayerDoorGuide>();
            if (!guide.TryAddBlueprint()) return false;
            collected = true; gameObject.SetActive(false); Destroy(gameObject); return true;
        }
    }
}
