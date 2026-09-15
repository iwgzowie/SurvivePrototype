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
    public event Action OnDeath;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log("Jugador recibió " + amount + " de daño. Vida actual: " + currentHealth);

        if (currentHealth > 0f)
        {
            return;
        }

        // La salud publica el resultado; la presentación y el bloqueo pertenecen al consumidor.
        IsDead = true;
        OnDeath?.Invoke();
    }
}
