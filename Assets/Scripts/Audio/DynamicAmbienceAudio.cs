using UnityEngine;
using UnityEngine.Audio;
using Survive.World;

[DisallowMultipleComponent]
public class DynamicAmbienceAudio : MonoBehaviour
{
    [Header("Sistema de dia y noche")]
    [SerializeField] private CityAtmosphere atmosphere;

    [Header("AudioSources")]
    [SerializeField] private AudioSource daySource;
    [SerializeField] private AudioSource nightSource;
    [SerializeField] private AudioSource rainSource;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixerGroup ambienceGroup;

    [Header("Volumenes")]
    [SerializeField, Range(0f, 1f)]
    private float dayVolume = 0.6f;

    [SerializeField, Range(0f, 1f)]
    private float nightVolume = 0.7f;

    [SerializeField, Range(0f, 1f)]
    private float rainVolume = 0.7f;

    [Header("Transiciones")]
    [SerializeField, Min(0.01f)]
    private float fadeSpeed = 0.5f;

    [Header("Lluvia")]
    [SerializeField] private bool raining;

    private float currentDay;
    private float currentNight;
    private float currentRain;

    private void Start()
    {
        Configure(daySource);
        Configure(nightSource);
        Configure(rainSource);

        float night = atmosphere != null
            ? Mathf.Clamp01(atmosphere.NightBlend)
            : 0f;

        currentDay = dayVolume * (1f - night);
        currentNight = nightVolume * night;
        currentRain = raining ? rainVolume : 0f;

        InitializeSource(daySource, currentDay);
        InitializeSource(nightSource, currentNight);
        InitializeSource(rainSource, currentRain);
    }

    private void Configure(AudioSource source)
    {
        if (source == null) return;

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;

        if (ambienceGroup != null)
            source.outputAudioMixerGroup = ambienceGroup;
    }

    private void InitializeSource(AudioSource source, float volume)
    {
        if (source == null) return;

        source.volume = volume;

        if (source.clip != null && !source.isPlaying)
            source.Play();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        float night = atmosphere != null
            ? Mathf.Clamp01(atmosphere.NightBlend)
            : 0f;

        float targetDay = dayVolume * (1f - night);
        float targetNight = nightVolume * night;
        float targetRain = raining ? rainVolume : 0f;

        float step = fadeSpeed * Time.deltaTime;

        currentDay = Mathf.MoveTowards(
            currentDay, targetDay, step);

        currentNight = Mathf.MoveTowards(
            currentNight, targetNight, step);

        currentRain = Mathf.MoveTowards(
            currentRain, targetRain, step);

        ApplyVolume(daySource, currentDay);
        ApplyVolume(nightSource, currentNight);
        ApplyVolume(rainSource, currentRain);
    }

    private void ApplyVolume(AudioSource source, float volume)
    {
        if (source == null) return;

        source.volume = volume;
    }

    public void SetRainEnabled(bool enabled)
    {
        raining = enabled;
    }

    public void SetRainIntensity(float intensity)
    {
        rainVolume = Mathf.Clamp01(intensity);
        raining = intensity > 0f;
    }
}

