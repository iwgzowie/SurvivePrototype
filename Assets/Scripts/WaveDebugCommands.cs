using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Comandos de depuración para probar el flujo de oleadas mediante teclado.
/// </summary>
public class WaveDebugCommands : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;

    private void Awake()
    {
        if (spawner == null)
        {
            spawner = GetComponent<EnemySpawner>();
        }
    }

    private void Update()
    {
        if (spawner == null) return;

        // Tecla R: Reiniciar todas las oleadas desde el principio (funciona en cualquier momento)
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartWaves();
        }

        if (!spawner.IsRunning) return;

        // Tecla K: Eliminar a todos los enemigos activos (Limpiar/Saltar oleada)
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            KillAllActiveEnemies();
        }
    }

    private void KillAllActiveEnemies()
    {
        // Obtiene copia de la lista de enemigos vivos para evitar modificar la colección durante la iteración
        var enemies = new System.Collections.Generic.List<EnemyHealth>(spawner.AliveEnemies);
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(99999f);
            }
        }
    }

    private void RestartWaves()
    {
        KillAllActiveEnemies();


        spawner.ResetAndRestartWaves();
    }
}
