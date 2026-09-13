using UnityEngine;
using UnityEngine.Rendering;

namespace Survive.Items
{
    /// <summary>Arma recogible. El jugador administra apuntado, cadencia y daño.</summary>
    [AddComponentMenu("Survive/Items/Weapon Item")]
    public sealed class WeaponItem : MasterItem
    {
        [Header("Disparo")]
        [Tooltip("Salida del proyectil visual; el eje local +Z apunta hacia adelante.")]
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(0.1f)] private float range = 80f;
        [SerializeField, Min(0.1f)] private float shotsPerSecond = 5f;
        [SerializeField] private bool automatic = true;

        [Header("Pose respecto del socket del jugador")]
        [SerializeField] private Vector3 equippedLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 equippedLocalEulerAngles = Vector3.zero;

        [Header("Presentación del disparo")]
        [Tooltip("Material incluido en el prefab para que el trazador también funcione en la build.")]
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private Color tracerColor = new Color(1f, 0.8f, 0.25f, 1f);
        [SerializeField, Min(0.001f)] private float tracerWidth = 0.015f;
        [SerializeField, Min(0.01f)] private float tracerLifetime = 0.06f;
        [Tooltip("Objeto visual opcional bajo Muzzle. No debe ser el objeto raíz del arma.")]
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Min(0.01f)] private float muzzleFlashLifetime = 0.045f;

        private LineRenderer shotTracer;
        private float tracerEndsAt;
        private float flashEndsAt;

        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public float Damage => damage;
        public float Range => range;
        public float ShotsPerSecond => shotsPerSecond;
        public bool Automatic => automatic;
        public Vector3 EquippedLocalPosition => equippedLocalPosition;
        public Vector3 EquippedLocalEulerAngles => equippedLocalEulerAngles;

        protected override void Reset()
        {
            base.Reset();
            SetItemType(ItemType.WeaponType);
        }

        protected override void Awake()
        {
            base.Awake();
            SetItemType(ItemType.WeaponType);
            SetFlashActive(false);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetItemType(ItemType.WeaponType);
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0.1f, range);
            shotsPerSecond = Mathf.Max(0.1f, shotsPerSecond);
            tracerWidth = Mathf.Max(0.001f, tracerWidth);
            tracerLifetime = Mathf.Max(0.01f, tracerLifetime);
            muzzleFlashLifetime = Mathf.Max(0.01f, muzzleFlashLifetime);
        }

        private void Update()
        {
            if (shotTracer != null && shotTracer.enabled && Time.time >= tracerEndsAt)
                shotTracer.enabled = false;
            if (Time.time >= flashEndsAt)
                SetFlashActive(false);
        }

        private void OnDisable()
        {
            if (shotTracer != null)
                shotTracer.enabled = false;
            SetFlashActive(false);
        }

        public void ShowShot(Vector3 start, Vector3 end, bool hit)
        {
            if (!isActiveAndEnabled || !IsEquipped)
                return;

            if (shotTracer == null && tracerMaterial != null)
                CreateTracer();

            if (shotTracer != null)
            {
                shotTracer.SetPosition(0, start);
                shotTracer.SetPosition(1, end);
                shotTracer.enabled = true;
                tracerEndsAt = Time.time + tracerLifetime;
            }
            flashEndsAt = Time.time + muzzleFlashLifetime;
            SetFlashActive(true);
        }

        private void CreateTracer()
        {
            GameObject visual = new GameObject("ShotTracer");
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            shotTracer = visual.AddComponent<LineRenderer>();
            shotTracer.useWorldSpace = true;
            shotTracer.positionCount = 2;
            shotTracer.sharedMaterial = tracerMaterial;
            shotTracer.startColor = tracerColor;
            shotTracer.endColor = tracerColor;
            shotTracer.startWidth = tracerWidth;
            shotTracer.endWidth = tracerWidth * 0.4f;
            shotTracer.numCapVertices = 2;
            shotTracer.shadowCastingMode = ShadowCastingMode.Off;
            shotTracer.receiveShadows = false;
            shotTracer.enabled = false;
        }

        private void SetFlashActive(bool active)
        {
            if (muzzleFlash != null && muzzleFlash != gameObject &&
                muzzleFlash.transform.IsChildOf(transform) && muzzleFlash.activeSelf != active)
                muzzleFlash.SetActive(active);
        }
    }
}
