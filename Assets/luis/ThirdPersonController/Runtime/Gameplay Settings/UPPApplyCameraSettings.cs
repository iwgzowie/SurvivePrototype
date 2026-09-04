using UPP.ThirdPersonController.CameraSystems;
using UnityEngine;

namespace UPP.ThirdPersonController.GameSettings
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UPP/Configuración/Aplicar configuración de cámara")]
    public class UPPApplyCameraSettings : MonoBehaviour
    {
        public UPPCameraController CameraController;

        private void Awake()
        {
            UPPGameSettings.OnChangeSettings += ApplySettings;
        }

        private void OnDestroy()
        {
            UPPGameSettings.OnChangeSettings -= ApplySettings;
        }

        private void OnEnable()
        {
            ApplySettings();
        }

        public void ApplySettings()
        {
            if (!CameraController)
                return;

            CameraController.GeneralSensibility = UPPGameSettings.CameraSensibility;
            CameraController.GeneralVerticalSensibility = UPPGameSettings.CameraSensibility;

            CameraController.InvertVertical = UPPGameSettings.CameraInvertVertical;
            CameraController.InvertHorizontal = UPPGameSettings.CameraInvertHorizontal;
        }
    }
}
