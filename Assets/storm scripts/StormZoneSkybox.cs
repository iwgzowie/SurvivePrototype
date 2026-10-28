using UnityEngine; // Herramientas de Unity

public class StormZoneSkybox : MonoBehaviour // Controla la activación de la tormenta
{
    [SerializeField] private Transform jugador; // Jugador asignado desde el Inspector
    [SerializeField] private StormLightingSkybox stormLightingSkybox; // Controlador de iluminación
    [SerializeField] private RainController rainController; // Controlador de la lluvia

    private bool tormentaActivada = false; // Guarda si la tormenta ya comenzó

    private void OnTriggerEnter(Collider other) // Se ejecuta cuando un collider entra en la zona
    {
        if (tormentaActivada) // Comprobamos si la tormenta ya está activada
        {
            return; // Evitamos activarla nuevamente
        }

        if (jugador == null) // Comprobamos que el jugador esté asignado
        {
            return; // Salimos si falta la referencia
        }

        if (other.transform != jugador && !other.transform.IsChildOf(jugador)) // Comprobamos si el collider pertenece al jugador o a uno de sus hijos
        {
            return; // Ignoramos los colliders de otros objetos
        }

        tormentaActivada = true; // Marcamos la tormenta como activada

        if (stormLightingSkybox != null) // Comprobamos que el controlador de iluminación esté asignado
        {
            stormLightingSkybox.ActivarNoche(); // Iniciamos la transición a la noche
        }

        if (rainController != null) // Comprobamos que el controlador de lluvia esté asignado
        {
            rainController.ActivarLluvia(); // Activamos la lluvia
        }
    }
}