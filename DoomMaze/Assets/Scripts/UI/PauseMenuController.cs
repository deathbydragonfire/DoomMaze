using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause overlay toggled by <see cref="PauseChangedEvent"/>. Uses a <see cref="CanvasGroup"/>
/// to show and hide without destroying the GameObject.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PauseMenuController : MonoBehaviour, IMenuHoverAudioProvider
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject  _settingsPanel;
    [SerializeField] private TMP_FontAsset _menuFont;
    [SerializeField] private Image _backgroundDim;
    [SerializeField] private Color _backgroundDimColor = new Color(0f, 0f, 0f, 0.68f);
    [Header("Tutorial Skip")]
    [SerializeField] private Button _skipTutorialButton;
    [SerializeField] private string _tutorialSceneName = "Tutorial";
    [SerializeField] private string _gameplaySceneName = "Gameplay";
    [SerializeField] private string _skipTutorialButtonText = "SKIP TUTORIAL";
    [SerializeField] private Vector2 _skipTutorialButtonPosition = new Vector2(0f, 180f);
    [SerializeField] private Vector2 _skipTutorialButtonSize = new Vector2(420f, 82f);
    [SerializeField] private float _skipTutorialButtonFontSize = 38f;
    [SerializeField] private float _skipTutorialGameplayFadeInDuration = 1.5f;
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

        _menuFont = MenuFontUtility.ResolveMenuFont(transform, _menuFont);
        EnsureBackgroundDim();
        EnsureTutorialSkipButton();
        MenuButtonHoverEffect.AttachToButtons(transform);
        ApplyMenuFont();
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

    private void Start()
    {
        RefreshTutorialSkipButtonVisibility();
        ApplyMenuFont();
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
        SetBackgroundDimVisible(visible);
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
        OpenSettingsPanel();
    }

    /// <summary>Unpauses and loads the Main Menu scene.</summary>
    public void OnMainMenu()
    {
        PlayClickSound();
        PauseManager.Instance?.SetPaused(false);
        SceneFlowManager.Instance?.LoadScene("MainMenu");
    }

    /// <summary>Unpauses and loads the Gameplay scene from the tutorial pause menu.</summary>
    public void OnSkipTutorial()
    {
        PlayClickSound();

        if (TutorialManager.TrySkipToGameplayFromPause())
            return;

        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            PauseManager.Instance.SetPaused(false);

        MusicManager.Instance?.Stop();

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadSceneWithFadeIn(_gameplaySceneName, _skipTutorialGameplayFadeInDuration);
            return;
        }

        SceneManager.LoadScene(_gameplaySceneName);
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

    private void EnsureTutorialSkipButton()
    {
        if (_skipTutorialButton == null)
            _skipTutorialButton = FindExistingSkipTutorialButton();

        if (_skipTutorialButton == null)
            _skipTutorialButton = CreateTutorialSkipButton();

        if (_skipTutorialButton == null)
            return;

        _skipTutorialButton.onClick.RemoveListener(OnSkipTutorial);
        _skipTutorialButton.onClick.AddListener(OnSkipTutorial);

        TMP_Text label = _skipTutorialButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = _skipTutorialButtonText;
            label.fontSize = _skipTutorialButtonFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = true;
        }

        RectTransform rectTransform = _skipTutorialButton.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = _skipTutorialButtonPosition;
            rectTransform.sizeDelta = _skipTutorialButtonSize;
        }

        RefreshTutorialSkipButtonVisibility();
    }

    private Button FindExistingSkipTutorialButton()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == "SkipTutorialButton")
                return buttons[i];
        }

        return null;
    }

    private Button CreateTutorialSkipButton()
    {
        Transform parent = FindPauseButtonParent();
        if (parent == null)
            parent = transform;

        GameObject buttonObject = new GameObject("SkipTutorialButton", typeof(RectTransform), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.transform as RectTransform;
        if (labelRect != null)
        {
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_Text templateLabel = FindTemplateButtonLabel(parent);
        if (templateLabel != null)
            label.font = templateLabel.font;

        return button;
    }

    private Transform FindPauseButtonParent()
    {
        Transform pausePanel = transform.Find("PausePanel");
        if (pausePanel != null)
            return pausePanel;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].transform.parent != null)
                return buttons[i].transform.parent;
        }

        return null;
    }

    private TMP_Text FindTemplateButtonLabel(Transform buttonParent)
    {
        if (buttonParent == null)
            return GetComponentInChildren<TMP_Text>(true);

        TMP_Text[] labels = buttonParent.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].GetComponentInParent<Button>() != _skipTutorialButton)
                return labels[i];
        }

        return GetComponentInChildren<TMP_Text>(true);
    }

    private void RefreshTutorialSkipButtonVisibility()
    {
        if (_skipTutorialButton == null)
            return;

        _skipTutorialButton.gameObject.SetActive(IsTutorialScene());
    }

    private bool IsTutorialScene()
    {
        return SceneManager.GetActiveScene().name == _tutorialSceneName;
    }

    private void OpenSettingsPanel()
    {
        if (_settingsPanel == null)
        {
            PlayClickSound();
            return;
        }

        SettingsMenuController settingsMenu = _settingsPanel.GetComponent<SettingsMenuController>();
        if (settingsMenu != null)
        {
            PlayClickSound();
            settingsMenu.Open(playSound: false, replacedMenuRoot: transform);
            return;
        }

        PlayClickSound();
        _settingsPanel.SetActive(true);
    }

    private void EnsureBackgroundDim()
    {
        if (_backgroundDim == null)
        {
            Transform existingDim = transform.Find("PauseBackgroundDim");
            if (existingDim != null)
                _backgroundDim = existingDim.GetComponent<Image>();
        }

        if (_backgroundDim == null)
        {
            GameObject dimObject = new GameObject("PauseBackgroundDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dimObject.transform.SetParent(transform, false);
            dimObject.transform.SetAsFirstSibling();

            RectTransform rectTransform = dimObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            _backgroundDim = dimObject.GetComponent<Image>();
        }

        _backgroundDim.raycastTarget = false;
        _backgroundDim.color = _backgroundDimColor;
        _backgroundDim.gameObject.SetActive(false);
    }

    private void SetBackgroundDimVisible(bool visible)
    {
        if (_backgroundDim == null)
            EnsureBackgroundDim();

        if (_backgroundDim != null)
        {
            _backgroundDim.color = _backgroundDimColor;
            _backgroundDim.gameObject.SetActive(visible);
            _backgroundDim.transform.SetAsFirstSibling();
        }
    }

    private void ApplyMenuFont()
    {
        _menuFont = MenuFontUtility.ResolveMenuFont(transform, _menuFont);
        MenuFontUtility.ApplyFont(transform, _menuFont);
    }
}
