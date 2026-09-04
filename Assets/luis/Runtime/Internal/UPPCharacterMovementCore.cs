using System.Collections.Generic;
using UPP.ThirdPersonController.CameraSystems;
using UnityEngine;
using UnityEngine.Events;

namespace UPP.ThirdPersonController.Internal
{
    /// <summary>
    /// Núcleo del controlador de locomoción UPP.
    /// No conoce inventario, ítems, armas, daño, salud, vehículos ni ragdoll.
    /// </summary>
    public abstract class UPPCharacterMovementCore : MonoBehaviour
    {
        private Animator cachedAnimator;
        private UPPAnimatorControllerComponent cachedAnimatorController;
        private Collider cachedCollider;
        private CapsuleCollider cachedCapsuleCollider;
        private Rigidbody cachedRigidbody;
        private Quaternion forwardOrientation;
        private Quaternion lastDirectionTransformRotation;
        private Vector3 desiredDirection;

        private Collider[] hitBoxes;

        protected RaycastHit _stepHit;
        protected RaycastHit FootStepHit;
        protected bool AdjustHeight;
        private bool goToStepPosition;
        private Vector3 startStepUpCharacterPosition;
        private Vector3 stepPosition;
        private float goingToStepTime;

        public Vector3 UpDirection { get; set; }
        public Quaternion UpOrientation { get; private set; }
        public UPPCameraController MyPivotCamera { get; set; }

        public enum MovementMode
        {
            Free,
            AlwaysInAimMode
        }

        public float VelocityMultiplier { get; set; }
        protected float VerticalY { get; set; }
        protected float HorizontalX { get; set; }
        public Transform DirectionTransform { get; private set; }

        protected Quaternion DesiredCameraRotation;
        protected float LastX;
        protected float LastY;
        protected float LastVelMult;

        [Header("Configuración de movimiento")]
        public MovementMode LocomotionMode;
        public bool SetRigidbodyVelocity = true;
        public float Speed = 3f;
        public float WalkSpeed = 0.5f;
        public float CrouchSpeed = 0.4f;
        public float RunSpeed = 1f;
        public float SprintingSpeedMax = 3f;
        public float SprintingAcceleration = 2f;
        public float SprintingDeceleration = 0.6f;
        public float RotationSpeed = 2f;
        public float JumpForce = 3f;
        public float StoppingSpeed = 2f;
        public float AirInfluenceControll = 0.5f;
        public float MaxWalkableAngle = 45f;
        public bool CurvedMovement = true;
        public bool LerpRotation;
        public bool BodyInclination = true;
        public Vector3 LookAtPosition;

        [Header("Configuración de sprint")]
        public bool SprintingSkill = true;
        protected bool CanSprint = true;
        protected bool ReachedMaxSprintSpeed;
        protected float CurrentSprintSpeedIntensity;
        public bool SprintOnRunButton;
        public bool UnlimitedSprintDuration;

        [Header("Configuración de pendientes")]
        public bool GroundAngleDesaceleration = true;
        public float GroundAngleDesacelerationMultiplier = 1.5f;
        protected float SlidingVelocity;
        public float GroundAngle;
        public Vector3 GroundNormal;
        public Vector3 GroundPoint;

        [Header("Root motion")]
        public bool RootMotion;
        public float RootMotionSpeed = 1f;
        public bool RootMotionRotation;
        public Vector3 RootMotionDeltaPosition;

        [Header("Rodada")]
        [Min(0.1f)] public float RollFailsafeDuration = 2f;

        [Header("Eventos de locomoción")]
        public UPPCharacterMovementEvents Events;

        [Header("Detección de suelo")]
        public LayerMask WhatIsGround;
        public float GroundCheckRadius = 0.1f;
        public float GroundCheckHeighOfsset = 0.1f;
        public float GroundCheckSize = 0.5f;

        [Header("Detección de paredes")]
        public LayerMask WhatIsWall;
        public float WallRayHeight = 1f;
        public float WallRayDistance = 0.6f;

        [Header("Corrección de escalones")]
        public bool EnableStepCorrection = true;
        public float UpStepSpeed = 5f;
        public LayerMask StepCorrectionMask;
        public float FootstepHeight = 0.4f;
        public float ForwardStepOffset = 0.6f;
        public float StepHeight = 0.02f;
        public bool EnableUngroundedStepUp = true;
        public float UngroundedStepUpSpeed = 4f;
        public float UngroundedStepUpRayDistance = 0.1f;
        public float StoppingTimeOnStepPosition = 0.5f;

        [Header("Apuntado direccional")]
        public PressAimMode AimMode;
        public float FireModeWalkSpeed = 0.5f;
        public float FireModeRunSpeed = 1.3f;
        public float FireModeCrouchSpeed = 0.5f;

        public enum PressAimMode
        {
            HoldToAim,
            OnePressToAim
        }

        public Transform HumanoidSpine;
        public Transform RightFootBone { get; private set; }
        public Transform LeftFootBone { get; private set; }
        public bool DisableAllMove { get; set; }
        public bool CanMove { get; set; } = true;
        public bool CanRotate { get; set; } = true;
        public bool IsMoving { get; protected set; }
        public bool IsRunning { get; set; }
        public bool IsSprinting { get; set; }
        public bool IsCrouched { get; set; }
        public bool IsProne { get; protected set; }
        public bool CanJump { get; set; }
        public bool IsJumping { get; set; }
        public bool IsGrounded { get; set; } = true;
        public bool IsSliding { get; set; }
        public bool IsAiming { get; set; }
        public bool FiringMode { get; set; }
        public bool FiringModeIK { get; set; }
        public bool IsRolling { get; set; }
        public bool WallAHead { get; set; }
        public Animator anim
        {
            get
            {
                if (!cachedAnimator)
                {
                    cachedAnimator = GetComponent<Animator>();
                }

                return cachedAnimator;
            }
        }

        public UPPAnimatorControllerComponent AnimatorController
        {
            get
            {
                if (cachedAnimatorController == null)
                {
                    cachedAnimatorController = GetComponent<UPPAnimatorControllerComponent>();
                }

                return cachedAnimatorController;
            }
        }

        public Rigidbody rb
        {
            get
            {
                if (!cachedRigidbody)
                {
                    cachedRigidbody = GetComponent<Rigidbody>();
                }

                return cachedRigidbody;
            }
        }

        public Collider coll
        {
            get
            {
                if (!cachedCollider)
                {
                    cachedCollider = GetComponent<Collider>();
                }

                return cachedCollider;
            }
        }

        public CapsuleCollider CapsuleCollider
        {
            get
            {
                if (!cachedCapsuleCollider)
                {
                    cachedCapsuleCollider = GetComponent<CapsuleCollider>();
                }

                return cachedCapsuleCollider;
            }
        }

        public IEnumerable<Collider> HitBoxes => hitBoxes;
        public bool IsPlayer => gameObject.CompareTag("Player");

        [System.Serializable]
        public struct UPPCharacterMovementEvents
        {
            private bool wasRunning;
            private bool wasMoving;
            private bool wasAiming;

            public UnityEvent OnRun;
            public UnityEvent OnStartRun;
            public UnityEvent OnEndRun;
            public UnityEvent OnSprinting;
            public UnityEvent OnRoll;
            public UnityEvent OnJump;
            public UnityEvent OnCrouch;
            public UnityEvent OnGetUp;
            public UnityEvent OnStartMoving;
            public UnityEvent OnIdle;
            public UnityEvent OnEnterFireMode;
            public UnityEvent OnExitFireMode;

            public void UpdateRuntimeEventsCallbacks(UPPCharacterMovementCore character)
            {
                if (wasRunning != character.IsRunning)
                {
                    wasRunning = character.IsRunning;
                    (wasRunning ? OnStartRun : OnEndRun)?.Invoke();
                }

                if (character.IsRunning)
                {
                    OnRun?.Invoke();
                }

                if (character.IsSprinting)
                {
                    OnSprinting?.Invoke();
                }

                if (wasMoving != character.IsMoving)
                {
                    wasMoving = character.IsMoving;
                    (wasMoving ? OnStartMoving : OnIdle)?.Invoke();
                }

                if (wasAiming != character.FiringMode)
                {
                    wasAiming = character.FiringMode;
                    (wasAiming ? OnEnterFireMode : OnExitFireMode)?.Invoke();
                }
            }

            public void SetEventListeners(UPPCharacterMovementCore character)
            {
                OnRoll?.AddListener(character._Roll);
                OnJump?.AddListener(character._Jump);
                OnCrouch?.AddListener(character._Crouch);
                OnGetUp?.AddListener(character._GetUp);
            }

            public void RemoveEventListeners(UPPCharacterMovementCore character)
            {
                OnRoll?.RemoveListener(character._Roll);
                OnJump?.RemoveListener(character._Jump);
                OnCrouch?.RemoveListener(character._Crouch);
                OnGetUp?.RemoveListener(character._GetUp);
            }
        }

        private void OnEnable()
        {
            Events.SetEventListeners(this);
        }

        private void OnDisable()
        {
            Events.RemoveEventListeners(this);
        }

        protected virtual void Awake()
        {
            CanMove = true;
            CanRotate = true;
            UpDirection = Vector3.up;
            UpOrientation = Quaternion.identity;
            forwardOrientation = Quaternion.identity;
            lastDirectionTransformRotation = Quaternion.identity;
            if (WhatIsGround == 0)
            {
                WhatIsGround = LayerMask.GetMask("Default", "Terrain", "Walls");
            }

            if (WhatIsWall == 0)
            {
                WhatIsWall = LayerMask.GetMask("Default", "Terrain", "Walls");
            }

            DirectionTransform = CreateEmptyTransform(
                "Dirección de movimiento",
                transform.position,
                transform.rotation,
                transform);

            FiringMode = false;
            FiringModeIK = false;
            hitBoxes = GetComponentsInChildren<Collider>();
            foreach (Collider hitBox in hitBoxes)
            {
                if (hitBox != coll)
                {
                    Physics.IgnoreCollision(coll, hitBox);
                }
            }

            if (IsPlayer)
            {
                MyPivotCamera = FindFirstObjectByType<UPPCameraController>();
            }

            if (HumanoidSpine == null)
            {
                HumanoidSpine = GetLastSpineBone();
            }

            LeftFootBone = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            RightFootBone = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        protected virtual void Start()
        {
        }

        public Vector3 GetLookPosition()
        {
            if (LookAtPosition != Vector3.zero)
            {
                return LookAtPosition;
            }

            if (MyPivotCamera && MyPivotCamera.mCamera)
            {
                Transform cameraTransform = MyPivotCamera.mCamera.transform;
                return cameraTransform.position + cameraTransform.forward * 100f;
            }

            return transform.position + transform.forward * 100f;
        }

        public Vector3 GetLookDirectionEulerAngles()
        {
            Vector3 origin = HumanoidSpine ? HumanoidSpine.position : transform.position;
            Vector3 direction = GetLookPosition() - origin;
            return direction.sqrMagnitude < 0.0001f
                ? transform.eulerAngles
                : Quaternion.LookRotation(direction.normalized, transform.up).eulerAngles;
        }

        public void SetForwardOrientation(Quaternion forwardRotation)
        {
            forwardOrientation = forwardRotation;
        }

        public Quaternion GetForwardOrientation()
        {
            if (!IsPlayer && MyPivotCamera != null)
            {
                MyPivotCamera = null;
            }

            if (forwardOrientation != Quaternion.identity)
            {
                return forwardOrientation;
            }

            if (MyPivotCamera != null && MyPivotCamera.mCamera != null)
            {
                return MyPivotCamera.mCamera.transform.rotation;
            }

            return transform.rotation;
        }

        public Transform GetLastSpineBone()
        {
            if (anim == null || !anim.isHuman)
            {
                return null;
            }

            return anim.GetBoneTransform(HumanBodyBones.UpperChest)
                ?? anim.GetBoneTransform(HumanBodyBones.Chest)
                ?? anim.GetBoneTransform(HumanBodyBones.Spine);
        }

        public RaycastHit StepHit => _stepHit;

        internal Vector2 GetAimMovementBlendTreeInput()
        {
            Vector3 value = WordSpaceToBlendTreeSpace(
                GetLookPosition(),
                DirectionTransform);
            return new Vector2(value.x, value.z);
        }

        internal float UpdateMovingTurn(float currentValue)
        {
            CalculateBodyRotation(ref currentValue);
            return currentValue;
        }

        internal float UpdateIdleTurn(float currentValue)
        {
            CalculateRotationIntensity(ref currentValue);
            return currentValue;
        }

        public void PhysicalIgnore(GameObject objectToIgnore, bool ignore)
        {
            if (objectToIgnore == null || hitBoxes == null)
            {
                return;
            }

            foreach (Collider externalCollider
                     in objectToIgnore.GetComponentsInChildren<Collider>(true))
            {
                PhysicalIgnore(externalCollider, ignore);
            }
        }

        public void PhysicalIgnore(Collider colliderToIgnore, bool ignore)
        {
            if (colliderToIgnore == null || hitBoxes == null)
            {
                return;
            }

            foreach (Collider hitBox in hitBoxes)
            {
                if (hitBox != null)
                {
                    Physics.IgnoreCollision(colliderToIgnore, hitBox, ignore);
                }
            }
        }

        protected Transform CreateEmptyTransform(
            string objectName = "Nuevo Transform",
            Vector3 position = default,
            Quaternion rotation = default,
            Transform parent = null,
            bool hide = false)
        {
            Transform newTransform = new GameObject(objectName).transform;
            newTransform.SetPositionAndRotation(position, rotation);
            newTransform.SetParent(parent);
            if (hide)
            {
                newTransform.hideFlags = HideFlags.HideInHierarchy;
                newTransform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            return newTransform;
        }

        public void SetMoveInput(
            float horizontalInput,
            float verticalInput,
            float smooth = -1f)
        {
            if (smooth <= 0f)
            {
                HorizontalX = horizontalInput;
                VerticalY = verticalInput;
                return;
            }

            HorizontalX = Mathf.Lerp(
                HorizontalX,
                horizontalInput,
                smooth * Time.deltaTime);
            VerticalY = Mathf.Lerp(
                VerticalY,
                verticalInput,
                smooth * Time.deltaTime);
        }
        public virtual void Rotate(float horizontalInput, float verticalInput)
        {
            if (!CanRotate)
            {
                return;
            }

            DesiredCameraRotation = GetForwardOrientation();
            desiredDirection = new Vector3(horizontalInput, 0f, verticalInput);
            Vector3 desiredEulerAngles = transform.localEulerAngles;

            if (IsMoving && desiredDirection.sqrMagnitude > 0.0001f)
            {
                DirectionTransform.rotation =
                    DesiredCameraRotation * Quaternion.LookRotation(desiredDirection.normalized);
                if (Vector3.Dot(transform.up, Vector3.up) < -0.989f)
                {
                    DirectionTransform.rotation = lastDirectionTransformRotation;
                }
                else
                {
                    lastDirectionTransformRotation = DirectionTransform.rotation;
                }

                float stanceMultiplier = IsProne || IsCrouched ? 0.5f : 1f;
                desiredEulerAngles.y = LerpRotation
                    ? Mathf.LerpAngle(
                        desiredEulerAngles.y,
                        DirectionTransform.eulerAngles.y,
                        stanceMultiplier * RotationSpeed * Time.deltaTime)
                    : Mathf.MoveTowardsAngle(
                        desiredEulerAngles.y,
                        DirectionTransform.eulerAngles.y,
                        stanceMultiplier * 100f * RotationSpeed * Time.deltaTime);
            }

            if (FiringMode && !IsRolling)
            {
                LookRotationToAimPosition(
                    GetLookPosition(),
                    RotationSpeed,
                    UpOrientation * Vector3.up);
            }
            else if (!RootMotionRotation || !RootMotion)
            {
                if (CurvedMovement)
                {
                    transform.localEulerAngles = desiredEulerAngles;
                }
                else if (desiredDirection.sqrMagnitude > 0.0001f)
                {
                    DirectionTransform.rotation =
                        Quaternion.FromToRotation(DirectionTransform.up, UpDirection)
                        * DirectionTransform.rotation;
                    transform.rotation = Quaternion.Lerp(
                        transform.rotation,
                        DirectionTransform.rotation,
                        (IsRolling ? 1.5f : 1f) * RotationSpeed * Time.deltaTime);
                }
            }

            Quaternion upDirection = Quaternion.FromToRotation(
                transform.up,
                IsProne ? GroundNormal : UpDirection);
            UpDirection = GroundNormal == Vector3.zero ? Vector3.up : UpDirection;
            UpOrientation = Quaternion.Lerp(
                transform.rotation,
                upDirection * transform.rotation,
                (IsGrounded ? 8f : 2f) * Time.deltaTime);
            transform.rotation = UpOrientation;
            DirectionTransform.rotation =
                Quaternion.FromToRotation(DirectionTransform.up, UpDirection)
                * DirectionTransform.rotation;
        }

        public virtual void DoLookAt(
            Vector3 targetPosition = default,
            float rotationSpeedMultiplier = 1f,
            bool freezeUpDirection = true)
        {
            Vector3 direction = targetPosition - transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                rotationSpeedMultiplier * RotationSpeed * Time.deltaTime);
            if (freezeUpDirection)
            {
                transform.rotation =
                    Quaternion.FromToRotation(transform.up, UpDirection)
                    * transform.rotation;
            }
        }

        public virtual void MoveForward(float speedMultiplier)
        {
            if (SetRigidbodyVelocity)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
                rb.linearVelocity =
                    transform.forward * speedMultiplier * Speed
                    + transform.up * localVelocity.y;
            }
            else
            {
                transform.Translate(
                    Vector3.forward * speedMultiplier * Speed * Time.deltaTime,
                    Space.Self);
            }
        }

        public virtual void Move(Vector3 movement, float speedMultiplier)
        {
            if (SetRigidbodyVelocity)
            {
                rb.linearVelocity = movement * speedMultiplier * Speed;
            }
            else
            {
                transform.Translate(
                    movement * speedMultiplier * Speed * Time.deltaTime,
                    Space.World);
            }
        }

        public virtual void Move(Transform movementDirection, float speedMultiplier)
        {
            if (SetRigidbodyVelocity)
            {
                Vector3 localVelocity =
                    movementDirection.InverseTransformDirection(rb.linearVelocity);
                rb.linearVelocity =
                    movementDirection.forward * speedMultiplier * Speed
                    + transform.up * localVelocity.y;
            }
            else
            {
                transform.Translate(
                    movementDirection.forward * speedMultiplier * Speed * Time.deltaTime,
                    Space.World);
            }
        }

        public virtual void MoveDirectional(float speedMultiplier)
        {
            if (SetRigidbodyVelocity)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
                rb.linearVelocity =
                    DirectionTransform.forward * speedMultiplier * Speed
                    + transform.up * localVelocity.y;
            }
            else
            {
                transform.Translate(
                    DirectionTransform.forward * speedMultiplier * Speed * Time.deltaTime,
                    Space.World);
            }
        }

        public void InAirMovementControl(bool jumpInert = true)
        {
            if (IsGrounded)
            {
                if (jumpInert)
                {
                    LastX = HorizontalX;
                    LastY = VerticalY;
                    LastVelMult = VelocityMultiplier;
                    CanMove = true;
                }

                return;
            }

            transform.Translate(0f, -Time.deltaTime, 0f);
            if (IsMoving)
            {
                transform.Translate(
                    DirectionTransform.forward
                    * AirInfluenceControll
                    * 0.5f
                    * Time.deltaTime,
                    Space.World);
            }
        }

        protected virtual void LookRotationToAimPosition(
            Vector3 position,
            float rotationSpeed = 10f,
            Vector3 upDirection = default)
        {
            if (IsRolling)
            {
                return;
            }

            Vector3 lookingDirection = (position - transform.position).normalized;
            if (lookingDirection.sqrMagnitude <= 0f)
            {
                return;
            }

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.FromToRotation(transform.forward, lookingDirection)
                * transform.rotation,
                3f * rotationSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.FromToRotation(
                    transform.up,
                    upDirection != Vector3.zero ? upDirection : Vector3.up)
                * transform.rotation;
        }

        protected virtual void DoFireModeMovement(bool firingMode)
        {
            if (!firingMode || IsRolling)
            {
                return;
            }

            if (CanMove && IsGrounded)
            {
                MoveDirectional(VelocityMultiplier);
            }

            if (IsMoving && IsGrounded && !WallAHead)
            {
                float targetSpeed = IsCrouched
                    ? FireModeCrouchSpeed
                    : IsRunning
                        ? FireModeRunSpeed
                        : FireModeWalkSpeed;
                VelocityMultiplier = Mathf.Lerp(
                    VelocityMultiplier,
                    targetSpeed - GroundAngleDesacelerationValue(),
                    5f * Time.deltaTime);
            }
            else if (!IsMoving)
            {
                VelocityMultiplier = Mathf.Lerp(
                    VelocityMultiplier,
                    0f,
                    5f * Time.deltaTime);
            }

            ReachedMaxSprintSpeed = false;
            CanSprint = true;
            CurrentSprintSpeedIntensity = 0f;
            IsSprinting = false;
        }

        protected virtual void DoFreeMovement(bool firingMode)
        {
            if (firingMode)
            {
                return;
            }

            if (IsGrounded && CanMove && !RootMotion)
            {
                if (CurvedMovement)
                {
                    MoveForward(VelocityMultiplier);
                }
                else
                {
                    Move(DirectionTransform, VelocityMultiplier);
                }
            }

            Sprinting();
            if (IsMoving)
            {
                float targetSpeed = IsCrouched && !IsRunning
                    ? CrouchSpeed
                    : IsRunning
                        ? RunSpeed
                        : WalkSpeed;
                if (!WallAHead)
                {
                    VelocityMultiplier = Mathf.Lerp(
                        VelocityMultiplier,
                        targetSpeed - GroundAngleDesacelerationValue(),
                        6f * Time.deltaTime);
                }
            }
            else
            {
                if (IsGrounded)
                {
                    VelocityMultiplier = Mathf.MoveTowards(
                        VelocityMultiplier,
                        0f,
                        (StoppingSpeed + Mathf.Lerp(0f, 0.5f, VelocityMultiplier))
                        * Time.deltaTime);
                }

                IsRunning = false;
                IsSprinting = false;
                ReachedMaxSprintSpeed = false;
                CurrentSprintSpeedIntensity = Mathf.MoveTowards(
                    CurrentSprintSpeedIntensity,
                    0f,
                    3f * Time.deltaTime);
                CanSprint = true;
            }
        }

        protected Vector3 WordSpaceToBlendTreeSpace(
            Vector3 lookAtPosition,
            Transform directionTransform)
        {
            Vector3 inputAxis = directionTransform.forward;
            if (inputAxis.sqrMagnitude <= 0f)
            {
                return inputAxis;
            }

            Vector3 normalizedLook = (lookAtPosition - transform.position).normalized;
            float forwardMagnitude = Mathf.Clamp(
                Vector3.Dot(inputAxis, normalizedLook),
                -1f,
                1f);
            float lateralMagnitude = Mathf.Clamp(
                Vector3.Dot(inputAxis, transform.right),
                -1f,
                1f);
            return new Vector3(lateralMagnitude, 0f, forwardMagnitude).normalized;
        }

        protected virtual void CalculateBodyRotation(ref float bodyRotation)
        {
            if (IsMoving && BodyInclination && CanMove && !WallAHead && IsGrounded)
            {
                bodyRotation = Mathf.LerpAngle(
                    bodyRotation,
                    DesiredRotationAngle() / 180f,
                    2.5f * Time.deltaTime);
                if (Mathf.Abs(DesiredRotationAngle()) < 10f)
                {
                    bodyRotation = Mathf.LerpAngle(
                        bodyRotation,
                        0f,
                        2f * Time.deltaTime);
                }
            }
            else
            {
                bodyRotation = Mathf.Lerp(bodyRotation, 0f, 8f * Time.deltaTime);
            }
        }

        private Vector3 oldEulerAngles;

        public void CalculateRotationIntensity(
            ref float rotationIntensity,
            float multiplier = 2f)
        {
            float difference = multiplier * Vector3.SignedAngle(
                transform.forward,
                Quaternion.Euler(oldEulerAngles) * Vector3.forward,
                transform.up);
            rotationIntensity = Mathf.LerpAngle(
                rotationIntensity,
                difference,
                5f * Time.deltaTime);
            oldEulerAngles = transform.eulerAngles;
        }

        protected float DesiredRotationAngle()
        {
            return Vector3.SignedAngle(
                transform.forward,
                DirectionTransform.forward,
                transform.up);
        }
        protected virtual void Sprinting()
        {
            if (!SprintingSkill)
            {
                return;
            }

            if (FiringMode)
            {
                CurrentSprintSpeedIntensity = 0f;
                ReachedMaxSprintSpeed = true;
            }

            if (IsRunning && IsSprinting)
            {
                if (CurrentSprintSpeedIntensity >= SprintingSpeedMax
                    && !UnlimitedSprintDuration)
                {
                    ReachedMaxSprintSpeed = true;
                }

                if (VelocityMultiplier <= SprintingSpeedMax
                    && !ReachedMaxSprintSpeed)
                {
                    CurrentSprintSpeedIntensity = Mathf.Lerp(
                        CurrentSprintSpeedIntensity,
                        SprintingSpeedMax + 0.3f,
                        SprintingAcceleration * Time.deltaTime);
                    VelocityMultiplier = Mathf.Lerp(
                        VelocityMultiplier,
                        CurrentSprintSpeedIntensity
                        - GroundAngleDesacelerationValue(),
                        10f * Time.deltaTime);
                }

                if (ReachedMaxSprintSpeed)
                {
                    CurrentSprintSpeedIntensity -=
                        SprintingDeceleration * Time.deltaTime;
                    VelocityMultiplier = CurrentSprintSpeedIntensity;
                    if (VelocityMultiplier < RunSpeed)
                    {
                        CanSprint = false;
                        IsSprinting = false;
                        ReachedMaxSprintSpeed = false;
                        CurrentSprintSpeedIntensity = RunSpeed;
                    }
                }
            }

            if (IsRunning && CanSprint && !IsSprinting && !SprintOnRunButton)
            {
                IsSprinting = true;
            }

            CurrentSprintSpeedIntensity = Mathf.Clamp(
                CurrentSprintSpeedIntensity,
                RunSpeed,
                SprintingSpeedMax);
        }

        protected virtual void GroundCheck()
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = false;
            Collider[] overlaps = Physics.OverlapBox(
                transform.position + transform.up * GroundCheckHeighOfsset,
                new Vector3(GroundCheckRadius, GroundCheckSize, GroundCheckRadius),
                transform.rotation,
                WhatIsGround,
                QueryTriggerInteraction.Ignore);

            bool touchesExternalGround = false;
            foreach (Collider overlap in overlaps)
            {
                if (overlap != null && !IsOwnCollider(overlap))
                {
                    touchesExternalGround = true;
                    break;
                }
            }

            if (touchesExternalGround && !IsJumping)
            {
                IsGrounded = true;
            }
            else if (wasGrounded && !SetRigidbodyVelocity && !AdjustHeight)
            {
                rb.AddForce(
                    DirectionTransform.forward * LastVelMult * rb.mass * Speed,
                    ForceMode.Impulse);
            }

            if (TryGetExternalHit(
                    transform.position + transform.up * 0.5f,
                    -transform.up,
                    out RaycastHit hit,
                    2f,
                    WhatIsGround))
            {
                GroundAngle = Vector3.Angle(Vector3.up, hit.normal);
                GroundNormal = hit.normal;
                GroundPoint = hit.point;
            }
            else
            {
                GroundNormal = Vector3.zero;
                GroundAngle = 0f;
                GroundPoint = Vector3.zero;
            }
        }

        protected Vector3 GetGroundPoint()
        {
            if (TryGetExternalHit(
                    transform.position + transform.up * 0.5f,
                    -transform.up,
                    out RaycastHit hit,
                    1000f,
                    WhatIsGround))
            {
                GroundPoint = hit.point;
            }
            else
            {
                GroundPoint = Vector3.zero;
            }

            return GroundPoint;
        }

        private bool TryGetExternalHit(
            Vector3 origin,
            Vector3 direction,
            out RaycastHit nearestHit,
            float distance,
            LayerMask mask)
        {
            nearestHit = default;
            float nearestDistance = float.PositiveInfinity;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                distance,
                mask,
                QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null
                    || IsOwnCollider(hit.collider)
                    || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestHit = hit;
                nearestDistance = hit.distance;
            }

            return nearestDistance < float.PositiveInfinity;
        }

        private bool IsOwnCollider(Collider candidate)
        {
            return candidate == coll
                || candidate.transform.IsChildOf(transform);
        }

        protected virtual void WallAHeadCheck()
        {
            WallAHead = Physics.Raycast(
                transform.position + transform.up * WallRayHeight,
                DirectionTransform.forward,
                out RaycastHit hit,
                WallRayDistance,
                WhatIsWall);

            if (WallAHead)
            {
                VelocityMultiplier = Mathf.Lerp(
                    VelocityMultiplier,
                    0f,
                    10f * Time.deltaTime);
                CurrentSprintSpeedIntensity = 0f;
            }
        }

        protected virtual void SlopeSlide()
        {
            if (GroundAngle > MaxWalkableAngle && IsGrounded)
            {
                IsSliding = true;
                if (MaxWalkableAngle > 0f)
                {
                    SlidingVelocity += Physics.gravity.y * Time.deltaTime;
                    transform.Translate(
                        -GroundNormal * SlidingVelocity * Time.deltaTime,
                        Space.World);
                    transform.Translate(
                        Vector3.up * SlidingVelocity * Time.deltaTime,
                        Space.World);
                }
            }
            else
            {
                SlidingVelocity = 0f;
                IsSliding = false;
            }
        }

        protected float StepAngle()
        {
            return _stepHit.point == Vector3.zero
                ? 0f
                : Vector3.Angle(transform.up, _stepHit.normal);
        }

        public float GroundAngleDesacelerationValue()
        {
            if (!GroundAngleDesaceleration || IsProne)
            {
                return 0f;
            }

            float clampedAngle = Mathf.Clamp(GroundAngle, 0f, 60f);
            float value = IsRunning
                ? clampedAngle / 200f
                : clampedAngle / 700f;
            return value * GroundAngleDesacelerationMultiplier;
        }

        protected virtual void StepCorrectionCalculation()
        {
            if (!CanMove)
            {
                return;
            }

            int stepMask = StepCorrectionMask.value == 0
                ? WhatIsGround.value
                : StepCorrectionMask.value;

            if (IsMoving
                && EnableUngroundedStepUp
                && !IsGrounded
                && LeftFootBone != null
                && RightFootBone != null
                && UngroundedStepUpSpeed > 0f
                && !WallAHead
                && Physics.SphereCast(
                    transform.position
                    + transform.up * FootstepHeight
                    + transform.forward * ForwardStepOffset,
                    CapsuleCollider.radius * 0.5f,
                    -transform.up,
                    out FootStepHit,
                    UngroundedStepUpRayDistance,
                    stepMask,
                    QueryTriggerInteraction.Ignore)
                && !goToStepPosition
                && FootStepHit.point.y > GroundPoint.y + StepHeight
                && FootStepHit.point.y > transform.position.y + StepHeight)
            {
                stepPosition = FootStepHit.point;
                goToStepPosition = true;
                startStepUpCharacterPosition = transform.position;
            }

            if (IsMoving
                && EnableStepCorrection
                && IsGrounded
                && !WallAHead
                && Physics.Raycast(
                    transform.position
                    + transform.up * FootstepHeight
                    + DirectionTransform.forward * ForwardStepOffset,
                    -Vector3.up,
                    out _stepHit,
                    FootstepHeight - StepHeight,
                    stepMask,
                    QueryTriggerInteraction.Ignore)
                && !AdjustHeight)
            {
                AdjustHeight =
                    _stepHit.point.y > transform.position.y
                    && StepAngle() < 10f;
            }
            else if (!AdjustHeight)
            {
                _stepHit.point = transform.position;
            }
        }

        protected virtual void StepCorrectionMovement()
        {
            if (goToStepPosition && EnableUngroundedStepUp)
            {
                goingToStepTime = Mathf.MoveTowards(
                    goingToStepTime,
                    1f + StoppingTimeOnStepPosition,
                    UngroundedStepUpSpeed * Time.deltaTime);
                transform.position = Vector3.Slerp(
                    startStepUpCharacterPosition,
                    stepPosition,
                    goingToStepTime);

                if (!IsJumping)
                {
                    AnimatorController?.SetGroundContact(true, 1.5f);
                }

                if (goingToStepTime >= 1f + StoppingTimeOnStepPosition)
                {
                    goToStepPosition = false;
                    goingToStepTime = 0f;
                    startStepUpCharacterPosition = transform.position;
                }
                else
                {
                    VelocityMultiplier = 0f;
                }

                return;
            }

            if (!AdjustHeight)
            {
                return;
            }

            Vector3 targetPosition = transform.position;
            if (_stepHit.collider)
            {
                targetPosition.y = _stepHit.point.y;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                UpStepSpeed * Time.deltaTime);
            if (Mathf.Abs(transform.position.y - targetPosition.y) <= 0.001f)
            {
                _stepHit.point = transform.position;
                AdjustHeight = false;
            }
        }

        protected virtual void ApplyRootMotionOnLocomotion()
        {
            if (!RootMotion || !IsGrounded || IsJumping || FiringMode)
            {
                return;
            }

            RootMotionDeltaPosition = Vector3.ProjectOnPlane(
                anim.deltaPosition,
                transform.up);
            float animationDeltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 horizontalVelocity =
                RootMotionDeltaPosition
                / animationDeltaTime
                * RootMotionSpeed;
            float verticalVelocity = Vector3.Dot(
                rb.linearVelocity,
                transform.up);
            rb.linearVelocity =
                horizontalVelocity
                + transform.up * verticalVelocity;

            if (RootMotionRotation)
            {
                rb.MoveRotation(rb.rotation * anim.deltaRotation);
            }
        }

        public virtual void _Move(
            float horizontalInput,
            float verticalInput,
            bool running)
        {
            HorizontalX = horizontalInput;
            VerticalY = verticalInput;
            IsRunning = running;
        }

        public virtual void _Jump()
        {
            if (!IsGrounded || IsJumping || IsRolling || !CanJump || IsProne)
            {
                _GetUp();
                return;
            }

            if (anim.GetCurrentAnimatorStateInfo(0)
                    .IsName("Prone Free Locomotion BlendTree")
                || anim.GetCurrentAnimatorStateInfo(0).IsName("CrouchToProne")
                || anim.GetCurrentAnimatorStateInfo(0)
                    .IsName("Prone FireMode BlendTree")
                || anim.GetCurrentAnimatorStateInfo(0).IsName("Prone To Crouch"))
            {
                return;
            }

            IsGrounded = false;
            IsJumping = true;
            CanJump = false;
            IsCrouched = false;
            rb.AddForce(transform.up * 200f * JumpForce, ForceMode.Impulse);
            if (!SetRigidbodyVelocity)
            {
                rb.AddForce(
                    DirectionTransform.forward * LastVelMult * rb.mass * Speed,
                    ForceMode.Impulse);
                VelocityMultiplier = 0f;
            }

            Invoke(nameof(DisableJumpState), 0.3f);
        }

        public virtual void _NewJumpDelay(
            float delay = 0.3f,
            bool jumpDecreaseSpeed = false)
        {
            if (!CanJump
                && !IsJumping
                && IsGrounded
                && !IsInvoking(nameof(EnableJump)))
            {
                if (jumpDecreaseSpeed)
                {
                    VelocityMultiplier *= 0.25f;
                }

                Invoke(nameof(EnableJump), delay);
            }
        }

        public virtual void _Crouch()
        {
            if (!IsGrounded)
            {
                return;
            }

            IsProne = false;
            IsCrouched = true;
        }

        public virtual void _Prone()
        {
            if (!IsGrounded)
            {
                return;
            }

            IsCrouched = true;
            IsProne = true;
        }

        public virtual void _GetUp()
        {
            if (IsProne)
            {
                IsCrouched = true;
                IsProne = false;
            }
            else
            {
                IsCrouched = false;
                IsProne = false;
            }
        }

        public virtual void _Roll()
        {
            if (!IsGrounded
                || IsRolling
                || IsProne
                || !CanMove
                || DisableAllMove
                || AnimatorController == null
                || !AnimatorController.CanTriggerRoll)
            {
                return;
            }

            AnimatorController.TriggerRoll();
            IsRolling = true;
            CancelInvoke(nameof(stopRolling));
            Invoke(nameof(stopRolling), RollFailsafeDuration);
        }

        public void disableMove()
        {
            CanMove = false;
        }

        public void enableMove()
        {
            CanMove = true;
            DisableAllMove = false;
        }

        public void disableRotation()
        {
            CanRotate = false;
        }

        public void enableRotation()
        {
            CanRotate = true;
        }

        public void disableFireModeIK()
        {
            FiringModeIK = false;
        }

        public void enableFireModeIK()
        {
            FiringModeIK = FiringMode;
        }

        public void stopRolling()
        {
            CancelInvoke(nameof(stopRolling));
            IsRolling = false;
            if (!DisableAllMove)
            {
                CanMove = true;
            }

            enableFireModeIK();
        }

        public void startRolling()
        {
            IsRolling = true;
            CanMove = false;
            disableFireModeIK();
        }

        private void DisableJumpState()
        {
            IsJumping = false;
        }

        private void EnableJump()
        {
            CanJump = true;
        }
    }
}
