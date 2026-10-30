using System;
using Survive.Economy;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UPP.ThirdPersonController;

namespace Survive.Turrets
{
    [DefaultExecutionOrder(1040)]
    [DisallowMultipleComponent]
    public sealed class PlayerTurretController : MonoBehaviour
    {
        [SerializeField] private TurretDefinition definition;
        [SerializeField, Min(1)] private int capacity = 10;
        [SerializeField, Min(.5f)] private float placementDistance = 5f;
        [SerializeField] private bool requireLockedCursor = true;
        [SerializeField] private Material validPreviewMaterial, invalidPreviewMaterial;
        private PlayerHealth health;
        private UPPCharacterMovementComponent movement;
        private AutoTurret preview;
        private LineRenderer rangeRing;
        private float inputBlockedUntil;
        private Vector3 placementPosition;
        private Quaternion placementRotation;
        public int Count { get; private set; }
        public int Capacity => Mathf.Max(1, capacity);
        public TurretDefinition Definition => definition;
        public bool IsPlacing { get; private set; }
        public bool BlocksWeaponInput => IsPlacing || Time.unscaledTime < inputBlockedUntil;
        public string PlacementMessage { get; private set; }
        public bool HasValidPlacement { get; private set; }
        public event Action Changed;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            movement = GetComponent<UPPCharacterMovementComponent>();
        }

        private bool Alive => isActiveAndEnabled && (health == null || !health.IsDead);
        private bool CanBuild => Alive && Time.timeScale > 0 && (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked)
            && movement != null && movement.isActiveAndEnabled && !movement.DisableAllMove && !movement.IsRolling
            && !(GetComponent<PlayerVendorController>()?.IsOpen ?? false);

        public bool CanReceive(TurretDefinition item) => Alive && item != null && item.DeployedPrefab != null
            && Count < Capacity && (definition == null || definition == item);

        public bool TryAdd(TurretDefinition item)
        {
            if (!CanReceive(item)) return false;
            definition = item;
            Count++;
            Changed?.Invoke();
            return true;
        }

        public bool BeginPlacement()
        {
            if (!CanBuild || Count <= 0 || definition == null || definition.DeployedPrefab == null) return false;
            if (IsPlacing) return true;
            preview = Instantiate(definition.DeployedPrefab);
            preview.name = "Turret_PlacementPreview";
            preview.enabled = false;
            foreach (Collider body in preview.GetComponentsInChildren<Collider>()) body.enabled = false;
            foreach (NavMeshObstacle obstacle in preview.GetComponentsInChildren<NavMeshObstacle>()) obstacle.enabled = false;
            foreach (AudioSource source in preview.GetComponentsInChildren<AudioSource>()) source.enabled = false;
            var ring = new GameObject("RangePreview");
            ring.transform.SetParent(preview.transform, false);
            rangeRing = ring.AddComponent<LineRenderer>();
            rangeRing.useWorldSpace = true;
            rangeRing.loop = true;
            rangeRing.positionCount = 64;
            rangeRing.widthMultiplier = .035f;
            IsPlacing = true;
            Changed?.Invoke();
            return true;
        }

        private void LateUpdate()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.tKey.wasPressedThisFrame)
            {
                if (IsPlacing) CancelPlacement(); else BeginPlacement();
            }
            if (!IsPlacing) return;
            if (!CanBuild || keys != null && keys.escapeKey.wasPressedThisFrame
                || Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            { CancelPlacement(); return; }
            RefreshPreview();
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryPlace();
        }

        private void RefreshPreview()
        {
            HasValidPlacement = false;
            PlacementMessage = "Apuntá a un suelo cercano";
            Camera camera = Camera.main;
            if (camera == null || preview == null) return;
            Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
            RaycastHit nearest = default;
            float best = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(ray,
                         Vector3.Distance(ray.origin, transform.position) + placementDistance + 2f,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= best) continue;
                nearest = hit; best = hit.distance;
            }
            preview.gameObject.SetActive(nearest.collider != null);
            if (nearest.collider == null) return;
            placementPosition = nearest.point;
            placementRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            HasValidPlacement = ValidatePlacement(nearest, out string reason);
            PlacementMessage = HasValidPlacement ? "[Clic] Colocar · [T / Esc / clic derecho] Cancelar" : reason;
            preview.transform.SetPositionAndRotation(placementPosition, placementRotation);
            Material material = HasValidPlacement ? validPreviewMaterial : invalidPreviewMaterial;
            foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>())
            {
                if (renderer is LineRenderer) continue;
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++) materials[index] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            rangeRing.sharedMaterial = material;
            for (int index = 0; index < 64; index++)
            {
                float angle = index * Mathf.PI * 2f / 64;
                rangeRing.SetPosition(index, placementPosition + new Vector3(Mathf.Cos(angle) * definition.Range,
                    .04f, Mathf.Sin(angle) * definition.Range));
            }
        }

        public bool ValidatePlacement(RaycastHit ground, out string reason)
        {
            reason = "Suelo inválido";
            if (definition == null || ground.collider == null || ground.collider.isTrigger
                || ground.collider.attachedRigidbody != null && !ground.collider.attachedRigidbody.isKinematic
                || ground.collider.GetComponentInParent<EnemyHealth>() != null
                || ground.collider.GetComponentInParent<AutoTurret>() != null
                || ground.collider.GetComponentInParent<PlayerHealth>() != null
                || ground.collider.GetComponentInParent<NpcVendor>() != null) return false;
            if (Vector3.Distance(transform.position, ground.point) > placementDistance)
            { reason = "Fuera de alcance"; return false; }
            if (Vector3.Angle(ground.normal, Vector3.up) > definition.MaximumSlope)
            { reason = "El suelo tiene demasiada pendiente"; return false; }
            Vector3 viewOrigin = transform.position + Vector3.up;
            Vector3 toGround = ground.point + Vector3.up * .08f - viewOrigin;
            foreach (RaycastHit hit in Physics.RaycastAll(viewOrigin, toGround.normalized, toGround.magnitude,
                         ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) { reason = "Hay un obstáculo en el camino"; return false; }
            float radius = definition.FootprintRadius;
            foreach (Collider obstacle in Physics.OverlapBox(ground.point + Vector3.up * .14f,
                         new Vector3(radius, .12f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (obstacle != ground.collider)
                { reason = "Hay un objeto debajo de la base"; return false; }
            // Cuatro apoyos evitan colocar la base flotando al borde de una plataforma.
            for (int index = 0; index < 4; index++)
            {
                float angle = index * Mathf.PI * .5f;
                Vector3 support = ground.point + new Vector3(Mathf.Cos(angle) * radius, .5f, Mathf.Sin(angle) * radius);
                if (!Physics.Raycast(support, Vector3.down, out RaycastHit foot, .8f, ~0, QueryTriggerInteraction.Ignore)
                    || Vector3.Angle(foot.normal, Vector3.up) > definition.MaximumSlope
                    || Mathf.Abs(foot.point.y - ground.point.y) > .18f)
                { reason = "La base necesita apoyo completo"; return false; }
            }
            foreach (Collider obstacle in Physics.OverlapBox(ground.point + Vector3.up * (definition.Height * .5f + .2f),
                         new Vector3(radius, definition.Height * .5f - .1f, radius), Quaternion.identity,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                // No se ignora al jugador ni el resto de una malla de suelo: también bloquean el volumen.
                reason = "No hay espacio para la torreta";
                return false;
            }
            reason = "";
            return true;
        }

        public bool TryPlace()
        {
            if (!IsPlacing || !CanBuild || Count <= 0) return false;
            // Nunca confiar en el color del cuadro anterior.
            RefreshPreview();
            if (!HasValidPlacement) return false;
            AutoTurret turret = Instantiate(definition.DeployedPrefab, placementPosition, placementRotation);
            if (turret == null) return false;
            Count--;
            CancelPlacement();
            Changed?.Invoke();
            return true;
        }

        public void CancelPlacement()
        {
            bool changed = IsPlacing;
            IsPlacing = false;
            HasValidPlacement = false;
            inputBlockedUntil = Time.unscaledTime + .2f;
            if (preview != null) { preview.gameObject.SetActive(false); Destroy(preview.gameObject); }
            preview = null;
            rangeRing = null;
            if (changed) Changed?.Invoke();
        }

        private void OnDisable() => CancelPlacement();
    }
}
