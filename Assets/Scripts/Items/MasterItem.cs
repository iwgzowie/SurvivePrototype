using UnityEngine;
using Survive.Combat;

namespace Survive.Items
{
    public enum ItemType
    {
        Generic = 0,
        WeaponType = 1
    }

    /// <summary>Base de un objeto recogible, con flotación y attachment al jugador.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public class MasterItem : MonoBehaviour
    {
        [Header("Tipo de objeto")]
        [SerializeField] private ItemType itemType = ItemType.Generic;

        [Header("Presentación en el mapa")]
        [Tooltip("Altura sobre el suelo en centímetros. 100 cm equivalen a 1 unidad de Unity.")]
        [SerializeField, Min(0f)] private float hoverHeightCentimeters = 100f;
        [SerializeField, Min(0f)] private float bobAmplitudeCentimeters = 5f;
        [SerializeField, Min(0f)] private float bobCyclesPerSecond = 0.65f;
        [SerializeField] private float rotationDegreesPerSecond = 45f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [Tooltip("Distancia sobre el punto de colocación desde donde se busca el suelo.")]
        [SerializeField, Min(0.05f)] private float groundProbeStartHeight = 2f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 50f;

        private Rigidbody itemBody;
        private Collider[] itemColliders;
        private bool[] worldColliderStates;
        private Transform[] itemTransforms;
        private int[] worldLayers;
        private Transform attachedOwner;
        private Vector3 worldGroundPosition;
        private Vector3 worldRestPosition;
        private Quaternion worldRestRotation;
        private float worldAnimationStartedAt;
        private bool worldPositionInitialized;

        public ItemType Type => itemType;
        public bool IsEquipped { get; private set; }

        protected virtual void Reset()
        {
            SphereCollider pickupCollider = GetComponent<SphereCollider>();
            if (pickupCollider != null)
            {
                pickupCollider.isTrigger = true;
                pickupCollider.radius = 0.6f;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.useGravity = false;
                body.isKinematic = true;
            }
        }

        protected virtual void Awake()
        {
            CacheWorldState();
            ConfigureWorldPhysics();
        }

        protected virtual void Start()
        {
            if (!IsEquipped && !worldPositionInitialized)
                InitializeWorldPose(transform.position, transform.rotation);
        }

        protected virtual void OnValidate()
        {
            hoverHeightCentimeters = Mathf.Max(0f, hoverHeightCentimeters);
            bobAmplitudeCentimeters = Mathf.Clamp(bobAmplitudeCentimeters, 0f, hoverHeightCentimeters);
            bobCyclesPerSecond = Mathf.Max(0f, bobCyclesPerSecond);
            groundProbeStartHeight = Mathf.Max(0.05f, groundProbeStartHeight);
            groundProbeDistance = Mathf.Max(0.1f, groundProbeDistance);
        }

        protected void SetItemType(ItemType value)
        {
            itemType = value;
        }

        private void CacheWorldState()
        {
            if (itemBody != null)
                return;

            itemBody = GetComponent<Rigidbody>();
            itemColliders = GetComponentsInChildren<Collider>(true);
            worldColliderStates = new bool[itemColliders.Length];
            for (int i = 0; i < itemColliders.Length; i++)
                worldColliderStates[i] = itemColliders[i].enabled;

            itemTransforms = GetComponentsInChildren<Transform>(true);
            worldLayers = new int[itemTransforms.Length];
            for (int i = 0; i < itemTransforms.Length; i++)
                worldLayers[i] = itemTransforms[i].gameObject.layer;
        }

        private void ConfigureWorldPhysics()
        {
            GetComponent<SphereCollider>().isTrigger = true;
            StopPhysics();
            itemBody.detectCollisions = true;
        }

        private void StopPhysics()
        {
            // Limpiar velocidades antes de hacerlo cinemático evita advertencias de PhysX.
            if (!itemBody.isKinematic)
            {
                itemBody.linearVelocity = Vector3.zero;
                itemBody.angularVelocity = Vector3.zero;
            }
            itemBody.useGravity = false;
            itemBody.isKinematic = true;
        }

        private void InitializeWorldPose(Vector3 position, Quaternion rotation)
        {
            worldGroundPosition = FindGround(position);
            worldRestPosition = worldGroundPosition + Vector3.up * (hoverHeightCentimeters * 0.01f);
            worldRestRotation = rotation;
            worldAnimationStartedAt = Time.time;
            worldPositionInitialized = true;
            itemBody.position = worldRestPosition;
            itemBody.rotation = worldRestRotation;
            transform.SetPositionAndRotation(worldRestPosition, worldRestRotation);
        }

        private Vector3 FindGround(Vector3 placementPosition)
        {
            Vector3 origin = placementPosition + Vector3.up * groundProbeStartHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down,
                groundProbeStartHeight + groundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Vector3 groundPosition = placementPosition;
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<PlayerWeaponController>() != null ||
                    (attachedOwner != null && hitTransform.IsChildOf(attachedOwner)) ||
                    hit.normal.y <= 0.1f || hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                groundPosition = hit.point;
            }
            return groundPosition;
        }

        private void FixedUpdate()
        {
            if (IsEquipped || !worldPositionInitialized)
                return;

            // Se recalcula desde el suelo original para editar la altura en Play sin acumular offsets.
            worldRestPosition = worldGroundPosition + Vector3.up * (hoverHeightCentimeters * 0.01f);
            float elapsed = Time.time - worldAnimationStartedAt;
            float offset = Mathf.Sin(elapsed * bobCyclesPerSecond * Mathf.PI * 2f)
                * (bobAmplitudeCentimeters * 0.01f);
            itemBody.MovePosition(worldRestPosition + Vector3.up * offset);
            itemBody.MoveRotation(Quaternion.AngleAxis(elapsed * rotationDegreesPerSecond, Vector3.up)
                * worldRestRotation);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPickUp(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // También recoge un arma que se habilitó cuando el jugador ya estaba dentro.
            TryPickUp(other);
        }

        private void TryPickUp(Collider other)
        {
            if (!isActiveAndEnabled || IsEquipped || itemType != ItemType.WeaponType)
                return;

            WeaponItem weapon = this as WeaponItem;
            if (weapon == null)
                return;

            PlayerWeaponController receiver = other.GetComponentInParent<PlayerWeaponController>();
            if (receiver != null && receiver.isActiveAndEnabled)
                receiver.TryEquip(weapon);
        }

        /// <summary>Equipa una sola vez; la pose se expresa en el espacio del socket.</summary>
        public bool TryAttach(Transform ownerRoot, Transform socket, Vector3 localPosition, Vector3 localEulerAngles)
        {
            if (!isActiveAndEnabled || IsEquipped || ownerRoot == null || socket == null ||
                ownerRoot.IsChildOf(transform) || socket.IsChildOf(transform))
                return false;

            CacheWorldState();
            IsEquipped = true;
            attachedOwner = ownerRoot;
            StopPhysics();
            itemBody.detectCollisions = false;
            foreach (Collider itemCollider in itemColliders)
            {
                if (itemCollider != null)
                    itemCollider.enabled = false;
            }
            foreach (Transform itemTransform in itemTransforms)
            {
                if (itemTransform != null)
                    itemTransform.gameObject.layer = ownerRoot.gameObject.layer;
            }

            // Conservar escala mundial permite usar sockets de modelos importados con otra escala.
            transform.SetParent(socket, true);
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(localEulerAngles);
            return true;
        }

        /// <summary>El punto recibido representa el suelo de colocación, no la altura flotante.</summary>
        public void PlaceInWorld(Vector3 position, Quaternion rotation)
        {
            CacheWorldState();
            transform.SetParent(null, true);
            IsEquipped = false;
            for (int i = 0; i < itemColliders.Length; i++)
            {
                if (itemColliders[i] != null)
                    itemColliders[i].enabled = worldColliderStates[i];
            }
            for (int i = 0; i < itemTransforms.Length; i++)
            {
                if (itemTransforms[i] != null)
                    itemTransforms[i].gameObject.layer = worldLayers[i];
            }
            ConfigureWorldPhysics();
            InitializeWorldPose(position, rotation);
            attachedOwner = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (IsEquipped)
                return;

            // La previsualización no cambia el transform del prefab ni acumula desplazamientos.
            Vector3 previewPosition = Application.isPlaying && worldPositionInitialized
                ? worldRestPosition
                : FindGround(transform.position) + Vector3.up * (hoverHeightCentimeters * 0.01f);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            Gizmos.DrawLine(previewPosition - Vector3.up * (hoverHeightCentimeters * 0.01f), previewPosition);
            Gizmos.DrawWireSphere(previewPosition, 0.15f);
        }
    }
}
