using Survive.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UPP.Runtime.Aiming;
using UPP.ThirdPersonController;

namespace Survive.Combat
{
    /// <summary>
    /// Equipa un arma del mundo y dispara desde su cañón usando el aim existente de UPP.
    /// La locomoción, la cámara y el IK continúan a cargo de sus componentes originales.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UPPCharacterMovementComponent))]
    [AddComponentMenu("Survive/Combate/Arma del jugador")]
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        [Header("Referencias del jugador")]
        [SerializeField] private UPPCharacterMovementComponent movement;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField, Tooltip("Punto de agarre. Sin referencia, busca WeaponSocket y luego la mano derecha humanoide.")]
        private Transform weaponSocket;

        [Header("Ajustes de agarre")]
        [SerializeField, Tooltip("Desplazamiento local adicional a la posición configurada en el arma, en metros.")]
        private Vector3 attachmentPositionOffset;
        [SerializeField, Tooltip("Rotación local adicional a la configurada en el arma, en grados.")]
        private Vector3 attachmentEulerOffset;
        [SerializeField, Tooltip("Posición del agarre provisional, relativa al jugador, cuando no hay socket ni mano humanoide.")]
        private Vector3 fallbackSocketLocalPosition = new Vector3(0.35f, 1.25f, 0.35f);
        [SerializeField] private Vector3 fallbackSocketLocalEulerAngles;
        [SerializeField, Tooltip("Orienta el cañón al apuntado de cámara sin modificar los huesos del personaje.")]
        private bool alignWeaponWhileAiming = true;

        [Header("Disparo")]
        [SerializeField, Tooltip("Botón de disparo editable. Por defecto: clic izquierdo y gatillo derecho de gamepad.")]
        private InputAction fireAction = CreateDefaultFireAction();
        [SerializeField, Tooltip("Capas que bloquean los disparos y pueden recibir impactos. Se ignoran triggers y el propio jugador.")]
        private LayerMask hitMask = ~0;
        [SerializeField, Tooltip("Impide disparar mientras el cursor está libre, por ejemplo al abrir un menú.")]
        private bool requireLockedCursor = true;
        [SerializeField, Min(0.001f), Tooltip("Radio de seguridad del cañón, en metros: impide disparar desde dentro de una pared.")]
        private float muzzleClearanceRadius = 0.025f;

        [Header("Mira de juego")]
        [SerializeField, Tooltip("Muestra una mira al equipar y apuntar; funciona aunque DrawDebug esté apagado.")]
        private bool showCrosshair = true;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField, Min(2f)] private float crosshairSize = 7f;
        [SerializeField, Min(1f)] private float crosshairThickness = 2f;

        private WeaponItem equippedWeapon;
        private Transform resolvedSocket;
        private Transform fallbackSocket;
        private CapsuleCollider playerCapsule;
        private UnityEngine.Camera aimCamera;
        private UPPAimTrace aimTrace;
        private UPPDebugCrosshair suppressedDebugCrosshair;
        private bool debugCrosshairWasEnabled;
        private float nextShotTime;
        private readonly RaycastHit[] rayHits = new RaycastHit[32];
        private readonly Collider[] overlapHits = new Collider[16];

        public WeaponItem EquippedWeapon => equippedWeapon != null && equippedWeapon.IsEquipped
            ? equippedWeapon
            : null;

        private Transform OwnerRoot => movement != null ? movement.transform : transform;

        private static InputAction CreateDefaultFireAction()
        {
            var action = new InputAction("Fire", InputActionType.Button);
            action.AddBinding("<Mouse>/leftButton");
            action.AddBinding("<Gamepad>/rightTrigger");
            return action;
        }

        private void Reset()
        {
            ResolvePlayerReferences();
        }

        private void Awake()
        {
            ResolvePlayerReferences();
        }

        private void OnEnable()
        {
            fireAction ??= CreateDefaultFireAction();
            fireAction.Enable();
        }

        private void OnDisable()
        {
            fireAction?.Disable();
            RestoreDebugCrosshair();
            RestoreGripRotation();
        }

        private void OnDestroy()
        {
            fireAction?.Dispose();
            RestoreDebugCrosshair();
            if (fallbackSocket != null)
            {
                Destroy(fallbackSocket.gameObject);
            }
        }

        private void OnValidate()
        {
            muzzleClearanceRadius = Mathf.Max(0.001f, muzzleClearanceRadius);
            crosshairSize = Mathf.Max(2f, crosshairSize);
            crosshairThickness = Mathf.Max(1f, crosshairThickness);
        }

        private void LateUpdate()
        {
            if (EquippedWeapon == null)
            {
                equippedWeapon = null;
                RestoreDebugCrosshair();
                return;
            }

            ResolveCamera();
            UpdateDebugCrosshair();
            RestoreGripRotation();
            if (CanAimAndFire() && TryGetAimTarget(out _, out Vector3 target, out _))
            {
                AlignWeapon(target);
            }

            // LateUpdate ocurre después de la cámara UPP y del trace, evitando disparar con el aim del cuadro anterior.
            bool wantsFire = fireAction != null && fireAction.enabled
                && (equippedWeapon.Automatic ? fireAction.IsPressed() : fireAction.WasPressedThisFrame());
            if (wantsFire)
            {
                TryFire();
            }
        }

        public bool TryEquip(WeaponItem weapon)
        {
            ResolvePlayerReferences();
            if (!isActiveAndEnabled || movement == null || !movement.isActiveAndEnabled
                || !movement.IsPlayer || (playerHealth != null && playerHealth.IsDead)
                || EquippedWeapon != null || weapon == null || weapon.IsEquipped
                || !weapon.isActiveAndEnabled)
            {
                return false;
            }

            resolvedSocket = ResolveSocket();
            if (!weapon.TryAttach(OwnerRoot, resolvedSocket,
                    weapon.EquippedLocalPosition + attachmentPositionOffset,
                    weapon.EquippedLocalEulerAngles + attachmentEulerOffset))
            {
                return false;
            }

            equippedWeapon = weapon;
            nextShotTime = Time.time;
            ResolveCamera();
            UpdateDebugCrosshair();
            return true;
        }

        /// <summary>Permite devolver el arma al mapa desde otro sistema sin agregar una tecla de inventario.</summary>
        public bool DropEquippedWeapon(Vector3 position, Quaternion rotation)
        {
            WeaponItem weapon = EquippedWeapon;
            if (weapon == null)
            {
                return false;
            }

            equippedWeapon = null;
            weapon.PlaceInWorld(position, rotation);
            RestoreDebugCrosshair();
            return true;
        }

        /// <summary>El disparo programático respeta las mismas condiciones y cadencia que el input.</summary>
        public bool TryFire()
        {
            if (!CanAimAndFire() || Time.time < nextShotTime)
            {
                return false;
            }

            ResolveCamera();
            if (!TryGetAimTarget(out Ray cameraRay, out Vector3 target, out RaycastHit cameraHit))
            {
                return false;
            }

            RestoreGripRotation();
            AlignWeapon(target);
            Vector3 muzzlePosition = equippedWeapon.Muzzle.position;
            float range = Mathf.Max(0.01f, equippedWeapon.Range);
            nextShotTime = Time.time + 1f / Mathf.Max(0.01f, equippedWeapon.ShotsPerSecond);

            // La cámara TPS puede mirar una pared que está detrás del cañón: no se invierte el disparo.
            Vector3 toTarget = target - muzzlePosition;
            if (Vector3.Dot(toTarget, cameraRay.direction) <= 0.001f)
            {
                equippedWeapon.ShowShot(muzzlePosition, muzzlePosition, true);
                return true;
            }

            if (TryFindMuzzleOverlap(muzzlePosition, out Vector3 blockedPoint))
            {
                equippedWeapon.ShowShot(muzzlePosition, blockedPoint, true);
                return true;
            }

            // También evita que el cañón atraviese una pared fina y aparezca del lado del enemigo.
            Vector3 safetyOrigin = playerCapsule != null && playerCapsule.enabled
                ? playerCapsule.bounds.center
                : resolvedSocket.position;
            Vector3 toMuzzle = muzzlePosition - safetyOrigin;
            if (toMuzzle.sqrMagnitude > 0.000001f
                && TryGetNearestHit(new Ray(safetyOrigin, toMuzzle.normalized), toMuzzle.magnitude,
                    out RaycastHit nearObstruction))
            {
                equippedWeapon.ShowShot(muzzlePosition, nearObstruction.point, true);
                return true;
            }

            float distance = Mathf.Min(range, toTarget.magnitude);
            Vector3 direction = toTarget.normalized;
            // La superficie objetivo puede quedar unas milésimas fuera del rayo por precisión numérica.
            // Sólo se acepta ese margen si sigue siendo el MISMO collider visto por la cámara.
            float queryDistance = Mathf.Min(range, toTarget.magnitude + 0.005f);
            bool hasHit = TryGetNearestHit(new Ray(muzzlePosition, direction), queryDistance, out RaycastHit hit)
                && (hit.distance <= distance || (cameraHit.collider != null && hit.collider == cameraHit.collider));
            Vector3 endpoint = hasHit ? hit.point : muzzlePosition + direction * distance;
            if (hasHit)
            {
                ApplyDamage(hit);
            }

            equippedWeapon.ShowShot(muzzlePosition, endpoint, hasHit);
            return true;
        }

        private bool CanAimAndFire()
        {
            return isActiveAndEnabled && EquippedWeapon != null && equippedWeapon.isActiveAndEnabled
                && movement != null && movement.isActiveAndEnabled && movement.IsPlayer
                && movement.IsAiming && !movement.DisableAllMove && !movement.IsRolling
                && (playerHealth == null || !playerHealth.IsDead) && Time.timeScale > 0f
                && (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
        }

        private void ResolvePlayerReferences()
        {
            if (movement == null)
            {
                movement = GetComponent<UPPCharacterMovementComponent>();
            }
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }
            if (playerCapsule == null)
            {
                playerCapsule = GetComponent<CapsuleCollider>();
            }
        }

        private Transform ResolveSocket()
        {
            if (weaponSocket != null && weaponSocket.IsChildOf(OwnerRoot))
            {
                return weaponSocket;
            }

            foreach (Transform candidate in OwnerRoot.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "WeaponSocket")
                {
                    return candidate;
                }
            }

            Animator animator = OwnerRoot.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    return hand;
                }
            }

            if (fallbackSocket == null)
            {
                fallbackSocket = new GameObject("WeaponSocketFallback").transform;
                fallbackSocket.SetParent(OwnerRoot, false);
                Debug.LogWarning("El jugador no tiene WeaponSocket ni mano derecha humanoide; se usa el agarre provisional configurable.", this);
            }
            fallbackSocket.localPosition = fallbackSocketLocalPosition;
            fallbackSocket.localRotation = Quaternion.Euler(fallbackSocketLocalEulerAngles);
            return fallbackSocket;
        }

        private void ResolveCamera()
        {
            UnityEngine.Camera camera = movement != null && movement.MyPivotCamera != null
                ? movement.MyPivotCamera.mCamera
                : null;
            if (camera == aimCamera && aimCamera != null && aimTrace != null)
            {
                return;
            }

            RestoreDebugCrosshair();
            aimCamera = camera;
            aimTrace = aimCamera != null ? aimCamera.GetComponent<UPPAimTrace>() : null;
        }

        private bool TryGetAimTarget(out Ray ray, out Vector3 target, out RaycastHit cameraHit)
        {
            ray = default;
            target = default;
            cameraHit = default;
            if (aimCamera == null || !aimCamera.isActiveAndEnabled || aimTrace == null)
            {
                return false;
            }

            aimTrace.RefreshTrace();
            ray = aimTrace.LastRay;
            if (ray.direction.sqrMagnitude < 0.000001f)
            {
                return false;
            }

            // Reutiliza el rayo UPP. La selección balística usa la máscara/rango del arma y admite escenas densas.
            float distance = Mathf.Max(0.01f, equippedWeapon.Range)
                + Vector3.Distance(ray.origin, equippedWeapon.Muzzle.position);
            target = TryGetNearestHit(ray, distance, out cameraHit)
                ? cameraHit.point
                : ray.GetPoint(distance);
            return true;
        }

        private void RestoreGripRotation()
        {
            if (EquippedWeapon != null)
            {
                equippedWeapon.transform.localRotation = Quaternion.Euler(
                    equippedWeapon.EquippedLocalEulerAngles + attachmentEulerOffset);
            }
        }

        private void AlignWeapon(Vector3 target)
        {
            if (!alignWeaponWhileAiming || EquippedWeapon == null
                || (aimCamera != null
                    && Vector3.Dot(target - equippedWeapon.transform.position, aimCamera.transform.forward) <= 0.001f))
            {
                return;
            }

            // Se rota únicamente el objeto equipado. Dos pasos corrigen el desplazamiento del cañón respecto del agarre.
            for (int iteration = 0; iteration < 2; iteration++)
            {
                Transform muzzle = equippedWeapon.Muzzle;
                Vector3 direction = target - muzzle.position;
                if (direction.sqrMagnitude <= 0.000001f)
                {
                    return;
                }
                equippedWeapon.transform.rotation = Quaternion.FromToRotation(muzzle.forward, direction.normalized)
                    * equippedWeapon.transform.rotation;
            }
        }

        private bool TryGetNearestHit(Ray ray, float distance, out RaycastHit selected)
        {
            selected = default;
            int count = Physics.RaycastNonAlloc(ray, rayHits, distance, hitMask, QueryTriggerInteraction.Ignore);
            RaycastHit[] candidates = rayHits;
            if (count == rayHits.Length)
            {
                // NonAlloc no ordena resultados: ante saturación, se consulta el conjunto completo.
                candidates = Physics.RaycastAll(ray, distance, hitMask, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }

            float nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit candidate = candidates[index];
                if (candidate.collider == null || IsOwnerCollider(candidate.collider)
                    || candidate.distance >= nearest)
                {
                    continue;
                }
                nearest = candidate.distance;
                selected = candidate;
            }
            return nearest < float.PositiveInfinity;
        }

        private bool TryFindMuzzleOverlap(Vector3 position, out Vector3 blockedPoint)
        {
            blockedPoint = position;
            int count = Physics.OverlapSphereNonAlloc(position, muzzleClearanceRadius, overlapHits,
                hitMask, QueryTriggerInteraction.Ignore);
            Collider[] candidates = overlapHits;
            if (count == overlapHits.Length)
            {
                candidates = Physics.OverlapSphere(position, muzzleClearanceRadius, hitMask, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }

            float nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                Collider candidate = candidates[index];
                if (candidate == null || IsOwnerCollider(candidate))
                {
                    continue;
                }
                Vector3 point = candidate.ClosestPoint(position);
                float distanceSquared = (point - position).sqrMagnitude;
                if (distanceSquared < nearest)
                {
                    nearest = distanceSquared;
                    blockedPoint = point;
                }
            }
            return nearest < float.PositiveInfinity;
        }

        private bool IsOwnerCollider(Collider candidate)
        {
            return candidate.transform.IsChildOf(OwnerRoot);
        }

        private void ApplyDamage(RaycastHit hit)
        {
            // La salud recibe el daño también cuando el impacto llega a un collider hijo.
            EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(equippedWeapon.Damage, hit.point);
            }
        }

        private void UpdateDebugCrosshair()
        {
            if (!showCrosshair || EquippedWeapon == null || aimCamera == null)
            {
                RestoreDebugCrosshair();
                return;
            }
            UPPDebugCrosshair debugCrosshair = aimCamera.GetComponent<UPPDebugCrosshair>();
            if (debugCrosshair == null || suppressedDebugCrosshair == debugCrosshair)
            {
                return;
            }
            RestoreDebugCrosshair();
            suppressedDebugCrosshair = debugCrosshair;
            debugCrosshairWasEnabled = debugCrosshair.enabled;
            debugCrosshair.enabled = false;
        }

        private void RestoreDebugCrosshair()
        {
            if (suppressedDebugCrosshair != null)
            {
                suppressedDebugCrosshair.enabled = debugCrosshairWasEnabled;
                suppressedDebugCrosshair = null;
            }
        }

        private void OnGUI()
        {
            if (!showCrosshair || !CanAimAndFire() || aimCamera == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            Rect viewport = aimCamera.pixelRect;
            float centerX = viewport.x + viewport.width * 0.5f;
            float centerY = Screen.height - viewport.y - viewport.height * 0.5f;
            Color previous = GUI.color;
            GUI.color = crosshairColor;
            GUI.DrawTexture(new Rect(centerX - crosshairSize, centerY - crosshairThickness * 0.5f,
                crosshairSize * 2f, crosshairThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - crosshairThickness * 0.5f, centerY - crosshairSize,
                crosshairThickness, crosshairSize * 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}

