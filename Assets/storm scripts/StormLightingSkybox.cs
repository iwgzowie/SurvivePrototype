using UnityEngine; // Herramientas de Unity

public class StormLightingSkybox : MonoBehaviour // Controla la iluminación de la tormenta y comunica el progreso al Skybox
{
    [SerializeField] private Light luzSol; // Directional Light que representa el sol
    [SerializeField] private float intensidadNoche = 0.1f; // Intensidad de la luz durante la tormenta
    [SerializeField] private float intensidadAmbientalNoche = 0.2f; // Intensidad de la iluminación ambiental durante la tormenta
    [SerializeField] private float tiempoTransicion = 30f; // Tiempo que tarda la transición completa
    [SerializeField] private Vector3 rotacionNoche = new Vector3(220f, -30f, 0f); // Rotación del sol durante la tormenta
    [SerializeField] private LightningController lightningController; // Controlador de los relámpagos
    [SerializeField] private SkyboxController skyboxController; // Controlador que se encarga de los colores del Skybox

    private float intensidadInicial; // Guarda la intensidad original de la Directional Light
    private float intensidadAmbientalInicial; // Guarda la iluminación ambiental original
    private Quaternion rotacionInicial; // Guarda la rotación original del sol
    private float tiempoTranscurrido = 0f; // Guarda cuánto tiempo lleva la transición

    private bool nocheActivada = false; // Indica si comenzó la transición

    private void Start() // Se ejecuta al comenzar el juego
    {
        if (luzSol == null) // Comprobamos que haya una Directional Light asignada
        {
            return; // Salimos si no hay una luz asignada
        }

        intensidadInicial = luzSol.intensity; // Guardamos la intensidad inicial del día
        intensidadAmbientalInicial = RenderSettings.ambientIntensity; // Guardamos la iluminación ambiental inicial
        rotacionInicial = luzSol.transform.rotation; // Guardamos la rotación inicial del sol
    }

    private void Update() // Se ejecuta cada frame
    {
        if (!nocheActivada) // Si todavía no comenzó la transición
        {
            return; // No hacemos nada
        }

        tiempoTranscurrido += Time.deltaTime; // Aumentamos el tiempo transcurrido

        float progreso = tiempoTranscurrido / tiempoTransicion; // Calculamos el progreso de la transición

        progreso = Mathf.Clamp01(progreso); // Limitamos el progreso entre 0 y 1

        luzSol.intensity = Mathf.Lerp(
            intensidadInicial, // Intensidad inicial del sol
            intensidadNoche, // Intensidad final de la tormenta
            progreso // Progreso de la transición
        );

        RenderSettings.ambientIntensity = Mathf.Lerp(
            intensidadAmbientalInicial, // Iluminación ambiental inicial
            intensidadAmbientalNoche, // Iluminación ambiental final
            progreso // Progreso de la transición
        );

        Quaternion rotacionObjetivo = Quaternion.Euler(rotacionNoche); // Convertimos la rotación final en Quaternion

        luzSol.transform.rotation = Quaternion.Lerp(
            rotacionInicial, // Rotación inicial del sol
            rotacionObjetivo, // Rotación final del sol
            progreso // Progreso de la transición
        );

        if (skyboxController != null) // Comprobamos que SkyboxController esté asignado
        {
            skyboxController.ActualizarTransicion(progreso); // Le enviamos el progreso actual de la transición
        }

        if (progreso >= 1f) // Comprobamos si terminó la transición
        {
            luzSol.intensity = intensidadNoche; // Aseguramos la intensidad final
            RenderSettings.ambientIntensity = intensidadAmbientalNoche; // Aseguramos la iluminación ambiental final
            luzSol.transform.rotation = rotacionObjetivo; // Aseguramos la rotación final del sol

            if (skyboxController != null) // Comprobamos que SkyboxController esté asignado
            {
                skyboxController.ActualizarTransicion(1f); // Aseguramos que el Skybox llegue al color final
            }

            nocheActivada = false; // Dejamos de actualizar la iluminación

            if (lightningController != null) // Comprobamos que LightningController esté asignado
            {
                lightningController.ActivarRelampagos(); // Activamos los relámpagos al terminar la noche
            }
        }
    }

    public void ActivarNoche() // Comienza la transición de día a noche
    {
        if (luzSol == null) // Comprobamos que haya una luz asignada
        {
            return; // Si no hay luz, no hacemos nada
        }

        tiempoTranscurrido = 0f; // Reiniciamos el contador de transición
        nocheActivada = true; // Activamos la transición
    }
}