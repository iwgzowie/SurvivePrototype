using UnityEngine;
using UnityEngine.Audio;

public class PauseSnapshotAudio : MonoBehaviour
{
    [Header("Snapshots")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerSnapshot gameplaySnapshot;
    [SerializeField] private AudioMixerSnapshot pauseSnapshot;

    [Header("Transiciones")]
    [SerializeField] private float pauseFade = 0.4f;
    [SerializeField] private float resumeFade = 0.7f;

    private void Start()
    {
        if (mixer != null)
            mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;

        if (gameplaySnapshot != null)
            gameplaySnapshot.TransitionTo(0f);
    }

    public void OnGamePaused()
    {
        if (pauseSnapshot != null)
            pauseSnapshot.TransitionTo(pauseFade);
    }

    public void OnGameResumed()
    {
        if (gameplaySnapshot != null)
            gameplaySnapshot.TransitionTo(resumeFade);
    }
}
