using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class TutorialCommandsAudio : MonoBehaviour
{
    [Header("Comandos en orden")]
    [SerializeField] private AudioClip[] commands;

    [Header("Referencias")]
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioMixerGroup voiceGroup;
    [SerializeField] private EnemySpawner spawner;

    [Header("Prioridades")]
    [SerializeField]
    private VoicePriorityManager voicePriority;

    [Header("Configuracion")]
    [SerializeField, Range(0f, 1f)]
    private float volume = 0.85f;

    [SerializeField]
    private bool playFirstOnStart = false;

    private int nextCommand;
    private int pendingCommands;
    private bool narrationPlaying;

    private void Awake()
    {
        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();

        if (voicePriority == null)
            voicePriority =
                FindFirstObjectByType<VoicePriorityManager>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.volume = volume;

        if (voiceGroup != null)
            voiceSource.outputAudioMixerGroup = voiceGroup;
    }

    private void OnEnable()
    {
        if (spawner == null)
            spawner = FindFirstObjectByType<EnemySpawner>();

        if (spawner != null)
            spawner.OnWaveCompleted += OnWaveCompleted;
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.OnWaveCompleted -= OnWaveCompleted;

        // Deshabilitar el componente no detiene su AudioSource automáticamente.
        // Primero cortar el comando y después liberar la prioridad.
        if (voiceSource != null) voiceSource.Stop();
        if (narrationPlaying && voicePriority != null)
            voicePriority.EndNarration();

        narrationPlaying = false;
    }

    private void Start()
    {
        if (playFirstOnStart)
            PlayNextCommand();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        // La grabacion actual termino.
        if (narrationPlaying &&
            !voiceSource.isPlaying)
        {
            narrationPlaying = false;

            if (voicePriority != null)
                voicePriority.EndNarration();
        }

        // Reproducir el siguiente audio pendiente.
        if (!narrationPlaying && pendingCommands > 0)
            PlayQueuedCommand();
    }

    public void PlayNextCommand()
    {
        if (!isActiveAndEnabled || commands == null)
            return;

        if (nextCommand + pendingCommands >= commands.Length)
            return;

        pendingCommands++;

        if (!narrationPlaying &&
            Time.timeScale > 0f)
        {
            PlayQueuedCommand();
        }
    }

    private void PlayQueuedCommand()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || voiceSource == null
            || pendingCommands <= 0 ||
            nextCommand >= commands.Length)
            return;

        pendingCommands--;

        AudioClip clip = commands[nextCommand];
        nextCommand++;

        if (clip == null)
        {
            Debug.LogWarning(
                "Falta configurar un Recording.");
            return;
        }

        // Prioridad: primero el Recording.
        if (voicePriority != null)
            voicePriority.BeginNarration();

        voiceSource.clip = clip;
        voiceSource.Play();

        narrationPlaying = true;

        Debug.Log("Recording: " + clip.name);
    }

    private void OnWaveCompleted(int waveIndex)
    {
        PlayNextCommand();
    }
}
