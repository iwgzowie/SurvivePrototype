using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class TransmissionSpeaker : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TransmissionManager manager;
    [SerializeField] private AudioSource speakerSource;
    [SerializeField] private AudioMixerGroup voiceGroup;

    [Header("Audio 3D")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 20f;

    private bool alreadyPlayed;

    private void Awake()
    {
        if (speakerSource == null)
            speakerSource = GetComponent<AudioSource>();

        if (manager == null)
            manager = FindFirstObjectByType<TransmissionManager>();

        speakerSource.playOnAwake = false;
        speakerSource.loop = false;
        speakerSource.spatialBlend = 1f;

        speakerSource.rolloffMode =
            AudioRolloffMode.Logarithmic;

        speakerSource.minDistance = minDistance;
        speakerSource.maxDistance = maxDistance;

        if (voiceGroup != null)
            speakerSource.outputAudioMixerGroup = voiceGroup;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (alreadyPlayed)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (manager == null)
            return;

        // Sólo consumir el poste si comenzó a sonar. Si hay otro audio con
        // prioridad, queda disponible para intentarlo al volver a entrar.
        if (manager.TryPlayNext(speakerSource))
            alreadyPlayed = true;
    }
}
