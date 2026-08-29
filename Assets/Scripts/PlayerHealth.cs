using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    public bool IsDead { get; private set; }

    private void Awake()
    {
        currentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        Debug.Log($"Jugador recibió {amount} de daño. Vida restante: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        IsDead = true;
        Debug.Log("¡El jugador ha muerto!");

        // Desactivar movimiento si usa PlayerController
        if (TryGetComponent<PlayerController>(out var controller))
        {
            controller.enabled = false;
        }

        // Si usas CharacterController o Animator, desactívalos o dispara el trigger aquí:
        // GetComponent<Animator>()?.SetTrigger("Die");
    }
}