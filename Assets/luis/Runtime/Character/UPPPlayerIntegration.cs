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

        [Header("Cámara")]
        [SerializeField] private UPPTPSCameraController cameraController;
        [SerializeField, Tooltip("Prefab del rig UPP que se crea cuando la escena no tiene una cámara compatible.")]
        private UPPTPSCameraController cameraRigPrefab;
        [SerializeField, Tooltip("Crea el rig configurado si no existe una cámara UPP en la escena.")]
        private bool createCameraIfMissing = true;

        private bool runtimeReferencesBound;
        private bool missingCameraPrefabReported;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            EnsureCameraController();
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
            EnsureCameraController();
            if (characterController == null || cameraController == null)
            {
                return;
            }

            cameraController.InputAsset = characterController.Inputs;
            cameraController.characterTarget = characterController;
            cameraController.TargetToFollow =
                characterController.HumanoidSpine != null
                    ? characterController.HumanoidSpine
                    : transform;
            characterController.MyPivotCamera = cameraController;

            UnityEngine.Camera gameplayCamera = cameraController.mCamera;
            gameplayCamera ??=
                cameraController.GetComponentInChildren<UnityEngine.Camera>(true);
            cameraController.mCamera = gameplayCamera;
            if (gameplayCamera == null)
            {
                return;
            }

            UPPAimTrace aimTrace =
                gameplayCamera.GetComponent<UPPAimTrace>();
            aimTrace ??= gameplayCamera.gameObject.AddComponent<UPPAimTrace>();
            aimTrace.Configure(gameplayCamera, transform);

            UPPIKCharacterComponent inverseKinematics =
                characterController.GetComponent<UPPIKCharacterComponent>();
            inverseKinematics?.ConfigureAimTrace(aimTrace);

            UPPDebugCrosshair crosshair =
                gameplayCamera.GetComponent<UPPDebugCrosshair>();
            crosshair ??=
                gameplayCamera.gameObject.AddComponent<UPPDebugCrosshair>();
            crosshair.Configure(aimTrace, cameraController);

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

            cameraController ??=
                FindFirstObjectByType<UPPTPSCameraController>();
        }

        private void EnsureCameraController()
        {
            ResolveReferences();
            if (cameraController != null || !createCameraIfMissing)
            {
                return;
            }

            if (cameraRigPrefab == null)
            {
                if (!missingCameraPrefabReported)
                {
                    Debug.LogError(
                        "No se encontró una cámara UPP y el prefab del rig no está asignado.",
                        this);
                    missingCameraPrefabReported = true;
                }

                return;
            }

            Transform followTarget =
                characterController != null
                && characterController.HumanoidSpine != null
                    ? characterController.HumanoidSpine
                    : transform;
            cameraController = Instantiate(
                cameraRigPrefab,
                followTarget.position,
                transform.rotation);
            cameraController.name = cameraRigPrefab.name;
            missingCameraPrefabReported = false;
        }
    }
}
