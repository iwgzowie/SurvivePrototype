using UnityEngine;
using UnityEngine.Audio;

public class AdaptiveMusicAudio : MonoBehaviour
{
    [Header("Enemy Spawner")]
    [SerializeField] private EnemySpawner spawner;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sourceA;
    [SerializeField] private AudioSource sourceB;

    [Header("Musicas")]
    [SerializeField] private AudioClip introMusic;
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip[] waveMusic;
    [SerializeField] private AudioClip[] bossMusic;

    [Header("Mixer")]
    [SerializeField] private AudioMixerGroup musicGroup;

    [Header("Volumen")]
    [SerializeField, Range(0f, 1f)]
    private float musicVolume = 0.6f;

    [Header("Sincronizacion musical")]
    [SerializeField] private float bpm = 125f;
    [SerializeField] private int beatsPerBar = 4;
    [SerializeField] private int fadeBars = 2;

    private AudioSource active;
    private AudioSource inactive;

    private double activeStartDsp;
    private double transitionStartDsp;
    private double transitionEndDsp;
    private double introEndDsp;

    private bool transitioning;
    private bool introStarted;
    private bool introRunning;
    private bool introExitScheduled;
    private bool firstWaveHandled;
    private bool bossRequested;

    private AudioClip queuedClip;
    private bool queuedLoop;

    private int lastWave = -1;
    private int lastBoss = -1;

    private double BarDuration =>
        60.0 / Mathf.Max(1f, bpm) *
        Mathf.Max(1, beatsPerBar);

    private void Awake()
    {
        if (spawner == null)
            spawner = FindFirstObjectByType<EnemySpawner>();

        Configure(sourceA);
        Configure(sourceB);

        active = sourceA;
        inactive = sourceB;
    }

    private void Configure(AudioSource source)
    {
        if (source == null) return;

        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.pitch = 1f;

        if (musicGroup != null)
            source.outputAudioMixerGroup = musicGroup;
    }

    private void OnEnable()
    {
        if (spawner == null)
            spawner = FindFirstObjectByType<EnemySpawner>();

        if (spawner == null) return;

        spawner.OnWaveStarted += OnWaveStarted;
        spawner.OnWaveCompleted += OnWaveCompleted;
        spawner.OnBossWaveStarted += OnBossStarted;
        spawner.OnRestStarted += OnRestStarted;
    }

    private void OnDisable()
    {
        if (spawner == null) return;

        spawner.OnWaveStarted -= OnWaveStarted;
        spawner.OnWaveCompleted -= OnWaveCompleted;
        spawner.OnBossWaveStarted -= OnBossStarted;
        spawner.OnRestStarted -= OnRestStarted;
    }

    private void Start()
    {
        if (active == null || inactive == null)
        {
            Debug.LogError(
                "AdaptiveMusicAudio: Faltan AudioSources.");
            enabled = false;
            return;
        }

        active.clip = menuMusic;
        active.loop = true;
        active.volume = musicVolume;

        activeStartDsp = AudioSettings.dspTime + 0.1;

        if (menuMusic != null)
            active.PlayScheduled(activeStartDsp);
    }

    private double NextBar(double now)
    {
        double firstPossible = now + 0.15;

        if (active.clip == null)
            return firstPossible;

        double bars = System.Math.Ceiling(
            (firstPossible - activeStartDsp) / BarDuration
        );

        return activeStartDsp +
            System.Math.Max(0, bars) * BarDuration;
    }

    private void RequestMusic(
        AudioClip clip,
        bool loop = true)
    {
        if (clip == null) return;

        if (transitioning)
        {
            queuedClip = clip;
            queuedLoop = loop;
            return;
        }

        if (active.clip == clip && active.isPlaying)
            return;

        ScheduleTransition(
            clip,
            loop,
            NextBar(AudioSettings.dspTime),
            BarDuration * Mathf.Max(1, fadeBars)
        );
    }

    private void ScheduleTransition(
        AudioClip clip,
        bool loop,
        double startDsp,
        double fadeSeconds)
    {
        if (clip == null || active == null ||
            inactive == null)
            return;

        inactive.Stop();
        inactive.clip = clip;
        inactive.loop = loop;
        inactive.volume = 0f;
        inactive.PlayScheduled(startDsp);

        transitionStartDsp = startDsp;
        transitionEndDsp = startDsp +
            System.Math.Max(0.05, fadeSeconds);

        transitioning = true;

        if (clip == introMusic)
            introEndDsp = startDsp + introMusic.length;

        Debug.Log(
            "Transicion musical programada: " + clip.name);
    }

    private void Update()
    {
        double now = AudioSettings.dspTime;

        if (transitioning)
        {
            double duration =
                transitionEndDsp - transitionStartDsp;

            float t = Mathf.Clamp01(
                (float)((now - transitionStartDsp) /
                duration));

            active.volume = musicVolume * (1f - t);
            inactive.volume = musicVolume * t;

            if (now >= transitionEndDsp)
            {
                active.Stop();

                AudioSource old = active;
                active = inactive;
                inactive = old;

                activeStartDsp = transitionStartDsp;
                transitioning = false;

                if (queuedClip != null)
                {
                    AudioClip next = queuedClip;
                    bool loop = queuedLoop;
                    queuedClip = null;

                    RequestMusic(next, loop);
                }
            }
        }

        // La introduccion debe terminar una sola vez.
        if (introRunning && !introExitScheduled &&
            !transitioning &&
            now >= introEndDsp - 0.5)
        {
            introExitScheduled = true;
            introRunning = false;

            AudioClip next = bossRequested
                ? PickBoss()
                : PickWave();

            // La siguiente pista empieza cuando
            // termina la introduccion.
            ScheduleTransition(
                next,
                true,
                System.Math.Max(now + 0.1, introEndDsp),
                0.08
            );
        }
    }

    // Se llama desde el boton Play del personaje.
    public void StartIntro()
    {
        if (introStarted) return;

        introStarted = true;
        introRunning = true;

        if (introMusic == null)
        {
            introRunning = false;
            RequestMusic(PickWave());
            return;
        }

        RequestMusic(introMusic, false);
    }

    private AudioClip PickNoRepeat(
        AudioClip[] clips,
        ref int last)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int valid = 0;
        int alternatives = 0;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;

            valid++;

            if (i != last)
                alternatives++;
        }

        if (valid == 0) return null;

        bool avoidRepeat = alternatives > 0;

        int choice = Random.Range(
            0, avoidRepeat ? alternatives : valid);

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null ||
                (avoidRepeat && i == last))
                continue;

            if (choice-- == 0)
            {
                last = i;
                return clips[i];
            }
        }

        return null;
    }

    private AudioClip PickWave()
    {
        return PickNoRepeat(waveMusic, ref lastWave);
    }

    private AudioClip PickBoss()
    {
        return PickNoRepeat(bossMusic, ref lastBoss);
    }

    private void OnWaveStarted(
        int index,
        EnemyWave wave)
    {
        if (!firstWaveHandled)
        {
            firstWaveHandled = true;

            if (!introStarted)
                StartIntro();

            return;
        }

        if (introRunning || bossRequested)
            return;

        RequestMusic(PickWave());
    }

    private void OnBossStarted()
    {
        bossRequested = true;

        if (introRunning)
            return;

        RequestMusic(PickBoss());
    }

    private void OnWaveCompleted(int index)
    {
        bossRequested = false;

        if (!introRunning)
            RequestMusic(menuMusic);
    }

    private void OnRestStarted()
    {
        bossRequested = false;

        if (!introRunning)
            RequestMusic(menuMusic);
    }
}
