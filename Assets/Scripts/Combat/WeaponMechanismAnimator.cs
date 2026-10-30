using System.Collections.Generic;
using Survive.Items;
using UnityEngine;

namespace Survive.Combat
{
    /// <summary>Anima piezas del arma; la munición y la cadencia siguen perteneciendo a WeaponItem/controlador.</summary>
    [DefaultExecutionOrder(1005)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponItem))]
    [AddComponentMenu("Survive/Combate/Mecanismos del arma")]
    public sealed class WeaponMechanismAnimator : MonoBehaviour
    {
        [SerializeField] private WeaponItem weapon;
        [SerializeField] private AnimationClip fireClip;
        [SerializeField] private AnimationClip reloadClip;
        [SerializeField] private Transform[] animatedParts;

        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private PlayerWeaponController ownerController;
        private PlayerHealth ownerHealth;
        private bool cachedRest;
        private bool firing;
        private bool reloading;
        private float fireStartedAt;
        private float normalizedTime;

        public AnimationClip FireClip => fireClip;
        public AnimationClip ReloadClip => reloadClip;
        public IReadOnlyList<Transform> AnimatedParts => animatedParts;
        public bool IsFiring => firing;
        public bool IsReloading => reloading;
        public float NormalizedTime => normalizedTime;

        private void Awake()
        {
            weapon ??= GetComponent<WeaponItem>();
            CacheRestPose();
        }

        private void OnEnable()
        {
            weapon ??= GetComponent<WeaponItem>();
            CacheRestPose();
            ResolveOwner();
            weapon.ShotPresented += OnShot;
            weapon.StateChanged += OnWeaponState;
            ResetPresentation();
        }

        private void OnDisable()
        {
            if (weapon != null)
            {
                weapon.ShotPresented -= OnShot;
                weapon.StateChanged -= OnWeaponState;
            }
            ResetPresentation();
        }

        private void OnTransformParentChanged() => ResolveOwner();

        private void ResolveOwner()
        {
            ownerController = GetComponentInParent<PlayerWeaponController>();
            ownerHealth = GetComponentInParent<PlayerHealth>();
        }

        private void CacheRestPose()
        {
            if (cachedRest || animatedParts == null) return;
            restPositions = new Vector3[animatedParts.Length];
            restRotations = new Quaternion[animatedParts.Length];
            for (int i = 0; i < animatedParts.Length; i++)
            {
                if (animatedParts[i] == null) continue;
                restPositions[i] = animatedParts[i].localPosition;
                restRotations[i] = animatedParts[i].localRotation;
            }
            cachedRest = true;
        }

        private bool CanPresent => weapon != null && weapon.isActiveAndEnabled && weapon.IsEquipped
            && (ownerController == null || ownerController.isActiveAndEnabled)
            && (ownerHealth == null || !ownerHealth.IsDead);

        private void OnShot(WeaponItem ignored)
        {
            if (!CanPresent || weapon.IsReloading || fireClip == null) return;
            ResetPresentation();
            firing = true;
            fireStartedAt = Time.time;
            Sample(fireClip, 0f);
        }

        private void OnWeaponState(WeaponItem ignored)
        {
            if (weapon == null) return;
            if (weapon.IsReloading)
            {
                if (!reloading) ResetPresentation();
                reloading = true;
                firing = false;
                Sample(reloadClip, weapon.ReloadProgress);
            }
            else if (reloading) ResetPresentation();
        }

        private void LateUpdate()
        {
            if (!CanPresent) { ResetPresentation(); return; }
            if (Time.timeScale <= 0f) return;
            if (weapon.IsReloading)
            {
                if (!reloading) ResetPresentation();
                reloading = true;
                firing = false;
                Sample(reloadClip, weapon.ReloadProgress);
                return;
            }
            if (reloading) ResetPresentation();
            if (!firing || fireClip == null) return;
            // La presentación cabe entre tiros incluso al ajustar la cadencia.
            float duration = Mathf.Max(.02f, Mathf.Min(fireClip.length, .9f / weapon.ShotsPerSecond));
            float progress = (Time.time - fireStartedAt) / duration;
            if (progress >= 1f) { ResetPresentation(); return; }
            Sample(fireClip, progress);
        }

        private void Sample(AnimationClip clip, float progress)
        {
            if (clip == null) return;
            normalizedTime = Mathf.Clamp01(progress);
            clip.SampleAnimation(gameObject, normalizedTime * clip.length);
        }

        /// <summary>Vista previa explícita sin disparar, gastar munición ni iniciar una recarga.</summary>
        public void PreviewPose(bool reload, float progress)
        {
            CacheRestPose();
            ResetPresentation();
            Sample(reload ? reloadClip : fireClip, progress);
        }

        public void ResetPresentation()
        {
            CacheRestPose();
            if (cachedRest)
                for (int i = 0; i < animatedParts.Length; i++)
                {
                    if (animatedParts[i] == null) continue;
                    animatedParts[i].localPosition = restPositions[i];
                    animatedParts[i].localRotation = restRotations[i];
                }
            firing = reloading = false;
            normalizedTime = 0f;
        }
    }
}
