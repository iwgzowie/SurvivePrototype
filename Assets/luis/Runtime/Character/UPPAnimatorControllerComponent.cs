using System;
using System.Collections.Generic;
using UnityEngine;

namespace UPP.ThirdPersonController
{
    /// <summary>
    /// Centraliza el controlador, las capas y los parámetros del Animator.
    /// La física y la decisión de root motion pertenecen al movimiento.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("UPP/Personaje/Controlador de Animator")]
    [RequireComponent(typeof(Animator))]
    public sealed class UPPAnimatorControllerComponent : MonoBehaviour
    {
        [Serializable]
        public sealed class ParameterMap
        {
            [Header("Índices de capas")]
            public int BaseLayerIndex;
            public int LegsLayerIndex = 1;

            [Header("Parámetros de locomoción")]
            public string Moving = "Moving";
            public string Running = "Running";
            public string Speed = "Speed";
            public string HorizontalInput = "Horizontal";
            public string VerticalInput = "Vertical";
            public string IdleTurn = "IdleTurn";
            public string MovingTurn = "MovingTurn";
            public string Grounded = "Grounded";
            public string Jumping = "Jumping";
            public string AimMode = "FireMode";
            public string Crouched = "Crouched";
            public string Prone = "Prone";
            public string Roll = "Roll";
            public string LandingIntensity = "LandingIntensity";
        }

        [Header("Referencias")]
        [SerializeField] private UPPCharacterMovementComponent movement;
        [SerializeField] private Animator animator;

        [Header("Controlador de animación")]
        [SerializeField] private RuntimeAnimatorController controllerAsset;
        [SerializeField] private bool assignControllerOnAwake = true;
        [SerializeField] private AnimatorUpdateMode updateMode = AnimatorUpdateMode.Fixed;
        [SerializeField, Min(0f)] private float layerBlendSpeed = 5f;
        [SerializeField] private ParameterMap parameters = new ParameterMap();

        private readonly Dictionary<int, AnimatorControllerParameterType>
            parameterTypes = new Dictionary<int, AnimatorControllerParameterType>();
        private RuntimeAnimatorController cachedController;
        private float legsLayerWeight;
        private float movingTurn;
        private float idleTurn;

        public Animator Animator => animator;
        public ParameterMap Parameters => parameters;
        public bool CanTriggerRoll =>
            HasParameter(parameters.Roll, AnimatorControllerParameterType.Trigger);

        private void Reset()
        {
            ResolveReferences();
            if (animator != null)
            {
                controllerAsset = animator.runtimeAnimatorController;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            if (animator == null)
            {
                enabled = false;
                return;
            }

            if (assignControllerOnAwake && controllerAsset != null)
            {
                animator.runtimeAnimatorController = controllerAsset;
            }
            else if (controllerAsset == null)
            {
                controllerAsset = animator.runtimeAnimatorController;
            }

            animator.updateMode = updateMode;
            RefreshParameterCache();
        }

        private void Update()
        {
            if (animator == null || movement == null)
            {
                return;
            }

            RefreshParameterCache();
            UpdateParameters();
            UpdateLayerWeights();
        }

        public void AssignController(RuntimeAnimatorController newController)
        {
            controllerAsset = newController;
            if (animator != null)
            {
                animator.runtimeAnimatorController = newController;
            }

            RefreshParameterCache(true);
        }

        public bool ValidateAnimatorContract()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            RefreshParameterCache(true);
            return IsValidLayer(parameters.BaseLayerIndex)
                && IsValidLayer(parameters.LegsLayerIndex)
                && HasParameter(parameters.Moving, AnimatorControllerParameterType.Bool)
                && HasParameter(parameters.Running, AnimatorControllerParameterType.Bool)
                && HasParameter(parameters.Speed, AnimatorControllerParameterType.Float)
                && HasParameter(parameters.HorizontalInput, AnimatorControllerParameterType.Float)
                && HasParameter(parameters.VerticalInput, AnimatorControllerParameterType.Float)
                && HasParameter(parameters.IdleTurn, AnimatorControllerParameterType.Float)
                && HasParameter(parameters.MovingTurn, AnimatorControllerParameterType.Float)
                && HasParameter(parameters.Grounded, AnimatorControllerParameterType.Bool)
                && HasParameter(parameters.Jumping, AnimatorControllerParameterType.Bool)
                && HasParameter(parameters.AimMode, AnimatorControllerParameterType.Bool)
                && HasParameter(parameters.Crouched, AnimatorControllerParameterType.Bool)
                && HasParameter(parameters.Prone, AnimatorControllerParameterType.Bool)
                && HasParameter(parameters.LandingIntensity, AnimatorControllerParameterType.Float)
                && HasParameter(parameters.Roll, AnimatorControllerParameterType.Trigger);
        }

        public void TriggerRoll()
        {
            SetTrigger(parameters.Roll);
        }

        public void SetGroundContact(bool grounded, float landingIntensity)
        {
            SetBool(parameters.Grounded, grounded);
            SetFloat(parameters.LandingIntensity, landingIntensity);
        }

        private void ResolveReferences()
        {
            movement ??= GetComponent<UPPCharacterMovementComponent>();
            animator ??= GetComponent<Animator>();
            parameters ??= new ParameterMap();
        }

        private void UpdateParameters()
        {
            Vector2 directionalInput = movement.GetAimMovementBlendTreeInput();
            float vertical = 0f;
            float horizontal = 0f;

            if (movement.FiringMode)
            {
                vertical = Mathf.Lerp(
                    GetFloat(parameters.VerticalInput),
                    3f * movement.VelocityMultiplier * directionalInput.y,
                    4f * Time.deltaTime);
                horizontal = Mathf.Lerp(
                    GetFloat(parameters.HorizontalInput),
                    3f * movement.VelocityMultiplier * directionalInput.x,
                    4f * Time.deltaTime);
            }

            SetFloat(parameters.VerticalInput, vertical);
            SetFloat(parameters.HorizontalInput, horizontal);
            SetFloat(parameters.Speed, movement.VelocityMultiplier);
            SetBool(parameters.Moving, movement.IsMoving);
            SetBool(parameters.Running, movement.IsRunning);
            SetBool(parameters.Grounded, movement.IsGrounded);
            SetBool(parameters.Jumping, movement.IsJumping);
            SetBool(parameters.Crouched, movement.IsCrouched);
            SetBool(parameters.Prone, movement.IsProne);
            SetBool(parameters.AimMode, movement.FiringMode);

            movingTurn = movement.UpdateMovingTurn(movingTurn);
            idleTurn = movement.UpdateIdleTurn(idleTurn);
            SetFloat(parameters.MovingTurn, movingTurn);
            SetFloat(parameters.IdleTurn, idleTurn);
        }

        private void UpdateLayerWeights()
        {
            bool useDirectionalLegs =
                movement.FiringMode
                && !movement.IsRolling
                && !movement.DisableAllMove;
            legsLayerWeight = Mathf.MoveTowards(
                legsLayerWeight,
                useDirectionalLegs ? 1f : 0f,
                layerBlendSpeed * Time.deltaTime);

            if (parameters.LegsLayerIndex >= 0
                && parameters.LegsLayerIndex < animator.layerCount)
            {
                animator.SetLayerWeight(
                    parameters.LegsLayerIndex,
                    legsLayerWeight);
            }
        }

        private void RefreshParameterCache(bool force = false)
        {
            RuntimeAnimatorController current = animator != null
                ? animator.runtimeAnimatorController
                : null;
            if (!force && cachedController == current)
            {
                return;
            }

            cachedController = current;
            parameterTypes.Clear();
            if (animator == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                parameterTypes[parameter.nameHash] = parameter.type;
            }
        }

        private bool HasParameter(
            string parameterName,
            AnimatorControllerParameterType expectedType)
        {
            if (string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            int hash = UnityEngine.Animator.StringToHash(parameterName);
            return parameterTypes.TryGetValue(hash, out AnimatorControllerParameterType type)
                && type == expectedType;
        }

        private bool IsValidLayer(int layerIndex)
        {
            return animator != null
                && layerIndex >= 0
                && layerIndex < animator.layerCount;
        }

        private float GetFloat(string parameterName)
        {
            return HasParameter(parameterName, AnimatorControllerParameterType.Float)
                ? animator.GetFloat(parameterName)
                : 0f;
        }

        private void SetBool(string parameterName, bool value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(parameterName, value);
            }
        }

        private void SetFloat(string parameterName, float value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(parameterName, value);
            }
        }

        private void SetTrigger(string parameterName)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(parameterName);
            }
        }
    }
}
