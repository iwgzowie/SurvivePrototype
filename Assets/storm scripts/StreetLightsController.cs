using UnityEngine; // Herramientas de Unity

public class StreetLightsController : MonoBehaviour // Controla las luces de las calles
{
    private Light[] luces; // Guarda todas las luces encontradas

    private void Awake() // Se ejecuta antes de comenzar el juego
    {
        luces = GetComponentsInChildren<Light>(true); // Busca las luces del objeto y sus hijos

        foreach (Light luz in luces) // Recorremos todas las luces encontradas
        {
            luz.enabled = false; // Apagamos las luces al comenzar
        }
    }

    public void ActivarLuces() // Enciende las luces de las calles
    {
        foreach (Light luz in luces) // Recorremos todas las luces encontradas
        {
            luz.enabled = true; // Encendemos cada luz
        }
    }
}