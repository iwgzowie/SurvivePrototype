using UnityEngine; // Herramientas de Unity

public class RainFollow : MonoBehaviour // Hace que la lluvia siga al jugador
{
    [SerializeField] private Transform jugador; // Jugador que queremos seguir
    [SerializeField] private float altura = 15f; // Altura fija de la lluvia sobre el jugador

    private void Update() // Se ejecuta cada frame
    {
        if (jugador == null) // Comprobamos que haya un jugador asignado
        {
            return; // Si no hay jugador, no hacemos nada
        }

        transform.position = new Vector3(
            jugador.position.x, // La lluvia sigue al jugador en X
            altura,             // La lluvia mantiene siempre esta altura
            jugador.position.z  // La lluvia sigue al jugador en Z
        );
    }
}