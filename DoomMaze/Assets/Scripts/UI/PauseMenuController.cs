using UnityEngine;

/// <summary>
/// Pause overlay toggled by <see cref="PauseChangedEvent"/>. Uses a <see cref="CanvasGroup"/>
/// to show and hide without destroying the GameObject.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PauseMenuController : MonoBehaviour, IMenuHoverAudioProvider
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject  _settingsPanel;
    [Header("Music")]
    [SerializeField] private string _pauseMusicTrackId;
    [Header("UI Audio")]
    [SerializeField] private AudioClip[] _hoverSounds;
    [Range(0f, 1f)] [SerializeField] private float _hoverSoundVolume = 1f;
    [SerializeField] private AudioClip[] _clickSounds;
    [Range(0f, 1f)] [SerializeField] private float _clickSoundVolume = 1f;

    private void Awake()
    {
        if (_canvasGroup   == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_settingsPanel == null) Debug.LogError("[PauseMenuController] _settingsPanel is not assigned.");

        MenuButtonHoverEffect.AttachToButtons(transform);
        ApplyPauseMusicSettings();
        SetVisible(false);
    }

    private void OnEnable()
    {
        EventBus<PauseChangedEvent>.Subscribe(OnPauseChanged);
        ApplyPauseMusicSettings();
    }

    private void OnDisable()
    {
        EventBus<PauseChangedEvent>.Unsubscribe(OnPauseChanged);
    }

    private void OnDestroy()
    {
        MusicManager.Instance?.ClearPauseTrack();
    }

    private void OnPauseChanged(PauseChangedEvent e)
    {
        SetVisible(e.IsPaused);
    }

    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha          = visible ? 1f : 0f;
        _canvasGroup.interactable   = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    /// <summary>Unpauses the game.</summary>
    public void OnResume()
    {
        PlayClickSound();
        PauseManager.Instance?.SetPaused(false);
    }

    /// <summary>Opens the settings panel.</summary>
    public void OnSettings()
    {
        PlayClickSound();
        if (_settingsPanel != null)
            _settingsPanel.SetActive(true);
    }

    /// <summary>Unpauses and loads the Main Menu scene.</summary>
    public void OnMainMenu()
    {
        PlayClickSound();
        PauseManager.Instance?.SetPaused(false);
        SceneFlowManager.Instance?.LoadScene("MainMenu");
    }

    /// <summary>Quits the application.</summary>
    public void OnQuit()
    {
        PlayClickSound();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void PlayMenuHoverSound()
    {
        AudioManager.Instance?.PlayUi(_hoverSounds, _hoverSoundVolume);
    }

    private void PlayClickSound()
    {
        AudioManager.Instance?.PlayUi(_clickSounds, _clickSoundVolume);
    }

    private void ApplyPauseMusicSettings()
    {
        MusicManager.Instance?.SetPauseTrack(_pauseMusicTrackId);
    }
}
