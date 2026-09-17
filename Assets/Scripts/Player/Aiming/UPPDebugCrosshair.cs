using UnityEngine;
using UPP.ThirdPersonController.CameraSystems;

namespace UPP.Runtime.Aiming
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UPP/Aim/Crosshair de depuración")]
    public sealed class UPPDebugCrosshair : MonoBehaviour
    {
        [SerializeField] private UPPAimTrace aimTrace;
        [SerializeField] private UPPTPSCameraController cameraController;
        [SerializeField] private bool alwaysVisible;
        [SerializeField] private float lineLength = 8f;
        [SerializeField] private float gap = 4f;
        [SerializeField] private float thickness = 2f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hitColor = Color.green;

        public bool DebugVisible { get; set; } = true;

        public bool IsVisible =>
            DebugVisible
            && (alwaysVisible
                || (cameraController != null
                    && cameraController.characterTarget != null
                    && cameraController.characterTarget.IsAiming));

        public void Configure(
            UPPAimTrace trace,
            UPPTPSCameraController controller)
        {
            aimTrace = trace;
            cameraController = controller;
            alwaysVisible = false;
        }

        private void OnGUI()
        {
            if (!IsVisible)
            {
                return;
            }

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            Color previousColor = GUI.color;
            GUI.color = aimTrace != null && aimTrace.HasHit ? hitColor : normalColor;

            DrawRect(centerX - gap - lineLength, centerY - thickness * 0.5f, lineLength, thickness);
            DrawRect(centerX + gap, centerY - thickness * 0.5f, lineLength, thickness);
            DrawRect(centerX - thickness * 0.5f, centerY - gap - lineLength, thickness, lineLength);
            DrawRect(centerX - thickness * 0.5f, centerY + gap, thickness, lineLength);
            DrawRect(centerX - 1f, centerY - 1f, 2f, 2f);

            GUI.color = previousColor;
        }

        private static void DrawRect(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        }
    }
}
