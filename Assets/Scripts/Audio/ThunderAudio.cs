using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class ThunderAudio : MonoBehaviour
{
    [Header("Truenos")]
    [SerializeField] private AudioClip[] thunderClips;

    [Header("Referencias")]
    [SerializeField] private AudioSource thunderSource;
    [SerializeField] private AudioMixerGroup ambienceGroup;

    [Header("Tormenta")]
    [SerializeField] private bool stormActive = false;
    [SerializeField] private float minDelay = 10f;
    [SerializeField] private float maxDelay = 25f;

    [Header("Variaciones")]
    [SerializeField, Range(0f, 1f)]
    private float minVolume = 0.45f;

    [SerializeField, Range(0f, 1f)]
    private float maxVolume = 0.85f;

    [SerializeField]
    private Vector2 pitchRange = new Vector2(0.85f, 1.05f);

    private AudioClip previousClip;
    private float nextThunderTime;
    private bool previousStormState;

    private void Awake()
    {
        thunderSource ??= GetComponent<AudioSource>();

        thunderSource.playOnAwake = false;
        thunderSource.loop = false;
        thunderSource.spatialBlend = 0f;

        if (ambienceGroup != null)
            thunderSource.outputAudioMixerGroup = ambienceGroup;
    }

    private void Start()
    {
        previousStormState = stormActive;
        ScheduleNextThunder();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        if (stormActive != previousStormState)
        {
            previousStormState = stormActive;

            if (stormActive)
                ScheduleNextThunder();
        }

        if (!stormActive)
            return;

        if (Time.time >= nextThunderTime)
        {
            PlayThunder();
            ScheduleNextThunder();
        }
    }

    private void ScheduleNextThunder()
    {
        float min = Mathf.Max(0.1f, minDelay);
        float max = Mathf.Max(min, maxDelay);

        nextThunderTime = Time.time + Random.Range(min, max);
    }

    public void SetStormActive(bool active)
    {
        stormActive = active;
        previousStormState = active;

        if (active)
            ScheduleNextThunder();
    }

    // Se puede llamar desde un evento de relampago.
    public void PlayThunder()
    {
        if (Time.timeScale <= 0f)
            return;

        if (thunderClips == null || thunderClips.Length == 0)
            return;

        int count = 0;

        foreach (AudioClip clip in thunderClips)
        {
            if (clip != null && clip != previousClip)
                count++;
        }

        bool avoidRepeat = count > 0;

        if (!avoidRepeat)
        {
            foreach (AudioClip clip in thunderClips)
                if (clip != null) count++;
        }

        if (count == 0)
            return;

        int selected = Random.Range(0, count);

        foreach (AudioClip clip in thunderClips)
        {
            if (clip == null ||
                (avoidRepeat && clip == previousClip))
                continue;

            if (selected-- != 0)
                continue;

            previousClip = clip;

            thunderSource.pitch = Random.Range(pitchRange.x, pitchRange.y);

            float volume = Random.Range(
                Mathf.Min(minVolume, maxVolume),
                Mathf.Max(minVolume, maxVolume));

            thunderSource.PlayOneShot(clip, volume);
            return;
        }
    }
}
