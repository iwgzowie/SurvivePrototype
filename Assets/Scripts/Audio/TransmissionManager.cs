using System.Collections.Generic;
using UnityEngine;

public class TransmissionManager : MonoBehaviour
{
    [Header("Transmisiones en orden")]
    [SerializeField] private AudioClip[] transmissions;

    [Header("Prioridades")]
    [SerializeField]
    private VoicePriorityManager voicePriority;

    private int nextTransmission;
    private AudioSource currentSpeaker;

    private readonly Queue<AudioSource> pendingSpeakers =
        new Queue<AudioSource>();

    private void Awake()
    {
        if (voicePriority == null)
            voicePriority =
                FindFirstObjectByType<VoicePriorityManager>();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        // Si habla un Recording, esperar.
        if (voicePriority != null &&
            voicePriority.IsNarrationActive)
            return;

        // Comprobar si termino la noticia actual.
        if (currentSpeaker != null)
        {
            if (currentSpeaker.isPlaying)
                return;

            if (voicePriority != null)
                voicePriority.ClearTransmission(currentSpeaker);

            currentSpeaker = null;
        }

        // Reproducir la siguiente noticia pendiente.
        while (pendingSpeakers.Count > 0)
        {
            AudioSource speaker = pendingSpeakers.Dequeue();

            if (speaker == null)
                continue;

            if (nextTransmission >= transmissions.Length)
                break;

            AudioClip clip = transmissions[nextTransmission];

            if (clip == null)
            {
                Debug.LogWarning(
                    "Falta Transmission " + nextTransmission);
                break;
            }

            speaker.Stop();
            speaker.clip = clip;
            speaker.loop = false;
            speaker.Play();

            currentSpeaker = speaker;
            nextTransmission++;

            if (voicePriority != null)
                voicePriority.RegisterTransmission(speaker);

            Debug.Log("Transmision: " + clip.name);
            break;
        }
    }

    public bool TryPlayNext(AudioSource speaker)
    {
        if (speaker == null ||
            transmissions == null)
            return false;

        // Evitar reservar mas parlantes que noticias.
        if (nextTransmission + pendingSpeakers.Count
            >= transmissions.Length)
            return false;

        if (speaker == currentSpeaker ||
            pendingSpeakers.Contains(speaker))
            return false;

        // Se agrega a la cola y espera su turno.
        pendingSpeakers.Enqueue(speaker);

        return true;
    }
}
