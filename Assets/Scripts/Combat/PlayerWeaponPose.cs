using Survive.Items;
using UnityEngine;
using UPP.ThirdPersonController;

namespace Survive.Combat
{
    /// <summary>Termina la pose sobre la animación evaluada sin separar el arma de su empuñadura.</summary>
    [DefaultExecutionOrder(1050)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Survive/Combate/Pose del arma")]
    public sealed class PlayerWeaponPose : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private UPPCharacterMovementComponent movement;
        [SerializeField] private PlayerWeaponController weaponController;
        [SerializeField, Min(1f)] private float blendSpeed = 14f;
        [Header("Retroceso visual de respaldo")]
        [SerializeField, Min(0f)] private float recoilPitchDegrees = 1.2f;
        [SerializeField, Min(0f)] private float recoilBackDistance = .025f;
        [SerializeField, Min(.1f)] private float recoverySpeed = 14f;
        [SerializeField, Min(0f)] private float maxRecoilPitch = 5f;
        [SerializeField, Min(0f)] private float maxRecoilDistance = .06f;

        private Transform rightHand, rightUpperArm, rightForearm;
        private Transform leftHand, leftUpperArm, leftForearm;
        private WeaponItem cachedWeapon;
        private Avatar cachedAvatar;
        private float aimWeight, kickPitch, kickDistance;
        private int evaluatedFrame = -1;

        public int LastSolvedFrame { get; private set; } = -1;
        /// <summary>-1 identifica el ajuste final de huesos después del Animator.</summary>
        public int LastSolvedLayer { get; private set; } = -1;
        public float LeftHandReachError { get; private set; }

        public bool HandlesAlignment => isActiveAndEnabled && animator != null && animator.isHuman
            && rightHand != null && weaponController != null && weaponController.EquippedWeapon != null
            && weaponController.EquippedWeapon.transform.IsChildOf(rightHand);

        private void Reset() => ResolveReferences();
        private void Awake() => ResolveReferences();

        private void OnEnable()
        {
            ResolveReferences();
            if (weaponController == null) return;
            weaponController.WeaponChanged += OnWeaponChanged;
            weaponController.ShotFired += OnShotFired;
            OnWeaponChanged(weaponController.EquippedWeapon);
        }

        private void OnDisable()
        {
            if (weaponController != null)
            {
                weaponController.WeaponChanged -= OnWeaponChanged;
                weaponController.ShotFired -= OnShotFired;
            }
            cachedWeapon = null;
            aimWeight = kickPitch = kickDistance = 0f;
            evaluatedFrame = -1;
        }

        private void ResolveReferences()
        {
            animator ??= GetComponent<Animator>();
            movement ??= GetComponentInParent<UPPCharacterMovementComponent>();
            weaponController ??= GetComponentInParent<PlayerWeaponController>();
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid) return;
            cachedAvatar = animator.avatar;
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        }

        private void Update()
        {
            if (animator != null && animator.avatar != cachedAvatar) ResolveReferences();
            WeaponItem weapon = weaponController != null ? weaponController.EquippedWeapon : null;
            if (weapon != cachedWeapon) OnWeaponChanged(weapon);
            float recovery = weapon != null && weapon.Definition != null
                ? weapon.Definition.RecoilRecoverySpeed : recoverySpeed;
            float decay = Mathf.Exp(-Mathf.Max(.1f, recovery) * Time.deltaTime);
            kickPitch *= decay;
            kickDistance *= decay;
        }

        private void OnWeaponChanged(WeaponItem weapon)
        {
            cachedWeapon = weapon;
            aimWeight = kickPitch = kickDistance = 0f;
        }

        private void OnShotFired(WeaponItem weapon)
        {
            float pitch = weapon != null && weapon.Definition != null
                ? weapon.Definition.RecoilDegrees : recoilPitchDegrees;
            float distance = weapon != null && weapon.Definition != null
                ? weapon.Definition.RecoilBackDistance : recoilBackDistance;
            kickPitch = Mathf.Min(maxRecoilPitch, kickPitch + Mathf.Max(0f, pitch));
            kickDistance = Mathf.Min(maxRecoilDistance, kickDistance + Mathf.Max(0f, distance));
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman) return;
            // El ajuste usa los huesos físicos después del IK de UPP, evitando
            // diferencias de ejes entre el objetivo humanoide y el rig importado.
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            evaluatedFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            if (evaluatedFrame != Time.frameCount || animator == null || !animator.isActiveAndEnabled) return;
            WeaponItem weapon = weaponController != null ? weaponController.EquippedWeapon : null;
            bool released = !HandlesAlignment || weapon == null || weapon.IsReloading
                || weaponController.IsReloadAnimationActive || movement == null
                || !movement.isActiveAndEnabled || movement.DisableAllMove || movement.IsRolling
                || (weaponController.Health != null && weaponController.Health.IsDead);
            if (released) { aimWeight = 0f; return; }
            bool hasAimPoint = weaponController.TryGetAimPoint(out Vector3 aimPoint);
            aimWeight = Mathf.MoveTowards(aimWeight, weaponController.IsAiming && hasAimPoint ? 1f : 0f,
                blendSpeed * Time.deltaTime);
            if (aimWeight <= 0f || !hasAimPoint) return;

            // Pose fresca del Animator. Cámara/trace corren antes (100/1000),
            // disparo después (1100); nunca se acumula sobre el cuadro anterior.
            Quaternion animatedRightRotation = rightHand.rotation;
            if (kickDistance > .00001f)
            {
                Vector3 direction = (aimPoint - rightHand.position).normalized;
                SolveArm(rightUpperArm, rightForearm, rightHand,
                    rightHand.position - direction * kickDistance * aimWeight);
            }
            for (int iteration = 0; iteration < 8; iteration++)
            {
                Vector3 direction = aimPoint - weapon.Muzzle.position;
                if (direction.sqrMagnitude <= .000001f) return;
                rightHand.rotation = Quaternion.FromToRotation(weapon.Muzzle.forward, direction.normalized)
                    * rightHand.rotation;
            }
            Quaternion targetRightRotation = Quaternion.AngleAxis(-kickPitch, weapon.transform.right)
                * rightHand.rotation;
            rightHand.rotation = Quaternion.Slerp(animatedRightRotation, targetRightRotation, aimWeight);

            if (weapon.TwoHanded && weapon.LeftHandGrip != null && leftHand != null)
            {
                Vector3 targetPosition = Vector3.Lerp(leftHand.position, weapon.LeftHandGrip.position, aimWeight);
                Quaternion targetRotation = Quaternion.Slerp(leftHand.rotation, weapon.LeftHandGrip.rotation, aimWeight);
                SolveArm(leftUpperArm, leftForearm, leftHand, targetPosition);
                leftHand.rotation = targetRotation;
                LeftHandReachError = Vector3.Distance(leftHand.position, weapon.LeftHandGrip.position);
            }
            LastSolvedFrame = Time.frameCount;
            LastSolvedLayer = -1;
        }

        /// <summary>Dos segmentos rígidos: conserva el plano de codo, sin estirar ni escalar huesos.</summary>
        private void SolveArm(Transform upperArm, Transform forearm, Transform hand, Vector3 target)
        {
            if (upperArm == null || forearm == null || hand == null) return;
            Vector3 root = upperArm.position;
            Vector3 upper = forearm.position - root;
            float a = upper.magnitude;
            float b = Vector3.Distance(hand.position, forearm.position);
            Vector3 direction = target - root;
            float distance = direction.magnitude;
            if (a < .0001f || b < .0001f || distance < .0001f) return;
            direction /= distance;
            float reachable = Mathf.Clamp(distance, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Vector3 bend = Vector3.ProjectOnPlane(upper, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(-transform.up, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(transform.right, direction);
            bend.Normalize();
            float along = (a * a - b * b + reachable * reachable) / (2f * reachable);
            float outward = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 elbow = root + direction * along + bend * outward;
            upperArm.rotation = Quaternion.FromToRotation(upper, elbow - root) * upperArm.rotation;
            Vector3 wrist = root + direction * reachable;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position,
                wrist - forearm.position) * forearm.rotation;
        }
    }
}
