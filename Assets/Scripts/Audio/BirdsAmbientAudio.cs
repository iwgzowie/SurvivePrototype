using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class BirdsAmbientAudio : MonoBehaviour
{
    [Header("Sonidos de pajaros")]
    [SerializeField] private AudioClip[] birdClips;

    [Header("Tiempo entre sonidos")]
    [SerializeField] private float minDelay = 4f;
    [SerializeField] private float maxDelay = 12f;

    [Header("Variaciones")]
    [SerializeField]
    private Vector2 pitchRange =
        new Vector2(0.9f, 1.1f);

    [SerializeField, Range(0f, 1f)]
    private float volume = 0.5f;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixerGroup ambienceGroup;

    private AudioSource source;
    private AudioClip previousClip;
    private float nextBirdTime;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.minDistance = 3f;
        source.maxDistance = 25f;

        if (ambienceGroup != null)
            source.outputAudioMixerGroup = ambienceGroup;
    }

    private void Start()
    {
        ScheduleNext();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        if (Time.time >= nextBirdTime)
        {
            PlayBird();
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        nextBirdTime = Time.time + Random.Range(minDelay, maxDelay);
    }

    private void PlayBird()
    {
        if (birdClips == null || birdClips.Length == 0)
            return;

        AudioClip clip = birdClips[Random.Range(0, birdClips.Length)];

        if (clip == null)
            return;

        source.pitch = Random.Range(pitchRange.x, pitchRange.y);

        source.PlayOneShot(clip, volume);
        previousClip = clip;
    }
}
