using System;
using UnityEngine;
using UnityEngine.Audio;
using UPP.ThirdPersonController;

[RequireComponent(typeof(AudioSource))]
public class PlayerFootstepAudio : MonoBehaviour
{
    [Serializable] 
    public class SurfaceClips
    {
      public FootstepSurfaceType surface;
      public AudioClip[] walk;
      public AudioClip[] run;
      public AudioClip[] jump;
      public AudioClip[] roll;
    }

    [Header("Referencias")]
    [SerializeField] private UPPCharacterMovementComponent movement;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Superficies y sonidos")]
    [SerializeField] private SurfaceClips[] surfaces =
    {
        new SurfaceClips { surface = FootstepSurfaceType.Mud },
        new SurfaceClips { surface = FootstepSurfaceType.Tile },
        new SurfaceClips { surface = FootstepSurfaceType.Concrete }
    };

    [Header("Ritmo de pasos")]
    [SerializeField] private float walkInterval = 0.48f;
    [SerializeField] private float runInterval = 0.32f;
    [SerializeField] private float crouchInterval = 0.65f;
    [SerializeField] private float aimInterval = 0.85f;

    [Header("Variaciones")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [SerializeField]
    private Vector2 pitchRange = new Vector2(0.96f, 1.04f);

    [Header("Deteccion de suelo")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float rayStartHeight = 1f;
    [SerializeField] private float rayDistance = 2.5f;
    [SerializeField]
    private FootstepSurfaceType defaultSurface = FootstepSurfaceType.Concrete;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private AudioClip previousClip;
    private float stepTimer;
    private bool previousJump;
    private bool previousRoll;

    private void Awake()
    {
        movement ??= GetComponent<UPPCharacterMovementComponent>();
        health ??= GetComponent<PlayerHealth>();
        source ??= GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        if (sfxGroup != null)
            source.outputAudioMixerGroup = sfxGroup;
    }

    private void Update()
    {
        if (movement == null || Time.timeScale <= 0f ||
            !movement.isActiveAndEnabled ||
            movement.DisableAllMove ||
            (health != null && health.IsDead))
        {
            stepTimer = 0f;
            return;
        }

        bool jumping = movement.IsJumping;
        bool rolling = movement.IsRolling;

        FootstepSurfaceType surface = DetectSurface();
        SurfaceClips clips = GetSurfaceClips(surface);

        if (clips == null) return;

        // Sonidos de acciones: solo al comenzar.
        if (jumping && !previousJump)
            PlayRandom(clips.jump);

        if (rolling && !previousRoll)
            PlayRandom(clips.roll);

        previousJump = jumping;
        previousRoll = rolling;

        // Evita pasos normales en el aire o durante una rodada.
        if (!movement.IsGrounded || jumping || rolling ||
            movement.IsProne || !movement.IsMoving)
        {
            stepTimer = 0f;
            return;
        }

        AudioClip[] selected;
        float interval;

        if (movement.IsRunning)
        {
            selected = clips.run;
            interval = runInterval;
        }
        else
        {
            selected = clips.walk;

            interval = movement.IsAiming
                ? aimInterval
                : walkInterval;
        }

        stepTimer -= Time.deltaTime;

        if (stepTimer <= 0f)
        {
            PlayRandom(selected);
            stepTimer = Mathf.Max(0.1f, interval);
        }
    }

    private FootstepSurfaceType DetectSurface()
    {
        Vector3 origin = transform.position + Vector3.up * rayStartHeight;

        int count = Physics.RaycastNonAlloc(
            origin, Vector3.down, hits, rayDistance,
            groundLayers, QueryTriggerInteraction.Ignore);

        float closest = float.PositiveInfinity;
        FootstepSurfaceType result = defaultSurface;

        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i].collider;

            if (col == null || col.transform.IsChildOf(transform))
                continue;

            if (hits[i].distance >= closest)
                continue;

            closest = hits[i].distance;

            FootstepSurface marker =
                col.GetComponentInParent<FootstepSurface>();

            result = marker != null
                ? marker.surfaceType : defaultSurface;
        }

        return result;
    }

    private SurfaceClips GetSurfaceClips(FootstepSurfaceType type)
    {
        foreach (SurfaceClips entry in surfaces)
        {
            if (entry != null && entry.surface == type)
                return entry;
        }

        return null;
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || source == null)
            return;

        int valid = 0;
        int alternatives = 0;

        foreach (AudioClip clip in clips)
        {
            if (clip == null) continue;
            valid++;
            if (clip != previousClip) alternatives++;
        }

        if (valid == 0) return;

        bool avoidRepeat = alternatives > 0;
        int selection = UnityEngine.Random.Range(0, avoidRepeat ? alternatives : valid);

        foreach (AudioClip clip in clips)
        {
            if (clip == null ||
                (avoidRepeat && clip == previousClip))
                continue;

            if (selection-- != 0) continue;

            source.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);

            source.PlayOneShot(clip, volume);
            previousClip = clip;
            return;
        }
    }
}

