using UnityEngine;
using UnityEngine.Audio;
using Survive.Combat;
using Survive.Items;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class PlayerVoiceAudio : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerWeaponController weapons;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioMixerGroup voiceGroup;

    [Header("Voces")]
    [SerializeField] private AudioClip[] damageClips;
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip[] deathClips;

    [Header("Configuracion")]
    [SerializeField, Range(0f, 1f)]
    private float volume = 0.8f;

    [SerializeField]
    private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [SerializeField] private float damageCooldown = 0.4f;
    [SerializeField] private float attackCooldown = 3f;

    private AudioClip previousClip;
    private float lastDamageTime = -100f;
    private float lastAttackTime = -100f;
    private bool dead;

    private void Awake()
    {
        health ??= GetComponent<PlayerHealth>();
        weapons ??= GetComponent<PlayerWeaponController>();
        voiceSource ??= GetComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;

        if (voiceGroup != null)
            voiceSource.outputAudioMixerGroup = voiceGroup;
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnHealthChanged += OnHealthChanged;
            health.OnDeath += OnDeath;
            dead = health.IsDead;
        }

        if (weapons != null)
            weapons.ShotFired += OnAttack;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= OnHealthChanged;
            health.OnDeath -= OnDeath;
        }

        if (weapons != null)
            weapons.ShotFired -= OnAttack;
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (dead || current <= 0f)
            return;

        if (Time.time - lastDamageTime < damageCooldown)
            return;

        lastDamageTime = Time.time;
        PlayRandom(damageClips);
    }

    private void OnAttack(WeaponItem weapon)
    {
        if (dead || Time.timeScale <= 0f)
            return;

        if (Time.time - lastAttackTime < attackCooldown)
            return;

        lastAttackTime = Time.time;
        PlayRandom(attackClips);
    }

    private void OnDeath()
    {
        if (dead) return;

        dead = true;
        voiceSource.Stop();

        PlayRandom(deathClips);
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 ||
            voiceSource == null)
            return;

        int available = 0;

        foreach (AudioClip clip in clips)
            if (clip != null && clip != previousClip)
                available++;

        // Si solamente hay un clip valido, permitir repetirlo.
        bool avoidRepeat = available > 0;

        if (!avoidRepeat)
        {
            foreach (AudioClip clip in clips)
                if (clip != null) available++;
        }

        if (available == 0) return;

        int choice = Random.Range(0, available);

        foreach (AudioClip clip in clips)
        {
            if (clip == null ||
                (avoidRepeat && clip == previousClip))
                continue;

            if (choice-- != 0) continue;

            previousClip = clip;

            voiceSource.pitch = Random.Range(
                pitchRange.x, pitchRange.y);

            voiceSource.PlayOneShot(clip, volume);
            return;
        }
    }
}

