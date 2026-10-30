using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Items
{
    /// <summary>Un sonido real vinculado a una fase normalizada de la recarga.</summary>
    [Serializable]
    public sealed class ReloadAudioCue
    {
        [SerializeField, Range(0f, 1f)] private float normalizedTime;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        public float NormalizedTime => Mathf.Clamp01(normalizedTime);
        public AudioClip Clip => clip;
        public float Volume => Mathf.Clamp01(volume);
    }

    /// <summary>Configuración compartida del arma; la munición pertenece a cada pickup.</summary>
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Survive/Items/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Rifle";
        [SerializeField, Tooltip("Fotografía del modelo usada en la hotbar.")]
        private Sprite icon;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(0.1f)] private float range = 80f;
        [SerializeField, Min(0.1f)] private float shotsPerSecond = 5f;
        [SerializeField] private bool automatic = true;
        [SerializeField, Min(1)] private int magazineSize = 30;
        [SerializeField, Min(0)] private int startingReserveAmmo = 180;
        [SerializeField, Min(0.01f)] private float reloadDuration = 2f;
        [SerializeField, Min(1)] private int pelletsPerShot = 1;
        [SerializeField, Range(0f, 90f)] private float spreadDegrees;

        [Header("Respuesta del disparo")]
        [SerializeField, Range(0f, 8f)] private float recoilDegrees = 1.2f;
        [SerializeField, Range(0f, 0.1f)] private float recoilBackDistance = 0.025f;
        [SerializeField, Min(1f)] private float recoilRecoverySpeed = 14f;
        [Header("Audio del disparo")]
        [SerializeField, Tooltip("Disparo cercano de respaldo cuando no se asignan variantes.")]
        private AudioClip shotSound;
        [SerializeField, Tooltip("Variantes de la capa cercana OneShot del paquete de sonidos.")]
        private AudioClip[] shotVariations = Array.Empty<AudioClip>();
        [SerializeField, Tooltip("Variantes de la capa acústica Stop, reproducida junto al disparo.")]
        private AudioClip[] shotMechanismVariations = Array.Empty<AudioClip>();
        [SerializeField, Tooltip("Variantes de la cola Tail, reproducida junto al disparo cercano.")]
        private AudioClip[] shotTailVariations = Array.Empty<AudioClip>();
        [Header("Audio de recarga")]
        [Tooltip("Audio real de respaldo cuando no hay cues de recarga configurados.")]
        [SerializeField] private AudioClip reloadSound;
        [SerializeField, Tooltip("Sonidos reales por fase de la recarga; conservan su duración y afinación originales.")]
        private ReloadAudioCue[] reloadAudioCues = Array.Empty<ReloadAudioCue>();
        [SerializeField, Range(0f, 1f)] private float reloadVolume = .5f;

        [Header("Volumen del disparo")]
        [SerializeField, Range(0f, 1f)] private float shotVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float shotMechanismVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float shotTailVolume = 0.4f;

        [Header("Efectos del arma")]
        [SerializeField] private WeaponVfxProfile vfxProfile;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public float Damage => Mathf.Max(0f, damage);
        public float Range => Mathf.Max(0.1f, range);
        public float ShotsPerSecond => Mathf.Max(0.1f, shotsPerSecond);
        public bool Automatic => automatic;
        public int MagazineSize => Mathf.Max(1, magazineSize);
        public int StartingReserveAmmo => Mathf.Max(0, startingReserveAmmo);
        public float ReloadDuration => Mathf.Max(0.01f, reloadDuration);
        public int PelletsPerShot => Mathf.Clamp(pelletsPerShot, 1, 64);
        public float SpreadDegrees => Mathf.Clamp(spreadDegrees, 0f, 89f);
        public float RecoilDegrees => Mathf.Clamp(recoilDegrees, 0f, 8f);
        public float RecoilBackDistance => Mathf.Clamp(recoilBackDistance, 0f, 0.1f);
        public float RecoilRecoverySpeed => Mathf.Max(1f, recoilRecoverySpeed);
        public AudioClip ShotSound => shotSound;
        public IReadOnlyList<AudioClip> ShotVariations => shotVariations ?? Array.Empty<AudioClip>();
        public IReadOnlyList<AudioClip> ShotMechanismVariations => shotMechanismVariations ?? Array.Empty<AudioClip>();
        public IReadOnlyList<AudioClip> ShotTailVariations => shotTailVariations ?? Array.Empty<AudioClip>();
        public AudioClip ReloadSound => reloadSound;
        public IReadOnlyList<ReloadAudioCue> ReloadAudioCues => reloadAudioCues ?? Array.Empty<ReloadAudioCue>();
        public float ReloadVolume => Mathf.Clamp01(reloadVolume);
        public float ShotVolume => Mathf.Clamp01(shotVolume);
        public float ShotMechanismVolume => Mathf.Clamp01(shotMechanismVolume);
        public float ShotTailVolume => Mathf.Clamp01(shotTailVolume);
        public WeaponVfxProfile VfxProfile => vfxProfile;
    }
}
