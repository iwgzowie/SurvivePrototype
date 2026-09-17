using UnityEngine;
using UnityEngine.InputSystem;

namespace UPP.ThirdPersonController.UPPInputSystem
{
    [CreateAssetMenu(
        fileName = "Player Character Inputs",
        menuName = "UPP/Inputs/Player Character Inputs")]
    public sealed class UPPPlayerCharacterInputAsset : ScriptableObject
    {
        [System.Serializable]
        public sealed class MovementActions
        {
            [Header("Movimiento")]
            [Tooltip("Movimiento del personaje. WASD en PC y stick izquierdo como alternativa.")]
            public InputAction MoveAction;

            [Tooltip("Rotación de cámara. Mouse en PC y stick derecho como alternativa.")]
            public InputAction LookAction;

            [Tooltip("Salto del personaje.")]
            public InputAction JumpAction;

            [Tooltip("Carrera o sprint mientras se mantiene presionado.")]
            public InputAction RunAction;

            [Header("Postura")]
            [Tooltip("Alterna o mantiene el modo de apuntado según la configuración del controlador.")]
            public InputAction AimAction;

            [Tooltip("Alterna la postura agachada.")]
            public InputAction CrouchAction;

            [Tooltip("Alterna la postura cuerpo a tierra.")]
            public InputAction ProneAction;

            [Tooltip("Ejecuta la rodada.")]
            public InputAction RollAction;

            internal void Reset()
            {
                MoveAction = new InputAction(
                    "Move",
                    InputActionType.Value,
                    expectedControlType: "Vector2");
                MoveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                MoveAction.AddBinding("<Gamepad>/leftStick");

                LookAction = new InputAction(
                    "Look",
                    InputActionType.Value,
                    expectedControlType: "Vector2");
                LookAction.AddBinding("<Mouse>/delta")
                    .WithProcessor("scaleVector2(x=0.2,y=0.1)");
                LookAction.AddBinding("<Gamepad>/rightStick")
                    .WithProcessor("scaleVector2(x=1,y=0.6)");

                JumpAction = new InputAction(
                    "Jump",
                    InputActionType.Button);
                JumpAction.AddBinding("<Keyboard>/space");
                JumpAction.AddBinding("<Gamepad>/buttonSouth");

                RunAction = new InputAction(
                    "Run",
                    InputActionType.Button);
                RunAction.AddBinding("<Keyboard>/leftShift");
                RunAction.AddBinding("<Gamepad>/leftStickPress");

                AimAction = new InputAction(
                    "Aim",
                    InputActionType.Button);
                AimAction.AddBinding("<Mouse>/rightButton");
                AimAction.AddBinding("<Gamepad>/leftTrigger");

                CrouchAction = new InputAction(
                    "Crouch",
                    InputActionType.Button);
                CrouchAction.AddBinding("<Keyboard>/c");
                CrouchAction.AddBinding("<Gamepad>/dpad/down");

                ProneAction = new InputAction(
                    "Prone",
                    InputActionType.Button);
                ProneAction.AddBinding("<Keyboard>/z");
                ProneAction.AddBinding("<Gamepad>/dpad/up");

                RollAction = new InputAction(
                    "Roll",
                    InputActionType.Button);
                RollAction.AddBinding("<Keyboard>/leftCtrl");
                RollAction.AddBinding("<Gamepad>/buttonEast");
            }

            internal void SetActive(bool active)
            {
                SetActionActive(MoveAction, active);
                SetActionActive(LookAction, active);
                SetActionActive(JumpAction, active);
                SetActionActive(RunAction, active);
                SetActionActive(AimAction, active);
                SetActionActive(CrouchAction, active);
                SetActionActive(ProneAction, active);
                SetActionActive(RollAction, active);
            }

            private static void SetActionActive(
                InputAction action,
                bool active)
            {
                if (action == null)
                {
                    return;
                }

                if (active)
                {
                    action.Enable();
                }
                else
                {
                    action.Disable();
                }
            }
        }

        [Tooltip("Acciones disponibles para locomoción y control de cámara.")]
        public MovementActions Movement = new MovementActions();

        public Vector2 MoveAxis
        {
            get
            {
                if (Movement?.MoveAction == null)
                {
                    return Vector2.zero;
                }

                return Movement.MoveAction.ReadValue<Vector2>().normalized;
            }
        }

        public Vector2 LookAxis =>
            Movement?.LookAction != null
                ? Movement.LookAction.ReadValue<Vector2>()
                : Vector2.zero;

        public bool IsJumpTriggered =>
            Movement?.JumpAction != null
            && Movement.JumpAction.WasPressedThisFrame();

        public bool IsRunPressed =>
            Movement?.RunAction != null
            && Movement.RunAction.IsPressed();

        public bool IsAimTriggered =>
            Movement?.AimAction != null
            && Movement.AimAction.WasPressedThisFrame();

        public bool IsAimPressed =>
            Movement?.AimAction != null
            && Movement.AimAction.IsPressed();

        public bool IsCrouchTriggered =>
            Movement?.CrouchAction != null
            && Movement.CrouchAction.WasPressedThisFrame();

        public bool IsProneTriggered =>
            Movement?.ProneAction != null
            && Movement.ProneAction.WasPressedThisFrame();

        public bool IsRollTriggered =>
            Movement?.RollAction != null
            && Movement.RollAction.WasPressedThisFrame();

        private void Reset()
        {
            Movement ??= new MovementActions();
            Movement.Reset();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged +=
                OnPlayModeStateChanged;
#endif
        }

        private void OnDisable()
        {
            SetActiveInputs(false);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -=
                OnPlayModeStateChanged;
#endif
        }

        public void SetActiveInputs(bool active)
        {
            Movement?.SetActive(active);
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(
            UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                SetActiveInputs(false);
            }
        }
#endif
    }
}
