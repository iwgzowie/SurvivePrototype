using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador básico para el Boss. 
/// Por ahora permanece estático para pruebas, pero puede recibir daño y ser destruido.
/// </summary>

public class BossBase : MonoBehaviour
{
    [Header("Pruebas / Comportamiento")]
    [Tooltip("Si está activo, el Boss no se moverá hacia el jugador.")]
    [SerializeField] private bool stayStaticForTesting = true;

    [Header("Componentes")]
    private NavMeshAgent navMeshAgent;
    private EnemyHealth healthComponent;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        healthComponent = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        // Si se activa el modo de prueba estático, detenemos el desplazamiento del NavMeshAgent
        if (stayStaticForTesting && navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
        }

        // Suscribirse a la muerte para limpiar referencias
        if (healthComponent != null)
        {
            healthComponent.OnDeath += HandleBossDeath;
        }
    }

    private void Update()
    {
        if (!stayStaticForTesting && navMeshAgent != null && navMeshAgent.enabled)
        {
            // Ejemplo de movimiento hacia el jugador
        }
    }

    private void HandleBossDeath()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= HandleBossDeath;
        }

        Debug.Log("¡El Boss fue eliminado!");

        Destroy(gameObject, 0.1f);
    }

    /// <summary>
    /// Permite activar o desactivar el movimiento estático en runtime
    /// </summary>
    public void SetStaticTestingMode(bool isStatic)
    {
        stayStaticForTesting = isStatic;
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = isStatic;
        }
    }
}
