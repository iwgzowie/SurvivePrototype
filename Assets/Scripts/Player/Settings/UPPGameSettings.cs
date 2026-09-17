using UnityEngine;
using UnityEngine.Events;

namespace UPP.ThirdPersonController.GameSettings
{
    [AddComponentMenu("UPP/Configuración/Configuración de cámara")]
    public sealed class UPPGameSettings : MonoBehaviour
    {
        private const string CameraInvertVerticalKey =
            "SETTINGS_CONTROLS_CAMERA_INVERT_VERTICAL";
        private const string CameraInvertHorizontalKey =
            "SETTINGS_CONTROLS_CAMERA_INVERT_HORIZONTAL";
        private const string CameraSensibilityKey =
            "SETTINGS_CONTROLS_CAMERA_SENSITIVE";
        /// <summary>
        /// Se emite cuando cambia o se vuelve a aplicar la configuración.
        /// </summary>
        public static event UnityAction OnChangeSettings;

        /// <summary>
        /// Indica si el movimiento vertical de la cámara está invertido.
        /// </summary>
        public static bool CameraInvertVertical
        {
            get => PlayerPrefs.GetInt(CameraInvertVerticalKey, 0) == 1;
            set
            {
                if (value == CameraInvertVertical)
                {
                    return;
                }

                PlayerPrefs.SetInt(
                    CameraInvertVerticalKey,
                    value ? 1 : 0);
                OnChangeSettings?.Invoke();
            }
        }

        /// <summary>
        /// Indica si el movimiento horizontal de la cámara está invertido.
        /// </summary>
        public static bool CameraInvertHorizontal
        {
            get => PlayerPrefs.GetInt(CameraInvertHorizontalKey, 0) == 1;
            set
            {
                if (value == CameraInvertHorizontal)
                {
                    return;
                }

                PlayerPrefs.SetInt(
                    CameraInvertHorizontalKey,
                    value ? 1 : 0);
                OnChangeSettings?.Invoke();
            }
        }

        /// <summary>
        /// Sensibilidad general de la cámara, limitada a un rango seguro.
        /// </summary>
        public static float CameraSensibility
        {
            get => PlayerPrefs.GetFloat(CameraSensibilityKey, 1f);
            set
            {
                float clampedValue = Mathf.Clamp(value, 0.01f, 10f);
                if (Mathf.Approximately(
                    clampedValue,
                    CameraSensibility))
                {
                    return;
                }

                PlayerPrefs.SetFloat(
                    CameraSensibilityKey,
                    clampedValue);
                OnChangeSettings?.Invoke();
            }
        }

        private void Awake()
        {
            ApplySettings();
        }

        /// <summary>
        /// Solicita a los consumidores que vuelvan a leer las preferencias.
        /// </summary>
        public static void ApplySettings()
        {
            OnChangeSettings?.Invoke();
        }

        /// <summary>
        /// Restablece únicamente las preferencias de cámara administradas aquí.
        /// </summary>
        [ContextMenu("Restablecer configuración de cámara", false, 100)]
        public void ResetSettings()
        {
            PlayerPrefs.DeleteKey(CameraInvertVerticalKey);
            PlayerPrefs.DeleteKey(CameraInvertHorizontalKey);
            PlayerPrefs.DeleteKey(CameraSensibilityKey);
            PlayerPrefs.Save();

            ApplySettings();
        }
    }
}
