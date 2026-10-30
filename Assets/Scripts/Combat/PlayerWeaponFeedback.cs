using System.Collections.Generic;
using Survive.Items;
using UnityEngine;

namespace Survive.Combat
{
    /// <summary>Presentación sonora por eventos; no administra munición ni cadencia.</summary>
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Survive/Combate/Audio de armas")]
    public sealed class PlayerWeaponFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponController weaponController;
        [SerializeField] private AudioSource shotSource;
        [SerializeField] private AudioSource reloadSource;

        private PlayerWeaponController subscribedController;
        private PlayerHealth subscribedHealth;
        private WeaponItem observedWeapon;
        private bool wasReloading;
        private bool pausedWithGame;
        private WeaponDefinition reloadDefinition;
        private bool usesReloadCues;
        private readonly HashSet<int> playedReloadCues = new HashSet<int>();
        private readonly System.Random audioRandom = new System.Random();
        private WeaponDefinition lastShotDefinition;
        private AudioClip lastNearClip;
        private AudioClip lastMechanismClip;
        private AudioClip lastTailClip;

        private void OnEnable()
        {
            Subscribe();
            RefreshWeapon(null);
        }

        private void Start()
        {
            Subscribe();
            RefreshWeapon(null);
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopAudio();
        }

        private void LateUpdate()
        {
            // El progreso pertenece al arma; los eventos de estado no se emiten en cada fase.
            RefreshWeapon(null);
        }

        private void SyncPauseState()
        {
            bool paused = Time.timeScale <= 0f;
            if (paused != pausedWithGame)
            {
                pausedWithGame = paused;
                if (paused)
                {
                    if (shotSource != null) shotSource.Pause();
                    if (reloadSource != null) reloadSource.Pause();
                }
                else
                {
                    if (subscribedHealth != null && subscribedHealth.IsDead)
                    {
                        StopAudio();
                        return;
                    }
                    if (shotSource != null) shotSource.UnPause();
                    if (reloadSource != null) reloadSource.UnPause();
                }
            }
        }

        private void Subscribe()
        {
            if (weaponController == null) weaponController = GetComponentInParent<PlayerWeaponController>();
            PlayerHealth health = weaponController != null ? weaponController.Health : null;
            if (health == null) health = GetComponentInParent<PlayerHealth>();
            if (subscribedController == weaponController && subscribedHealth == health) return;
            Unsubscribe();
            subscribedController = weaponController;
            subscribedHealth = health;
            if (subscribedController != null)
            {
                subscribedController.ShotFired += PlayShot;
                subscribedController.WeaponChanged += RefreshWeapon;
                subscribedController.WeaponStateChanged += RefreshWeapon;
            }
            if (subscribedHealth != null) subscribedHealth.OnDeath += StopAudio;
        }

        private void Unsubscribe()
        {
            if (subscribedController != null)
            {
                subscribedController.ShotFired -= PlayShot;
                subscribedController.WeaponChanged -= RefreshWeapon;
                subscribedController.WeaponStateChanged -= RefreshWeapon;
            }
            if (subscribedHealth != null) subscribedHealth.OnDeath -= StopAudio;
            subscribedController = null;
            subscribedHealth = null;
        }

        private void PlayShot(WeaponItem weapon)
        {
            if (shotSource == null || weapon == null || weapon.Definition == null
                || (subscribedHealth != null && subscribedHealth.IsDead)) return;

            WeaponDefinition definition = weapon.Definition;
            if (lastShotDefinition != definition)
            {
                lastShotDefinition = definition;
                lastNearClip = lastMechanismClip = lastTailClip = null;
            }
            // Las tres capas comparten el instante del tiro y conservan su afinación natural.
            // El azar de presentación es local: no modifica la dispersión de gameplay.
            shotSource.pitch = 1f;
            PlayShotLayer(definition.ShotVariations, definition.ShotSound, definition.ShotVolume, ref lastNearClip);
            PlayShotLayer(definition.ShotMechanismVariations, null, definition.ShotMechanismVolume, ref lastMechanismClip);
            PlayShotLayer(definition.ShotTailVariations, null, definition.ShotTailVolume, ref lastTailClip);
        }

        private void PlayShotLayer(IReadOnlyList<AudioClip> clips, AudioClip fallback, float volume, ref AudioClip previous)
        {
            int available = 0;
            int alternatives = 0;
            for (int index = 0; index < clips.Count; index++)
            {
                AudioClip candidate = clips[index];
                if (candidate == null) continue;
                available++;
                if (candidate != previous) alternatives++;
            }

            AudioClip selected = fallback;
            if (available > 0)
            {
                bool avoidPrevious = alternatives > 0;
                int choice = audioRandom.Next(avoidPrevious ? alternatives : available);
                for (int index = 0; index < clips.Count; index++)
                {
                    AudioClip candidate = clips[index];
                    if (candidate == null || (avoidPrevious && candidate == previous)) continue;
                    if (choice-- != 0) continue;
                    selected = candidate;
                    break;
                }
            }
            if (selected == null) return;
            previous = selected;
            shotSource.PlayOneShot(selected, volume);
        }

        private void RefreshWeapon(WeaponItem ignored)
        {
            SyncPauseState();
            WeaponItem current = weaponController != null && weaponController.isActiveAndEnabled
                && (subscribedHealth == null || !subscribedHealth.IsDead)
                ? weaponController.EquippedWeapon : null;
            if (current != null && !current.isActiveAndEnabled) current = null;
            if (current != observedWeapon)
            {
                StopReload();
                if (observedWeapon != null) observedWeapon.ReloadCompleted -= OnReloadCompleted;
                observedWeapon = current;
                if (observedWeapon != null) observedWeapon.ReloadCompleted += OnReloadCompleted;
            }
            bool reloading = current != null && current.isActiveAndEnabled && current.IsReloading;
            if (!reloading)
            {
                // Al completar se deja terminar la cola natural. Una cancelación corta la fuente.
                if (wasReloading) StopReload();
                return;
            }

            if (!wasReloading || reloadDefinition != current.Definition)
                BeginReload(current);
            if (!pausedWithGame && Time.timeScale > 0f)
                PlayReloadCues(current.ReloadProgress);
        }

        private void BeginReload(WeaponItem weapon)
        {
            StopReload();
            wasReloading = true;
            reloadDefinition = weapon.Definition;
            if (reloadDefinition == null) return;

            IReadOnlyList<ReloadAudioCue> cues = reloadDefinition.ReloadAudioCues;
            for (int index = 0; index < cues.Count; index++)
            {
                ReloadAudioCue cue = cues[index];
                if (cue == null || cue.Clip == null) continue;
                usesReloadCues = true;
                // Rehabilitar el componente a mitad de una recarga no repite fases anteriores.
                if (weapon.ReloadProgress > .0001f && cue.NormalizedTime <= weapon.ReloadProgress)
                    playedReloadCues.Add(index);
            }
            if (reloadSource != null)
            {
                reloadSource.clip = null;
                reloadSource.pitch = 1f;
                reloadSource.volume = 1f;
            }
            // El índice -1 representa el único clip legado; se evalúa también tras una pausa.
            if (weapon.ReloadProgress > .0001f) playedReloadCues.Add(-1);
        }

        private void PlayReloadCues(float progress)
        {
            if (reloadSource == null || reloadDefinition == null) return;
            reloadSource.pitch = 1f;
            reloadSource.volume = 1f;
            if (!usesReloadCues)
            {
                AudioClip fallback = reloadDefinition.ReloadSound;
                if (fallback != null && playedReloadCues.Add(-1))
                    reloadSource.PlayOneShot(fallback, reloadDefinition.ReloadVolume);
                return;
            }

            IReadOnlyList<ReloadAudioCue> cues = reloadDefinition.ReloadAudioCues;
            for (int index = 0; index < cues.Count; index++)
            {
                ReloadAudioCue cue = cues[index];
                if (cue == null || cue.Clip == null || cue.NormalizedTime > progress
                    || !playedReloadCues.Add(index)) continue;
                reloadSource.PlayOneShot(cue.Clip, reloadDefinition.ReloadVolume * cue.Volume);
            }
        }

        private void OnReloadCompleted(WeaponItem weapon)
        {
            if (weapon != observedWeapon || !wasReloading) return;
            if (!isActiveAndEnabled || weaponController == null || !weaponController.isActiveAndEnabled
                || weaponController.EquippedWeapon != weapon
                || (subscribedHealth != null && subscribedHealth.IsDead))
            {
                StopReload();
                return;
            }
            // Un frame largo puede saltar una fase. Solo el final real permite recuperar esos cues;
            // CancelReload nunca debe disparar los sonidos que todavía no se alcanzaron.
            // El controlador completa antes de este LateUpdate, también al salir de la pausa.
            SyncPauseState();
            if (Time.timeScale > 0f) PlayReloadCues(1f);
            ResetReloadTracking();
        }

        private void StopReload()
        {
            if (reloadSource != null) reloadSource.Stop();
            ResetReloadTracking();
        }

        private void ResetReloadTracking()
        {
            wasReloading = false;
            reloadDefinition = null;
            usesReloadCues = false;
            playedReloadCues.Clear();
        }

        private void StopAudio()
        {
            StopReload();
            if (shotSource != null) shotSource.Stop();
            if (observedWeapon != null) observedWeapon.ReloadCompleted -= OnReloadCompleted;
            observedWeapon = null;
        }
    }
}
