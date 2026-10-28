using UnityEngine; // Herramientas de Unity

public class LightningController : MonoBehaviour // Controla los relámpagos de la tormenta
{
    [SerializeField] private Light luzRelampago1; // Primera luz utilizada para simular el relámpago
    [SerializeField] private Light luzRelampago2; // Segunda luz utilizada para simular el relámpago

    [Header("Intensidad")] // Agrupa las opciones de intensidad en el Inspector
    [SerializeField] private float intensidadPrimerDestello = 8f; // Intensidad del primer destello
    [SerializeField] private float intensidadSegundoDestello = 4f; // Intensidad del segundo destello

    [Header("Duración")] // Agrupa las opciones de duración
    [SerializeField] private float duracionPrimerDestello = 1f; // Cuánto dura encendido el primer destello
    [SerializeField] private float duracionSegundoDestello = 0.15f; // Cuánto dura encendido el segundo destello

    [Header("Tiempo entre destellos")] // Agrupa los tiempos del patrón doble
    [SerializeField] private float tiempoEntreDestellos = 0.1f; // Tiempo apagado entre el primer y segundo destello

    [Header("Tiempo entre relámpagos")] // Agrupa el tiempo aleatorio entre relámpagos
    [SerializeField] private float tiempoMinimo = 3f; // Tiempo mínimo entre relámpagos
    [SerializeField] private float tiempoMaximo = 8f; // Tiempo máximo entre relámpagos

    private float tiempoSiguienteRelampago; // Guarda el tiempo restante hasta el próximo relámpago

    private bool relampagosActivados = false; // Indica si ya terminó la transición a noche

    private void Start() // Se ejecuta al comenzar el juego
    {
        luzRelampago1.intensity = 0f; // Nos aseguramos de que la primera luz empiece apagada
        luzRelampago2.intensity = 0f; // Nos aseguramos de que la segunda luz empiece apagada
    }

    private void Update() // Se ejecuta cada frame
    {
        if (!relampagosActivados) // Si todavía no se activaron los relámpagos
        {
            return; // No hacemos nada
        }

        tiempoSiguienteRelampago -= Time.deltaTime; // Reducimos el tiempo restante

        if (tiempoSiguienteRelampago <= 0f) // Comprobamos si llegó el momento del relámpago
        {
            int modoRelampago = Random.Range(0, 2); // Elegimos aleatoriamente entre los dos modos

            if (modoRelampago == 0) // Si salió el modo 0
            {
                RelampagoSimple(); // Ejecutamos un único destello
            }
            else // Si salió el modo 1
            {
                RelampagoDoble(); // Ejecutamos dos destellos
            }

            tiempoSiguienteRelampago = Random.Range(tiempoMinimo, tiempoMaximo); // Elegimos el tiempo hasta el próximo relámpago
        }
    }

    public void ActivarRelampagos() // Activa los relámpagos cuando ya es de noche
    {
        if (relampagosActivados) // Comprobamos si ya estaban activados
        {
            return; // Evitamos activarlos nuevamente
        }

        relampagosActivados = true; // Permitimos que comiencen los relámpagos

        tiempoSiguienteRelampago = Random.Range(tiempoMinimo, tiempoMaximo); // Esperamos un tiempo aleatorio antes del primer relámpago
    }

    private void RelampagoSimple() // Realiza un único destello con las dos luces
    {
        luzRelampago1.intensity = intensidadPrimerDestello; // Encendemos la primera luz
        luzRelampago2.intensity = intensidadPrimerDestello; // Encendemos la segunda luz

        Invoke(nameof(ApagarRelampago), duracionPrimerDestello); // Apagamos las dos luces después de la duración configurada
    }

    private void RelampagoDoble() // Realiza dos destellos consecutivos con las dos luces
    {
        luzRelampago1.intensity = intensidadPrimerDestello; // Encendemos la primera luz
        luzRelampago2.intensity = intensidadPrimerDestello; // Encendemos la segunda luz

        Invoke(nameof(SegundoDestello), duracionPrimerDestello); // Esperamos antes del segundo destello
    }

    private void SegundoDestello() // Prepara el segundo destello
    {
        luzRelampago1.intensity = 0f; // Apagamos la primera luz
        luzRelampago2.intensity = 0f; // Apagamos la segunda luz

        Invoke(nameof(EncenderSegundoDestello), tiempoEntreDestellos); // Esperamos antes de encender nuevamente
    }

    private void EncenderSegundoDestello() // Enciende el segundo destello
    {
        luzRelampago1.intensity = intensidadSegundoDestello; // Encendemos la primera luz
        luzRelampago2.intensity = intensidadSegundoDestello; // Encendemos la segunda luz

        Invoke(nameof(ApagarRelampago), duracionSegundoDestello); // Apagamos después de la duración configurada
    }

    private void ApagarRelampago() // Apaga completamente los dos relámpagos
    {
        luzRelampago1.intensity = 0f; // Apagamos la primera luz
        luzRelampago2.intensity = 0f; // Apagamos la segunda luz
    }
}