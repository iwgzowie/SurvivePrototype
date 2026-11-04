using System.Collections.Generic;
using UnityEngine;

namespace Survive.Combat
{
    /// <summary>Construye cuerpos articulados en el esqueleto real del zombi al morir por explosión.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyRagdoll : MonoBehaviour
    {
        public bool IsActive { get; private set; }
        public int BodyCount => bodies.Count;
        private readonly List<Rigidbody> bodies = new List<Rigidbody>();
        private static readonly HumanBodyBones[] Bones = { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg };
        private static readonly HumanBodyBones[] Ends = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck,
            HumanBodyBones.LastBone, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };

        public bool TryActivate(Vector3 origin, float impulse)
        {
            if (IsActive) return true;
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid) return false;
            var transforms = new List<Transform>(); var ends = new List<Transform>();
            for (int i = 0; i < Bones.Length; i++)
            {
                Transform bone = animator.GetBoneTransform(Bones[i]); if (bone == null) continue;
                transforms.Add(bone); ends.Add(Ends[i] != HumanBodyBones.LastBone ? animator.GetBoneTransform(Ends[i]) : null);
            }
            if (transforms.Count < 8) return false;
            animator.enabled = false;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            Rigidbody rootBody = GetComponent<Rigidbody>(); if (rootBody != null) rootBody.isKinematic = true;
            var colliders = new List<Collider>();
            for (int i = 0; i < transforms.Count; i++)
            {
                Transform bone = transforms[i]; Rigidbody body = bone.GetComponent<Rigidbody>();
                if (body == null) body = bone.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true; body.mass = i == 0 ? 8 : i < 3 ? 5 : 2;
                body.useGravity = true; body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; body.maxAngularVelocity = 15;
                body.linearDamping = .12f; body.angularDamping = .35f;
                Collider collider;
                float scale = Mathf.Max(.001f, Mathf.Abs(bone.lossyScale.x));
                if (ends[i] == null)
                {
                    SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>(); sphere.radius = .12f / scale; collider = sphere;
                }
                else
                {
                    Vector3 segment = bone.InverseTransformPoint(ends[i].position);
                    CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                    capsule.center = segment * .5f;
                    Vector3 a = new Vector3(Mathf.Abs(segment.x), Mathf.Abs(segment.y), Mathf.Abs(segment.z));
                    capsule.direction = a.x > a.y && a.x > a.z ? 0 : a.y > a.z ? 1 : 2;
                    capsule.radius = (i < 3 ? .13f : .065f) / scale;
                    capsule.height = Mathf.Max(capsule.radius * 2, segment.magnitude); collider = capsule;
                }
                colliders.Add(collider); bodies.Add(body);
            }
            for (int i = 1; i < bodies.Count; i++)
            {
                Transform parent = bodies[i].transform.parent; Rigidbody connected = null;
                while (parent != null && parent != transform)
                { Rigidbody candidate = parent.GetComponent<Rigidbody>(); if (candidate != null && bodies.Contains(candidate)) { connected = candidate; break; } parent = parent.parent; }
                if (connected == null) connected = bodies[0];
                CharacterJoint joint = bodies[i].gameObject.AddComponent<CharacterJoint>(); joint.connectedBody = connected;
                joint.enableProjection = true; joint.projectionDistance = .05f; joint.projectionAngle = 10;
                joint.lowTwistLimit = new SoftJointLimit { limit = -30 }; joint.highTwistLimit = new SoftJointLimit { limit = 30 };
                joint.swing1Limit = new SoftJointLimit { limit = 40 }; joint.swing2Limit = new SoftJointLimit { limit = 30 };
            }
            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++) Physics.IgnoreCollision(colliders[i], colliders[j]);
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
            IsActive = true;
            Vector3 push = transform.position + Vector3.up - origin; push.y = Mathf.Max(.6f, push.y);
            push = push.normalized * Mathf.Max(0, impulse);
            foreach (Rigidbody body in bodies) { body.isKinematic = false; body.AddForce(push, ForceMode.VelocityChange); }
            // El impacto desequilibra la pelvis: evita que el cuerpo conserve una pose vertical durante el vuelo.
            Vector3 outward = transform.position - origin; outward.y = 0;
            if (outward.sqrMagnitude < .0001f) outward = transform.forward;
            bodies[0].AddTorque(Vector3.Cross(Vector3.up, outward.normalized) * Mathf.Max(0, impulse) * .65f, ForceMode.VelocityChange);
            return true;
        }
    }
}
