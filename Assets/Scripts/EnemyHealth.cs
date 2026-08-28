using System;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Death Settings")]
    [SerializeField] private float destroyDelay = 2.5f; // Tiempo antes de destruir el objeto (para dejar correr la animación)

    // Eventos para conectar interfaces (UI) u otros sistemas
    public event Action<float, float> OnHealthChanged; // (vidaActual, vidaMaxima)
    public event Action OnDeath;

    private bool isDead = false;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        OnDeath?.Invoke();

        // 1. Desactivar componentes para que el enemigo no siga atacando ni bloqueando
        if (TryGetComponent<NavMeshAgent>(out var agent)) agent.enabled = false;
        if (TryGetComponent<EnemyAI>(out var ai)) ai.enabled = false;
        if (TryGetComponent<Collider>(out var col)) col.enabled = false;

        // 2. Disparar animación de muerte si existe un Animator
        if (TryGetComponent<Animator>(out var anim))
        {
            anim.SetTrigger("Die");
        }

        // 3. Destruir el GameObject tras la animación
        Destroy(gameObject, destroyDelay);
    }
}