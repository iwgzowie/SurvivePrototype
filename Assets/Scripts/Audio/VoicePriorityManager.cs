using UnityEngine;
using UnityEngine.Audio;

public class VoicePriorityManager : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mixer;

    [SerializeField]
    private string musicParameter = "MusicDuckVolume";

    [Header("Ducking")]
    [SerializeField] private float normalMusicDb = 0f;
    [SerializeField] private float duckedMusicDb = -12f;
    [SerializeField] private float fadeSpeed = 20f;

    private AudioSource transmissionSource;
    private bool narrationActive;
    private float currentMusicDb;

    public bool IsNarrationActive => narrationActive;

    private void Start()
    {
        currentMusicDb = normalMusicDb;
        ApplyVolume();
    }

    private void Update()
    {
        if (mixer == null)
            return;

        float target = narrationActive
            ? duckedMusicDb
            : normalMusicDb;

        currentMusicDb = Mathf.MoveTowards(
            currentMusicDb,
            target,
            fadeSpeed * Time.unscaledDeltaTime
        );

        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (mixer != null)
            mixer.SetFloat(musicParameter, currentMusicDb);
    }

    public void BeginNarration()
    {
        if (narrationActive)
            return;

        narrationActive = true;

        if (transmissionSource != null &&
            transmissionSource.isPlaying)
        {
            transmissionSource.Pause();
        }
    }

    public void EndNarration()
    {
        narrationActive = false;

        if (transmissionSource != null)
            transmissionSource.UnPause();
    }

    public void RegisterTransmission(AudioSource source)
    {
        transmissionSource = source;

        if (narrationActive && transmissionSource != null)
            transmissionSource.Pause();
    }

    public void ClearTransmission(AudioSource source)
    {
        if (transmissionSource == source)
            transmissionSource = null;
    }
}

