using UPP.Runtime.Aiming;
using UPP.ThirdPersonController;
using UPP.ThirdPersonController.CameraSystems;
using UnityEngine;

namespace UPP.Runtime.Character
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UPPCharacterMovementComponent))]
    [AddComponentMenu("UPP/Character/Integración del jugador")]
    public sealed class UPPPlayerIntegration : MonoBehaviour
    {
        [SerializeField] private UPPCharacterMovementComponent characterController;
        [SerializeField] private UPPTPSCameraController cameraController;
        private bool runtimeReferencesBound;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            BindRuntimeReferences();
        }

        private void LateUpdate()
        {
            if (!runtimeReferencesBound)
            {
                BindRuntimeReferences();
            }
        }

        public void Configure(
            UPPCharacterMovementComponent playerController,
            UPPTPSCameraController playerCamera)
        {
            characterController = playerController;
            cameraController = playerCamera;
            runtimeReferencesBound = false;
            BindRuntimeReferences();
        }

        public void BindRuntimeReferences()
        {
            ResolveReferences();
            if (characterController == null || cameraController == null)
            {
                return;
            }

            if (cameraController != null)
            {
                cameraController.InputAsset = characterController.Inputs;
                cameraController.characterTarget = characterController;
                cameraController.TargetToFollow =
                    characterController.HumanoidSpine != null
                        ? characterController.HumanoidSpine
                        : transform;
            }

            UnityEngine.Camera gameplayCamera = cameraController.mCamera;
            if (gameplayCamera == null)
            {
                return;
            }

            UPPAimTrace aimTrace =
                gameplayCamera.GetComponent<UPPAimTrace>();
            aimTrace?.Configure(gameplayCamera, transform);

            UPPIKCharacterComponent inverseKinematics =
                characterController.GetComponent<UPPIKCharacterComponent>();
            inverseKinematics?.ConfigureAimTrace(aimTrace);

            UPPDebugCrosshair crosshair =
                gameplayCamera.GetComponent<UPPDebugCrosshair>();
            if (crosshair != null && aimTrace != null)
            {
                crosshair.Configure(aimTrace, cameraController);
            }

            UPPGizmosDebugComponent debugComponent =
                characterController.GetComponent<UPPGizmosDebugComponent>();
            debugComponent?.Configure(cameraController, aimTrace, crosshair);
            runtimeReferencesBound = true;
        }

        private void ResolveReferences()
        {
            characterController ??= GetComponent<UPPCharacterMovementComponent>();

            if (cameraController == null && UnityEngine.Camera.main != null)
            {
                cameraController =
                    UnityEngine.Camera.main.GetComponentInParent<UPPTPSCameraController>();
            }
        }
    }
}
