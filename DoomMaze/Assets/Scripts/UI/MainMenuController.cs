using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the Main Menu scene canvas. Wires New Game, Continue, Settings, and Quit buttons.
/// Interactability of Continue is determined at runtime via <see cref="SaveManager"/>.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button     _continueButton;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private bool       _playMenuMusicOnStart = true;
    [SerializeField] private string     _menuMusicTrackId;

    private bool _hasRequestedMenuMusic;

    private void Awake()
    {
        if (_continueButton == null) Debug.LogError("[MainMenuController] _continueButton is not assigned.");
        if (_settingsPanel  == null) Debug.LogError("[MainMenuController] _settingsPanel is not assigned.");

        MenuButtonHoverEffect.AttachToButtons(transform);
    }

    private void OnEnable()
    {
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        TryPlayMenuMusic();
    }

    private void OnDisable()
    {
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
    }

    private void Start()
    {
        if (_continueButton != null && SaveManager.Instance != null)
            _continueButton.interactable = SaveManager.Instance.HasSaveFile();

        TryPlayMenuMusic();
    }

    /// <summary>Deletes any existing save and loads the Gameplay scene.</summary>
    public void OnNewGame()
    {
        SaveManager.Instance?.DeleteSave();
        SceneFlowManager.Instance?.LoadScene("Gameplay");
    }

    /// <summary>Loads the existing save and transitions to the Gameplay scene.</summary>
    public void OnContinue()
    {
        SaveManager.Instance?.LoadGame();
        SceneFlowManager.Instance?.LoadScene("Gameplay");
    }

    /// <summary>Opens the settings panel.</summary>
    public void OnSettings()
    {
        if (_settingsPanel != null)
            _settingsPanel.SetActive(true);
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Playing)
            TryPlayMenuMusic();
    }

    private void TryPlayMenuMusic()
    {
        if (!_playMenuMusicOnStart || _hasRequestedMenuMusic || string.IsNullOrWhiteSpace(_menuMusicTrackId))
            return;

        if (MusicManager.Instance == null)
            return;

        _hasRequestedMenuMusic = true;
        MusicManager.Instance.PlayTrack(_menuMusicTrackId);
    }

    /// <summary>Quits the application.</summary>
    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
