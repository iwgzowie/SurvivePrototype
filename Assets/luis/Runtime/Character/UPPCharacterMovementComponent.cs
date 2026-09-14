using UPP.ThirdPersonController.Internal;
using UPP.ThirdPersonController.UPPInputSystem;
using UnityEngine;

namespace UPP.ThirdPersonController
{
    /// <summary>
    /// Controla el input, los estados y la física de locomoción del personaje.
    /// No conoce inventario, armas, daño ni otros dominios de gameplay.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("UPP/Personaje/Movimiento del personaje")]
    [RequireComponent(typeof(Rigidbody), typeof(Animator), typeof(CapsuleCollider))]
    [RequireComponent(typeof(UPPAnimatorControllerComponent))]
    [RequireComponent(typeof(UPPIKCharacterComponent))]
    public sealed class UPPCharacterMovementComponent : UPPCharacterMovementCore
    {
        [Header("Entrada")]
        public UPPPlayerCharacterInputAsset Inputs;

        [Header("Opciones del controlador")]
        public bool UseDefaultControllerInput = true;
        public bool AutoRun = true;
        public bool WalkOnRunButton = true;
        public bool DecreaseSpeedOnJump;
        public bool BlockVerticalInput;
        public bool BlockHorizontalInput;
        public bool BlockFireModeOnCursorVisible;
        public bool EnableRoll = true;
        public bool EnableAim = true;
        public bool EnableProne = true;

        [Header("Cápsula adaptativa")]
        public CapsuleCollider CapsuleToResize;
        public float HeightOffset = 0.73f;
        public float ProneAndRollCenterY = 0.38f;
        public Vector3 CenterOffset;

        private float initialCapsuleHeight;
        private Transform headBone;

        protected override void Awake()
        {
            base.Awake();
            InitializeCapsule();
        }

        protected override void Start()
        {
            base.Start();
            if (Inputs != null)
            {
                Inputs.SetActiveInputs(true);
            }
        }

        private void FixedUpdate()
        {
            if (Time.timeScale == 0f)
            {
                return;
            }

            // El sensado y las escrituras del cuerpo comparten el paso de física.
            GroundCheck();
            WallAHeadCheck();
            if (!DisableAllMove)
            {
                StepCorrectionCalculation();
                Rotate(HorizontalX, VerticalY);
            }

            UpdateCapsule();
            if (DisableAllMove)
            {
                return;
            }

            Movement();
            SlopeSlide();
            StepCorrectionMovement();
        }

        private void Update()
        {
            if (Time.timeScale == 0f)
            {
                return;
            }

            if (!DisableAllMove)
            {
                ControllerInputs();
            }

            Events.UpdateRuntimeEventsCallbacks(this);
        }

        /// <summary>
        /// Traduce el asset de entrada al estado de locomoción y apuntado.
        /// </summary>
        public void ControllerInputs()
        {
            if (!UseDefaultControllerInput)
            {
                return;
            }

            if (Inputs == null)
            {
                Debug.LogError(
                    $"El personaje {name} no tiene asignado un " +
                    $"{nameof(UPPPlayerCharacterInputAsset)}.",
                    this);
                return;
            }

            Vector2 moveAxis = Inputs.MoveAxis;
            if (BlockHorizontalInput)
            {
                moveAxis.x = 0f;
            }

            if (BlockVerticalInput)
            {
                moveAxis.y = 0f;
            }

            HorizontalX = moveAxis.x;
            VerticalY = moveAxis.y;

            if (Inputs.IsCrouchTriggered)
            {
                StopSprint();
                if (!IsCrouched || IsProne)
                {
                    _Crouch();
                }
                else
                {
                    _GetUp();
                }
            }

            if (EnableProne && Inputs.IsProneTriggered)
            {
                StopSprint();
                if (!IsProne)
                {
                    _Prone();
                }
                else
                {
                    _GetUp();
                    _GetUp();
                }
            }

            if (MaxWalkableAngle > 0f
                && GroundAngle > MaxWalkableAngle / 1.5f
                && IsProne)
            {
                _GetUp();
            }

            if (IsCrouched && Inputs.IsRunPressed)
            {
                _GetUp();
            }

            if (Inputs.IsJumpTriggered && !IsJumping)
            {
                _Jump();
            }

            _NewJumpDelay(0.2f, DecreaseSpeedOnJump);

            if (EnableRoll && Inputs.IsRollTriggered)
            {
                _Roll();
            }

            UpdateRunning(Inputs.IsRunPressed);
            UpdateAim(
                EnableAim && Inputs.IsAimPressed,
                EnableAim && Inputs.IsAimTriggered);
        }

        private void StopSprint()
        {
            IsRunning = false;
            IsSprinting = false;
            ReachedMaxSprintSpeed = false;
            CanSprint = true;
            CurrentSprintSpeedIntensity = 0f;
        }

        private void UpdateRunning(bool runPressed)
        {
            bool hasDirectionalInput =
                Mathf.Abs(HorizontalX) > 0.5f
                || Mathf.Abs(VerticalY) > 0.5f;

            if (runPressed)
            {
                IsRunning = !WalkOnRunButton;
                if (SprintingSkill
                    && SprintOnRunButton
                    && !FiringMode
                    && !IsCrouched
                    && CanSprint
                    && hasDirectionalInput)
                {
                    IsRunning = true;
                    IsSprinting = true;
                }
            }
            else
            {
                IsRunning = false;
                if (SprintingSkill && SprintOnRunButton)
                {
                    IsSprinting = false;
                }
            }

            if (SprintingSkill
                && IsSprinting
                && UnlimitedSprintDuration
                && !FiringMode)
            {
                ReachedMaxSprintSpeed = false;
            }

            if (AutoRun && !IsCrouched && hasDirectionalInput)
            {
                if (!WalkOnRunButton || !runPressed)
                {
                    IsRunning = true;
                }
            }
        }

        private void UpdateAim(bool aimPressed, bool aimTriggered)
        {
            if (LocomotionMode == MovementMode.AlwaysInAimMode)
            {
                IsAiming = true;
            }
            else if (!EnableAim)
            {
                IsAiming = false;
            }
            else if (AimMode == PressAimMode.OnePressToAim && aimTriggered)
            {
                IsAiming = !IsAiming;
            }
            else if (AimMode == PressAimMode.HoldToAim)
            {
                IsAiming = aimPressed;
            }

            if (BlockFireModeOnCursorVisible && Cursor.visible)
            {
                IsAiming = false;
            }

            if (IsRolling || DisableAllMove)
            {
                IsAiming = false;
            }

            FiringMode = IsAiming;
            FiringModeIK = IsAiming && !IsRolling;
            if (FiringMode)
            {
                StopSprint();
            }
        }

        private void Movement()
        {
            UpdateLocomotionMode();
            anim.applyRootMotion =
                RootMotion
                && IsGrounded
                && !IsJumping
                && !IsRolling
                && !FiringMode;

            if (IsRolling)
            {
                if (CurvedMovement)
                {
                    MoveForward(1.5f);
                }
                else
                {
                    MoveDirectional(1.5f);
                }
            }

            if (!CanMove)
            {
                VelocityMultiplier = 0f;
                return;
            }

            InAirMovementControl(true);
            IsMoving =
                Mathf.Abs(VerticalY) > 0.0001f
                || Mathf.Abs(HorizontalX) > 0.0001f;
            DoFreeMovement(FiringMode);
            DoFireModeMovement(FiringMode);
        }

        private void UpdateLocomotionMode()
        {
            if (LocomotionMode != MovementMode.AlwaysInAimMode)
            {
                return;
            }

            IsAiming = true;
            FiringMode = true;
            FiringModeIK = !IsRolling;
        }

        public void DisableLocomotion(float duration = 0f)
        {
            CancelInvoke(nameof(enableMove));
            HorizontalX = 0f;
            VerticalY = 0f;
            VelocityMultiplier = 0f;
            DisableAllMove = true;
            CanMove = false;

            if (LocomotionMode != MovementMode.AlwaysInAimMode)
            {
                IsAiming = false;
                FiringMode = false;
                FiringModeIK = false;
            }

            if (duration > 0f)
            {
                Invoke(nameof(enableMove), duration);
            }
        }

        public void EnableMove()
        {
            CancelInvoke(nameof(enableMove));
            enableMove();
        }

        private void OnAnimatorMove()
        {
            ApplyRootMotionOnLocomotion();
        }

        private void InitializeCapsule()
        {
            CapsuleToResize ??= GetComponent<CapsuleCollider>();
            if (CapsuleToResize == null || anim == null || !anim.isHuman)
            {
                return;
            }

            initialCapsuleHeight = CapsuleToResize.height;
            headBone = anim.GetBoneTransform(HumanBodyBones.Head);
        }

        private void UpdateCapsule()
        {
            if (CapsuleToResize == null
                || headBone == null
                || RightFootBone == null
                || LeftFootBone == null)
            {
                return;
            }

            CapsuleToResize.height =
                HeadFeetDistance() * initialCapsuleHeight * HeightOffset;

            if (IsRolling || IsProne)
            {
                CapsuleToResize.direction = 2;
                CapsuleToResize.center =
                    new Vector3(0f, ProneAndRollCenterY, 0f);
                return;
            }

            CapsuleToResize.direction = 1;
            if (IsGrounded)
            {
                CapsuleToResize.center =
                    CenterOffset
                    + new Vector3(0f, CapsuleToResize.height * 0.5f, 0f);
                return;
            }

            Vector3 localBodyCenter = transform.InverseTransformPoint(GetBodyCenter());
            CapsuleToResize.center =
                CenterOffset + new Vector3(0f, localBodyCenter.y, 0f);
        }

        public float HeadFeetDistance()
        {
            return headBone == null
                ? 0f
                : Vector3.Distance(headBone.position, GetMiddlePointBetweenFeet());
        }

        public Vector3 GetMiddlePointBetweenFeet()
        {
            if (RightFootBone == null || LeftFootBone == null)
            {
                return Vector3.zero;
            }

            return Vector3.Lerp(
                RightFootBone.position,
                LeftFootBone.position,
                0.5f);
        }

        public Vector3 GetBodyCenter()
        {
            return headBone == null
                ? GetMiddlePointBetweenFeet()
                : Vector3.Lerp(
                    headBone.position,
                    GetMiddlePointBetweenFeet(),
                    0.5f);
        }
    }
}
