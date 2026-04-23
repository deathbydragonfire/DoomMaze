using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reads from <see cref="SaveManager.CurrentSettings"/> on open, previews changes live,
/// and writes back on Apply. Closing without applying reverts live changes.
/// </summary>
public class SettingsMenuController : MonoBehaviour, IMenuHoverAudioProvider
{
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Slider _sensitivitySlider;
    [SerializeField] private Slider _fovSlider;
    [SerializeField] private Toggle _invertYToggle;
    [SerializeField] private Toggle _fullscreenToggle;
    [Header("UI Audio")]
    [SerializeField] private AudioClip[] _hoverSounds;
    [Range(0f, 1f)] [SerializeField] private float _hoverSoundVolume = 1f;
    [SerializeField] private AudioClip[] _clickSounds;
    [Range(0f, 1f)] [SerializeField] private float _clickSoundVolume = 1f;

    private void Awake()
    {
        if (_masterVolumeSlider == null) Debug.LogError("[SettingsMenuController] _masterVolumeSlider is not assigned.");
        if (_musicVolumeSlider  == null) Debug.LogError("[SettingsMenuController] _musicVolumeSlider is not assigned.");
        if (_sfxVolumeSlider    == null) Debug.LogError("[SettingsMenuController] _sfxVolumeSlider is not assigned.");
        if (_sensitivitySlider  == null) Debug.LogError("[SettingsMenuController] _sensitivitySlider is not assigned.");
        if (_fovSlider          == null) Debug.LogError("[SettingsMenuController] _fovSlider is not assigned.");
        if (_invertYToggle      == null) Debug.LogError("[SettingsMenuController] _invertYToggle is not assigned.");
        if (_fullscreenToggle   == null) Debug.LogError("[SettingsMenuController] _fullscreenToggle is not assigned.");
    }

    private void OnEnable()
    {
        RegisterLiveCallbacks();
    }

    private void OnDisable()
    {
        UnregisterLiveCallbacks();
    }

    /// <summary>Populates all controls from current settings and shows the panel.</summary>
    public void Open()
    {
        PopulateControls();
        PlayClickSound();
        gameObject.SetActive(true);
    }

    /// <summary>Reverts unapplied changes and hides the panel.</summary>
    public void Close()
    {
        PlayClickSound();
        CloseInternal(revertLiveAudio: true);
    }

    /// <summary>Writes all control values to settings, applies display settings, and saves.</summary>
    public void OnApply()
    {
        PlayClickSound();
        if (SaveManager.Instance == null) return;

        SettingsData settings = SaveManager.Instance.CurrentSettings;

        settings.MasterVolume     = _masterVolumeSlider != null ? _masterVolumeSlider.value : settings.MasterVolume;
        settings.MusicVolume      = _musicVolumeSlider  != null ? _musicVolumeSlider.value  : settings.MusicVolume;
        settings.SfxVolume        = _sfxVolumeSlider    != null ? _sfxVolumeSlider.value    : settings.SfxVolume;
        settings.MouseSensitivity = _sensitivitySlider  != null ? _sensitivitySlider.value  : settings.MouseSensitivity;
        settings.Fov              = _fovSlider          != null ? _fovSlider.value          : settings.Fov;
        settings.InvertY          = _invertYToggle      != null && _invertYToggle.isOn;
        settings.Fullscreen       = _fullscreenToggle   != null && _fullscreenToggle.isOn;

        Screen.SetResolution(settings.ResolutionWidth, settings.ResolutionHeight, settings.Fullscreen);

        SaveManager.Instance.SaveSettings();
        CloseInternal(revertLiveAudio: false);
    }

    public void PlayMenuHoverSound()
    {
        AudioManager.Instance?.PlayUi(_hoverSounds, _hoverSoundVolume);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void PopulateControls()
    {
        if (SaveManager.Instance == null) return;

        SettingsData settings = SaveManager.Instance.CurrentSettings;

        if (_masterVolumeSlider != null) _masterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
        if (_musicVolumeSlider  != null) _musicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
        if (_sfxVolumeSlider    != null) _sfxVolumeSlider.SetValueWithoutNotify(settings.SfxVolume);
        if (_sensitivitySlider  != null) _sensitivitySlider.SetValueWithoutNotify(settings.MouseSensitivity);
        if (_fovSlider          != null) _fovSlider.SetValueWithoutNotify(settings.Fov);
        if (_invertYToggle      != null) _invertYToggle.SetIsOnWithoutNotify(settings.InvertY);
        if (_fullscreenToggle   != null) _fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);
    }

    private void RegisterLiveCallbacks()
    {
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (_musicVolumeSlider  != null) _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (_sfxVolumeSlider    != null) _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
    }

    private void UnregisterLiveCallbacks()
    {
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (_musicVolumeSlider  != null) _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (_sfxVolumeSlider    != null) _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
    }

    private void OnMasterVolumeChanged(float value) => AudioManager.Instance?.SetMasterVolume(value);
    private void OnMusicVolumeChanged(float value)  => AudioManager.Instance?.SetMusicVolume(value);
    private void OnSfxVolumeChanged(float value)    => AudioManager.Instance?.SetSfxVolume(value);

    private void PlayClickSound()
    {
        AudioManager.Instance?.PlayUi(_clickSounds, _clickSoundVolume);
    }

    private void CloseInternal(bool revertLiveAudio)
    {
        if (revertLiveAudio)
            RevertLiveAudio();

        gameObject.SetActive(false);
    }

    private void RevertLiveAudio()
    {
        if (SaveManager.Instance == null || AudioManager.Instance == null) return;

        SettingsData settings = SaveManager.Instance.CurrentSettings;
        AudioManager.Instance.SetMasterVolume(settings.MasterVolume);
        AudioManager.Instance.SetMusicVolume(settings.MusicVolume);
        AudioManager.Instance.SetSfxVolume(settings.SfxVolume);
    }
}
