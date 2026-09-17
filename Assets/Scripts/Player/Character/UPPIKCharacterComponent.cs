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
        [Tooltip("Altura mínima del pie. También se respeta la distancia a la planta definida por el Avatar.")]
        public float FootHeight = 0.1f;
        public float MaxStepHeight = 0.6f;
        public bool UseDynamicFootPlacing = true;
        public string LeftFootHeightCurveName = "LeftFootHeight";
        public string RightFootHeightCurveName = "RightFootHeight";
        public bool SmoothTransitions = true;
        public float SpeedInTransition = 6f;
        public float SpeedOutTransition = 6f;
        [Tooltip("Escala la elevación animada de la zancada. El valor 1 conserva el movimiento completo del clip.")]
        public float FootHeightMultiplier = 1f;
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
        private readonly RaycastHit[] groundHits = new RaycastHit[32];
        private float smoothedLeftGroundOffset;
        private float smoothedRightGroundOffset;
        private Quaternion smoothedLeftFootRotation = Quaternion.identity;
        private Quaternion smoothedRightFootRotation = Quaternion.identity;
        private Vector3 unaffectedUpward;
        private float leftContactWeight;
        private float rightContactWeight;
        private float transitionIKtoFKWeight;
        private float bodyPositionOffset;
        private float groundAngle;
        private float aimIKWeight;
        private float leanSpeed;
        private float lean;

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
            if (animator == null || !animator.isHuman)
            {
                enabled = false;
                return;
            }

            RootBone ??= animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        private void Start()
        {
            StartFootPlacement();
        }

        private void LateUpdate()
        {
            // Consumir una sola vez la evaluación del Animator. No reutilizar una
            // pose vieja entre ticks Fixed ni escribir el offset de la pelvis acá.
            if (capturedLeanPose)
            {
                capturedLeanPose = false;
                ApplyBodyLean();
            }
        }

        private void OnDisable()
        {
            ResetPlacementState();
            capturedLeanPose = false;
        }

        public void StartFootPlacement()
        {
            ResolveReferences();
            if (GroundLayers.value == 0)
            {
                GroundLayers = LayerMask.GetMask("Default");
            }

            started = animator != null && animator.isHuman;
            if (started)
            {
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                started = leftFoot != null && rightFoot != null;
            }

            ResetPlacementState();
        }

        private void ResetPlacementState()
        {
            leftHit = rightHit = false;
            leftHitPlaceBase = rightHitPlaceBase = bodyGroundHit = default;
            smoothedLeftGroundOffset = smoothedRightGroundOffset = 0f;
            smoothedLeftFootRotation = smoothedRightFootRotation = Quaternion.identity;
            leftContactWeight = rightContactWeight = 0f;
            LeftFootRotationWeight = RightFootRotationWeight = 0f;
            LeftFootHeightFromGround = RightFootHeightFromGround = 0f;
            transitionIKtoFKWeight = bodyPositionOffset = 0f;
            TheresGroundBelow = false;
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

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            int baseLayerIndex = animatorController != null
                ? animatorController.Parameters.BaseLayerIndex
                : 0;
            int footLayerIndex = baseLayerIndex;
            if (animatorController != null)
            {
                int legsLayer = animatorController.Parameters.LegsLayerIndex;
                // El controlador UPP tiene IKPass en Base y Legs. Resolver una
                // sola vez, después de la capa que aporta la zancada de aim.
                if (legsLayer > baseLayerIndex && legsLayer < animator.layerCount
                    && animator.GetLayerWeight(legsLayer) > 0f)
                {
                    footLayerIndex = legsLayer;
                }
            }

            if (layerIndex == baseLayerIndex && footLayerIndex != baseLayerIndex)
            {
                ClearFootWeights();
                ApplyAimLookAt();
            }

            if (layerIndex != footLayerIndex)
            {
                return;
            }

            ApplyFootPlacement();
            if (footLayerIndex == baseLayerIndex)
            {
                ApplyAimLookAt();
            }
            capturedLeanPose = true;
        }

        private void ClearFootWeights()
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
        }

        private void ApplyFootPlacement()
        {
            ClearFootWeights();
            if (!started || leftFoot == null || rightFoot == null)
            {
                return;
            }

            // GetIK devuelve la pose de ESTA evaluación antes de corregir la
            // pelvis. Los Transform de los huesos pueden contener la pose resuelta
            // anteriormente; no deben realimentar el siguiente objetivo.
            Vector3 animatedLeft = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 animatedRight = animator.GetIKPosition(AvatarIKGoal.RightFoot);
            Quaternion animatedLeftRotation = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            Quaternion animatedRightRotation = animator.GetIKRotation(AvatarIKGoal.RightFoot);
            Vector3 animatedBody = animator.bodyPosition;
            AnimationYBodyPosition = animatedBody.y;

            bool canPlaceFeet = movement != null
                && movement.IsGrounded && !movement.IsJumping
                && !movement.IsRolling && !movement.IsProne
                && Vector3.Angle(transform.up, Vector3.up) <= 30f;
            BlockBodyPositioning = !canPlaceFeet || (movement != null && movement.IsCrouched);
            float targetWeight = EnableFootPlacement && canPlaceFeet ? 1f : 0f;
            transitionIKtoFKWeight = SmoothTransitions && canPlaceFeet
                ? Mathf.MoveTowards(transitionIKtoFKWeight, targetWeight,
                    Mathf.Max(0f, targetWeight > 0f ? SpeedInTransition : SpeedOutTransition)
                    * Time.deltaTime)
                : targetWeight;
            float weight = Mathf.Clamp01(GlobalWeight) * transitionIKtoFKWeight;

            if (!canPlaceFeet || weight <= 0f)
            {
                ResetPlacementState();
                NewAnimationBodyPosition = animatedBody;
                LastBodyPositionY = animatedBody.y;
                return;
            }

            float leftSoleHeight = Mathf.Max(0f, FootHeight, animator.leftFeetBottomHeight);
            float rightSoleHeight = Mathf.Max(0f, FootHeight, animator.rightFeetBottomHeight);
            LeftFootHeightFromGround = GetAnimatedFootLift(
                animatedLeft, leftSoleHeight, LeftFootHeightCurveName);
            RightFootHeightFromGround = GetAnimatedFootLift(
                animatedRight, rightSoleHeight, RightFootHeightCurveName);

            bool hadLeftHit = leftHit;
            bool hadRightHit = rightHit;
            leftHit = TryGroundHit(animatedLeft + Vector3.up * Mathf.Max(0f, RaycastHeight),
                Radius, RaycastMaxDistance, animatedLeft, out leftHitPlaceBase);
            rightHit = TryGroundHit(animatedRight + Vector3.up * Mathf.Max(0f, RaycastHeight),
                Radius, RaycastMaxDistance, animatedRight, out rightHitPlaceBase);

            UpdateFootTarget(animatedLeft, leftHit, hadLeftHit, leftHitPlaceBase,
                LeftFootHeightFromGround, ref smoothedLeftGroundOffset,
                ref smoothedLeftFootRotation, ref leftContactWeight,
                ref LeftFootRotationWeight);
            UpdateFootTarget(animatedRight, rightHit, hadRightHit, rightHitPlaceBase,
                RightFootHeightFromGround, ref smoothedRightGroundOffset,
                ref smoothedRightFootRotation, ref rightContactWeight,
                ref RightFootRotationWeight);

            ApplyBodyPlacement(animatedBody, weight);
            ApplyFootGoal(AvatarIKGoal.LeftFoot, animatedLeft, animatedLeftRotation,
                leftSoleHeight, LeftFootHeightFromGround, leftHit, leftHitPlaceBase,
                smoothedLeftGroundOffset, smoothedLeftFootRotation,
                leftContactWeight * weight, LeftFootRotationWeight);
            ApplyFootGoal(AvatarIKGoal.RightFoot, animatedRight, animatedRightRotation,
                rightSoleHeight, RightFootHeightFromGround, rightHit, rightHitPlaceBase,
                smoothedRightGroundOffset, smoothedRightFootRotation,
                rightContactWeight * weight, RightFootRotationWeight);
        }

        private float GetAnimatedFootLift(Vector3 animatedPosition, float soleHeight, string curveName)
        {
            float lift = Mathf.Max(0f, animatedPosition.y - transform.position.y - soleHeight);
            if (UseDynamicFootPlacing)
            {
                // Con 1 la corrección de terreno preserva la zancada completa;
                // valores menores son una reducción elegida explícitamente.
                return lift * Mathf.Max(0f, FootHeightMultiplier);
            }

            if (!string.IsNullOrEmpty(curveName))
            {
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    if (parameter.name == curveName && parameter.type == AnimatorControllerParameterType.Float)
                    {
                        return Mathf.Max(lift, animator.GetFloat(curveName) * 0.5f);
                    }
                }
            }
            return lift;
        }

        private bool TryGroundHit(Vector3 origin, float radius, float distance,
            Vector3 footPosition, out RaycastHit closestHit)
        {
            closestHit = default;
            int count = Physics.SphereCastNonAlloc(origin, Mathf.Max(0.001f, radius),
                Vector3.down, groundHits, Mathf.Max(0f, distance), GroundLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = groundHits;
            if (count == groundHits.Length)
            {
                // Sólo el caso saturado necesita una consulta con asignación;
                // la consulta habitual no genera basura por pie/frame.
                hits = Physics.SphereCastAll(origin, Mathf.Max(0.001f, radius),
                    Vector3.down, Mathf.Max(0f, distance), GroundLayers,
                    QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            float closestDistance = float.PositiveInfinity;
            float maxAngle = movement != null ? Mathf.Clamp(movement.MaxWalkableAngle, 0f, 75f) : 45f;
            float lowestOffset = -Mathf.Max(0f, MaxStepHeight, MaxBodyCrouchHeight);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)
                    || Vector3.Angle(Vector3.up, hit.normal) > maxAngle)
                {
                    continue;
                }

                float height = GetGroundHeight(hit, footPosition) - transform.position.y;
                if (height > Mathf.Max(0f, MaxStepHeight) || height < lowestOffset
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestHit = hit;
                closestDistance = hit.distance;
            }
            return closestHit.collider != null;
        }

        private static float GetGroundHeight(RaycastHit hit, Vector3 atPosition)
        {
            // SphereCast puede tocar al costado en una pendiente. Proyectar el
            // plano al X/Z del pie evita confundir ese punto con su altura de apoyo.
            return hit.point.y - (hit.normal.x * (atPosition.x - hit.point.x)
                + hit.normal.z * (atPosition.z - hit.point.z)) / Mathf.Max(0.001f, hit.normal.y);
        }

        private static float SmoothingFactor(float speed)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, speed) * Time.deltaTime);
        }

        private void UpdateFootTarget(Vector3 animatedPosition, bool hasHit, bool hadHit,
            RaycastHit hit, float lift, ref float smoothedGroundOffset,
            ref Quaternion smoothedRotation, ref float contactWeight, ref float rotationWeight)
        {
            if (!hasHit)
            {
                smoothedGroundOffset = 0f;
                smoothedRotation = Quaternion.identity;
                contactWeight = rotationWeight = 0f;
                return;
            }

            float groundOffset = GetGroundHeight(hit, animatedPosition) - transform.position.y;
            smoothedGroundOffset = hadHit
                ? Mathf.Lerp(smoothedGroundOffset, groundOffset, SmoothingFactor(20f))
                : groundOffset;
            smoothedRotation = Quaternion.Slerp(smoothedRotation,
                Quaternion.FromToRotation(Vector3.up, hit.normal), SmoothingFactor(20f));
            contactWeight = SmoothTransitions
                ? Mathf.MoveTowards(contactWeight, 1f, Mathf.Max(0f, SpeedInTransition) * Time.deltaTime)
                : 1f;
            rotationWeight = Mathf.Lerp(rotationWeight, 1f - Mathf.Clamp01(lift / 0.3f),
                SmoothingFactor(8f));
        }

        private void ApplyFootGoal(AvatarIKGoal goal, Vector3 animatedPosition,
            Quaternion animatedRotation, float soleHeight, float lift, bool hasHit,
            RaycastHit hit, float groundOffset, Quaternion rotation, float weight, float rotationWeight)
        {
            if (!hasHit || weight <= 0f)
            {
                return; // ClearFootWeights ya dejó este pie en FK.
            }

            Vector3 position = animatedPosition;
            float slopeSoleHeight = soleHeight / Mathf.Max(0.25f, hit.normal.y);
            position.y = transform.position.y + groundOffset
                + Mathf.Max(slopeSoleHeight, soleHeight + lift);
            animator.SetIKPosition(goal, position);
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotation(goal, rotation * animatedRotation);
            animator.SetIKRotationWeight(goal, weight * rotationWeight);
        }

        private void ApplyBodyPlacement(Vector3 animatedBody, float weight)
        {
            TheresGroundBelow = TryGroundHit(
                transform.position + Vector3.up * Mathf.Max(0f, RaycastDistanceToGround),
                GroundCheckRadius, RaycastDistanceToGround + 0.2f,
                transform.position, out bodyGroundHit);
            groundAngle = TheresGroundBelow ? Vector3.Angle(Vector3.up, bodyGroundHit.normal) : 0f;

            float targetOffset = 0f;
            if (EnableDynamicBodyPlacing && !BlockBodyPositioning)
            {
                if (leftHit)
                {
                    targetOffset = Mathf.Min(targetOffset, smoothedLeftGroundOffset);
                }
                if (rightHit)
                {
                    targetOffset = Mathf.Min(targetOffset, smoothedRightGroundOffset);
                }
                targetOffset = Mathf.Max(targetOffset, -Mathf.Max(0f, MaxBodyCrouchHeight));
            }

            bodyPositionOffset = EnableDynamicBodyPlacing && !BlockBodyPositioning
                ? Mathf.Lerp(bodyPositionOffset, targetOffset,
                    SmoothingFactor(UpAndDownForce + groundAngle / 20f))
                : 0f;
            // Suavizar solamente el desnivel. La altura de la animación actual y
            // la traslación del root nunca pasan por un filtro ni por LastBodyPositionY.
            float stopLeanOffset = 0f;
            if (EnableDynamicBodyPlacing && RootBoneSpineMovement
                && CanApplyBodyLean() && !movement.IsMoving)
            {
                // Conservar el gesto al frenar como desplazamiento de esta pose,
                // equivalente a un tick y acotado a 3 cm, sin acumularlo cada frame.
                stopLeanOffset = -Mathf.Min(0.03f, Mathf.Max(0f, RootBoneDownMovementIntensity)
                    * Mathf.Abs(lean) * 0.1f * Time.fixedDeltaTime);
            }
            NewAnimationBodyPosition = animatedBody
                + Vector3.up * ((bodyPositionOffset + stopLeanOffset) * weight);
            if (!JustCalculateBodyPosition)
            {
                animator.bodyPosition = NewAnimationBodyPosition;
            }
            LastBodyPositionY = animator.bodyPosition.y;
        }

        private void ApplyAimLookAt()
        {
            if (movement == null)
            {
                animator.SetLookAtWeight(0f);
                return;
            }

            aimIKWeight = Mathf.MoveTowards(aimIKWeight,
                EnableAimLookAt && movement.FiringModeIK ? 1f : 0f,
                Mathf.Max(0f, AimIKTransitionSpeed) * Time.deltaTime);
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
            animator.SetLookAtWeight(intensity * aimIKWeight, bodyWeight, HeadIKBodyWeight);
            animator.SetLookAtPosition(lookPosition);
        }

        private bool CanApplyBodyLean()
        {
            // Modificar una cadera o un ancestro de los pies después del solver
            // desplazaría apoyos ya resueltos. El lean es de la columna superior.
            return RootBone != null && movement != null && RootBoneSpineLean
                && (leftFoot == null || !leftFoot.IsChildOf(RootBone))
                && (rightFoot == null || !rightFoot.IsChildOf(RootBone))
                && !movement.IsAiming && !movement.FiringMode
                && !movement.IsRolling && movement.IsGrounded;
        }

        private void ApplyBodyLean()
        {
            if (RootBone == null || movement == null)
            {
                return;
            }

            if (!CanApplyBodyLean())
            {
                leanSpeed = lean = 0f;
                return;
            }

            leanSpeed = Mathf.Lerp(leanSpeed, movement.VelocityMultiplier, SmoothingFactor(10f));
            float divisor = Mathf.Max(0.01f, BlockForwardLeanWeight);
            float targetLean = movement.IsMoving
                ? leanSpeed * RootBoneLeanIntensity / divisor
                : -(leanSpeed * RootBoneLeanIntensity * 0.5f);
            lean = Mathf.Lerp(lean, targetLean, SmoothingFactor(RootBoneLeanSpeed));

            // Capturar después del solver, no desde un callback anterior. El lean
            // modifica la pose actual una vez y no acumula desplazamiento corporal.
            unaffectedUpward = RootBone.up;
            Vector3 euler = RootBone.localEulerAngles;
            switch (AxisToLean)
            {
                case LeanAxis.X: euler.x += lean; break;
                case LeanAxis.Y: euler.y += lean; break;
                case LeanAxis.Z: euler.z += lean; break;
            }
            RootBone.localRotation = Quaternion.Euler(euler);

        }
    }
}
