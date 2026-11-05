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

    private void Awake()
    {
        if (voicePriority == null)
            voicePriority =
                FindFirstObjectByType<VoicePriorityManager>();
    }

    private void OnEnable()
    {
        if (voicePriority != null)
            voicePriority.NarrationStarted += StopCurrentTransmission;
    }

    private void OnDisable()
    {
        if (voicePriority != null)
            voicePriority.NarrationStarted -= StopCurrentTransmission;
        StopCurrentTransmission();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        // El corte es inmediato por evento; esta comprobación también cubre
        // una reactivación del gestor durante un comando.
        if (voicePriority != null &&
            voicePriority.IsNarrationActive)
        {
            StopCurrentTransmission();
            return;
        }

        ReleaseFinishedTransmission();
    }

    public bool TryPlayNext(AudioSource speaker)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || speaker == null
            || !speaker.isActiveAndEnabled || transmissions == null
            || (voicePriority != null && voicePriority.IsNarrationActive))
            return false;

        ReleaseFinishedTransmission();
        if (currentSpeaker != null) return false;

        int index = nextTransmission;
        while (index < transmissions.Length && transmissions[index] == null) index++;
        if (index >= transmissions.Length) return false;

        // Reservar la prioridad antes de arrancar el audio, incluso si dos
        // postes reciben OnTriggerEnter dentro del mismo frame.
        if (voicePriority != null && !voicePriority.RegisterTransmission(speaker)) return false;

        speaker.Stop();
        speaker.clip = transmissions[index];
        speaker.loop = false;
        currentSpeaker = speaker;
        nextTransmission = index + 1;
        speaker.Play();
        Debug.Log("Transmision: " + speaker.clip.name);
        return true;
    }

    private void ReleaseFinishedTransmission()
    {
        if (currentSpeaker != null && currentSpeaker.isActiveAndEnabled && currentSpeaker.isPlaying) return;
        StopCurrentTransmission();
    }

    private void StopCurrentTransmission()
    {
        if (currentSpeaker != null) currentSpeaker.Stop();
        if (voicePriority != null) voicePriority.ClearTransmission(currentSpeaker);
        currentSpeaker = null;
    }
}
