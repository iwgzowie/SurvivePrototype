using UnityEngine;

public class HealthPotionPickup : MonoBehaviour
{
    [Header("Curación")]
    [SerializeField] private float healAmount = 25f;

    [Header("Efectos Visuales")]
    [SerializeField] private float rotationSpeed = 75f;
    [SerializeField] private bool enableFloat = true;
    [SerializeField] private float floatSpeed = 2.5f;
    [SerializeField] private float floatAmplitude = 0.15f;

    [Header("Audio / Efectos (Opcional)")]
    [SerializeField] private AudioClip healSound;
    [SerializeField] private GameObject healVfx;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        
        transform.Rotate(Vector3.up * (rotationSpeed * Time.deltaTime));

       
        if (enableFloat)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                // Verifica si el jugador necesita curación            
                if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
                {
                    return; // No recoge la poción si ya está al 100%
                }
                

                
                playerHealth.Heal(healAmount);
                Debug.Log($"¡Poción consumida! +{healAmount} de vida.");

                
                if (healSound != null)
                {
                    AudioSource.PlayClipAtPoint(healSound, transform.position);
                }

                
                if (healVfx != null)
                {
                    Instantiate(healVfx, other.transform.position, Quaternion.identity);
                }

                
                Destroy(gameObject);
            }
        }
    }
}