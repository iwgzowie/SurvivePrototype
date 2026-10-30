using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survive.Items
{
    /// <summary>Arma recogible. El jugador administra apuntado, cadencia y daño.</summary>
    [AddComponentMenu("Survive/Items/Weapon Item")]
    public sealed class WeaponItem : MasterItem
    {
        [Header("Preset compartido")]
        [Tooltip("Si no se asigna, conserva los valores locales de los prefabs existentes.")]
        [SerializeField] private WeaponDefinition definition;

        [Header("Disparo")]
        [Tooltip("Salida del proyectil visual; el eje local +Z apunta hacia adelante.")]
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(0.1f)] private float range = 80f;
        [SerializeField, Min(0.1f)] private float shotsPerSecond = 5f;
        [SerializeField] private bool automatic = true;

        [Header("Munición local sin preset")]
        [SerializeField, Min(1)] private int magazineSize = 30;
        [SerializeField, Min(0)] private int startingReserveAmmo = 180;
        [SerializeField, Min(0.01f)] private float reloadDuration = 2f;
        [SerializeField, Min(1)] private int pelletsPerShot = 1;
        [SerializeField, Range(0f, 90f)] private float spreadDegrees;

        [Header("Pose respecto del socket del jugador")]
        [SerializeField] private Vector3 equippedLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 equippedLocalEulerAngles = Vector3.zero;
        [Tooltip("Objetivo de la muñeca izquierda en el espacio del arma; el IK mantiene el apoyo sobre el guardamanos.")]
        [SerializeField] private Transform leftHandGrip;
        [SerializeField] private bool twoHanded = true;

        [Header("Presentación del disparo")]
        [Tooltip("Material incluido en el prefab para que el trazador también funcione en la build.")]
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private Color tracerColor = new Color(1f, 0.8f, 0.25f, 1f);
        [SerializeField, Min(0.001f)] private float tracerWidth = 0.015f;
        [SerializeField, Min(0.01f)] private float tracerLifetime = 0.06f;
        [Tooltip("Objeto visual opcional bajo Muzzle. No debe ser el objeto raíz del arma.")]
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Min(0.01f)] private float muzzleFlashLifetime = 0.045f;

        private const int MaximumTracers = 64;
        private LineRenderer[] shotTracers = Array.Empty<LineRenderer>();
        private float tracerEndsAt;
        private float flashEndsAt;
        private bool ammunitionInitialized;
        private int ammoInMagazine;
        private int reserveAmmo;
        private bool isReloading;
        private float reloadElapsed;

        public event Action<WeaponItem> StateChanged;
        /// <summary>Final real de la recarga, antes de notificar el nuevo estado de munición.</summary>
        public event Action<WeaponItem> ReloadCompleted;
        /// <summary>Un aviso visual por tiro, aunque el disparo tenga varios perdigones.</summary>
        public event Action<WeaponItem> ShotPresented;
        /// <summary>Impacto geométrico real; no interpreta el indicador visual de disparo bloqueado.</summary>
        public event Action<WeaponItem, RaycastHit> ImpactPresented;

        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public WeaponDefinition Definition => definition;
        public string DisplayName => definition != null ? definition.DisplayName : name;
        public float Damage => definition != null ? definition.Damage : Mathf.Max(0f, damage);
        public float Range => definition != null ? definition.Range : Mathf.Max(0.1f, range);
        public float ShotsPerSecond => definition != null ? definition.ShotsPerSecond : Mathf.Max(0.1f, shotsPerSecond);
        public bool Automatic => definition != null ? definition.Automatic : automatic;
        public int MagazineSize => definition != null ? definition.MagazineSize : Mathf.Max(1, magazineSize);
        public float ReloadDuration => definition != null ? definition.ReloadDuration : Mathf.Max(0.01f, reloadDuration);
        public int PelletsPerShot => definition != null ? definition.PelletsPerShot : Mathf.Clamp(pelletsPerShot, 1, 64);
        public float SpreadDegrees => definition != null ? definition.SpreadDegrees : Mathf.Clamp(spreadDegrees, 0f, 89f);
        public int AmmoInMagazine
        {
            get
            {
                EnsureAmmunitionInitialized();
                return ammoInMagazine;
            }
        }
        public int ReserveAmmo
        {
            get
            {
                EnsureAmmunitionInitialized();
                return reserveAmmo;
            }
        }
        public bool HasAmmo => AmmoInMagazine > 0;
        public bool IsReloading => isReloading;
        public float ReloadProgress => isReloading ? Mathf.Clamp01(reloadElapsed / ReloadDuration) : 0f;
        public Vector3 EquippedLocalPosition => equippedLocalPosition;
        public Vector3 EquippedLocalEulerAngles => equippedLocalEulerAngles;
        public Transform LeftHandGrip => leftHandGrip;
        public bool TwoHanded => twoHanded;

        protected override void Reset()
        {
            base.Reset();
            SetItemType(ItemType.WeaponType);
        }

        protected override void Awake()
        {
            base.Awake();
            SetItemType(ItemType.WeaponType);
            EnsureAmmunitionInitialized();
            SetFlashActive(false);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetItemType(ItemType.WeaponType);
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0.1f, range);
            shotsPerSecond = Mathf.Max(0.1f, shotsPerSecond);
            magazineSize = Mathf.Max(1, magazineSize);
            startingReserveAmmo = Mathf.Max(0, startingReserveAmmo);
            reloadDuration = Mathf.Max(0.01f, reloadDuration);
            pelletsPerShot = Mathf.Max(1, pelletsPerShot);
            spreadDegrees = Mathf.Clamp(spreadDegrees, 0f, 90f);
            tracerWidth = Mathf.Max(0.001f, tracerWidth);
            tracerLifetime = Mathf.Max(0.01f, tracerLifetime);
            muzzleFlashLifetime = Mathf.Max(0.01f, muzzleFlashLifetime);
        }

        private void Update()
        {
            if (Time.time >= tracerEndsAt)
                HideTracers();
            if (Time.time >= flashEndsAt)
                SetFlashActive(false);
        }

        private void OnDisable()
        {
            CancelReload();
            HideTracers();
            SetFlashActive(false);
        }

        private void EnsureAmmunitionInitialized()
        {
            if (ammunitionInitialized)
                return;

            ammunitionInitialized = true;
            ammoInMagazine = MagazineSize;
            reserveAmmo = definition != null ? definition.StartingReserveAmmo : Mathf.Max(0, startingReserveAmmo);
        }

        /// <summary>Consume una bala por disparo, también si el preset genera varios perdigones.</summary>
        public bool TryConsumeRound()
        {
            EnsureAmmunitionInitialized();
            if (isReloading || ammoInMagazine <= 0)
                return false;

            ammoInMagazine--;
            StateChanged?.Invoke(this);
            return true;
        }

        public bool TryStartReload()
        {
            EnsureAmmunitionInitialized();
            if (isReloading || ammoInMagazine >= MagazineSize || reserveAmmo <= 0)
                return false;

            isReloading = true;
            reloadElapsed = 0f;
            StateChanged?.Invoke(this);
            return true;
        }

        /// <summary>El controlador entrega tiempo escalado para que la pausa detenga la recarga.</summary>
        public bool TickReload(float deltaTime)
        {
            if (!isReloading || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return false;

            reloadElapsed += deltaTime;
            if (reloadElapsed < ReloadDuration)
                return false;

            int transferred = Mathf.Min(Mathf.Max(0, MagazineSize - ammoInMagazine), reserveAmmo);
            ammoInMagazine += transferred;
            reserveAmmo -= transferred;
            isReloading = false;
            reloadElapsed = 0f;
            ReloadCompleted?.Invoke(this);
            StateChanged?.Invoke(this);
            return true;
        }

        public void CancelReload()
        {
            if (!isReloading)
                return;

            isReloading = false;
            reloadElapsed = 0f;
            StateChanged?.Invoke(this);
        }

        /// <summary>Devuelve la cantidad realmente agregada y evita desbordar la reserva.</summary>
        public int AddReserveAmmo(int amount)
        {
            EnsureAmmunitionInitialized();
            int added = Mathf.Min(Mathf.Max(0, amount), int.MaxValue - reserveAmmo);
            if (added > 0)
            {
                reserveAmmo += added;
                StateChanged?.Invoke(this);
            }
            return added;
        }

        /// <summary>Reinicio explícito: equipar o soltar el arma nunca repone su munición.</summary>
        public void ResetAmmunition()
        {
            ammunitionInitialized = false;
            isReloading = false;
            reloadElapsed = 0f;
            EnsureAmmunitionInitialized();
            StateChanged?.Invoke(this);
        }

        public void ShowShot(Vector3 start, Vector3 end, bool hit, int tracerIndex = 0)
        {
            if (!isActiveAndEnabled || !IsEquipped)
                return;

            tracerIndex = Mathf.Clamp(tracerIndex, 0, MaximumTracers - 1);
            // El índice cero inicia el disparo y limpia trazas de una ráfaga anterior.
            if (tracerIndex == 0)
            {
                HideTracers();
                ShotPresented?.Invoke(this);
            }

            if (tracerMaterial != null)
            {
                int capacity = Mathf.Clamp(Mathf.Max(PelletsPerShot, tracerIndex + 1), 1, MaximumTracers);
                if (shotTracers.Length < capacity)
                    Array.Resize(ref shotTracers, capacity);
                if (shotTracers[tracerIndex] == null)
                    shotTracers[tracerIndex] = CreateTracer(tracerIndex);
            }

            LineRenderer tracer = tracerIndex < shotTracers.Length ? shotTracers[tracerIndex] : null;
            if (tracer != null)
            {
                tracer.SetPosition(0, start);
                tracer.SetPosition(1, end);
                tracer.enabled = true;
                tracerEndsAt = Time.time + tracerLifetime;
            }
            flashEndsAt = Time.time + muzzleFlashLifetime;
            SetFlashActive(true);
        }

        public void ShowImpact(RaycastHit hit)
        {
            if (isActiveAndEnabled && IsEquipped && hit.collider != null && hit.normal.sqrMagnitude > .0001f)
                ImpactPresented?.Invoke(this, hit);
        }

        private void HideTracers()
        {
            foreach (LineRenderer tracer in shotTracers)
            {
                if (tracer != null && tracer.enabled)
                    tracer.enabled = false;
            }
        }

        private LineRenderer CreateTracer(int index)
        {
            GameObject visual = new GameObject($"ShotTracer_{index}");
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            LineRenderer tracer = visual.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.sharedMaterial = tracerMaterial;
            tracer.startColor = tracerColor;
            tracer.endColor = tracerColor;
            tracer.startWidth = tracerWidth;
            tracer.endWidth = tracerWidth * 0.4f;
            tracer.numCapVertices = 2;
            tracer.shadowCastingMode = ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.enabled = false;
            return tracer;
        }

        private void SetFlashActive(bool active)
        {
            if (muzzleFlash != null && muzzleFlash != gameObject &&
                muzzleFlash.transform.IsChildOf(transform) && muzzleFlash.activeSelf != active)
                muzzleFlash.SetActive(active);
        }
    }
}
