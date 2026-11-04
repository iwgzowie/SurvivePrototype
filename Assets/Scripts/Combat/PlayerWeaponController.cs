using System;
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
        [SerializeField] private PlayerWeaponInventory inventory;
        [SerializeField] private Animator weaponAnimator;
        [SerializeField] private AnimationClip reloadAnimationClip;
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
        [SerializeField] private InputAction reloadAction = CreateDefaultReloadAction();
        [SerializeField] private InputAction dropAction = CreateDefaultDropAction();
        [SerializeField, Tooltip("Inicia recarga al vaciar el cargador si todavía queda reserva.")]
        private bool automaticReloadOnEmpty = true;
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
        private readonly RaycastHit[] pelletHits = new RaycastHit[64];
        private float hitMarkerEndsAt;
        private float autoReloadReadyAt;
        private PlayerWeaponPose weaponPose;
        private int reloadLayer = -1;
        private bool hasReloadParameters;
        private RuntimeAnimatorController cachedAnimatorController;
        private static readonly int ReloadingHash = Animator.StringToHash("WeaponReloading");
        private static readonly int ReloadSpeedHash = Animator.StringToHash("WeaponReloadSpeed");
        private float crosshairKick;

        public event Action<WeaponItem> WeaponChanged;
        public event Action<WeaponItem> WeaponStateChanged;
        public event Action<WeaponItem> ShotFired;
        public PlayerHealth Health => playerHealth;
        public PlayerWeaponInventory Inventory => inventory != null ? inventory : inventory = GetComponent<PlayerWeaponInventory>();
        public bool IsAiming => CanOperateWeapon() && movement.IsAiming;
        internal Transform WeaponOwnerRoot => OwnerRoot;
        internal bool CanChangeWeaponSelection => isActiveAndEnabled
            && !(GetComponent<Survive.Turrets.PlayerTurretController>()?.BlocksWeaponInput ?? false)
            && movement != null && movement.isActiveAndEnabled && movement.IsPlayer
            && !movement.DisableAllMove && !movement.IsRolling
            && (playerHealth == null || !playerHealth.IsDead) && Time.timeScale > 0f
            && (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);

        public bool IsReloadAnimationActive => weaponAnimator != null && reloadLayer >= 0
            && (weaponAnimator.GetCurrentAnimatorStateInfo(reloadLayer).IsTag("WeaponReload")
                || (weaponAnimator.IsInTransition(reloadLayer)
                    && weaponAnimator.GetNextAnimatorStateInfo(reloadLayer).IsTag("WeaponReload")));

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

        private static InputAction CreateDefaultReloadAction()
        {
            var action = new InputAction("Reload", InputActionType.Button);
            action.AddBinding("<Keyboard>/r");
            action.AddBinding("<Gamepad>/buttonWest");
            return action;
        }

        private static InputAction CreateDefaultDropAction()
        {
            var action = new InputAction("DropWeapon", InputActionType.Button);
            action.AddBinding("<Keyboard>/g");
            action.AddBinding("<Gamepad>/dpad/down");
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
            reloadAction ??= CreateDefaultReloadAction();
            dropAction ??= CreateDefaultDropAction();
            fireAction.Enable();
            reloadAction.Enable();
            dropAction.Enable();
            SubscribeWeapon();
            SyncReloadAnimation();
            WeaponStateChanged?.Invoke(EquippedWeapon);
        }

        private void OnDisable()
        {
            fireAction?.Disable();
            reloadAction?.Disable();
            dropAction?.Disable();
            equippedWeapon?.CancelReload();
            SyncReloadAnimation();
            UnsubscribeWeapon();
            WeaponStateChanged?.Invoke(EquippedWeapon);
            RestoreDebugCrosshair();
            RestoreGripRotation();
        }

        private void OnDestroy()
        {
            fireAction?.Dispose();
            reloadAction?.Dispose();
            dropAction?.Dispose();
            UnsubscribeWeapon();
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

        private void Update()
        {
            // Una capa vacía de cuerpo superior no debe reemplazar la pose de aim.
            // Mantenerla sólo durante la acción y su transición de salida.
            SyncReloadAnimation();
        }

        private void LateUpdate()
        {
            crosshairKick = Mathf.MoveTowards(crosshairKick, 0f, Time.deltaTime * 32f);
            if (EquippedWeapon == null)
            {
                if (!ReferenceEquals(equippedWeapon, null))
                {
                    UnsubscribeWeapon();
                    equippedWeapon = null;
                    WeaponChanged?.Invoke(null);
                    WeaponStateChanged?.Invoke(null);
                }
                equippedWeapon = null;
                RestoreDebugCrosshair();
                return;
            }

            ResolveCamera();
            UpdateDebugCrosshair();
            RestoreGripRotation();
            if (CanOperateWeapon())
            {
                equippedWeapon.TickReload(Time.deltaTime);
                if (dropAction != null && dropAction.WasPressedThisFrame())
                {
                    DropInFront();
                    return;
                }
                if (reloadAction != null && reloadAction.WasPressedThisFrame()) TryReload();
                if (automaticReloadOnEmpty && !equippedWeapon.HasAmmo && !equippedWeapon.IsReloading
                    && equippedWeapon.ReserveAmmo > 0 && Time.time >= autoReloadReadyAt)
                    TryReload();
            }
            else if (movement == null || !movement.isActiveAndEnabled || movement.DisableAllMove
                || movement.IsRolling || (playerHealth != null && playerHealth.IsDead))
            {
                equippedWeapon.CancelReload();
            }
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
            if (Inventory != null) return inventory.TryCollect(weapon);
            if (!isActiveAndEnabled || movement == null || !movement.isActiveAndEnabled
                || !movement.IsPlayer || (playerHealth != null && playerHealth.IsDead)
                || movement.DisableAllMove || movement.IsRolling || Time.timeScale <= 0f
                || (requireLockedCursor && Cursor.lockState != CursorLockMode.Locked)
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
            SubscribeWeapon();
            nextShotTime = Time.time;
            autoReloadReadyAt = Time.time + 0.18f;
            SyncReloadAnimation();
            ResolveCamera();
            UpdateDebugCrosshair();
            WeaponChanged?.Invoke(weapon);
            WeaponStateChanged?.Invoke(weapon);
            return true;
        }

        /// <summary>Reemplaza el arma y deja la anterior en el lugar del pickup, conservando su munición.</summary>
        public bool TrySwapWeapon(WeaponItem weapon)
        {
            if (Inventory != null) return inventory.TryCollect(weapon);
            if (EquippedWeapon == null) return TryEquip(weapon);
            if (!CanOperateWeapon() || weapon == null || !weapon.isActiveAndEnabled || weapon.IsEquipped)
                return false;

            Vector3 worldPosition = weapon.transform.position;
            Quaternion worldRotation = weapon.transform.rotation;
            Transform socket = ResolveSocket();
            if (!weapon.TryAttach(OwnerRoot, socket,
                    weapon.EquippedLocalPosition + attachmentPositionOffset,
                    weapon.EquippedLocalEulerAngles + attachmentEulerOffset)) return false;

            WeaponItem previous = equippedWeapon;
            previous.CancelReload();
            UnsubscribeWeapon();
            previous.PlaceInWorld(worldPosition, worldRotation);
            equippedWeapon = weapon;
            resolvedSocket = socket;
            SubscribeWeapon();
            // La interacción no debe disparar el arma nueva en el mismo cuadro.
            nextShotTime = Time.time + 0.15f;
            autoReloadReadyAt = Time.time + 0.18f;
            SyncReloadAnimation();
            WeaponChanged?.Invoke(weapon);
            WeaponStateChanged?.Invoke(weapon);
            return true;
        }

        /// <summary>Primitiva del inventario: cambia attachment sin pasar por la recogida pública.</summary>
        internal bool TrySetInventorySelection(WeaponItem weapon, Transform storage, bool dropPrevious,
            Vector3 dropPosition, Quaternion dropRotation)
        {
            ResolvePlayerReferences();
            if (!CanChangeWeaponSelection) return false;
            WeaponItem previous = EquippedWeapon;
            if (previous == weapon) return true;
            if (previous != null && (!previous.IsOwnedBy(OwnerRoot) || storage == null
                || (storage != OwnerRoot && !storage.IsChildOf(OwnerRoot)))) return false;
            if (weapon != null && (!weapon.enabled || (!weapon.IsOwnedBy(OwnerRoot)
                && (weapon.IsEquipped || !weapon.isActiveAndEnabled)))) return false;

            Transform socket = weapon != null ? ResolveSocket() : null;
            Transform previousSocket = previous != null ? previous.transform.parent : null;
            Vector3 previousPosition = previous != null ? previous.transform.localPosition : Vector3.zero;
            Vector3 previousEuler = previous != null ? previous.transform.localEulerAngles : Vector3.zero;
            previous?.CancelReload();
            // El arma anterior sigue activa hasta confirmar el attachment nuevo;
            // una referencia inválida no debe hacerla desaparecer.
            if (previous != null && !dropPrevious
                && !previous.TryMoveOwnedAttachment(OwnerRoot, storage, Vector3.zero, Vector3.zero)) return false;

            bool wasActive = weapon != null && weapon.gameObject.activeSelf;
            if (weapon != null)
            {
                if (!wasActive) weapon.gameObject.SetActive(true);
                bool attached = weapon.IsOwnedBy(OwnerRoot)
                    ? weapon.TryMoveOwnedAttachment(OwnerRoot, socket,
                        weapon.EquippedLocalPosition + attachmentPositionOffset,
                        weapon.EquippedLocalEulerAngles + attachmentEulerOffset)
                    : weapon.TryAttach(OwnerRoot, socket,
                        weapon.EquippedLocalPosition + attachmentPositionOffset,
                        weapon.EquippedLocalEulerAngles + attachmentEulerOffset);
                if (!attached)
                {
                    if (!wasActive) weapon.gameObject.SetActive(false);
                    if (previous != null && !dropPrevious)
                        previous.TryMoveOwnedAttachment(OwnerRoot, previousSocket, previousPosition, previousEuler);
                    return false;
                }
            }

            UnsubscribeWeapon();
            if (previous != null)
            {
                if (dropPrevious) previous.PlaceInWorld(dropPosition, dropRotation);
                else previous.gameObject.SetActive(false);
            }
            equippedWeapon = weapon;
            resolvedSocket = socket;
            SubscribeWeapon();
            // Cambiar de slot no dispara en el cuadro de la selección.
            nextShotTime = Time.time + 0.15f;
            autoReloadReadyAt = Time.time + 0.18f;
            SyncReloadAnimation();
            ResolveCamera();
            if (weapon != null) UpdateDebugCrosshair();
            else RestoreDebugCrosshair();
            WeaponChanged?.Invoke(weapon);
            WeaponStateChanged?.Invoke(weapon);
            return true;
        }

        public bool TryReload()
        {
            return CanOperateWeapon() && equippedWeapon.TryStartReload();
        }

        private void DropInFront()
        {
            Vector3 origin = playerCapsule != null ? playerCapsule.bounds.center : OwnerRoot.position + Vector3.up;
            float distance = 1.2f;
            if (TryGetNearestHit(new Ray(origin, OwnerRoot.forward), distance, out RaycastHit obstruction))
                distance = Mathf.Max(0f, obstruction.distance - 0.4f);
            DropEquippedWeapon(OwnerRoot.position + OwnerRoot.forward * distance, Quaternion.identity);
        }

        private void SubscribeWeapon()
        {
            if (equippedWeapon == null) return;
            equippedWeapon.StateChanged -= ForwardWeaponState;
            equippedWeapon.StateChanged += ForwardWeaponState;
        }

        private void UnsubscribeWeapon()
        {
            if (equippedWeapon != null) equippedWeapon.StateChanged -= ForwardWeaponState;
        }

        private void ForwardWeaponState(WeaponItem weapon)
        {
            SyncReloadAnimation();
            WeaponStateChanged?.Invoke(weapon);
        }

        private void SyncReloadAnimation()
        {
            if (weaponAnimator == null) return;
            if (cachedAnimatorController != weaponAnimator.runtimeAnimatorController)
            {
                cachedAnimatorController = weaponAnimator.runtimeAnimatorController;
                reloadLayer = weaponAnimator.GetLayerIndex("Weapon Actions");
                bool hasBool = false, hasSpeed = false;
                foreach (AnimatorControllerParameter parameter in weaponAnimator.parameters)
                {
                    hasBool |= parameter.nameHash == ReloadingHash && parameter.type == AnimatorControllerParameterType.Bool;
                    hasSpeed |= parameter.nameHash == ReloadSpeedHash && parameter.type == AnimatorControllerParameterType.Float;
                }
                hasReloadParameters = hasBool && hasSpeed;
            }
            if (!hasReloadParameters) return;
            WeaponItem weapon = EquippedWeapon;
            float clipDuration = reloadAnimationClip != null ? reloadAnimationClip.length : 2.2f;
            weaponAnimator.SetFloat(ReloadSpeedHash, weapon != null ? clipDuration / weapon.ReloadDuration : 1f);
            bool reloading = isActiveAndEnabled && weapon != null && weapon.IsReloading;
            weaponAnimator.SetBool(ReloadingHash, reloading);
            if (reloadLayer >= 0)
                weaponAnimator.SetLayerWeight(reloadLayer,
                    isActiveAndEnabled && weapon != null && (reloading || IsReloadAnimationActive) ? 1f : 0f);
        }

        /// <summary>Comparte el mismo objetivo balístico con el solver de las manos.</summary>
        public bool TryGetAimPoint(out Vector3 target)
        {
            target = default;
            if (EquippedWeapon == null) return false;
            ResolveCamera();
            return TryGetAimTarget(out _, out target, out _);
        }

        /// <summary>Permite devolver el arma al mapa desde otro sistema sin agregar una tecla de inventario.</summary>
        public bool DropEquippedWeapon(Vector3 position, Quaternion rotation)
        {
            if (Inventory != null) return inventory.TryDropSelected(position, rotation);
            return TryDropInventorySelection(position, rotation);
        }

        internal bool TryDropInventorySelection(Vector3 position, Quaternion rotation)
        {
            if (!CanChangeWeaponSelection) return false;
            WeaponItem weapon = EquippedWeapon;
            if (weapon == null)
            {
                return false;
            }

            weapon.CancelReload();
            UnsubscribeWeapon();
            equippedWeapon = null;
            weapon.PlaceInWorld(position, rotation);
            SyncReloadAnimation();
            RestoreDebugCrosshair();
            WeaponChanged?.Invoke(null);
            WeaponStateChanged?.Invoke(null);
            return true;
        }

        /// <summary>El disparo programático respeta las mismas condiciones y cadencia que el input.</summary>
        public bool TryFire()
        {
            if (!CanAimAndFire() || Time.time < nextShotTime || !equippedWeapon.HasAmmo)
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
            ExplosiveAmmoDefinition explosive = equippedWeapon.LoadedExplosiveAmmo;
            if (!equippedWeapon.TryConsumeRound()) return false;
            GameStatsManager.Instance?.RegisterShot();
            nextShotTime = Time.time + 1f / Mathf.Max(0.01f, equippedWeapon.ShotsPerSecond);
            autoReloadReadyAt = Time.time + 0.18f;
            crosshairKick = Mathf.Min(10f, crosshairKick + 2f);

            // La cámara TPS puede mirar una pared que está detrás del cañón: no se invierte el disparo.
            Vector3 toTarget = target - muzzlePosition;
            if (Vector3.Dot(toTarget, cameraRay.direction) <= 0.001f)
            {
                equippedWeapon.ShowShot(muzzlePosition, muzzlePosition, true);
                ShotFired?.Invoke(equippedWeapon);
                return true;
            }

            if (TryFindMuzzleOverlap(muzzlePosition, out Vector3 blockedPoint))
            {
                equippedWeapon.ShowShot(muzzlePosition, blockedPoint, true);
                ShotFired?.Invoke(equippedWeapon);
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
                equippedWeapon.ShowImpact(nearObstruction);
                if (explosive != null && ExplosiveRoundImpact.Detonate(nearObstruction, explosive, OwnerRoot)) GameStatsManager.Instance?.RegisterHit();
                ShotFired?.Invoke(equippedWeapon);
                return true;
            }

            Vector3 aimDirection = toTarget.normalized;
            int pelletCount = Mathf.Clamp(equippedWeapon.PelletsPerShot, 1, pelletHits.Length);
            for (int pellet = 0; pellet < pelletCount; pellet++)
            {
                Vector3 direction = aimDirection;
                bool hasSpread = equippedWeapon.SpreadDegrees > 0f;
                if (hasSpread)
                {
                    Vector2 spread = UnityEngine.Random.insideUnitCircle
                        * Mathf.Tan(equippedWeapon.SpreadDegrees * Mathf.Deg2Rad);
                    direction = Quaternion.LookRotation(aimDirection) * new Vector3(spread.x, spread.y, 1f).normalized;
                }
                float distance = hasSpread ? range : Mathf.Min(range, toTarget.magnitude);
                // La superficie objetivo puede quedar unas milésimas fuera del rayo por precisión numérica.
                // Sólo se acepta ese margen si sigue siendo el MISMO collider visto por la cámara.
                float queryDistance = hasSpread ? range : Mathf.Min(range, toTarget.magnitude + 0.005f);
                bool hasHit = TryGetNearestHit(new Ray(muzzlePosition, direction), queryDistance, out RaycastHit hit)
                    && (hit.distance <= distance || (cameraHit.collider != null && hit.collider == cameraHit.collider));
                Vector3 endpoint = hasHit ? hit.point : muzzlePosition + direction * distance;
                pelletHits[pellet] = hasHit ? hit : default;

                equippedWeapon.ShowShot(muzzlePosition, endpoint, hasHit, pellet);
                if (hasHit) equippedWeapon.ShowImpact(hit);
            }
            // Resolver toda la geometría antes del daño evita atravesar un enemigo que muere
            // y desactiva sus colliders con el primer perdigón del mismo disparo.
            bool hitEnemy = false;
            for (int pellet = 0; pellet < pelletCount; pellet++)
                if (pelletHits[pellet].collider != null)
                {
                    if (explosive != null)
                    {
                        hitEnemy = ExplosiveRoundImpact.Detonate(pelletHits[pellet], explosive, OwnerRoot);
                        hitMarkerEndsAt = Time.unscaledTime + .12f; break;
                    }
                    var hitHealth = pelletHits[pellet].collider.GetComponentInParent<EnemyHealth>();
                    if (hitHealth != null && !hitHealth.IsDead) hitEnemy = true;
                    ApplyDamage(pelletHits[pellet]);
                }
            if (hitEnemy) GameStatsManager.Instance?.RegisterHit();
            ShotFired?.Invoke(equippedWeapon);
            return true;
        }

        private bool CanAimAndFire()
        {
            return CanOperateWeapon() && movement.IsAiming && !equippedWeapon.IsReloading && !IsReloadAnimationActive;
        }

        private bool CanOperateWeapon()
        {
            return isActiveAndEnabled && EquippedWeapon != null && equippedWeapon.isActiveAndEnabled
                && !(GetComponent<Survive.Turrets.PlayerTurretController>()?.BlocksWeaponInput ?? false)
                && movement != null && movement.isActiveAndEnabled && movement.IsPlayer
                && !movement.DisableAllMove && !movement.IsRolling
                && (playerHealth == null || !playerHealth.IsDead) && Time.timeScale > 0f
                && (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
        }

        private void ResolvePlayerReferences()
        {
            if (inventory == null) inventory = GetComponent<PlayerWeaponInventory>();
            if (weaponAnimator == null) weaponAnimator = GetComponent<Animator>();
            if (weaponPose == null) weaponPose = GetComponent<PlayerWeaponPose>();
            SyncReloadAnimation();
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
            if (!alignWeaponWhileAiming || (weaponPose != null && weaponPose.HandlesAlignment) || EquippedWeapon == null
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
                if (!enemyHealth.IsDead) hitMarkerEndsAt = Time.unscaledTime + 0.12f;
                enemyHealth.TakeDamage(equippedWeapon.Damage, hit.point, OwnerRoot.gameObject);
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
            if (!showCrosshair || !CanOperateWeapon() || !movement.IsAiming || aimCamera == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            Rect viewport = aimCamera.pixelRect;
            float centerX = viewport.x + viewport.width * 0.5f;
            float centerY = Screen.height - viewport.y - viewport.height * 0.5f;
            Color previous = GUI.color;
            GUI.color = Time.unscaledTime < hitMarkerEndsAt ? new Color(1f, 0.35f, 0.2f)
                : equippedWeapon.IsReloading ? new Color(1f, 0.75f, 0.2f) : crosshairColor;
            float gap = 3f + crosshairKick;
            float arm = crosshairSize * 0.65f;
            GUI.DrawTexture(new Rect(centerX - gap - arm, centerY - crosshairThickness * 0.5f,
                arm, crosshairThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX + gap, centerY - crosshairThickness * 0.5f,
                arm, crosshairThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - crosshairThickness * 0.5f, centerY - gap - arm,
                crosshairThickness, arm), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - crosshairThickness * 0.5f, centerY + gap,
                crosshairThickness, arm), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}

