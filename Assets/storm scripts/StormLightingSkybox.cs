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
    [SerializeField] private StreetLightsController streetLightsController; // Controlador de las luces de las calles

    private float intensidadInicial;
    private float intensidadAmbientalInicial;
    private Quaternion rotacionInicial;
    private float tiempoTranscurrido = 0f;

    private bool nocheActivada = false;

    private void Start()
    {
        if (luzSol == null)
        {
            return;
        }

        intensidadInicial = luzSol.intensity;
        intensidadAmbientalInicial = RenderSettings.ambientIntensity;
        rotacionInicial = luzSol.transform.rotation;
    }

    private void Update()
    {
        if (!nocheActivada)
        {
            return;
        }

        tiempoTranscurrido += Time.deltaTime;

        float progreso = tiempoTranscurrido / tiempoTransicion;

        progreso = Mathf.Clamp01(progreso);

        luzSol.intensity = Mathf.Lerp(
            intensidadInicial,
            intensidadNoche,
            progreso
        );

        RenderSettings.ambientIntensity = Mathf.Lerp(
            intensidadAmbientalInicial,
            intensidadAmbientalNoche,
            progreso
        );

        Quaternion rotacionObjetivo = Quaternion.Euler(rotacionNoche);

        luzSol.transform.rotation = Quaternion.Lerp(
            rotacionInicial,
            rotacionObjetivo,
            progreso
        );

        if (skyboxController != null)
        {
            skyboxController.ActualizarTransicion(progreso);
        }

        if (progreso >= 1f)
        {
            luzSol.intensity = intensidadNoche;
            RenderSettings.ambientIntensity = intensidadAmbientalNoche;
            luzSol.transform.rotation = rotacionObjetivo;

            if (skyboxController != null)
            {
                skyboxController.ActualizarTransicion(1f);
            }

            nocheActivada = false;

            if (lightningController != null)
            {
                lightningController.ActivarRelampagos();
            }

            if (streetLightsController != null)
            {
                streetLightsController.ActivarLuces();
            }
        }
    }

    public void ActivarNoche()
    {
        if (luzSol == null)
        {
            return;
        }

        tiempoTranscurrido = 0f;
        nocheActivada = true;
    }
}