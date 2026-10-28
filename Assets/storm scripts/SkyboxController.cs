using UnityEngine; // Herramientas de Unity

public class SkyboxController : MonoBehaviour // Controla los colores y las estrellas del Skybox
{
    [Header("Material del Skybox")] // Agrupa la referencia al material
    [SerializeField] private Material skyboxMaterial; // Material original del Skybox

    [Header("Colores de Noche")] // Agrupa los colores de la noche
    [SerializeField] private Color zenithColorNoche = Color.black; // Color superior del cielo durante la noche
    [SerializeField] private Color horizonColorNoche = Color.black; // Color del horizonte durante la noche
    [SerializeField] private Color horizonFogColorNoche = Color.black; // Color de la niebla del horizonte durante la noche
    [SerializeField] private Color groundColorNoche = Color.black; // Color del suelo del cielo durante la noche

    [Header("Estrellas")] // Agrupa la configuración de las estrellas
    [SerializeField] private bool estrellasActivadasNoche = true; // Indica si las estrellas estarán activadas durante la noche

    private Material materialInstancia; // Copia del material que usaremos durante el juego

    private Color zenithColorInicial; // Guarda el Zenith original
    private Color horizonColorInicial; // Guarda el Horizon original
    private Color horizonFogColorInicial; // Guarda el Horizon Fog original
    private Color groundColorInicial; // Guarda el Ground original

    private void Start() // Se ejecuta al comenzar el juego
    {
        if (skyboxMaterial == null) // Comprobamos que haya un material asignado
        {
            return; // Si no hay material, salimos
        }

        materialInstancia = new Material(skyboxMaterial); // Creamos una copia para no modificar el material original

        RenderSettings.skybox = materialInstancia; // Usamos la copia como Skybox de esta escena

        zenithColorInicial = materialInstancia.GetColor("_ZenithColor"); // Guardamos el Zenith original
        horizonColorInicial = materialInstancia.GetColor("_HorizonColor"); // Guardamos el Horizon original
        horizonFogColorInicial = materialInstancia.GetColor("_HorizonFogColor"); // Guardamos el Horizon Fog original
        groundColorInicial = materialInstancia.GetColor("_GroundColor"); // Guardamos el Ground original

        materialInstancia.SetFloat(
            "_EnableStars",
            0f
        ); // Comenzamos el juego con las estrellas apagadas
    }

    public void ActualizarTransicion(float progreso) // Recibe el progreso de la transición desde StormLightingSkybox
    {
        if (materialInstancia == null) // Comprobamos que exista la copia del material
        {
            return; // Si no existe, salimos
        }

        Color zenithActual = Color.Lerp(
            zenithColorInicial,
            zenithColorNoche,
            progreso
        ); // Mezclamos el Zenith original con el color de noche

        Color horizonActual = Color.Lerp(
            horizonColorInicial,
            horizonColorNoche,
            progreso
        ); // Mezclamos el Horizon original con el color de noche

        Color horizonFogActual = Color.Lerp(
            horizonFogColorInicial,
            horizonFogColorNoche,
            progreso
        ); // Mezclamos el Horizon Fog original con el de noche

        Color groundActual = Color.Lerp(
            groundColorInicial,
            groundColorNoche,
            progreso
        ); // Mezclamos el Ground original con el de noche

        materialInstancia.SetColor(
            "_ZenithColor",
            zenithActual
        ); // Aplicamos el color superior

        materialInstancia.SetColor(
            "_HorizonColor",
            horizonActual
        ); // Aplicamos el color del horizonte

        materialInstancia.SetColor(
            "_HorizonFogColor",
            horizonFogActual
        ); // Aplicamos el color de la niebla del horizonte

        materialInstancia.SetColor(
            "_GroundColor",
            groundActual
        ); // Aplicamos el color del suelo del cielo

        if (progreso >= 1f) // Comprobamos si terminó la transición
        {
            materialInstancia.SetFloat(
                "_EnableStars",
                estrellasActivadasNoche ? 1f : 0f
            ); // Activamos las estrellas al llegar completamente a la noche
        }
    }
}