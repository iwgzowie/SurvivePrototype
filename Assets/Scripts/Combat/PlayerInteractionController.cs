using Survive.Items;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UPP.ThirdPersonController;

namespace Survive.Combat
{
    /// <summary>Selecciona el arma visible desde la cámara y la equipa al interactuar.</summary>
    [DefaultExecutionOrder(1150)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerWeaponController))]
    [AddComponentMenu("Survive/Combate/Interacción del jugador")]
    public sealed class PlayerInteractionController : MonoBehaviour
    {
        [Header("Referencias del jugador")]
        [SerializeField] private PlayerWeaponController weaponController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField, Tooltip("Sin referencia, usa la cámara principal creada por UPP.")]
        private Camera sourceCamera;

        [Header("Detección")]
        [SerializeField, Min(0.1f), Tooltip("Distancia máxima entre el origen del jugador y el centro del arma, en metros.")]
        private float interactionDistance = 2.5f;
        [SerializeField, Tooltip("Origen local usado para medir el alcance y comprobar el trayecto hacia el arma.")]
        private Vector3 interactionOriginOffset = new Vector3(0f, 1f, 0f);
        [SerializeField, Tooltip("Incluí las capas del arma y todas las superficies que bloquean su recogida.")]
        private LayerMask hitMask = ~0;
        [SerializeField, Tooltip("Oculta la interacción cuando un menú libera el cursor.")]
        private bool requireLockedCursor = true;

        [Header("Entrada")]
        [SerializeField] private InputAction interactAction = CreateDefaultInteractAction();

        [Header("Aviso de interacción")]
        [SerializeField] private CanvasGroup promptCanvasGroup;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private string promptMessage = "[E] Recoger arma";

        private UPPCharacterMovementComponent movement;
        private readonly RaycastHit[] rayHits = new RaycastHit[64];
        private readonly Collider[] originOverlaps = new Collider[32];

        public WeaponItem CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;
        public float InteractionDistance => interactionDistance;

        private Transform OwnerRoot => movement != null ? movement.transform : transform;

        private static InputAction CreateDefaultInteractAction()
        {
            return new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
        }

        private void Reset()
        {
            ResolvePlayerReferences();
        }

        private void Awake()
        {
            ResolvePlayerReferences();
            SetTarget(null);
        }

        private void OnEnable()
        {
            interactAction ??= CreateDefaultInteractAction();
            interactAction.Enable();
        }

        private void OnDisable()
        {
            interactAction?.Disable();
            SetTarget(null);
        }

        private void OnDestroy()
        {
            interactAction?.Dispose();
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.1f, interactionDistance);
        }

        private void LateUpdate()
        {
            RefreshTarget();
            if (HasTarget && interactAction != null && interactAction.enabled
                && interactAction.WasPressedThisFrame())
            {
                TryInteract();
            }
        }

        private void ResolvePlayerReferences()
        {
            if (weaponController == null)
                weaponController = GetComponent<PlayerWeaponController>();
            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>();
            if (movement == null)
                movement = GetComponent<UPPCharacterMovementComponent>();
        }

        private bool CanInteract()
        {
            return isActiveAndEnabled && Time.timeScale > 0f
                && weaponController != null && weaponController.isActiveAndEnabled
                && weaponController.EquippedWeapon == null
                && movement != null && movement.isActiveAndEnabled && movement.IsPlayer
                && !movement.DisableAllMove && !movement.IsRolling
                && (playerHealth == null || !playerHealth.IsDead)
                && (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
        }

        /// <summary>Actualiza el objetivo y el aviso; también sirve para consultar la interacción desde otra lógica.</summary>
        public void RefreshTarget()
        {
            ResolvePlayerReferences();
            if (!CanInteract())
            {
                SetTarget(null);
                return;
            }

            if (sourceCamera == null || !sourceCamera.isActiveAndEnabled)
                sourceCamera = Camera.main;
            if (sourceCamera == null || !sourceCamera.isActiveAndEnabled)
            {
                SetTarget(null);
                return;
            }

            Ray ray = sourceCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // El alcance sale del jugador; la separación de la cámara TPS no reduce la distancia de recogida.
            Vector3 playerOrigin = OwnerRoot.TransformPoint(interactionOriginOffset);
            float cameraDistance = Vector3.Distance(ray.origin, playerOrigin) + interactionDistance;
            int count = Cast(ray, cameraDistance, QueryTriggerInteraction.Collide, out RaycastHit[] hits);
            float nearestWeapon = float.PositiveInfinity;
            float nearestObstacle = float.PositiveInfinity;
            WeaponItem candidate = null;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || IsOwnCollider(collider))
                    continue;

                WeaponItem weapon = collider.GetComponentInParent<WeaponItem>();
                if (weapon != null && weapon.isActiveAndEnabled && !weapon.IsEquipped
                    && weapon.Type == ItemType.WeaponType)
                {
                    if (hits[i].distance < nearestWeapon)
                    {
                        nearestWeapon = hits[i].distance;
                        candidate = weapon;
                    }
                }
                else if (!collider.isTrigger)
                {
                    nearestObstacle = Mathf.Min(nearestObstacle, hits[i].distance);
                }
            }

            if (candidate == null || nearestObstacle <= nearestWeapon
                || (candidate.transform.position - playerOrigin).sqrMagnitude
                    > interactionDistance * interactionDistance)
            {
                SetTarget(null);
                return;
            }

            // El trigger puede sobresalir de una pared y la cámara puede mirar alrededor de una esquina.
            Vector3 targetPosition = candidate.transform.position;
            if (!HasClearPath(ray.origin, targetPosition, candidate)
                || !HasClearPath(playerOrigin, targetPosition, candidate))
            {
                SetTarget(null);
                return;
            }

            SetTarget(candidate);
        }

        /// <summary>Revalida distancia, salud, pausa y obstáculos antes de reutilizar el equipamiento existente.</summary>
        public bool TryInteract()
        {
            RefreshTarget();
            if (CurrentTarget == null)
                return false;

            bool equipped = weaponController.TryEquip(CurrentTarget);
            RefreshTarget();
            return equipped;
        }

        private bool IsOwnCollider(Collider collider)
        {
            return collider.transform.IsChildOf(OwnerRoot);
        }

        private int Cast(Ray ray, float distance, QueryTriggerInteraction triggers, out RaycastHit[] hits)
        {
            int count = Physics.RaycastNonAlloc(ray, rayHits, distance, hitMask, triggers);
            hits = rayHits;
            if (count == rayHits.Length)
            {
                // No perder una pared si la consulta supera el búfer reutilizable.
                hits = Physics.RaycastAll(ray, distance, hitMask, triggers);
                count = hits.Length;
            }
            return count;
        }

        private bool HasClearPath(Vector3 origin, Vector3 destination, WeaponItem target)
        {
            // Un raycast no detecta la superficie si su origen ya está dentro del collider.
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, 0.01f, originOverlaps,
                hitMask, QueryTriggerInteraction.Ignore);
            Collider[] overlaps = originOverlaps;
            if (overlapCount == originOverlaps.Length)
            {
                overlaps = Physics.OverlapSphere(origin, 0.01f, hitMask, QueryTriggerInteraction.Ignore);
                overlapCount = overlaps.Length;
            }
            for (int i = 0; i < overlapCount; i++)
            {
                Collider collider = overlaps[i];
                if (collider != null && !IsOwnCollider(collider)
                    && !collider.transform.IsChildOf(target.transform))
                    return false;
            }

            Vector3 delta = destination - origin;
            if (delta.sqrMagnitude < 0.000001f)
                return true;
            int count = Cast(new Ray(origin, delta.normalized), delta.magnitude,
                QueryTriggerInteraction.Ignore, out RaycastHit[] hits);
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider != null && !IsOwnCollider(collider)
                    && !collider.transform.IsChildOf(target.transform))
                    return false;
            }
            return true;
        }

        private void SetTarget(WeaponItem target)
        {
            CurrentTarget = target;
            bool visible = target != null;
            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = visible ? 1f : 0f;
                promptCanvasGroup.interactable = false;
                promptCanvasGroup.blocksRaycasts = false;
            }
            if (promptText != null)
            {
                promptText.enabled = visible;
                if (visible && promptText.text != promptMessage)
                    promptText.text = promptMessage;
            }
        }
    }
}
