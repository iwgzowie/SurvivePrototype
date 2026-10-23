using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    [Header("Identificador de la Llave")]
    [SerializeField] private string keyID = "BossKey";

    [Header("Efectos Visuales (Opcional)")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatAmplitude = 0.25f;

    private Vector3 startPosition;

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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            //  Notificar al sistema de inventario o al script del jugador
            // Ejemplo si tienes un script en el player:
            // PlayerInventory inventory = other.GetComponent<PlayerInventory>();
            // if (inventory != null) inventory.AddKey(keyID);

            Debug.Log($"¡Llave recogida: {keyID}!");

            
            Destroy(gameObject);
        }
    }
}