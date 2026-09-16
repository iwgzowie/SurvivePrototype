using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Salud")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    private float currentHealth;

    public bool IsDead { get; private set; }
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
        IsDead = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            return;
        }

        float nextHealth = Mathf.Max(0f, currentHealth - amount);
        if (nextHealth == currentHealth)
        {
            return;
        }

        currentHealth = nextHealth;
        Debug.Log("Jugador recibió " + amount + " de daño. Vida actual: " + currentHealth);

        // Publicar un estado coherente permite consultar IsDead desde cualquiera de los eventos.
        bool died = currentHealth <= 0f;
        if (died)
        {
            IsDead = true;
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // La salud publica el resultado; la presentación y el bloqueo pertenecen al consumidor.
        if (died)
        {
            OnDeath?.Invoke();
        }
    }
}
