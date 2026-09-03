using UPP.Runtime.Aiming;
using UPP.ThirdPersonController.CameraSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace UPP.ThirdPersonController
{
    /// <summary>
    /// Centraliza los visuales de diagnóstico del personaje, la cámara y el aim.
    /// No modifica las mecánicas observadas; sólo controla su presentación de debug.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UPP/Personaje/Gizmos y debug")]
    public sealed class UPPGizmosDebugComponent : MonoBehaviour
    {
        [Header("Interruptor general")]
        public bool DrawDebug = true;
        public bool OnlyInEditMode;

        [Header("Movimiento")]
        public bool DrawGroundCheck = true;
        public bool DrawWallCheck = true;
        public bool DrawStepCorrection = true;

        [Header("IK y animación procedural")]
        public bool DrawFootIK = true;
        public bool DrawBodyPlacement = true;
        public bool DrawBodyLean = true;

        [Header("Cámara y aim")]
        public bool DrawCamera = true;
        public bool DrawAimTrace = true;
        public bool DrawCrosshair = true;

        [Header("Colores")]
        public Color WireColor = new Color(0.9f, 0.4f, 0.2f, 0.5f);

        [SerializeField, HideInInspector]
        private UPPCharacterMovementComponent movement;
        [SerializeField, HideInInspector]
        private UPPIKCharacterComponent inverseKinematics;
        [SerializeField, HideInInspector]
        private UPPTPSCameraController cameraController;
        [SerializeField, HideInInspector]
        private UPPAimTrace aimTrace;
        [SerializeField, HideInInspector]
        private UPPDebugCrosshair crosshair;

        private bool RuntimeDebugEnabled =>
            DrawDebug && (!OnlyInEditMode || !Application.isPlaying);

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyExternalVisibility();
        }

        private void Start()
        {
            ResolveReferences();
            ApplyExternalVisibility();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyExternalVisibility();
        }

        private void OnDisable()
        {
            SetExternalVisibility(false);
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyExternalVisibility();
        }

        public void Configure(
            UPPTPSCameraController playerCamera,
            UPPAimTrace playerAimTrace,
            UPPDebugCrosshair playerCrosshair)
        {
            cameraController = playerCamera;
            aimTrace = playerAimTrace;
            crosshair = playerCrosshair;
            ApplyExternalVisibility();
        }

        private void ResolveReferences()
        {
            movement ??= GetComponent<UPPCharacterMovementComponent>();
            inverseKinematics ??= GetComponent<UPPIKCharacterComponent>();

            if (cameraController == null && Camera.main != null)
            {
                cameraController =
                    Camera.main.GetComponentInParent<UPPTPSCameraController>();
            }

            if (cameraController != null && cameraController.mCamera != null)
            {
                aimTrace ??= cameraController.mCamera.GetComponent<UPPAimTrace>();
                crosshair ??=
                    cameraController.mCamera.GetComponent<UPPDebugCrosshair>();
            }
        }

        private void ApplyExternalVisibility()
        {
            SetExternalVisibility(RuntimeDebugEnabled);
        }

        private void SetExternalVisibility(bool visible)
        {
            if (crosshair != null)
            {
                crosshair.DebugVisible = visible && DrawCrosshair;
            }
        }

        private void OnDrawGizmos()
        {
            if (!RuntimeDebugEnabled)
            {
                return;
            }

            ResolveReferences();
            if (movement != null)
            {
                DrawMovementGizmos();
                DrawIKGizmos();
            }

            DrawCameraGizmos();
            DrawAimGizmo();
        }

        private void DrawMovementGizmos()
        {
            if (DrawWallCheck)
            {
                Vector3 origin =
                    movement.transform.position
                    + movement.transform.up * movement.WallRayHeight;
                Vector3 direction = movement.DirectionTransform != null
                    ? movement.DirectionTransform.forward
                    : movement.transform.forward;
                Gizmos.color = movement.WallAHead ? Color.green : Color.red;
                Gizmos.DrawLine(
                    origin,
                    origin + direction * movement.WallRayDistance);
            }

            if (DrawGroundCheck)
            {
                Gizmos.color = movement.IsGrounded ? Color.black : Color.green;
                Gizmos.DrawWireCube(
                    movement.transform.position
                    + movement.transform.up * movement.GroundCheckHeighOfsset,
                    new Vector3(
                        movement.GroundCheckRadius,
                        movement.GroundCheckSize,
                        movement.GroundCheckRadius) * 2f);
            }

            if (!DrawStepCorrection)
            {
                return;
            }

            Vector3 forward = movement.DirectionTransform != null
                ? movement.DirectionTransform.forward
                : movement.transform.forward;
            Vector3 lower =
                movement.transform.position
                + forward * movement.ForwardStepOffset
                + movement.transform.up * movement.StepHeight;
            Vector3 upper =
                movement.transform.position
                + forward * movement.ForwardStepOffset
                + movement.transform.up * movement.FootstepHeight;
            if (movement.StepHit.collider != null)
            {
                lower = movement.StepHit.point;
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(lower, 0.05f);
            }

            Gizmos.color = WireColor;
            Gizmos.DrawLine(lower, upper);
            Gizmos.DrawWireCube(lower, new Vector3(0.4f, 0.04f, 0.4f));
            Gizmos.DrawWireCube(upper, new Vector3(0.4f, 0.04f, 0.4f));
        }

        private void DrawIKGizmos()
        {
            if (inverseKinematics == null)
            {
                return;
            }

            if (DrawFootIK)
            {
                DrawFoot(
                    inverseKinematics.LeftFoot,
                    inverseKinematics.HasLeftFootHit,
                    inverseKinematics.LeftFootHit,
                    Color.yellow);
                DrawFoot(
                    inverseKinematics.RightFoot,
                    inverseKinematics.HasRightFootHit,
                    inverseKinematics.RightFootHit,
                    new Color(0.2f, 0.4f, 1f));
            }

            if (DrawBodyPlacement)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(
                    inverseKinematics.NewAnimationBodyPosition,
                    0.08f);
            }

#if UNITY_EDITOR
            if (DrawBodyLean
                && inverseKinematics.RootBone != null
                && inverseKinematics.BodyLeanReferenceUp.sqrMagnitude > 0f)
            {
                float angle = Vector3.SignedAngle(
                    inverseKinematics.BodyLeanReferenceUp,
                    inverseKinematics.RootBone.up,
                    inverseKinematics.RootBone.right);
                if (!Mathf.Approximately(angle, 0f))
                {
                    Handles.color = Color.Lerp(Color.green, Color.red, angle / 10f);
                    Handles.DrawWireArc(
                        inverseKinematics.RootBone.position,
                        -inverseKinematics.RootBone.right,
                        inverseKinematics.RootBone.up,
                        angle,
                        0.5f);
                }
            }
#endif
        }

        private static void DrawFoot(
            Transform foot,
            bool hasHit,
            RaycastHit hit,
            Color color)
        {
            if (foot == null)
            {
                return;
            }

            Gizmos.color = color;
            if (hasHit)
            {
                Gizmos.DrawLine(foot.position, hit.point);
                Gizmos.DrawWireSphere(hit.point, 0.05f);
                Gizmos.DrawRay(hit.point, hit.normal * 0.2f);
            }
            else
            {
                Gizmos.DrawWireSphere(foot.position, 0.03f);
            }
        }

        private void DrawCameraGizmos()
        {
            if (!DrawCamera
                || cameraController == null
                || cameraController.mCamera == null)
            {
                return;
            }

            UnityEngine.Camera playerCamera = cameraController.mCamera;
            Transform cameraTransform = playerCamera.transform;
            float scale = playerCamera.nearClipPlane + 0.015f;
            Vector3 nearCenter =
                cameraTransform.position + cameraTransform.forward * scale;

            Gizmos.color = Color.white;
            Gizmos.DrawLine(cameraController.transform.position, nearCenter);
            Gizmos.DrawWireSphere(cameraTransform.position, 0.01f);
            Gizmos.DrawWireSphere(cameraController.transform.position, 0.03f);
            Gizmos.DrawLine(
                nearCenter + cameraTransform.right * scale,
                nearCenter - cameraTransform.right * scale);
            Gizmos.DrawLine(
                nearCenter + cameraTransform.up * scale,
                nearCenter - cameraTransform.up * scale);
        }

        private void DrawAimGizmo()
        {
            if (!DrawAimTrace || aimTrace == null)
            {
                return;
            }

            Ray ray = aimTrace.LastRay;
            Vector3 target = aimTrace.AimPoint;
            if (ray.direction.sqrMagnitude < 0.0001f
                && cameraController != null
                && cameraController.mCamera != null)
            {
                ray = cameraController.mCamera.ViewportPointToRay(
                    new Vector3(0.5f, 0.5f, 0f));
                target = ray.origin + ray.direction * 25f;
            }

            if (ray.direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Gizmos.color = aimTrace.HasHit ? Color.green : Color.cyan;
            Gizmos.DrawLine(ray.origin, target);
            if (aimTrace.HasHit)
            {
                Gizmos.DrawWireSphere(target, 0.04f);
            }
        }
    }
}
