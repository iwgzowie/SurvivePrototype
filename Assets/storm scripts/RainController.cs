using UnityEngine; // Herramientas de Unity

public class RainController : MonoBehaviour // Controla el sistema de partículas de lluvia
{
    [SerializeField] private ParticleSystem lluvia; // Partículas Rain asignadas desde el Inspector

    public void ActivarLluvia() // Método que inicia la lluvia
    {
        if (lluvia == null) // Comprobamos que Rain esté asignado
        {
            return; // Si no está asignado, salimos del método
        }

        lluvia.gameObject.SetActive(true); // Nos aseguramos de que el objeto Rain esté activo
        lluvia.Play(); // Iniciamos las partículas de lluvia
    }
}