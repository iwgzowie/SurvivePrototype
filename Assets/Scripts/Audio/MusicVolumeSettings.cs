using System;
using UnityEngine;

namespace Survive.Audio
{
    /// <summary>Una preferencia de música compartida por opciones y pausa.</summary>
    public static class MusicVolumeSettings
    {
        public const string PreferenceKey = "Survive.Options.MusicVolume";
        static float volume = -1f;
        static bool dirty;
        public static event Action<float> Changed;

        public static float Volume
        {
            get
            {
                if (volume < 0f)
                {
                    float saved = PlayerPrefs.GetFloat(PreferenceKey, 1f);
                    volume = float.IsNaN(saved) || float.IsInfinity(saved) ? 1f : Mathf.Clamp01(saved);
                }
                return volume;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            volume = -1f;
            dirty = false;
            Changed = null;
        }

        public static void SetVolume(float value, bool persist = true)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            value = Mathf.Clamp01(value);
            volume = value;
            if (Application.isPlaying && persist)
            {
                PlayerPrefs.SetFloat(PreferenceKey, value);
                dirty = true;
            }
            Changed?.Invoke(value);
        }

        public static void Save()
        {
            if (!Application.isPlaying || !dirty) return;
            PlayerPrefs.Save();
            dirty = false;
        }
    }
}
