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

    private const string MASTER_VOLUME_PARAM = "MasterVolume";
    private const string MUSIC_VOLUME_PARAM  = "MusicVolume";
    private const string SFX_VOLUME_PARAM    = "SfxVolume";

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

    /// <summary>Plays a clip on the UI source.</summary>
    public void PlayUi(AudioClip clip)
    {
        if (clip == null) return;
        _uiSource.PlayOneShot(clip);
    }

    /// <summary>Sets Master mixer volume. Value is 0–1, converted to dB internally.</summary>
    public void SetMasterVolume(float normalizedVolume)
    {
        _mixer.SetFloat(MASTER_VOLUME_PARAM, NormalizedToDb(normalizedVolume));
    }

    /// <summary>Sets Music mixer volume.</summary>
    public void SetMusicVolume(float normalizedVolume)
    {
        _mixer.SetFloat(MUSIC_VOLUME_PARAM, NormalizedToDb(normalizedVolume));
    }

    /// <summary>Sets SFX mixer volume.</summary>
    public void SetSfxVolume(float normalizedVolume)
    {
        _mixer.SetFloat(SFX_VOLUME_PARAM, NormalizedToDb(normalizedVolume));
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void ApplyAudioSettings()
    {
        if (SaveManager.Instance == null) return;

        SettingsData settings = SaveManager.Instance.CurrentSettings;
        SetMasterVolume(settings.MasterVolume);
        SetMusicVolume(settings.MusicVolume);
        SetSfxVolume(settings.SfxVolume);
    }

    private static float NormalizedToDb(float value)
    {
        return Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f;
    }
}
