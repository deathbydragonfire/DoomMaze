using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Owns the Main Menu scene canvas. Wires New Game, Settings, and Quit buttons.
/// </summary>
public class MainMenuController : MonoBehaviour, IMenuHoverAudioProvider
{
    private const string DifficultyMenuRootName = "DifficultyMenu";
    private const string DifficultyMenuFontPath = "Assets/Fonts/Unutterable_Font_1_07/TrueType (.ttf)/Unutterable-Regular SDF 1.asset";

    [SerializeField] private Button     _continueButton;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private TMP_FontAsset _menuFont;
    [SerializeField] private string     _tutorialSceneName = "Tutorial";
    [SerializeField] private string     _gameplaySceneName = "Gameplay";
    [SerializeField] private bool       _playMenuMusicOnStart = true;
    [SerializeField] private string     _menuMusicTrackId;
    [Header("New Game Transition")]
    [SerializeField] private Transform _newGameShakeTarget;
    [SerializeField] private Image _newGameFadeOverlay;
    [SerializeField] private Color _newGameFadeColor = Color.black;
    [SerializeField] private float _newGameGlitchIntensity = 1f;
    [SerializeField] private float _newGameGlitchDuration = 0.28f;
    [SerializeField] private float _newGameShakeMagnitude = 18f;
    [SerializeField] private float _newGameShakeDuration = 0.28f;
    [SerializeField] private float _newGameFadeDuration = 0.35f;
    [SerializeField] private float _newGameFadeHoldDuration = 0.05f;
    [SerializeField] private HypeVolumeController _newGameGlitchController;
    [Header("UI Audio")]
    [SerializeField] private AudioClip[] _hoverSounds;
    [Range(0f, 1f)] [SerializeField] private float _hoverSoundVolume = 1f;
    [SerializeField] private AudioClip[] _clickSounds;
    [Range(0f, 1f)] [SerializeField] private float _clickSoundVolume = 1f;
    [SerializeField] private AudioClip _newGameSound;
    [Range(0f, 1f)] [SerializeField] private float _newGameSoundVolume = 1f;

    private bool _hasRequestedMenuMusic;
    private bool _isNewGameTransitioning;
    private Coroutine _newGameTransitionCoroutine;
    private Button[] _menuButtons;
    private bool[] _menuButtonInteractableStates;
    private Vector3 _shakeBaseLocalPosition;
    private VolumeProfile _runtimeGlitchProfile;
    private RectTransform _difficultyMenuRoot;
    private readonly List<MenuObjectState> _difficultyMenuObjectStates = new List<MenuObjectState>();

    private void Awake()
    {
        if (_settingsPanel  == null) Debug.LogError("[MainMenuController] _settingsPanel is not assigned.");

        HideContinueButton();
        ApplyMenuFont();

        _menuButtons = GetComponentsInChildren<Button>(true);
        _menuButtonInteractableStates = _menuButtons != null ? new bool[_menuButtons.Length] : null;
        ReflowMainMenuButtons();

        if (_newGameShakeTarget == null)
            _newGameShakeTarget = transform;

        if (_newGameShakeTarget != null)
            _shakeBaseLocalPosition = _newGameShakeTarget.localPosition;

        EnsureFadeOverlay();
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

        if (_newGameTransitionCoroutine != null)
        {
            StopCoroutine(_newGameTransitionCoroutine);
            _newGameTransitionCoroutine = null;
        }

        RestoreDifficultyMenuReplacedObjects();
        ResetTransitionVisuals();
    }

    private void Start()
    {
        TryPlayMenuMusic();
        HideContinueButton();
        ReflowMainMenuButtons();
        ApplyMenuFont();
    }

    /// <summary>Opens difficulty selection before starting a fresh run.</summary>
    public void OnNewGame()
    {
        if (_isNewGameTransitioning)
            return;

        OpenDifficultyMenu();
    }

    private void OnDifficultySelected(GameDifficulty difficulty)
    {
        if (_isNewGameTransitioning)
            return;

        GameDifficultyManager.SetDifficulty(difficulty);
        HideDifficultyMenuRoot();
        _newGameTransitionCoroutine = StartCoroutine(NewGameTransitionRoutine());
    }

    /// <summary>Unused. Runs are always started fresh from New Game.</summary>
    public void OnContinue()
    {
        HideContinueButton();
    }

    /// <summary>Opens the settings panel.</summary>
    public void OnSettings()
    {
        if (_isNewGameTransitioning || IsDifficultyMenuOpen())
            return;

        OpenSettingsPanel();
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
        if (_isNewGameTransitioning || IsDifficultyMenuOpen())
            return;

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

    private void PlayNewGameSound()
    {
        if (_newGameSound != null)
        {
            AudioManager.Instance?.PlayUi(_newGameSound, _newGameSoundVolume);
            return;
        }

        PlayClickSound();
    }

    private IEnumerator NewGameTransitionRoutine()
    {
        _isNewGameTransitioning = true;
        SetMenuButtonsInteractable(false);
        EnsureFadeOverlay();

        PlayNewGameSound();
        SaveManager.Instance?.DeleteSave();

        HypeVolumeController glitchController = EnsureGlitchController();
        if (glitchController != null)
        {
            float durationScale = _newGameGlitchDuration / Mathf.Max(0.01f, 0.28f);
            glitchController.PulseGlitch(_newGameGlitchIntensity, durationScale);
        }

        yield return StartCoroutine(ShakeTransitionRoutine());
        yield return StartCoroutine(FadeToBlackRoutine());

        if (_newGameFadeHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(_newGameFadeHoldDuration);
        else
            yield return null;

        SceneFlowManager.Instance?.LoadScene(GetNewGameSceneName());
    }

    private IEnumerator ShakeTransitionRoutine()
    {
        if (_newGameShakeTarget == null || _newGameShakeDuration <= 0f || _newGameShakeMagnitude <= 0f)
            yield break;

        float elapsed = 0f;
        float seedX = Random.value * 100f;
        float seedY = Random.value * 100f;
        float duration = Mathf.Max(0.01f, _newGameShakeDuration);

        while (elapsed < duration)
        {
            float falloff = 1f - Mathf.Clamp01(elapsed / duration);
            float noiseX = (Mathf.PerlinNoise(seedX + elapsed * 28f, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, seedY + elapsed * 28f) - 0.5f) * 2f;

            _newGameShakeTarget.localPosition = _shakeBaseLocalPosition
                + new Vector3(noiseX, noiseY, 0f) * (_newGameShakeMagnitude * falloff);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _newGameShakeTarget.localPosition = _shakeBaseLocalPosition;
    }

    private IEnumerator FadeToBlackRoutine()
    {
        if (_newGameFadeOverlay == null)
            yield break;

        SetFadeOverlayAlpha(0f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _newGameFadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFadeOverlayAlpha(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetFadeOverlayAlpha(1f);
    }

    private void EnsureFadeOverlay()
    {
        if (_newGameFadeOverlay != null)
        {
            ConfigureFadeOverlay(_newGameFadeOverlay);
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        GameObject overlayObject = new GameObject("NewGameFadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.transform.SetParent(canvas.transform, false);
        overlayObject.transform.SetAsLastSibling();

        RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        _newGameFadeOverlay = overlayObject.GetComponent<Image>();
        ConfigureFadeOverlay(_newGameFadeOverlay);
    }

    private void ConfigureFadeOverlay(Image overlay)
    {
        if (overlay == null)
            return;

        overlay.raycastTarget = false;
        overlay.color = new Color(_newGameFadeColor.r, _newGameFadeColor.g, _newGameFadeColor.b, 0f);
    }

    private void SetFadeOverlayAlpha(float alpha)
    {
        if (_newGameFadeOverlay == null)
            return;

        Color fadeColor = _newGameFadeColor;
        fadeColor.a = Mathf.Clamp01(alpha);
        _newGameFadeOverlay.color = fadeColor;
    }

    private HypeVolumeController EnsureGlitchController()
    {
        if (_newGameGlitchController != null)
            return _newGameGlitchController;

        _newGameGlitchController = FindFirstObjectByType<HypeVolumeController>();
        if (_newGameGlitchController != null)
            return _newGameGlitchController;

        Camera sceneCamera = Camera.main;
        if (sceneCamera == null)
            return null;

        GameObject glitchObject = new GameObject("MainMenuTransitionGlitch");
        glitchObject.transform.SetParent(sceneCamera.transform, false);

        Volume volume = glitchObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 1f;

        _runtimeGlitchProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = _runtimeGlitchProfile;

        _newGameGlitchController = glitchObject.AddComponent<HypeVolumeController>();
        return _newGameGlitchController;
    }

    private void SetMenuButtonsInteractable(bool isInteractable)
    {
        if (_menuButtons == null)
            return;

        for (int i = 0; i < _menuButtons.Length; i++)
        {
            if (_menuButtons[i] == null)
                continue;

            if (!isInteractable && _menuButtonInteractableStates != null && i < _menuButtonInteractableStates.Length)
                _menuButtonInteractableStates[i] = _menuButtons[i].interactable;

            if (isInteractable && _menuButtonInteractableStates != null && i < _menuButtonInteractableStates.Length)
            {
                _menuButtons[i].interactable = _menuButtonInteractableStates[i];
                continue;
            }

            _menuButtons[i].interactable = isInteractable;
        }
    }

    private void ResetTransitionVisuals()
    {
        _isNewGameTransitioning = false;
        SetMenuButtonsInteractable(true);

        if (_newGameShakeTarget != null)
            _newGameShakeTarget.localPosition = _shakeBaseLocalPosition;

        SetFadeOverlayAlpha(0f);
    }

    private void HideContinueButton()
    {
        if (_continueButton == null)
            return;

        _continueButton.interactable = false;
        _continueButton.gameObject.SetActive(false);
    }

    private string GetNewGameSceneName()
    {
        bool skipTutorial = SaveManager.Instance != null &&
            SaveManager.Instance.CurrentSettings != null &&
            SaveManager.Instance.CurrentSettings.SkipTutorial;

        return skipTutorial ? _gameplaySceneName : _tutorialSceneName;
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

    private void OpenDifficultyMenu()
    {
        PlayClickSound();
        EnsureDifficultyMenu();
        HideMainMenuButtonsForDifficulty();

        if (_difficultyMenuRoot != null)
        {
            _difficultyMenuRoot.gameObject.SetActive(true);
            _difficultyMenuRoot.SetAsLastSibling();
        }
    }

    private void OnDifficultyBack()
    {
        PlayClickSound();
        HideDifficultyMenuRoot();
        RestoreDifficultyMenuReplacedObjects();
    }

    private bool IsDifficultyMenuOpen()
    {
        return _difficultyMenuRoot != null && _difficultyMenuRoot.gameObject.activeSelf;
    }

    private void EnsureDifficultyMenu()
    {
        if (_difficultyMenuRoot != null)
            return;

        Transform existingRoot = transform.Find(DifficultyMenuRootName);
        if (existingRoot != null)
        {
            _difficultyMenuRoot = existingRoot as RectTransform;
            _menuFont = ResolveDifficultyMenuFont();
            MenuFontUtility.ApplyFont(_difficultyMenuRoot, _menuFont);
            return;
        }

        _menuFont = ResolveDifficultyMenuFont();

        GameObject rootObject = new GameObject(DifficultyMenuRootName, typeof(RectTransform), typeof(CanvasRenderer));
        rootObject.transform.SetParent(transform, false);

        _difficultyMenuRoot = rootObject.GetComponent<RectTransform>();
        _difficultyMenuRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _difficultyMenuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _difficultyMenuRoot.pivot = new Vector2(0.5f, 0.5f);
        _difficultyMenuRoot.sizeDelta = new Vector2(620f, 430f);
        _difficultyMenuRoot.anchoredPosition = new Vector2(0f, -70f);

        GameObject contentObject = new GameObject("DifficultyContent", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentObject.transform.SetParent(_difficultyMenuRoot, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = MenuFontUtility.CreateText(
            "Title",
            contentObject.transform,
            "DIFFICULTY",
            _menuFont,
            54f,
            TextAlignmentOptions.Center);
        SetDifficultyLayoutSize(title.gameObject, 620f, 76f);

        CreateDifficultyButton(contentObject.transform, "EasyButton", "EASY", GameDifficulty.Easy);
        CreateDifficultyButton(contentObject.transform, "NormalButton", "NORMAL", GameDifficulty.Normal);
        CreateDifficultyButton(contentObject.transform, "HardButton", "HARD", GameDifficulty.Hard);

        Button backButton = MenuFontUtility.CreateTextButton("BackButton", contentObject.transform, "BACK", _menuFont, new Vector2(360f, 62f));
        backButton.onClick.AddListener(OnDifficultyBack);
        SetDifficultyLayoutSize(backButton.gameObject, 360f, 62f);

        MenuButtonHoverEffect.AttachToButtons(_difficultyMenuRoot);
        _difficultyMenuRoot.gameObject.SetActive(false);
    }

    private TMP_FontAsset ResolveDifficultyMenuFont()
    {
#if UNITY_EDITOR
        TMP_FontAsset difficultyFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DifficultyMenuFontPath);
        if (difficultyFont != null)
            return difficultyFont;
#endif

        return MenuFontUtility.ResolveMenuFont(transform, _menuFont);
    }

    private void CreateDifficultyButton(Transform parent, string name, string label, GameDifficulty difficulty)
    {
        Button button = MenuFontUtility.CreateTextButton(name, parent, label, _menuFont, new Vector2(420f, 70f));
        button.onClick.AddListener(() => OnDifficultySelected(difficulty));
        SetDifficultyLayoutSize(button.gameObject, 420f, 70f);
    }

    private void SetDifficultyLayoutSize(GameObject target, float width, float height)
    {
        RectTransform rectTransform = target.transform as RectTransform;
        if (rectTransform != null)
            rectTransform.sizeDelta = new Vector2(width, height);

        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = target.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = height;
        layoutElement.minWidth = width;
        layoutElement.minHeight = height;
    }

    private void HideMainMenuButtonsForDifficulty()
    {
        if (_difficultyMenuObjectStates.Count > 0)
            return;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || IsDifficultyMenuChild(button.transform) || IsSettingsPanelChild(button.transform))
                continue;

            _difficultyMenuObjectStates.Add(new MenuObjectState(button.gameObject, button.gameObject.activeSelf));
            button.gameObject.SetActive(false);
        }
    }

    private void HideDifficultyMenuRoot()
    {
        if (_difficultyMenuRoot != null)
            _difficultyMenuRoot.gameObject.SetActive(false);
    }

    private void RestoreDifficultyMenuReplacedObjects()
    {
        for (int i = 0; i < _difficultyMenuObjectStates.Count; i++)
        {
            MenuObjectState state = _difficultyMenuObjectStates[i];
            if (state.GameObject != null)
                state.GameObject.SetActive(state.WasActive);
        }

        _difficultyMenuObjectStates.Clear();
    }

    private void ApplyMenuFont()
    {
        _menuFont = MenuFontUtility.ResolveMenuFont(transform, _menuFont);
        MenuFontUtility.ApplyFont(transform, _menuFont);
    }

    private void ReflowMainMenuButtons()
    {
        List<Button> activeButtons = new List<Button>();
        List<float> allMenuButtonYPositions = new List<float>();

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button == _continueButton || IsSettingsPanelChild(button.transform))
                continue;

            RectTransform rectTransform = button.transform as RectTransform;
            if (rectTransform == null)
                continue;

            allMenuButtonYPositions.Add(rectTransform.anchoredPosition.y);
            if (button.gameObject.activeSelf)
                activeButtons.Add(button);
        }

        if (_continueButton != null)
        {
            RectTransform continueRect = _continueButton.transform as RectTransform;
            if (continueRect != null)
                allMenuButtonYPositions.Add(continueRect.anchoredPosition.y);
        }

        if (activeButtons.Count <= 1)
            return;

        activeButtons.Sort((a, b) =>
        {
            RectTransform aRect = a.transform as RectTransform;
            RectTransform bRect = b.transform as RectTransform;
            return bRect.anchoredPosition.y.CompareTo(aRect.anchoredPosition.y);
        });

        float centerY = 0f;
        for (int i = 0; i < allMenuButtonYPositions.Count; i++)
            centerY += allMenuButtonYPositions[i];

        centerY = allMenuButtonYPositions.Count > 0 ? centerY / allMenuButtonYPositions.Count : 0f;

        float spacing = GetAverageButtonSpacing(activeButtons);
        float startY = centerY + spacing * (activeButtons.Count - 1) * 0.5f;

        for (int i = 0; i < activeButtons.Count; i++)
        {
            RectTransform rectTransform = activeButtons[i].transform as RectTransform;
            Vector2 position = rectTransform.anchoredPosition;
            position.y = startY - spacing * i;
            rectTransform.anchoredPosition = position;
        }
    }

    private float GetAverageButtonSpacing(List<Button> buttons)
    {
        const float fallbackSpacing = 92f;
        if (buttons == null || buttons.Count < 2)
            return fallbackSpacing;

        float totalSpacing = 0f;
        int spacingCount = 0;
        for (int i = 1; i < buttons.Count; i++)
        {
            RectTransform previous = buttons[i - 1].transform as RectTransform;
            RectTransform current = buttons[i].transform as RectTransform;
            if (previous == null || current == null)
                continue;

            totalSpacing += Mathf.Abs(previous.anchoredPosition.y - current.anchoredPosition.y);
            spacingCount++;
        }

        return spacingCount > 0 ? Mathf.Max(64f, totalSpacing / spacingCount) : fallbackSpacing;
    }

    private bool IsSettingsPanelChild(Transform candidate)
    {
        return _settingsPanel != null && candidate != null && candidate.IsChildOf(_settingsPanel.transform);
    }

    private bool IsDifficultyMenuChild(Transform candidate)
    {
        return _difficultyMenuRoot != null && candidate != null && candidate.IsChildOf(_difficultyMenuRoot);
    }

    private readonly struct MenuObjectState
    {
        public MenuObjectState(GameObject gameObject, bool wasActive)
        {
            GameObject = gameObject;
            WasActive = wasActive;
        }

        public GameObject GameObject { get; }
        public bool WasActive { get; }
    }
}
