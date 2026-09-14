using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Conecta los controles existentes de pantalla y volumen con opciones persistentes.</summary>
[DisallowMultipleComponent]
public sealed class MenuOptions : MonoBehaviour
{
    [Header("Controles existentes")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider volumeSlider;

    [Header("Preferencias")]
    [SerializeField] private bool persistSettings = true;
    [SerializeField] private string preferenceKeyPrefix = "Survive.Options";

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private TMP_Dropdown boundResolutionDropdown;
    private Toggle boundFullscreenToggle;
    private Slider boundVolumeSlider;
    private int selectedResolution;
    private bool fullscreen;
    private bool preferencesDirty;

    private string PreferenceKey(string suffix)
    {
        string prefix = string.IsNullOrWhiteSpace(preferenceKeyPrefix) ? "Survive.Options" : preferenceKeyPrefix;
        return prefix + "." + suffix;
    }

    private void OnEnable()
    {
        UnbindControls();
        InitializeOptions();

        boundResolutionDropdown = resolutionDropdown;
        boundFullscreenToggle = fullscreenToggle;
        boundVolumeSlider = volumeSlider;
        if (boundResolutionDropdown != null)
        {
            boundResolutionDropdown.onValueChanged.AddListener(SetResolution);
        }
        if (boundFullscreenToggle != null)
        {
            boundFullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
        if (boundVolumeSlider != null)
        {
            boundVolumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    private void OnDisable()
    {
        UnbindControls();
        SavePreferences();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SavePreferences();
        }
    }

    private void OnApplicationQuit()
    {
        SavePreferences();
    }

    private void InitializeOptions()
    {
        resolutions.Clear();
        var unique = new HashSet<Vector2Int>();
        foreach (Resolution available in Screen.resolutions)
        {
            var size = new Vector2Int(available.width, available.height);
            if (size.x > 0 && size.y > 0 && unique.Add(size))
            {
                resolutions.Add(size);
            }
        }

        // También conserva tamaños de ventana que no figuran en los modos del monitor.
        var currentSize = new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        if (unique.Add(currentSize))
        {
            resolutions.Add(currentSize);
        }
        resolutions.Sort((left, right) => left.x == right.x ? left.y.CompareTo(right.y) : left.x.CompareTo(right.x));

        var desiredSize = currentSize;
        fullscreen = Screen.fullScreen;
        float volume = AudioListener.volume;
        if (persistSettings)
        {
            desiredSize.x = PlayerPrefs.GetInt(PreferenceKey("Width"), currentSize.x);
            desiredSize.y = PlayerPrefs.GetInt(PreferenceKey("Height"), currentSize.y);
            fullscreen = PlayerPrefs.GetInt(PreferenceKey("Fullscreen"), fullscreen ? 1 : 0) != 0;
            volume = PlayerPrefs.GetFloat(PreferenceKey("Volume"), volume);
        }

        selectedResolution = resolutions.IndexOf(desiredSize);
        if (selectedResolution < 0)
        {
            // Un monitor diferente puede no admitir la resolución guardada.
            selectedResolution = resolutions.IndexOf(currentSize);
        }
        if (resolutionDropdown != null)
        {
            var labels = new List<string>(resolutions.Count);
            foreach (Vector2Int size in resolutions)
            {
                labels.Add($"{size.x} x {size.y}");
            }
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(selectedResolution);
            resolutionDropdown.RefreshShownValue();
        }
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        }
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;
            volumeSlider.SetValueWithoutNotify(Mathf.Clamp01(volume));
        }
        if (Application.isPlaying)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }
        if (resolutions[selectedResolution] != currentSize || fullscreen != Screen.fullScreen)
        {
            ApplyDisplaySettings();
        }
    }

    /// <summary>Selecciona un índice del dropdown; sus opciones se completan al habilitar el componente.</summary>
    public void SetResolution(int index)
    {
        if (index < 0 || index >= resolutions.Count)
        {
            return;
        }
        selectedResolution = index;
        if (resolutionDropdown != null)
        {
            resolutionDropdown.SetValueWithoutNotify(index);
        }
        ApplyDisplaySettings();
        StoreDisplayPreferences();
    }

    /// <summary>Alterna entre ventana y pantalla completa sin bordes.</summary>
    public void SetFullscreen(bool enabled)
    {
        fullscreen = enabled;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(enabled);
        }
        ApplyDisplaySettings();
        StoreDisplayPreferences();
    }

    /// <summary>Aplica el volumen global normalizado entre cero y uno.</summary>
    public void SetVolume(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return;
        }
        value = Mathf.Clamp01(value);
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(value);
        }
        if (Application.isPlaying)
        {
            AudioListener.volume = value;
            if (persistSettings)
            {
                PlayerPrefs.SetFloat(PreferenceKey("Volume"), value);
                // Se guarda al cerrar las opciones; arrastrar no escribe en disco cada cuadro.
                preferencesDirty = true;
            }
        }
    }

    private void ApplyDisplaySettings()
    {
        if (!Application.isPlaying || Application.isBatchMode || resolutions.Count == 0)
        {
            return;
        }
        Vector2Int size = resolutions[selectedResolution];
        Screen.SetResolution(size.x, size.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    private void StoreDisplayPreferences()
    {
        if (!Application.isPlaying || !persistSettings || resolutions.Count == 0)
        {
            return;
        }
        Vector2Int size = resolutions[selectedResolution];
        PlayerPrefs.SetInt(PreferenceKey("Width"), size.x);
        PlayerPrefs.SetInt(PreferenceKey("Height"), size.y);
        PlayerPrefs.SetInt(PreferenceKey("Fullscreen"), fullscreen ? 1 : 0);
        preferencesDirty = true;
        SavePreferences();
    }

    private void SavePreferences()
    {
        if (Application.isPlaying && persistSettings && preferencesDirty)
        {
            PlayerPrefs.Save();
            preferencesDirty = false;
        }
    }

    private void UnbindControls()
    {
        if (boundResolutionDropdown != null)
        {
            boundResolutionDropdown.onValueChanged.RemoveListener(SetResolution);
        }
        if (boundFullscreenToggle != null)
        {
            boundFullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
        }
        if (boundVolumeSlider != null)
        {
            boundVolumeSlider.onValueChanged.RemoveListener(SetVolume);
        }
        boundResolutionDropdown = null;
        boundFullscreenToggle = null;
        boundVolumeSlider = null;
    }
}
