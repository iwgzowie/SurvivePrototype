using System;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class ZombieFootstepAudio : MonoBehaviour
{
    public enum EnemyFootstepType
    {
        ZombieWalk,
        ZombieRun,
        BossWalk
    }

    [Serializable]
    public class SurfaceSounds
    {
        public FootstepSurfaceType surface;
        public AudioClip[] clips;
    }

    [Header("Tipo de enemigo")]
    [SerializeField]
    private EnemyFootstepType footstepType = EnemyFootstepType.ZombieWalk;

    [Header("Referencias")]
    [SerializeField] private EnemyHealth health;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Superficies")]
    [SerializeField]
    private SurfaceSounds[] surfaces =
    {
        new SurfaceSounds
        {
            surface = FootstepSurfaceType.Concrete
        },
        new SurfaceSounds
        {
            surface = FootstepSurfaceType.Tile
        },
        new SurfaceSounds
        {
            surface = FootstepSurfaceType.Mud
        }
    };

    [Header("Configuracion")]
    [SerializeField, Range(0f, 1f)]
    private float volume = 0.75f;

    [SerializeField]
    private Vector2 pitchRange =
        new Vector2(0.93f, 1.07f);

    [Header("Audio 3D")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 18f;

    [Header("Deteccion de superficie")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [SerializeField] private float rayStartHeight = 1f;
    [SerializeField] private float rayDistance = 3f;

    [SerializeField]
    private FootstepSurfaceType defaultSurface =
        FootstepSurfaceType.Concrete;

    private readonly RaycastHit[] hits = new RaycastHit[32];

    private AudioClip previousClip;
    private Transform enemyRoot;
    private float lastStepTime = float.NegativeInfinity;

    public EnemyFootstepType Type => footstepType;
    public int PlayedSteps { get; private set; }
    public AudioClip LastClip => previousClip;
    public FootstepSurfaceType LastSurface { get; private set; }
    public AudioSource Source => footstepSource;

    private void Awake()
    {
        health ??= GetComponentInParent<EnemyHealth>();

        enemyRoot = health != null
            ? health.transform
            : transform.root;

        var animator = enemyRoot.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.gameObject != gameObject)
        {
            var relay = animator.GetComponent<ZombieFootstepAnimationEvents>();
            if (relay == null) relay = animator.gameObject.AddComponent<ZombieFootstepAnimationEvents>();
            relay.Bind(this);
        }

        // Usar una fuente exclusiva para footsteps.
        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
        }

        footstepSource.playOnAwake = false;
        footstepSource.loop = false;
        footstepSource.spatialBlend = 1f;
        footstepSource.rolloffMode =
            AudioRolloffMode.Logarithmic;

        footstepSource.minDistance = minDistance;
        footstepSource.maxDistance = maxDistance;

        if (sfxGroup != null)
        {
            footstepSource.outputAudioMixerGroup = sfxGroup;
        }
    }

    // Esta funcion la llama el Animation Event.
    public void Footstep()
    {
        if (!isActiveAndEnabled ||
            Time.timeScale <= 0f ||
            Time.time - lastStepTime < .08f ||
            (health != null && health.IsDead))
        {
            return;
        }

        FootstepSurfaceType surface = DetectSurface();
        LastSurface = surface;

        AudioClip[] clips = GetClips(surface);

        PlayRandom(clips);
    }

    private FootstepSurfaceType DetectSurface()
    {
        Vector3 origin = transform.position + Vector3.up * rayStartHeight;

        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            hits,
            rayDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearest = float.PositiveInfinity;
        FootstepSurfaceType detected = defaultSurface;

        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i].collider;

            if (col == null)
                continue;

            // Ignorar todos los colliders del enemigo.
            if (col.transform.IsChildOf(enemyRoot))
                continue;

            if (hits[i].distance >= nearest)
                continue;

            nearest = hits[i].distance;

            detected = FootstepSurface.Resolve(col, defaultSurface);
        }

        return detected;
    }

    private AudioClip[] GetClips(FootstepSurfaceType surface)
    {
        foreach (SurfaceSounds entry in surfaces)
        {
            if (entry != null && entry.surface == surface)
                return entry.clips;
        }

        return null;
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 ||
            footstepSource == null)
        {
            return;
        }

        int available = 0;
        int alternatives = 0;

        foreach (AudioClip clip in clips)
        {
            if (clip == null)
                continue;

            available++;

            if (clip != previousClip)
                alternatives++;
        }

        if (available == 0)
            return;

        bool avoidRepeat = alternatives > 0;

        int choice = UnityEngine.Random.Range(
            0,
            avoidRepeat ? alternatives : available
        );

        foreach (AudioClip clip in clips)
        {
            if (clip == null ||
                (avoidRepeat && clip == previousClip))
            {
                continue;
            }

            if (choice-- != 0)
                continue;

            previousClip = clip;

            footstepSource.pitch = UnityEngine.Random.Range(
                Mathf.Min(pitchRange.x, pitchRange.y),
                Mathf.Max(pitchRange.x, pitchRange.y)
            );

            footstepSource.PlayOneShot(clip, volume);
            lastStepTime = Time.time;
            PlayedSteps++;
            return;
        }
    }
}
