using UnityEngine; // Herramientas de Unity

public class LightningSystem : MonoBehaviour // Controla el movimiento del sistema de relámpagos
{
    [SerializeField] private Transform jugador; // Referencia al Player que queremos seguir

    private void Update() // Se ejecuta cada frame
    {
        if (jugador == null) // Comprobamos que el Player esté asignado
        {
            return; // Si no está asignado, no hacemos nada
        }

        transform.position = new Vector3(
            jugador.position.x, // Seguimos al Player en el eje X
            transform.position.y, // Mantenemos la altura configurada en el Inspector
            jugador.position.z  // Seguimos al Player en el eje Z
        );
    }
}