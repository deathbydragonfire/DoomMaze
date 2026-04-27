using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Persistent singleton. Owns the Audio Mixer asset and all child <see cref="AudioSource"/> references.
/// Exposes <see cref="PlaySfx"/>, <see cref="PlayUi"/>, and mixer volume methods.
/// No gameplay code calls <see cref="AudioSource.Play"/> directly.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _uiSource;
    [SerializeField] private AudioMixerGroup _musicGroup;
    [SerializeField] private AudioMixerGroup _uiGroup;
    [SerializeField] private AudioMixerGroup _gameplayGroup;
    [Header("Decay Ducking")]
    [SerializeField] [Range(0f, 1f)] private float _decayMusicDuckTarget = 0.18f;
    [SerializeField] [Range(0f, 1f)] private float _decayGameplayDuckTarget = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float _decayUiDuckTarget = 0.45f;

    private const string MASTER_VOLUME_PARAM = "MasterVolume";
    private const string MUSIC_VOLUME_PARAM  = "MusicVolume";
    private const string UI_VOLUME_PARAM = "UiVolume";
    private const string GAMEPLAY_VOLUME_PARAM = "GameplayVolume";
    private const string LEGACY_UI_VOLUME_PARAM = "MyExposedParam";
    private const string LEGACY_SFX_VOLUME_PARAM = "SFXVolume";
    private const string LEGACY_SFX_VOLUME_PARAM_ALT = "SfxVolume";

    private float _masterVolume = 1f;
    private float _musicVolume = 1f;
    private float _uiVolume = 1f;
    private float _gameplayVolume = 1f;
    private float _bossSfxVolume = 1f;
    private float _decayDuckIntensity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_sfxSource == null)
            Debug.LogError("[AudioManager] _sfxSource is not assigned. Assign in the Inspector.");
        if (_uiSource == null)
            Debug.LogError("[AudioManager] _uiSource is not assigned. Assign in the Inspector.");
        if (_mixer == null)
            Debug.LogError("[AudioManager] _mixer is not assigned. Assign in the Inspector.");

        ConfigureGameplaySource(_sfxSource);
        ConfigureUiSource(_uiSource);
    }

    private void Start()
    {
        ApplyAudioSettings();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Plays a single clip on the SFX source. No-ops if clip is null.</summary>
    public void PlaySfx(AudioClip clip)
    {
        PlaySfx(clip, 1f);
    }

    public void PlaySfx(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;
        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    /// <summary>Picks a random clip from the array and plays it. No-ops if array is null or empty.</summary>
    public void PlaySfx(AudioClip[] clips)
    {
        PlaySfx(clips, 1f);
    }

    public void PlaySfx(AudioClip[] clips, float volumeScale)
    {
        if (clips == null || clips.Length == 0) return;
        PlaySfx(clips[Random.Range(0, clips.Length)], volumeScale);
    }

    public void PlayBossSfx(AudioClip clip)
    {
        PlayBossSfx(clip, 1f);
    }

    public void PlayBossSfx(AudioClip clip, float volumeScale)
    {
        PlaySfx(clip, Mathf.Clamp01(volumeScale) * _bossSfxVolume);
    }

    public void PlayBossSfx(AudioClip[] clips)
    {
        PlayBossSfx(clips, 1f);
    }

    public void PlayBossSfx(AudioClip[] clips, float volumeScale)
    {
        if (clips == null || clips.Length == 0) return;
        PlayBossSfx(clips[Random.Range(0, clips.Length)], volumeScale);
    }

    /// <summary>Plays a clip on the UI source.</summary>
    public void PlayUi(AudioClip clip)
    {
        PlayUi(clip, 1f);
    }

    public void PlayUi(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;
        _uiSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayUi(AudioClip[] clips)
    {
        PlayUi(clips, 1f);
    }

    public void PlayUi(AudioClip[] clips, float volumeScale)
    {
        if (clips == null || clips.Length == 0) return;
        PlayUi(clips[Random.Range(0, clips.Length)], volumeScale);
    }

    /// <summary>Sets Master mixer volume. Value is 0–1, converted to dB internally.</summary>
    public void SetMasterVolume(float normalizedVolume)
    {
        _masterVolume = Mathf.Clamp01(normalizedVolume);
        ApplyMixerVolumes();
    }

    /// <summary>Sets Music mixer volume.</summary>
    public void SetMusicVolume(float normalizedVolume)
    {
        _musicVolume = Mathf.Clamp01(normalizedVolume);
        ApplyMixerVolumes();
    }

    /// <summary>Sets UI mixer volume.</summary>
    public void SetUiVolume(float normalizedVolume)
    {
        _uiVolume = Mathf.Clamp01(normalizedVolume);
        ApplyMixerVolumes();
    }

    /// <summary>Sets gameplay mixer volume.</summary>
    public void SetGameplayVolume(float normalizedVolume)
    {
        _gameplayVolume = Mathf.Clamp01(normalizedVolume);
        ApplyMixerVolumes();
    }

    public void SetBossSfxVolume(float normalizedVolume)
    {
        _bossSfxVolume = Mathf.Clamp01(normalizedVolume);
    }

    /// <summary>Compatibility wrapper for older gameplay SFX callers.</summary>
    public void SetSfxVolume(float normalizedVolume)
    {
        SetGameplayVolume(normalizedVolume);
    }

    public void ConfigureMusicSource(AudioSource source)
    {
        AssignOutputGroup(source, GetMusicGroup());
    }

    public void ConfigureUiSource(AudioSource source)
    {
        AssignOutputGroup(source, GetUiGroup());
    }

    public void ConfigureGameplaySource(AudioSource source)
    {
        AssignOutputGroup(source, GetGameplayGroup());
    }

    public void ConfigureDecaySource(AudioSource source)
    {
        if (source != null)
            source.outputAudioMixerGroup = null;
    }

    public void SetDecayDuckIntensity(float intensity)
    {
        float clamped = Mathf.Clamp01(intensity);
        if (Mathf.Approximately(_decayDuckIntensity, clamped))
            return;

        _decayDuckIntensity = clamped;
        ApplyMixerVolumes();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void ApplyAudioSettings()
    {
        if (SaveManager.Instance == null) return;

        SettingsData settings = SaveManager.Instance.CurrentSettings;
        _masterVolume = Mathf.Clamp01(settings.MasterVolume);
        _musicVolume = Mathf.Clamp01(settings.MusicVolume);
        _uiVolume = Mathf.Clamp01(settings.UiVolume);
        _gameplayVolume = Mathf.Clamp01(settings.GameplayVolume);
        _bossSfxVolume = Mathf.Clamp01(settings.BossSfxVolume);
        ApplyMixerVolumes();
    }

    private void ApplyMixerVolumes()
    {
        SetMixerVolume(_masterVolume, MASTER_VOLUME_PARAM);
        SetMixerVolume(GetDuckedVolume(_musicVolume, _decayMusicDuckTarget), MUSIC_VOLUME_PARAM);
        SetMixerVolume(GetDuckedVolume(_uiVolume, _decayUiDuckTarget), UI_VOLUME_PARAM, LEGACY_UI_VOLUME_PARAM);
        SetMixerVolume(
            GetDuckedVolume(_gameplayVolume, _decayGameplayDuckTarget),
            GAMEPLAY_VOLUME_PARAM,
            LEGACY_SFX_VOLUME_PARAM,
            LEGACY_SFX_VOLUME_PARAM_ALT);
    }

    private float GetDuckedVolume(float baseVolume, float duckTarget)
    {
        return Mathf.Lerp(baseVolume, baseVolume * Mathf.Clamp01(duckTarget), _decayDuckIntensity);
    }

    private static float NormalizedToDb(float value)
    {
        return Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f;
    }

    private void SetMixerVolume(float normalizedVolume, params string[] parameterNames)
    {
        if (_mixer == null || parameterNames == null)
            return;

        float dbValue = NormalizedToDb(Mathf.Clamp01(normalizedVolume));
        for (int i = 0; i < parameterNames.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(parameterNames[i]) && _mixer.SetFloat(parameterNames[i], dbValue))
                return;
        }

        Debug.LogWarning($"[AudioManager] None of the mixer parameters were found: {string.Join(", ", parameterNames)}");
    }

    private AudioMixerGroup GetMusicGroup()
    {
        if (_musicGroup == null)
            _musicGroup = FindMixerGroup("Music");

        return _musicGroup;
    }

    private AudioMixerGroup GetUiGroup()
    {
        if (_uiGroup == null)
            _uiGroup = FindMixerGroup("UI");

        return _uiGroup;
    }

    private AudioMixerGroup GetGameplayGroup()
    {
        if (_gameplayGroup == null)
            _gameplayGroup = FindMixerGroup("SFX");

        return _gameplayGroup;
    }

    private AudioMixerGroup FindMixerGroup(string groupName)
    {
        if (_mixer == null || string.IsNullOrWhiteSpace(groupName))
            return null;

        AudioMixerGroup[] groups = _mixer.FindMatchingGroups(groupName);
        return groups != null && groups.Length > 0 ? groups[0] : null;
    }

    private static void AssignOutputGroup(AudioSource source, AudioMixerGroup group)
    {
        if (source != null && group != null)
            source.outputAudioMixerGroup = group;
    }
}
