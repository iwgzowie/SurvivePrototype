using UPP.Runtime.Aiming;
using UnityEngine;

namespace UPP.ThirdPersonController
{
    /// <summary>
    /// Centraliza las soluciones IK humanoides del personaje: pies, cuerpo,
    /// mirada de aim y lean procedural.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    [AddComponentMenu("UPP/Personaje/IK del personaje")]
    [RequireComponent(typeof(Animator))]
    public sealed class UPPIKCharacterComponent : MonoBehaviour
    {
        public enum LeanAxis
        {
            X,
            Y,
            Z
        }

        [Header("Referencias")]
        [SerializeField] private UPPCharacterMovementComponent movement;
        [SerializeField] private Animator animator;
        [SerializeField] private UPPAnimatorControllerComponent animatorController;
        [SerializeField] private UPPAimTrace aimTrace;

        [Header("Posicionamiento de pies")]
        public bool EnableFootPlacement = true;
        public LayerMask GroundLayers;
        public float RaycastMaxDistance = 2f;
        public float RaycastHeight = 1f;
        public float FootHeight = 0.1f;
        public float MaxStepHeight = 0.6f;
        public bool UseDynamicFootPlacing = true;
        public string LeftFootHeightCurveName = "LeftFootHeight";
        public string RightFootHeightCurveName = "RightFootHeight";
        public bool SmoothTransitions = true;
        public float SpeedInTransition = 6f;
        public float SpeedOutTransition = 6f;
        public float FootHeightMultiplier = 0.6f;
        [Range(0f, 1f)] public float GlobalWeight = 1f;
        public float Radius = 0.1f;

        [Header("Posicionamiento dinámico del cuerpo")]
        public bool EnableDynamicBodyPlacing = true;
        public float UpAndDownForce = 10f;
        public float MaxBodyCrouchHeight = 0.65f;
        [Tooltip("Sólo calcula la posición corporal; no la aplica al Animator.")]
        public bool JustCalculateBodyPosition;
        public float RaycastDistanceToGround = 1.2f;
        public float GroundCheckRadius = 0.1f;

        [Header("Mirada durante aim")]
        public bool EnableAimLookAt = true;
        [Range(0f, 1f)] public float LookAtBodyWeight = 0.5f;
        [Range(0f, 1f)] public float HeadIKBodyWeight = 1f;
        public float AimIKTransitionSpeed = 6f;

        [Header("Lean procedural")]
        public Transform RootBone;
        public bool RootBoneSpineLean = true;
        public bool RootBoneSpineMovement = true;
        public float RootBoneLeanIntensity = 20f;
        public float RootBoneLeanSpeed = 8f;
        public float RootBoneDownMovementIntensity = 0.25f;
        public float BlockForwardLeanWeight = 4f;
        public LeanAxis AxisToLean;

        [HideInInspector] public bool BlockBodyPositioning;
        [HideInInspector] public bool TheresGroundBelow;
        [HideInInspector] public float LeftFootHeightFromGround;
        [HideInInspector] public float RightFootHeightFromGround;
        [HideInInspector] public float LeftFootRotationWeight;
        [HideInInspector] public float RightFootRotationWeight;
        [HideInInspector] public float LastBodyPositionY;
        [HideInInspector] public Vector3 NewAnimationBodyPosition;
        [HideInInspector] public float AnimationYBodyPosition;

        private bool started;
        private bool leftHit;
        private bool rightHit;
        private bool capturedLeanPose;
        private RaycastHit leftHitPlaceBase;
        private RaycastHit rightHitPlaceBase;
        private RaycastHit bodyGroundHit;
        private Transform leftFoot;
        private Transform rightFoot;
        private Transform leftFootUpperBase;
        private Transform rightFootUpperBase;
        private Transform leftFootTarget;
        private Transform rightFootTarget;
        private Vector3 smoothedLeftFootPosition;
        private Vector3 smoothedRightFootPosition;
        private Quaternion smoothedLeftFootRotation;
        private Quaternion smoothedRightFootRotation;
        private Vector3 unaffectedEulerAngles;
        private Vector3 unaffectedUpward;
        private float leftFootHeight;
        private float rightFootHeight;
        private float animationLeftFootPositionY;
        private float animationRightFootPositionY;
        private float transitionIKtoFKWeight;
        private float bodyPositionOffset;
        private float groundAngle;
        private float aimIKWeight;
        private float leanSpeed;
        private float lean;
        private bool shouldApplyFootIK;

        public bool HasLeftFootHit => leftHit;
        public bool HasRightFootHit => rightHit;
        public RaycastHit LeftFootHit => leftHitPlaceBase;
        public RaycastHit RightFootHit => rightHitPlaceBase;
        public Transform LeftFoot => leftFoot;
        public Transform RightFoot => rightFoot;
        public Vector3 BodyLeanReferenceUp => unaffectedUpward;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            if (animator == null)
            {
                enabled = false;
                return;
            }

            RootBone ??= animator.GetBoneTransform(HumanBodyBones.Hips);
        }

        private void Start()
        {
            StartFootPlacement();
        }

        private void Update()
        {
            if (movement == null)
            {
                return;
            }

            bool canPlaceFeet =
                movement.IsGrounded
                && !movement.IsJumping
                && !movement.IsRolling
                && !movement.IsProne;
            shouldApplyFootIK = EnableFootPlacement && canPlaceFeet;
            BlockBodyPositioning = !canPlaceFeet || movement.IsCrouched;
            aimIKWeight = Mathf.MoveTowards(
                aimIKWeight,
                movement.FiringModeIK ? 1f : 0f,
                AimIKTransitionSpeed * Time.deltaTime);
        }

        private void LateUpdate()
        {
            ApplyBodyLean();
        }

        private void OnDestroy()
        {
            DestroyRuntimeHelper(leftFootTarget);
            DestroyRuntimeHelper(rightFootTarget);
            DestroyRuntimeHelper(leftFootUpperBase);
            DestroyRuntimeHelper(rightFootUpperBase);
        }

        public void StartFootPlacement()
        {
            ResolveFootDependencies();
            started =
                leftFoot != null
                && rightFoot != null
                && leftFootTarget != null
                && rightFootTarget != null;

            if (!started)
            {
                return;
            }

            leftFootTarget.position = leftFoot.position;
            rightFootTarget.position = rightFoot.position;
        }

        public Vector3 GetCalculatedAnimatorCenterOfMass()
        {
            return NewAnimationBodyPosition;
        }

        public void ConfigureAimTrace(UPPAimTrace trace)
        {
            aimTrace = trace;
        }

        private void ResolveReferences()
        {
            movement ??= GetComponent<UPPCharacterMovementComponent>();
            animator ??= GetComponent<Animator>();
            animatorController ??= GetComponent<UPPAnimatorControllerComponent>();
        }

        private void ResolveFootDependencies()
        {
            if (GroundLayers.value == 0)
            {
                GroundLayers = LayerMask.GetMask("Default");
            }

            if (animator == null || !animator.isHuman)
            {
                return;
            }

            leftFoot ??= animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot ??= animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null)
            {
                return;
            }

            smoothedLeftFootPosition =
                leftFoot.position - transform.forward * 0.1f;
            smoothedRightFootPosition =
                rightFoot.position - transform.forward * 0.1f;
            smoothedLeftFootRotation = leftFoot.rotation;
            smoothedRightFootRotation = rightFoot.rotation;

            leftFootTarget ??= CreateRuntimeHelper("Posición del pie izquierdo");
            rightFootTarget ??= CreateRuntimeHelper("Posición del pie derecho");
            leftFootUpperBase ??= CreateRuntimeHelper(
                "Base superior del pie izquierdo",
                leftFoot);
            rightFootUpperBase ??= CreateRuntimeHelper(
                "Base superior del pie derecho",
                rightFoot);

            leftFootTarget.SetPositionAndRotation(leftFoot.position, leftFoot.rotation);
            rightFootTarget.SetPositionAndRotation(rightFoot.position, rightFoot.rotation);
            leftFootUpperBase.SetPositionAndRotation(leftFoot.position, leftFoot.rotation);
            rightFootUpperBase.SetPositionAndRotation(rightFoot.position, rightFoot.rotation);
        }

        private static Transform CreateRuntimeHelper(string objectName, Transform parent = null)
        {
            Transform helper = new GameObject(objectName).transform;
            helper.SetParent(parent);
            helper.gameObject.hideFlags = HideFlags.HideAndDontSave;
            return helper;
        }

        private static void DestroyRuntimeHelper(Transform helper)
        {
            if (helper != null)
            {
                Destroy(helper.gameObject);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            int baseLayerIndex = animatorController != null
                ? animatorController.Parameters.BaseLayerIndex
                : 0;
            if (layerIndex != baseLayerIndex || animator == null)
            {
                return;
            }

            CaptureBodyLeanPose();
            ApplyFootPlacement();
            ApplyAimLookAt();
        }

        private void ApplyFootPlacement()
        {
            if (!started
                || leftFoot == null
                || rightFoot == null
                || leftFootTarget == null
                || rightFootTarget == null)
            {
                return;
            }

            bool useFootIK = shouldApplyFootIK
                && Vector3.Angle(transform.up, Vector3.up) <= 30f;
            CalculateFootTargets(useFootIK);
            AnimationYBodyPosition = animator.bodyPosition.y;

            if (Vector3.Angle(transform.up, Vector3.up) < 40f)
            {
                ApplyBodyPlacement();
            }

            if (transitionIKtoFKWeight < 0.1f
                || GlobalWeight < 0.01f
                || !leftHit
                || !rightHit)
            {
                return;
            }

            animationLeftFootPositionY = Mathf.Clamp01(
                Mathf.Abs(transform.position.y - (leftFoot.position.y - FootHeight)));
            animationRightFootPositionY = Mathf.Clamp01(
                Mathf.Abs(transform.position.y - (rightFoot.position.y - FootHeight)));

            ApplyFootGoal(
                AvatarIKGoal.LeftFoot,
                leftFoot,
                smoothedLeftFootPosition,
                smoothedLeftFootRotation,
                LeftFootRotationWeight,
                leftHit,
                leftHitPlaceBase);
            ApplyFootGoal(
                AvatarIKGoal.RightFoot,
                rightFoot,
                smoothedRightFootPosition,
                smoothedRightFootRotation,
                RightFootRotationWeight,
                rightHit,
                rightHitPlaceBase);
        }

        private void CalculateFootTargets(bool useFootIK)
        {
            if (UseDynamicFootPlacing)
            {
                LeftFootHeightFromGround =
                    FootHeightMultiplier * animationLeftFootPositionY;
                RightFootHeightFromGround =
                    FootHeightMultiplier * animationRightFootPositionY;
            }
            else
            {
                LeftFootHeightFromGround = Mathf.Lerp(
                    LeftFootHeightFromGround,
                    animator.GetFloat(LeftFootHeightCurveName) * 0.5f,
                    20f * Time.deltaTime);
                RightFootHeightFromGround = Mathf.Lerp(
                    RightFootHeightFromGround,
                    animator.GetFloat(RightFootHeightCurveName) * 0.5f,
                    20f * Time.deltaTime);
            }

            leftHit = Physics.SphereCast(
                leftFoot.position
                + transform.up * RaycastHeight
                + leftFootUpperBase.forward * 0.12f,
                Radius,
                -transform.up,
                out leftHitPlaceBase,
                RaycastMaxDistance,
                GroundLayers,
                QueryTriggerInteraction.Ignore);
            rightHit = Physics.SphereCast(
                rightFoot.position
                + transform.up * RaycastHeight
                + rightFootUpperBase.forward * 0.12f,
                Radius,
                -transform.up,
                out rightHitPlaceBase,
                RaycastMaxDistance,
                GroundLayers,
                QueryTriggerInteraction.Ignore);

            UpdateFootTarget(
                leftFoot,
                leftFootTarget,
                leftHit,
                leftHitPlaceBase,
                LeftFootHeightFromGround,
                ref leftFootHeight,
                ref smoothedLeftFootPosition,
                ref smoothedLeftFootRotation,
                ref LeftFootRotationWeight,
                15f);
            UpdateFootTarget(
                rightFoot,
                rightFootTarget,
                rightHit,
                rightHitPlaceBase,
                RightFootHeightFromGround,
                ref rightFootHeight,
                ref smoothedRightFootPosition,
                ref smoothedRightFootRotation,
                ref RightFootRotationWeight,
                20f);

            float targetWeight = useFootIK ? 1f : 0f;
            transitionIKtoFKWeight = SmoothTransitions
                ? Mathf.MoveTowards(
                    transitionIKtoFKWeight,
                    targetWeight,
                    (useFootIK ? SpeedInTransition : SpeedOutTransition)
                    * Time.deltaTime)
                : targetWeight;
        }

        private void UpdateFootTarget(
            Transform foot,
            Transform target,
            bool hasHit,
            RaycastHit hit,
            float heightFromGround,
            ref float calculatedFootHeight,
            ref Vector3 smoothedPosition,
            ref Quaternion smoothedRotation,
            ref float rotationWeight,
            float positionSpeed)
        {
            calculatedFootHeight = Mathf.Clamp(
                FootHeight
                - Vector3.SignedAngle(
                    foot.up,
                    transform.up,
                    transform.right) / 500f,
                -0.2f,
                0.2f);

            if (hasHit)
            {
                target.position = hit.point;
                target.rotation =
                    Quaternion.FromToRotation(transform.up, hit.normal)
                    * transform.rotation;
                Vector3 desiredPosition = hit.point.y < transform.position.y + MaxStepHeight
                    ? target.position
                        + hit.normal * calculatedFootHeight
                        + transform.up * heightFromGround
                    : transform.position
                        + transform.up * (FootHeight + heightFromGround);
                smoothedPosition = Vector3.Lerp(
                    smoothedPosition,
                    desiredPosition,
                    positionSpeed * Time.deltaTime);

                Vector3 rotationAxis = Vector3.Cross(Vector3.up, hit.normal);
                float rotationAngle = Vector3.Angle(Vector3.up, hit.normal);
                Quaternion desiredRotation =
                    Quaternion.AngleAxis(rotationAngle * GlobalWeight, rotationAxis);
                smoothedRotation = Quaternion.Lerp(
                    smoothedRotation,
                    desiredRotation,
                    20f * Time.deltaTime);
            }
            else
            {
                target.position = foot.position;
                smoothedPosition = foot.position;
            }

            rotationWeight = Mathf.Lerp(
                rotationWeight,
                heightFromGround < 0.3f ? 1f : 0f,
                (heightFromGround < 0.3f ? 8f : 1f) * Time.deltaTime);
        }

        private void ApplyFootGoal(
            AvatarIKGoal goal,
            Transform foot,
            Vector3 position,
            Quaternion rotation,
            float rotationWeight,
            bool hasHit,
            RaycastHit hit)
        {
            if (!hasHit || hit.point.y >= transform.position.y + RaycastHeight)
            {
                return;
            }

            animator.SetIKPosition(
                goal,
                new Vector3(foot.position.x, position.y, foot.position.z));
            animator.SetIKPositionWeight(
                goal,
                GlobalWeight * transitionIKtoFKWeight);
            animator.SetIKRotationWeight(
                goal,
                GlobalWeight * transitionIKtoFKWeight * rotationWeight);
            animator.SetIKRotation(
                goal,
                rotation * animator.GetIKRotation(goal));
        }

        private void ApplyBodyPlacement()
        {
            Physics.SphereCast(
                transform.position + transform.up * RaycastDistanceToGround,
                GroundCheckRadius,
                -transform.up,
                out bodyGroundHit,
                RaycastDistanceToGround + 0.2f,
                GroundLayers,
                QueryTriggerInteraction.Ignore);
            TheresGroundBelow = bodyGroundHit.collider != null;
            groundAngle = TheresGroundBelow
                ? Vector3.Angle(Vector3.up, bodyGroundHit.normal)
                : 0f;

            if (!EnableDynamicBodyPlacing || BlockBodyPositioning)
            {
                bodyPositionOffset = 0f;
                NewAnimationBodyPosition = animator.bodyPosition;
                LastBodyPositionY = animator.bodyPosition.y;
                return;
            }

            if (!leftHit || !rightHit || Mathf.Approximately(LastBodyPositionY, 0f))
            {
                LastBodyPositionY = AnimationYBodyPosition;
                bodyPositionOffset = 0f;
                NewAnimationBodyPosition = animator.bodyPosition;
                return;
            }

            float leftOffset =
                leftHitPlaceBase.point.y
                - transform.position.y
                - LeftFootHeightFromGround * 0.5f;
            float rightOffset =
                rightHitPlaceBase.point.y
                - transform.position.y
                - RightFootHeightFromGround * 0.5f;
            bodyPositionOffset = Mathf.Clamp(
                Mathf.Min(leftOffset, rightOffset),
                -MaxBodyCrouchHeight,
                0f);

            NewAnimationBodyPosition =
                animator.bodyPosition + transform.up * bodyPositionOffset;
            NewAnimationBodyPosition.y = Mathf.Lerp(
                LastBodyPositionY,
                NewAnimationBodyPosition.y,
                (UpAndDownForce + groundAngle / 20f) * Time.deltaTime);

            float distance = Mathf.Abs(AnimationYBodyPosition - LastBodyPositionY);
            if (!JustCalculateBodyPosition && distance < 1f)
            {
                animator.bodyPosition = NewAnimationBodyPosition;
            }

            LastBodyPositionY = animator.bodyPosition.y;
        }

        private void ApplyAimLookAt()
        {
            if (!EnableAimLookAt || movement == null || !animator.isHuman)
            {
                return;
            }

            Vector3 lookPosition = aimTrace != null && aimTrace.AimPoint != Vector3.zero
                ? aimTrace.AimPoint
                : movement.GetLookPosition();
            Vector3 direction = lookPosition - transform.position;
            float intensity = direction.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(transform.forward, direction.normalized))
                : 0f;
            float bodyWeight = movement.IsProne
                ? (LookAtBodyWeight > 0f ? 0.1f : 0f)
                : LookAtBodyWeight;
            animator.SetLookAtWeight(
                intensity * aimIKWeight,
                bodyWeight,
                HeadIKBodyWeight);
            animator.SetLookAtPosition(lookPosition);
        }

        private void CaptureBodyLeanPose()
        {
            if (RootBone == null)
            {
                return;
            }

            unaffectedEulerAngles = RootBone.localEulerAngles;
            unaffectedUpward = RootBone.up;
            capturedLeanPose = true;
        }

        private void ApplyBodyLean()
        {
            if (!capturedLeanPose || RootBone == null || movement == null)
            {
                return;
            }

            bool canLean =
                RootBoneSpineLean
                && !movement.IsAiming
                && !movement.FiringMode
                && !movement.IsRolling
                && movement.IsGrounded;
            if (!canLean)
            {
                leanSpeed = 0f;
                lean = 0f;
                return;
            }

            leanSpeed = Mathf.Lerp(
                leanSpeed,
                movement.VelocityMultiplier,
                10f * Time.deltaTime);
            float divisor = Mathf.Max(0.01f, BlockForwardLeanWeight);
            float targetLean = movement.IsMoving
                ? leanSpeed * RootBoneLeanIntensity / divisor
                : -(leanSpeed * RootBoneLeanIntensity * 0.5f);
            lean = Mathf.Lerp(
                lean,
                targetLean,
                RootBoneLeanSpeed * Time.deltaTime);

            if (!movement.IsMoving && RootBoneSpineMovement)
            {
                LastBodyPositionY -=
                    RootBoneDownMovementIntensity
                    * Mathf.Abs(lean)
                    * 0.1f
                    * Time.deltaTime;
            }

            Vector3 euler = unaffectedEulerAngles;
            switch (AxisToLean)
            {
                case LeanAxis.X:
                    euler.x += lean;
                    break;
                case LeanAxis.Y:
                    euler.y += lean;
                    break;
                case LeanAxis.Z:
                    euler.z += lean;
                    break;
            }

            RootBone.localRotation = Quaternion.Euler(euler);
        }
    }
}
