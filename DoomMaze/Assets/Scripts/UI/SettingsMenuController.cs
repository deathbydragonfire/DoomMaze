using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the settings screen. The controller can repair older scenes by generating
/// the expected sliders, toggles, Apply button, and Back button at runtime.
/// </summary>
public class SettingsMenuController : MonoBehaviour, IMenuHoverAudioProvider
{
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Slider _uiVolumeSlider;
    [SerializeField] private Slider _gameplayVolumeSlider;
    [SerializeField] private Slider _sensitivitySlider;
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private Toggle _skipTutorialToggle;
    [SerializeField] private TMP_FontAsset _menuFont;
    [Header("UI Audio")]
    [SerializeField] private AudioClip[] _hoverSounds;
    [Range(0f, 1f)] [SerializeField] private float _hoverSoundVolume = 1f;
    [SerializeField] private AudioClip[] _clickSounds;
    [Range(0f, 1f)] [SerializeField] private float _clickSoundVolume = 1f;

    private const string GeneratedRootName = "GeneratedSettingsScreen";
    private readonly List<MenuObjectState> _replacedMenuObjectStates = new();
    private RectTransform _generatedRoot;
    private bool _hasUnappliedLiveAudioChanges;

    private void Awake()
    {
        _menuFont = MenuFontUtility.ResolveMenuFont(transform, _menuFont);
        EnsureSettingsUi();
        ApplyMenuFont();
    }

    private void OnEnable()
    {
        EnsureSettingsUi();
        PopulateControls();
        RegisterLiveCallbacks();
        ApplyMenuFont();
    }

    private void OnDisable()
    {
        UnregisterLiveCallbacks();
    }

    public void Open()
    {
        Open(playSound: true, replacedMenuRoot: null);
    }

    public void Open(bool playSound)
    {
        Open(playSound, replacedMenuRoot: null);
    }

    public void Open(bool playSound, Transform replacedMenuRoot)
    {
        if (playSound)
            PlayClickSound();

        gameObject.SetActive(true);
        EnsureSettingsUi();
        HideReplacedMenu(replacedMenuRoot != null ? replacedMenuRoot : transform.parent);
        PopulateControls();
        ApplyMenuFont();
    }

    public void Close()
    {
        PlayClickSound();
        CloseInternal(revertLiveAudio: true);
    }

    public void OnBack()
    {
        Close();
    }

    public void OnApply()
    {
        PlayClickSound();
        if (SaveManager.Instance == null) return;

        SettingsData settings = SaveManager.Instance.CurrentSettings;

        settings.MasterVolume = 1f;
        settings.MusicVolume = _musicVolumeSlider != null ? _musicVolumeSlider.value : settings.MusicVolume;
        settings.UiVolume = _uiVolumeSlider != null ? _uiVolumeSlider.value : settings.UiVolume;
        settings.GameplayVolume = _gameplayVolumeSlider != null ? _gameplayVolumeSlider.value : settings.GameplayVolume;
        settings.SfxVolume = settings.GameplayVolume;
        settings.MouseSensitivity = _sensitivitySlider != null ? _sensitivitySlider.value : settings.MouseSensitivity;
        settings.InvertY = false;
        settings.Fullscreen = _fullscreenToggle != null && _fullscreenToggle.isOn;
        settings.SkipTutorial = _skipTutorialToggle != null && _skipTutorialToggle.isOn;
        settings.Normalize();

        ApplyDisplaySettings(settings);

        SaveManager.Instance.SaveSettings();
        _hasUnappliedLiveAudioChanges = false;
        PopulateControls();
    }

    public void OnResetSettings()
    {
        PlayClickSound();
        if (SaveManager.Instance == null)
            return;

        SettingsData resetSettings = new SettingsData();
        resetSettings.Normalize();

        SettingsData currentSettings = SaveManager.Instance.CurrentSettings;
        currentSettings.SettingsVersion = resetSettings.SettingsVersion;
        currentSettings.MasterVolume = resetSettings.MasterVolume;
        currentSettings.MusicVolume = resetSettings.MusicVolume;
        currentSettings.UiVolume = resetSettings.UiVolume;
        currentSettings.GameplayVolume = resetSettings.GameplayVolume;
        currentSettings.SfxVolume = resetSettings.SfxVolume;
        currentSettings.MouseSensitivity = resetSettings.MouseSensitivity;
        currentSettings.InvertY = false;
        currentSettings.Fov = resetSettings.Fov;
        currentSettings.Fullscreen = resetSettings.Fullscreen;
        currentSettings.ResolutionWidth = resetSettings.ResolutionWidth;
        currentSettings.ResolutionHeight = resetSettings.ResolutionHeight;
        currentSettings.SkipTutorial = resetSettings.SkipTutorial;
        currentSettings.Normalize();

        AudioManager.Instance?.SetMasterVolume(currentSettings.MasterVolume);
        AudioManager.Instance?.SetMusicVolume(currentSettings.MusicVolume);
        AudioManager.Instance?.SetUiVolume(currentSettings.UiVolume);
        AudioManager.Instance?.SetGameplayVolume(currentSettings.GameplayVolume);
        ApplyDisplaySettings(currentSettings);

        SaveManager.Instance.SaveSettings();
        _hasUnappliedLiveAudioChanges = false;
        PopulateControls();
    }

    public void PlayMenuHoverSound()
    {
        AudioManager.Instance?.PlayUi(_hoverSounds, _hoverSoundVolume);
    }

    private void EnsureSettingsUi()
    {
        EnsurePanelFillsParent();
        ResolveControlReferences();

        bool needsGeneratedUi =
            _generatedRoot == null ||
            _musicVolumeSlider == null ||
            _uiVolumeSlider == null ||
            _gameplayVolumeSlider == null ||
            _skipTutorialToggle == null;

        if (!needsGeneratedUi)
            return;

        BuildGeneratedSettingsUi();
        ResolveControlReferences();
        MenuButtonHoverEffect.AttachToButtons(transform);
    }

    private void EnsurePanelFillsParent()
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void BuildGeneratedSettingsUi()
    {
        Transform existingGeneratedRoot = transform.Find(GeneratedRootName);
        if (existingGeneratedRoot != null)
            Destroy(existingGeneratedRoot.gameObject);

        _menuFont = MenuFontUtility.ResolveMenuFont(transform, _menuFont);

        GameObject rootObject = new GameObject(GeneratedRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.SetAsLastSibling();
        _generatedRoot = rootObject.GetComponent<RectTransform>();
        _generatedRoot.anchorMin = Vector2.zero;
        _generatedRoot.anchorMax = Vector2.one;
        _generatedRoot.offsetMin = Vector2.zero;
        _generatedRoot.offsetMax = Vector2.zero;

        Image background = rootObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.86f);
        background.raycastTarget = true;

        GameObject contentObject = new GameObject("SettingsContent", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentObject.transform.SetParent(rootObject.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(760f, 760f);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = MenuFontUtility.CreateText(
            "Title",
            contentObject.transform,
            "SETTINGS",
            _menuFont,
            58f,
            TextAlignmentOptions.Center);
        SetLayoutSize(title.gameObject, 760f, 74f);

        _musicVolumeSlider = CreateSlider(contentObject.transform, "MusicVolumeSlider", "MUSIC", 0f, 1f);
        _uiVolumeSlider = CreateSlider(contentObject.transform, "UiVolumeSlider", "UI", 0f, 1f);
        _gameplayVolumeSlider = CreateSlider(contentObject.transform, "GameplayVolumeSlider", "GAMEPLAY", 0f, 1f);
        _sensitivitySlider = CreateSlider(contentObject.transform, "SensitivitySlider", "MOUSE", 0.1f, 4f);
        _fullscreenToggle = CreateToggle(contentObject.transform, "FullscreenToggle", "FULLSCREEN");
        _skipTutorialToggle = CreateToggle(contentObject.transform, "SkipTutorialToggle", "SKIP TUTORIAL");

        GameObject buttonRow = CreateRow(contentObject.transform, "ButtonRow", 760f, 70f);
        HorizontalLayoutGroup buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.spacing = 22f;

        Button applyButton = MenuFontUtility.CreateTextButton("ApplyButton", buttonRow.transform, "APPLY", _menuFont, new Vector2(230f, 64f));
        applyButton.onClick.AddListener(OnApply);

        Button resetButton = MenuFontUtility.CreateTextButton("ResetButton", buttonRow.transform, "RESET", _menuFont, new Vector2(230f, 64f));
        resetButton.onClick.AddListener(OnResetSettings);

        Button backButton = MenuFontUtility.CreateTextButton("BackButton", buttonRow.transform, "BACK", _menuFont, new Vector2(230f, 64f));
        backButton.onClick.AddListener(OnBack);

        DisableLegacyChildrenExceptGenerated();
    }

    private Slider CreateSlider(Transform parent, string name, string labelText, float minValue, float maxValue)
    {
        GameObject row = CreateRow(parent, $"{name}Row", 760f, 52f);

        TextMeshProUGUI label = MenuFontUtility.CreateText("Label", row.transform, labelText, _menuFont, 28f, TextAlignmentOptions.Left);
        SetLayoutSize(label.gameObject, 230f, 52f);

        GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row.transform, false);
        SetLayoutSize(sliderObject, 460f, 42f);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(460f, 42f);

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.offsetMin = new Vector2(0f, -5f);
        backgroundRect.offsetMax = new Vector2(0f, 5f);
        backgroundObject.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.offsetMin = new Vector2(0f, -5f);
        fillAreaRect.offsetMax = new Vector2(0f, 5f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillObject.GetComponent<Image>().color = new Color(0.72f, 0.06f, 0.06f, 1f);

        GameObject handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(9f, 0f);
        handleAreaRect.offsetMax = new Vector2(-9f, 0f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(handleAreaObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(22f, 34f);
        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = Color.white;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = false;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private Toggle CreateToggle(Transform parent, string name, string labelText)
    {
        GameObject row = CreateRow(parent, $"{name}Row", 760f, 44f);

        GameObject toggleObject = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(row.transform, false);
        SetLayoutSize(toggleObject, 42f, 42f);

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        GameObject checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkmarkObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;
        Image checkmarkImage = checkmarkObject.GetComponent<Image>();
        checkmarkImage.color = new Color(0.72f, 0.06f, 0.06f, 1f);

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;

        TextMeshProUGUI label = MenuFontUtility.CreateText("Label", row.transform, labelText, _menuFont, 28f, TextAlignmentOptions.Left);
        SetLayoutSize(label.gameObject, 640f, 44f);
        return toggle;
    }

    private GameObject CreateRow(Transform parent, string name, float width, float height)
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        SetLayoutSize(row, width, height);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    private void SetLayoutSize(GameObject target, float width, float height)
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

    private void DisableLegacyChildrenExceptGenerated()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child != _generatedRoot)
                child.gameObject.SetActive(false);
        }
    }

    private void PopulateControls()
    {
        if (SaveManager.Instance == null) return;

        SettingsData settings = SaveManager.Instance.CurrentSettings;
        settings.Normalize();

        if (_musicVolumeSlider != null) _musicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
        if (_uiVolumeSlider != null) _uiVolumeSlider.SetValueWithoutNotify(settings.UiVolume);
        if (_gameplayVolumeSlider != null) _gameplayVolumeSlider.SetValueWithoutNotify(settings.GameplayVolume);
        if (_sensitivitySlider != null) _sensitivitySlider.SetValueWithoutNotify(settings.MouseSensitivity);
        if (_fullscreenToggle != null) _fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);
        if (_skipTutorialToggle != null) _skipTutorialToggle.SetIsOnWithoutNotify(settings.SkipTutorial);
    }

    private void RegisterLiveCallbacks()
    {
        UnregisterLiveCallbacks();

        if (_musicVolumeSlider != null) _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (_uiVolumeSlider != null) _uiVolumeSlider.onValueChanged.AddListener(OnUiVolumeChanged);
        if (_gameplayVolumeSlider != null) _gameplayVolumeSlider.onValueChanged.AddListener(OnGameplayVolumeChanged);
    }

    private void UnregisterLiveCallbacks()
    {
        if (_musicVolumeSlider != null) _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (_uiVolumeSlider != null) _uiVolumeSlider.onValueChanged.RemoveListener(OnUiVolumeChanged);
        if (_gameplayVolumeSlider != null) _gameplayVolumeSlider.onValueChanged.RemoveListener(OnGameplayVolumeChanged);
    }

    private void OnMusicVolumeChanged(float value)
    {
        _hasUnappliedLiveAudioChanges = true;
        AudioManager.Instance?.SetMusicVolume(value);
    }

    private void OnUiVolumeChanged(float value)
    {
        _hasUnappliedLiveAudioChanges = true;
        AudioManager.Instance?.SetUiVolume(value);
    }

    private void OnGameplayVolumeChanged(float value)
    {
        _hasUnappliedLiveAudioChanges = true;
        AudioManager.Instance?.SetGameplayVolume(value);
    }

    private void PlayClickSound()
    {
        AudioManager.Instance?.PlayUi(_clickSounds, _clickSoundVolume);
    }

    private void CloseInternal(bool revertLiveAudio)
    {
        if (revertLiveAudio && _hasUnappliedLiveAudioChanges)
            RevertLiveAudio();

        _hasUnappliedLiveAudioChanges = false;
        RestoreReplacedMenu();
        gameObject.SetActive(false);
    }

    private void RevertLiveAudio()
    {
        if (SaveManager.Instance == null || AudioManager.Instance == null) return;

        SettingsData settings = SaveManager.Instance.CurrentSettings;
        settings.Normalize();

        AudioManager.Instance.SetMasterVolume(settings.MasterVolume);
        AudioManager.Instance.SetMusicVolume(settings.MusicVolume);
        AudioManager.Instance.SetUiVolume(settings.UiVolume);
        AudioManager.Instance.SetGameplayVolume(settings.GameplayVolume);
    }

    private void ResolveControlReferences()
    {
        if (_generatedRoot == null)
            _generatedRoot = transform.Find(GeneratedRootName) as RectTransform;

        if (_masterVolumeSlider == null) _masterVolumeSlider = FindChildComponentByName<Slider>("MasterVolumeSlider");
        if (_musicVolumeSlider == null) _musicVolumeSlider = FindChildComponentByName<Slider>("MusicVolumeSlider");
        if (_sfxVolumeSlider == null) _sfxVolumeSlider = FindChildComponentByName<Slider>("SfxVolumeSlider");
        if (_uiVolumeSlider == null) _uiVolumeSlider = FindChildComponentByName<Slider>("UiVolumeSlider");
        if (_uiVolumeSlider == null) _uiVolumeSlider = FindChildComponentByName<Slider>("UIVolumeSlider");
        if (_uiVolumeSlider == null) _uiVolumeSlider = _masterVolumeSlider;
        if (_gameplayVolumeSlider == null) _gameplayVolumeSlider = FindChildComponentByName<Slider>("GameplayVolumeSlider");
        if (_gameplayVolumeSlider == null) _gameplayVolumeSlider = _sfxVolumeSlider;
        if (_sensitivitySlider == null) _sensitivitySlider = FindChildComponentByName<Slider>("SensitivitySlider");
        if (_fullscreenToggle == null) _fullscreenToggle = FindChildComponentByName<Toggle>("FullscreenToggle");
        if (_skipTutorialToggle == null) _skipTutorialToggle = FindChildComponentByName<Toggle>("SkipTutorialToggle");
    }

    private void ApplyDisplaySettings(SettingsData settings)
    {
        if (settings == null)
            return;

        FullScreenMode mode = settings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.fullScreenMode = mode;
        Screen.fullScreen = settings.Fullscreen;
        Screen.SetResolution(settings.ResolutionWidth, settings.ResolutionHeight, mode);
    }

    private T FindChildComponentByName<T>(string objectName) where T : Component
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].name == objectName)
                return components[i];
        }

        return null;
    }

    private void HideReplacedMenu(Transform menuRoot)
    {
        if (menuRoot == null || _replacedMenuObjectStates.Count > 0)
            return;

        for (int i = 0; i < menuRoot.childCount; i++)
        {
            Transform child = menuRoot.GetChild(i);
            if (child == null || child == transform || child.IsChildOf(transform) || ShouldKeepVisibleWhenSettingsOpen(child))
                continue;

            _replacedMenuObjectStates.Add(new MenuObjectState(child.gameObject, child.gameObject.activeSelf));
            child.gameObject.SetActive(false);
        }
    }

    private bool ShouldKeepVisibleWhenSettingsOpen(Transform child)
    {
        string objectName = child.name;
        return objectName == "PauseBackgroundDim" ||
               objectName == "SceneFadeInCanvas" ||
               objectName == "NewGameFadeOverlay";
    }

    private void RestoreReplacedMenu()
    {
        for (int i = 0; i < _replacedMenuObjectStates.Count; i++)
        {
            MenuObjectState state = _replacedMenuObjectStates[i];
            if (state.GameObject != null)
                state.GameObject.SetActive(state.WasActive);
        }

        _replacedMenuObjectStates.Clear();
    }

    private void ApplyMenuFont()
    {
        _menuFont = MenuFontUtility.ResolveMenuFont(transform, _menuFont);
        MenuFontUtility.ApplyFont(transform, _menuFont);
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
