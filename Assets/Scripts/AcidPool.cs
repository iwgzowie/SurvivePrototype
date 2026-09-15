using UnityEngine;

public class AcidPool : MonoBehaviour
{
    [SerializeField] private float damage;
    [SerializeField] private float damageInterval;
    
    private PlayerHealth playerHealth;
    private float damageTimer;
    private float lifetime = 7f;
    private float damageEffectTimer = 0f;
    private float damageEffectDuration = 1.5f;
    private bool acidEffectActive = false;
    private bool playerInside = false;


    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerHealth = other.GetComponent<PlayerHealth>();

            playerInside = true;
            acidEffectActive = true;

            damageEffectTimer = 0f;
            damageTimer = 0f;

            // Daño inmediato al entrar.
            playerHealth.TakeDamage(damage);
        }
        
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            damageEffectTimer = damageEffectDuration;
            damageTimer = 0f;
        }
    }

    private void Update()
    {
        if (playerHealth == null || !acidEffectActive)
            return;

        damageTimer += Time.deltaTime;

        if (playerInside)
        {
            if (damageTimer >= damageInterval)
            {
                Debug.Log("Ácido: daño normal.");

                playerHealth.TakeDamage(damage);

                damageTimer = 0f;
            }
        }
        else
        {
            damageEffectTimer -= Time.deltaTime;

            if (damageTimer >= damageInterval)
            {
                float residualDamage = damage * 0.3f;

                Debug.Log("Ácido residual: daño reducido.");

                playerHealth.TakeDamage(residualDamage);

                damageTimer = 0f;
            }

            // Terminan los 2 segundos.
            if (damageEffectTimer <= 0f)
            {
                acidEffectActive = false;
                playerHealth = null;

                Debug.Log("El efecto residual del ácido terminó.");
            }
        }
    }

}