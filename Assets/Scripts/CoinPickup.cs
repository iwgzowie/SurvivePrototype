using UnityEngine;
using Survive.Economy;

public class CoinPickup : MonoBehaviour
{
    [Header("Valor de la Moneda")]
    [SerializeField] private int coinValue = 1;

    [Header("Efectos Visuales")]
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private bool enableFloat = true;
    [SerializeField] private float floatSpeed = 3f;
    [SerializeField] private float floatAmplitude = 0.15f;

    [Header("Audio / Efectos (Opcional)")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private GameObject collectVfx;

    private Vector3 startPosition;
    private bool collected;

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
        PlayerWallet wallet = other.GetComponentInParent<PlayerWallet>();
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (!collected && wallet != null && coinValue > 0 && Time.timeScale > 0f
            && (health == null || !health.IsDead))
        {
            collected = true;
            wallet.AddCoins(coinValue);

            Debug.Log($"¡Moneda recolectada! +{coinValue}");

           
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position);
            }

            
            if (collectVfx != null)
            {
                Instantiate(collectVfx, transform.position, Quaternion.identity);
            }

            
            Destroy(gameObject);
        }
    }
}
