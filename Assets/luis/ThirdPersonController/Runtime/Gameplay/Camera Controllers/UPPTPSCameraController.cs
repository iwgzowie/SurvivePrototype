using UPP.ThirdPersonController.UPPInputSystem;
using UnityEngine;

namespace UPP.ThirdPersonController.CameraSystems
{
    [AddComponentMenu("UPP/Cámaras/Controlador de cámara en tercera persona")]
    public class UPPTPSCameraController : UPPCameraController
    {
        public UPPPlayerCharacterInputAsset InputAsset;
        public UPPCharacterMovementComponent characterTarget;

        [Header("Configuración")]
        public bool FollowUpTarget;

        [Header("Rotación automática")]
        public bool EnableAutoRotator;
        public float AutoRotateTime = 5;
        public float AutoRotationSpeed = 4;

        public CameraState NormalCameraState = new CameraState("Cámara normal");
        public CameraState AimCameraState = new CameraState(
            "Cámara de apuntado",
            1.5f,
            40,
            50,
            0,
            0,
            0,
            0.5f,
            0.5f);

        
        protected float CurrentTimeToAutoRotation;
        protected bool IsAutoRotationActivated;

        public float RawMouseX { get; private set; }
        public float RawMouseY { get; private set; }
        public float SmoothedMouseX { get; private set; }
        public float SmoothedMouseY { get; private set; }

        protected override void Start()
        {
            var defaultTargetToFollow = TargetToFollow;

            
            base.Start();

            if (TargetToFollow != null
                && TargetToFollow.root.TryGetComponent(
                    out UPPCharacterMovementComponent uppCharacter))
            {
                characterTarget = uppCharacter;

                
                if (defaultTargetToFollow == null)
                    TargetToFollow = characterTarget.HumanoidSpine;
            }
        }
        
        protected virtual void Update()
        {
            SmoothedMouseY = Mathf.Lerp(SmoothedMouseY, RawMouseY, 10 * Time.deltaTime);
            SmoothedMouseX = Mathf.Lerp(SmoothedMouseX, RawMouseX, 10 * Time.deltaTime);

            SetRotationInput();

            if (FollowUpTarget)
            {
                RotateCamera(RawMouseX, RawMouseY, upward: characterTarget == null ? TargetToFollow.up : characterTarget.transform.up);
            }
            else
            {
                RotateCamera(RawMouseX, RawMouseY);
            }

            if (EnableAutoRotator)
            {
                
                if (characterTarget != null)
                {
                    NormalAutoRotation(characterTarget);
                }
                else
                {
                    NormalAutoRotation(TargetToFollow);
                }
            }

            
            UpdateCharacterState(characterTarget);
            ChangeCameraStateAccordingCharacterState(CurrentState);
            Aiming = characterTarget != null && characterTarget.IsAiming;
        }

        protected virtual void SetRotationInput()
        {
            if (!InputAsset)
            {
                Debug.LogError(
                    $"La cámara {name} no tiene asignado un asset de entrada.",
                    this);

                RawMouseX = 0;
                RawMouseY = 0;
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                RawMouseX = 0;
                RawMouseY = 0;
                return;
            }

            RawMouseX = InputAsset.LookAxis.y;
            RawMouseY = InputAsset.LookAxis.x;
        }

        
        protected virtual void FixedUpdate()
        {
            if (TargetToFollow != null)
            {
                SetPivotCameraPosition(GetCurrentCameraState.GetCameraPivotPosition(TargetToFollow), true);
            }
        }

        
        protected virtual void LateUpdate()
        {
            if (mCamera == null)
            {
                return;
            }

            SetCameraPosition(GetCurrentCameraState.GetCameraPosition(mCamera.transform), false);
            SetCameraCollision(GetCurrentCameraState.CollisionLayers);
            SetFieldOfView(GetCurrentCameraState.CameraFieldOfView);
        }
        protected override void OnCameraRotate()
        {
            StopAutoRotation();
        }

        public enum PlayerStates
        {
            Normal = 0,
            Aiming = 1
        }
        public PlayerStates CurrentState { get; protected set; }

        protected virtual void UpdateCharacterState(
            UPPCharacterMovementComponent character)
        {
            if (character == null)
            {
                CurrentState = PlayerStates.Normal;
                return;
            }

            CurrentState = character.IsAiming || character.FiringMode
                ? PlayerStates.Aiming
                : PlayerStates.Normal;
        }
        protected virtual void ChangeCameraStateAccordingCharacterState(PlayerStates characterState)
        {
            if (IsTransitioningToCustomState) return;

            switch (characterState)
            {
                case PlayerStates.Normal:
                    SetCameraStateTransition(GetCurrentCameraState, NormalCameraState);
                    break;
                case PlayerStates.Aiming:
                    SetCameraStateTransition(GetCurrentCameraState, AimCameraState);
                    break;
            }
        }
        protected virtual void NormalAutoRotation(
            UPPCharacterMovementComponent character)
        {
            if (character == null || EnableAutoRotator == false) return;
            if (character.FiringMode) { CurrentTimeToAutoRotation = 0; return; }
            if (character.IsMoving) CurrentTimeToAutoRotation += 2 * Time.deltaTime;
            AutoRotator(character.transform, AutoRotateTime, AutoRotationSpeed, AutoRotationSpeed);
        }
        protected virtual void NormalAutoRotation(Transform targetToFollow)
        {
            if (targetToFollow == null || EnableAutoRotator == false) return;

            AutoRotator(targetToFollow, AutoRotateTime, AutoRotationSpeed, AutoRotationSpeed);
        }
        public virtual void AutoRotator(Transform targetRotation, float MaxTimeToAutoRotation, float HorizontalSpeed = 5, float VerticalSpeed = 3, float AngleToStopAutoRotation = 90)
        {
            if (Vector3.Angle(targetRotation.up, Vector3.up) > AngleToStopAutoRotation)
            {
                Debug.Log(
                    "Se desactivó la rotación automática de cámara al superar "
                    + $"el ángulo de {AngleToStopAutoRotation} grados.",
                    this);
                return;
            }
            if (IsAutoRotationActivated == true)
            {
                rotytarget = Mathf.LerpAngle(rotytarget, targetRotation.rotation.eulerAngles.y, HorizontalSpeed * Time.fixedDeltaTime);
                rotxtarget = Mathf.LerpAngle(rotxtarget, 0, VerticalSpeed * Time.fixedDeltaTime);
                if (FollowUpTarget)
                {
                    RotateCamera(RawMouseX, RawMouseY, upward: characterTarget == null ? TargetToFollow.up : characterTarget.transform.up);
                }
                else
                {
                    RotateCamera(RawMouseX, RawMouseY);
                }
            }
            else
            {
                CurrentTimeToAutoRotation += Time.deltaTime;
                if (CurrentTimeToAutoRotation >= MaxTimeToAutoRotation) { IsAutoRotationActivated = true; CurrentTimeToAutoRotation = 0; }
            }
        }
        public virtual void StopAutoRotation()
        {
            
            CurrentTimeToAutoRotation = 0; IsAutoRotationActivated = false;
        }

    }

}
