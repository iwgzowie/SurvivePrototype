using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Comandos de depuración para probar el flujo de oleadas mediante teclado.
/// </summary>
public class WaveDebugCommands : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;
    private bool showDebugMenu = false;

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

        // Tecla K: Alternar la apertura/cierre del menú de comandos
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            showDebugMenu = !showDebugMenu;
            Debug.Log($"Menú de depuración {(showDebugMenu ? "ABIERTO" : "CERRADO")}");
        }

        if (showDebugMenu && Keyboard.current != null)
        {
            // Tecla N: Resetear oleadas
            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                ResetWaves();
            }

            // Tecla M: Matar enemigos de la oleada actual
            if (Keyboard.current.mKey.wasPressedThisFrame)
            {
                KillAllActiveEnemies();
            }

            // Tecla B: Matar al Boss activo
            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                KillActiveBoss();
            }

            // Tecla T: Saltear el tiempo de descanso post-Boss
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                SkipRestPeriod();
            }
        }
    }

    private void OnGUI()
    {
        if (!showDebugMenu) return;

        GUILayout.BeginArea(new Rect(20, 20, 340, 190), "=== MENÚ DE DEBUG (Oleadas) ===", GUI.skin.window);
        GUILayout.Space(5);
        GUILayout.Label("Usa las siguientes teclas con el menú abierto:");
        GUILayout.Label("[N] - Resetear oleada / Reiniciar partida");
        GUILayout.Label("[M] - Matar enemigos de la oleada actual");
        GUILayout.Label("[B] - Matar instantáneamente al Boss activo");
        GUILayout.Label("[T] - Saltear descanso (2 min tras el Boss)");
        GUILayout.Space(5);
        if (GUILayout.Button("Cerrar Menú (Presionar K)"))
        {
            showDebugMenu = false;
        }
        GUILayout.EndArea();
    }

    private void KillAllActiveEnemies()
    {
        var enemies = new System.Collections.Generic.List<EnemyHealth>(spawner.AliveEnemies);
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(99999f);
            }
        }
        Debug.Log("Debug: Todos los enemigos fueron eliminados.");
    }

    private void KillActiveBoss()
    {
        BossBase[] activeBosses = FindObjectsByType<BossBase>(FindObjectsSortMode.None);
        bool bossFound = false;
        foreach (var boss in activeBosses)
        {
            if (boss != null && boss.Health != null && !boss.Health.IsDead)
            {
                boss.Health.TakeDamage(999999f);
                bossFound = true;
                Debug.Log("Debug: Boss eliminado mediante comando.");
            }
        }
        if (!bossFound)
        {
            Debug.Log("Debug: No se encontró ningún Boss activo en la escena.");
        }
    }
    private void SkipRestPeriod()
    {
        if (spawner.State == WaveRunState.Resting)
        {
            spawner.SkipRest();
            Debug.Log("Debug: Tiempo de descanso salteado.");
        }
        else
        {
            Debug.Log("Debug: No hay un periodo de descanso activo en este momento.");
        }
    }

    private void ResetWaves()
    {
        KillAllActiveEnemies();
        spawner.ResetAndRestartWaves();
        Debug.Log("Debug: Oleadas reseteadas desde el principio.");
    }
}
