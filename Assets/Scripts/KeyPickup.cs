using UnityEngine;


[RequireComponent(typeof(Collider))]
public class KeyPickup : MonoBehaviour
{
    
    public static bool getBossKey = false;

    [Header("Identificador de la Llave")]
    [SerializeField] private string keyID = "BossKey";

    [Header("Efectos Visuales")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatAmplitude = 0.25f;

    private Vector3 startPosition;

    private void Awake()
    {
        
        getBossKey = false;

        
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            
            getBossKey = true;

            
            Destroy(gameObject);
        }
    }
}