using System;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(AudioSource))]
public class ZombieVoiceAudio : MonoBehaviour
{
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
    [SerializeField, Range(0f, 1f)]
    private float volume = 0.75f;

    [SerializeField]
    private Vector2 pitchRange =
        new Vector2(0.9f, 1.1f);

    [Header("Audio 3D")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 25f;

    private AudioClip previousClip;
    private float nextIdleTime;
    private bool dead;
    private bool wasAttacking;
    private bool subscribed;

    private void Awake()
    {
        health ??= GetComponent<EnemyHealth>();
        animator ??= GetComponentInChildren<Animator>();
        voiceSource ??= GetComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 1f;
        voiceSource.rolloffMode = AudioRolloffMode.Logarithmic;
        voiceSource.minDistance = minDistance;
        voiceSource.maxDistance = maxDistance;

        if (voiceGroup != null)
            voiceSource.outputAudioMixerGroup = voiceGroup;
    }

    private void OnEnable()
    {
        if (health == null)
            health = GetComponent<EnemyHealth>();

        if (health != null && !subscribed)
        {
            health.OnHealthChanged += OnDamage;
            health.OnDeath += OnDeath;
            subscribed = true;
        }

        dead = health != null && health.IsDead;
        wasAttacking = false;
        ScheduleIdle();
    }

    private void OnDisable()
    {
        if (health != null && subscribed)
        {
            health.OnHealthChanged -= OnDamage;
            health.OnDeath -= OnDeath;
            subscribed = false;
        }
    }

    private void Update()
    {
        if (dead || Time.timeScale <= 0f)
            return;

        // Detectar la entrada a una animacion de ataque.
        bool attacking = IsAttackAnimationPlaying();

        if (attacking && !wasAttacking)
        {
            PlayRandom(attackClips);
            ScheduleIdle();
        }

        wasAttacking = attacking;

        // Gruñidos ocasionales cuando no ataca.
        if (!attacking && Time.time >= nextIdleTime)
        {
            PlayRandom(idleClips);
            ScheduleIdle();
        }
    }

    private bool IsAttackAnimationPlaying()
    {
        if (animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null)
            return false;

        AnimatorStateInfo current =
            animator.GetCurrentAnimatorStateInfo(0);

        if (current.IsTag("Attack"))
            return true;

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next =
                animator.GetNextAnimatorStateInfo(0);

            if (next.IsTag("Attack"))
                return true;
        }

        return false;
    }

    private void OnDamage(float current, float maximum)
    {
        if (dead || current <= 0f)
            return;

        PlayRandom(damageClips);
        ScheduleIdle();
    }

    private void OnDeath()
    {
        if (dead)
            return;

        dead = true;

        // Evitar que el gruñido anterior tape la muerte.
        voiceSource.Stop();

        AudioClip clip = ChooseRandom(deathClips);
        if (clip == null)
            return;

        // La voz puede terminar aunque destruyan al zombi.
        GameObject audioObject = new GameObject("ZombieDeathAudio");
        audioObject.transform.position = transform.position;

        AudioSource deathSource =
            audioObject.AddComponent<AudioSource>();

        deathSource.playOnAwake = false;
        deathSource.spatialBlend = 1f;
        deathSource.rolloffMode = AudioRolloffMode.Logarithmic;
        deathSource.minDistance = minDistance;
        deathSource.maxDistance = maxDistance;
        deathSource.outputAudioMixerGroup =
            voiceSource.outputAudioMixerGroup;

        deathSource.pitch = UnityEngine.Random.Range(
            pitchRange.x, pitchRange.y);

        deathSource.PlayOneShot(clip, volume);

        Destroy(audioObject,
            clip.length / Mathf.Max(0.01f, deathSource.pitch) + 0.2f);
    }

    private void ScheduleIdle()
    {
        float minimum = Mathf.Max(0.1f, minIdleDelay);
        float maximum = Mathf.Max(minimum, maxIdleDelay);

        nextIdleTime = Time.time +
            UnityEngine.Random.Range(minimum, maximum);
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (voiceSource == null)
            return;

        AudioClip clip = ChooseRandom(clips);

        if (clip == null)
            return;

        voiceSource.pitch = UnityEngine.Random.Range(
            pitchRange.x, pitchRange.y);

        voiceSource.PlayOneShot(clip, volume);
    }

    private AudioClip ChooseRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int count = 0;
        int alternatives = 0;

        foreach (AudioClip clip in clips)
        {
            if (clip == null)
                continue;

            count++;

            if (clip != previousClip)
                alternatives++;
        }

        if (count == 0)
            return null;

        bool avoidRepeat = alternatives > 0;

        int selected = UnityEngine.Random.Range(
            0, avoidRepeat ? alternatives : count);

        foreach (AudioClip clip in clips)
        {
            if (clip == null ||
                (avoidRepeat && clip == previousClip))
                continue;

            if (selected-- != 0)
                continue;

            previousClip = clip;
            return clip;
        }

        return null;
    }
}
