using UnityEngine;

public class RainController : MonoBehaviour
{
    [SerializeField] private ParticleSystem lluvia;
    [Header("Audio de la tormenta")]
    [SerializeField] private ThunderAudio thunderAudio;
    [SerializeField] private DynamicAmbienceAudio ambienceAudio;

    // Al detener la emisión, las gotas existentes pueden seguir vivas unos segundos.
    public bool IsRaining => lluvia != null && lluvia.gameObject.activeInHierarchy && lluvia.isEmitting;

    private void OnEnable() => SincronizarAudio(IsRaining);
    private void Start() => SincronizarAudio(IsRaining);
    private void OnDisable() => SincronizarAudio(false);

    public void ActivarLluvia()
    {
        if (lluvia == null) return;

        lluvia.gameObject.SetActive(true);
        if (!lluvia.isEmitting) lluvia.Play();
        SincronizarAudio(true);
    }

    public void DesactivarLluvia()
    {
        if (lluvia != null)
            lluvia.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        SincronizarAudio(false);
    }

    private void SincronizarAudio(bool activa)
    {
        // Respaldo para escenas que todavía no tengan las referencias asignadas.
        if (thunderAudio == null || ambienceAudio == null)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (thunderAudio == null) thunderAudio = root.GetComponentInChildren<ThunderAudio>(true);
                if (ambienceAudio == null) ambienceAudio = root.GetComponentInChildren<DynamicAmbienceAudio>(true);
                if (thunderAudio != null && ambienceAudio != null) break;
            }
        }

        if (ambienceAudio != null) ambienceAudio.SetRainEnabled(activa);
        if (thunderAudio != null) thunderAudio.SetStormActive(activa);
    }
}
