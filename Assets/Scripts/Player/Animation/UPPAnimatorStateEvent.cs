using UnityEngine;

namespace UPP.ThirdPersonController.AnimatorStateMachineBehaviours
{
    /// <summary>
    /// Ejecuta eventos de locomoción en un punto normalizado del estado.
    /// </summary>
    public sealed class UPPAnimatorStateEvent : StateMachineBehaviour
    {
        public enum UPPAnimDefaultEvents
        {
            None = 0,

            // Los valores 1, 2 y 3 pertenecían a eventos de armas eliminados.
            DisableMovement = 4,
            EnableMovement = 5,
            DisableRotation = 6,
            EnableRotation = 7,
            DisableAimIK = 8,
            EnableAimIK = 9,
            StopRolling = 10,
            StartRolling = 11
        }

        public UPPAnimDefaultEvents DefaultEvent = UPPAnimDefaultEvents.None;

        [Range(0f, 1f)]
        public float Duration;

        private UPPCharacterMovementComponent movement;

        [HideInInspector]
        public bool CalledAnimationEvent;

        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            CalledAnimationEvent = false;
        }

        public override void OnStateUpdate(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (CalledAnimationEvent || stateInfo.normalizedTime < Duration)
                return;

            CalledAnimationEvent = true;

            if (DefaultEvent == UPPAnimDefaultEvents.None)
                return;

            if (movement == null)
            {
                movement = animator.GetComponent<UPPCharacterMovementComponent>();
            }

            if (movement == null)
            {
                Debug.LogError(
                    $"UPP: el estado '{stateInfo.fullPathHash}' intentó ejecutar " +
                    "un evento de locomoción, pero no encontró el controlador.",
                    animator);
                return;
            }

            CallDefaultEvent(DefaultEvent, movement);
        }

        public static void CallDefaultEvent(
            UPPAnimDefaultEvents defaultEvent,
            UPPCharacterMovementComponent targetMovement)
        {
            if (targetMovement == null)
                return;

            switch (defaultEvent)
            {
                case UPPAnimDefaultEvents.DisableMovement:
                    targetMovement.disableMove();
                    break;
                case UPPAnimDefaultEvents.EnableMovement:
                    targetMovement.enableMove();
                    break;
                case UPPAnimDefaultEvents.DisableRotation:
                    targetMovement.disableRotation();
                    break;
                case UPPAnimDefaultEvents.EnableRotation:
                    targetMovement.enableRotation();
                    break;
                case UPPAnimDefaultEvents.DisableAimIK:
                    targetMovement.disableFireModeIK();
                    break;
                case UPPAnimDefaultEvents.EnableAimIK:
                    targetMovement.enableFireModeIK();
                    break;
                case UPPAnimDefaultEvents.StopRolling:
                    targetMovement.stopRolling();
                    break;
                case UPPAnimDefaultEvents.StartRolling:
                    targetMovement.startRolling();
                    break;
            }
        }
    }
}
