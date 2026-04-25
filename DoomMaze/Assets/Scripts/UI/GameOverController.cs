using TMPro;
using UnityEngine;

/// <summary>
/// Listens to <see cref="GameStateChangedEvent"/> and activates the Death or Victory panel
/// accordingly. Unlocks the cursor when either panel is shown.
/// </summary>
public class GameOverController : MonoBehaviour, IMenuHoverAudioProvider
{
    [SerializeField] private GameObject _deathPanel;
    [SerializeField] private GameObject _victoryPanel;
    [SerializeField] private TMP_FontAsset _menuFont;
    [Header("UI Audio")]
    [SerializeField] private AudioClip[] _hoverSounds;
    [Range(0f, 1f)] [SerializeField] private float _hoverSoundVolume = 1f;
    [SerializeField] private AudioClip[] _clickSounds;
    [Range(0f, 1f)] [SerializeField] private float _clickSoundVolume = 1f;

    private void Awake()
    {
        if (_deathPanel   == null) Debug.LogError("[GameOverController] _deathPanel is not assigned.");
        if (_victoryPanel == null) Debug.LogError("[GameOverController] _victoryPanel is not assigned.");

        MenuButtonHoverEffect.AttachToButtons(transform);
        ApplyMenuFont();
        SetPanelsActive(false, false);
    }

    private void OnEnable()
    {
        EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
    }

    private void OnDisable()
    {
        EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
    }

    private void OnStateChanged(GameStateChangedEvent e)
    {
        bool isDead    = e.NewState == GameState.Dead;
        bool isVictory = e.NewState == GameState.Victory;

        SetPanelsActive(isDead, isVictory);

        if (isDead || isVictory)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }

    private void SetPanelsActive(bool death, bool victory)
    {
        if (_deathPanel   != null) _deathPanel.SetActive(death);
        if (_victoryPanel != null) _victoryPanel.SetActive(victory);
        ApplyMenuFont();
    }

    /// <summary>Reloads the current scene to restart the game.</summary>
    public void OnRestart()
    {
        PlayClickSound();
        SceneFlowManager.Instance?.ReloadCurrentScene();
    }

    /// <summary>Loads the Main Menu scene.</summary>
    public void OnMainMenu()
    {
        PlayClickSound();
        SceneFlowManager.Instance?.LoadScene("MainMenu");
    }

    public void PlayMenuHoverSound()
    {
        AudioManager.Instance?.PlayUi(_hoverSounds, _hoverSoundVolume);
    }

    private void PlayClickSound()
    {
        AudioManager.Instance?.PlayUi(_clickSounds, _clickSoundVolume);
    }

    private void ApplyMenuFont()
    {
        _menuFont = MenuFontUtility.ResolveMenuFont(transform, _menuFont);
        MenuFontUtility.ApplyFont(transform, _menuFont);
    }
}
