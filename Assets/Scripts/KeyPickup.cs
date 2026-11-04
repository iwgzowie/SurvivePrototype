using UnityEngine;
using Survive.Items;


[RequireComponent(typeof(Collider))]
public class KeyPickup : MonoBehaviour, IWorldInteractable
{
    [Header("Efectos Visuales")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatAmplitude = 0.25f;

    private Vector3 startPosition;
    private bool collected;

    public string InteractionPrompt => "[E] Recoger llave del boss";

    private void Awake()
    {
        

        
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        
        transform.Rotate(Vector3.up * (rotationSpeed * Time.deltaTime));

        
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    public bool CanInteract(GameObject player)
    {
        PlayerHealth health = player != null ? player.GetComponentInParent<PlayerHealth>() : null;
        PlayerKeyInventory inventory = PlayerKeyInventory.ForPlayer(player);
        return isActiveAndEnabled && !collected && Time.timeScale > 0f && health != null && !health.IsDead
            && (inventory == null || inventory.CanReceiveKey);
    }

    public bool Interact(GameObject player)
    {
        if (!CanInteract(player)) return false;
        collected = true;
        PlayerKeyInventory inventory = PlayerKeyInventory.ForPlayer(player, true);
        if (inventory == null || !inventory.TryAddKey()) { collected = false; return false; }
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
