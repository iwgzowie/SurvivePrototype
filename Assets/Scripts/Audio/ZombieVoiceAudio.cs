using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(AudioSource))]
public class ZombieVoiceAudio : MonoBehaviour
{
    public const int MaxSimultaneousVoices = 3;
    public const int MaxVoicesPerCluster = 2;
    public const float ClusterRadius = 8f;
    public static int ActiveVoiceCount => VoiceBudget.Count;
    public AudioSource Source => voiceSource;
    public int PlayedVoices { get; private set; }

    [Header("Referencias")]
    [SerializeField] private EnemyHealth health;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioMixerGroup voiceGroup;
    [Header("Voces del zombi")]
    [SerializeField] private AudioClip[] idleClips;
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip[] damageClips;
    [SerializeField] private AudioClip[] deathClips;
    [Header("Frecuencia de gruñidos")]
    [SerializeField] private float minIdleDelay = 5f;
    [SerializeField] private float maxIdleDelay = 12f;
    [Header("Variaciones")]
    [SerializeField, Range(0f, 1f)] private float volume = .75f;
    [SerializeField] private Vector2 pitchRange = new Vector2(.9f, 1.1f);
    [Header("Audio 3D")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 25f;

    AudioClip previousClip;
    float nextIdleTime, sourceVolume = 1f;
    bool dead, wasAttacking, subscribed;
    enum VoiceKind { Idle, Attack, Damage, Death }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetBudget() => VoiceBudget.Reset();

    void Awake()
    {
        health ??= GetComponent<EnemyHealth>();
        animator ??= GetComponentInChildren<Animator>();
        voiceSource ??= GetComponent<AudioSource>();
        if (voiceSource != null) sourceVolume = voiceSource.volume;
        // Los prefabs recibidos compartían esta fuente con los pasos. Detener una voz no debe silenciarlos.
        if (voiceSource == null || voiceSource.gameObject == gameObject)
        {
            var original = voiceSource;
            var child = new GameObject("ZombieVoice");child.transform.SetParent(transform, false);
            voiceSource = child.AddComponent<AudioSource>();
            if (original != null) { voiceSource.mute = original.mute; voiceSource.outputAudioMixerGroup = original.outputAudioMixerGroup; }
        }
        Configure(voiceSource);
    }

    void Configure(AudioSource source)
    {
        source.playOnAwake = false;source.loop = false;source.spatialBlend = 1;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;source.maxDistance = maxDistance;
        if (voiceGroup != null) source.outputAudioMixerGroup = voiceGroup;
    }

    void OnEnable()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (health != null && !subscribed)
        { health.OnHealthChanged += OnDamage;health.OnDeath += OnDeath;subscribed = true; }
        dead = health != null && health.IsDead;wasAttacking = false;ScheduleIdle();
    }
    void OnDisable()
    {
        if (health != null && subscribed)
        { health.OnHealthChanged -= OnDamage;health.OnDeath -= OnDeath;subscribed = false; }
        VoiceBudget.Release(voiceSource);
    }
    void Update()
    {
        if (dead || Time.timeScale <= 0) return;
        bool attacking = IsAttackAnimationPlaying();
        if (attacking && !wasAttacking) { PlayRandom(attackClips, VoiceKind.Attack);ScheduleIdle(); }
        wasAttacking = attacking;
        if (!attacking && Time.time >= nextIdleTime) { PlayRandom(idleClips, VoiceKind.Idle);ScheduleIdle(); }
    }
    bool IsAttackAnimationPlaying()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")
            || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Attack");
    }
    void OnDamage(float current, float maximum)
    {
        if (dead || current <= 0) return;
        PlayRandom(damageClips, VoiceKind.Damage);ScheduleIdle();
    }
    void OnDeath()
    {
        if (dead) return;
        dead = true;VoiceBudget.Release(voiceSource);
        AudioClip clip = ChooseRandom(deathClips);
        if (clip == null || Time.timeScale <= 0) return;
        var audioObject = new GameObject("ZombieDeathAudio");audioObject.transform.position = transform.position;
        var source = audioObject.AddComponent<AudioSource>();Configure(source);
        source.outputAudioMixerGroup = voiceSource.outputAudioMixerGroup;
        float pitch = RandomPitch();
        if (!VoiceBudget.TryPlay(source, clip, pitch, volume * sourceVolume, VoiceKind.Death)) { Destroy(audioObject);return; }
        PlayedVoices++;Destroy(audioObject, clip.length / pitch + .2f);
    }
    void ScheduleIdle()
    {
        float minimum = Mathf.Max(.1f, minIdleDelay);
        nextIdleTime = Time.time + UnityEngine.Random.Range(minimum, Mathf.Max(minimum, maxIdleDelay));
    }
    void PlayRandom(AudioClip[] clips, VoiceKind kind)
    {
        if (!isActiveAndEnabled || dead || Time.timeScale <= 0 || voiceSource == null) return;
        var clip = ChooseRandom(clips);
        if (clip != null && VoiceBudget.TryPlay(voiceSource, clip, RandomPitch(), volume * sourceVolume, kind)) PlayedVoices++;
    }
    float RandomPitch() => Mathf.Max(.01f, UnityEngine.Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y)));
    AudioClip ChooseRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        int count = 0, alternatives = 0;
        foreach (var clip in clips) if (clip != null) { count++;if (clip != previousClip) alternatives++; }
        if (count == 0) return null;
        bool avoidRepeat = alternatives > 0;int selected = UnityEngine.Random.Range(0, avoidRepeat ? alternatives : count);
        foreach (var clip in clips)
        {
            if (clip == null || avoidRepeat && clip == previousClip) continue;
            if (selected-- != 0) continue;
            previousClip = clip;return clip;
        }
        return null;
    }

    static class VoiceBudget
    {
        sealed class Slot { public AudioSource source;public VoiceKind kind;public double start,end; }
        static readonly List<Slot> slots = new List<Slot>();
        static AudioListener listener;
        static double nextIdle;
        public static int Count { get { Prune();return slots.Count; } }
        public static void Reset() { slots.Clear();listener = null;nextIdle = 0; }
        static AudioListener Listener()
        {
            if (listener != null && listener.isActiveAndEnabled) return listener;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled) { listener = candidate;return listener; }
            return null;
        }
        static bool Audible(AudioSource source)
        {
            var target = Listener();
            return target != null && (source.transform.position - target.transform.position).sqrMagnitude <= source.maxDistance * source.maxDistance;
        }
        static void Remove(int index)
        {
            if (slots[index].source != null) slots[index].source.Stop();slots.RemoveAt(index);
        }
        static void Prune()
        {
            double now = AudioSettings.dspTime;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (slot.source == null || !slot.source.isActiveAndEnabled || now >= slot.end
                    || !slot.source.isPlaying && now > slot.start + .1 || !Audible(slot.source)) Remove(i);
            }
        }
        public static void Release(AudioSource source)
        {
            for (int i = slots.Count - 1; i >= 0; i--) if (slots[i].source == source) Remove(i);
        }
        static int Victim(VoiceKind kind, Vector3 position, bool local)
        {
            int result = -1;var target = Listener();if (target == null) return -1;
            float requestDistance = Vector3.Distance(position, target.transform.position);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (local && (slot.source.transform.position - position).sqrMagnitude > ClusterRadius * ClusterRadius) continue;
                if (slot.kind > kind || slot.kind == kind && Vector3.Distance(slot.source.transform.position, target.transform.position) <= requestDistance + 2f) continue;
                if (result < 0 || slot.kind < slots[result].kind
                    || slot.kind == slots[result].kind && Vector3.Distance(slot.source.transform.position, target.transform.position) > Vector3.Distance(slots[result].source.transform.position, target.transform.position)) result = i;
            }
            return result;
        }
        public static bool TryPlay(AudioSource source, AudioClip clip, float pitch, float volume, VoiceKind kind)
        {
            Prune();
            if (source == null || clip == null || !source.isActiveAndEnabled || Time.timeScale <= 0 || !Audible(source)) return false;
            for (int i = 0; i < slots.Count; i++) if (slots[i].source == source)
            { if (kind <= slots[i].kind) return false;Remove(i);break; }
            double now = AudioSettings.dspTime;
            if (kind == VoiceKind.Idle)
            { if (now < nextIdle) return false;foreach (var slot in slots) if (slot.kind == VoiceKind.Idle) return false; }
            int nearby = 0;Vector3 position = source.transform.position;
            foreach (var slot in slots) if ((slot.source.transform.position - position).sqrMagnitude <= ClusterRadius * ClusterRadius) nearby++;
            if (nearby >= MaxVoicesPerCluster)
            { int victim = Victim(kind, position, true);if (victim < 0) return false;Remove(victim); }
            if (slots.Count >= MaxSimultaneousVoices)
            { int victim = Victim(kind, position, false);if (victim < 0) return false;Remove(victim); }
            source.Stop();source.clip = clip;source.pitch = pitch;source.volume = volume;source.Play();
            slots.Add(new Slot { source = source, kind = kind, start = now, end = now + clip.length / pitch + .05 });
            if (kind == VoiceKind.Idle) nextIdle = now + .75;
            return true;
        }
    }
}
