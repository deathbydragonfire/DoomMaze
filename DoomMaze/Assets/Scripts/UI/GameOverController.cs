using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Listens to <see cref="GameStateChangedEvent"/> and activates the Death or Victory panel
/// accordingly. Unlocks the cursor when either panel is shown.
/// </summary>
public class GameOverController : MonoBehaviour, IMenuHoverAudioProvider
{
    private const string VictoryMusicPath = "Assets/Audio/Music/Chrome Triumph.wav";

    [SerializeField] private GameObject _deathPanel;
    [SerializeField] private GameObject _victoryPanel;
    [SerializeField] private TMP_FontAsset _menuFont;

    [Header("Victory Reveal")]
    [SerializeField] private float _victoryWhiteFadeDuration = 1.8f;
    [SerializeField] private float _victoryTextFadeDuration = 1.1f;
    [SerializeField] private float _victoryAutoReturnDelay = 3.5f;

    [Header("Victory Music")]
    [SerializeField] private AudioClip _victoryMusic;
    [SerializeField] [Range(0f, 1f)] private float _victoryMusicVolume = 1f;
    [SerializeField] private float _victoryMusicFadeOutDuration = 1.2f;
    [SerializeField] private float _victoryMusicFadeInDuration = 1.2f;

    [Header("UI Audio")]
    [SerializeField] private AudioClip[] _hoverSounds;
    [Range(0f, 1f)] [SerializeField] private float _hoverSoundVolume = 1f;
    [SerializeField] private AudioClip[] _clickSounds;
    [Range(0f, 1f)] [SerializeField] private float _clickSoundVolume = 1f;

    private Coroutine _victoryRevealRoutine;
    private Image _victoryWhiteOverlay;
    private CanvasGroup _victoryLabelGroup;
    private CanvasGroup[] _victoryButtonGroups;
    private Button[] _victoryButtons;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_victoryMusic == null)
            _victoryMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(VictoryMusicPath);
    }
#endif

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

        if (_victoryRevealRoutine != null)
        {
            StopCoroutine(_victoryRevealRoutine);
            _victoryRevealRoutine = null;
        }
    }

    private void OnStateChanged(GameStateChangedEvent e)
    {
        bool isDead    = e.NewState == GameState.Dead;
        bool isVictory = e.NewState == GameState.Victory;

        if (isVictory)
            StartVictoryReveal();
        else
            SetPanelsActive(isDead, false);

        if (isDead || isVictory)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            InputManager.Instance?.EnableUIControls();
        }
    }

    private void SetPanelsActive(bool death, bool victory)
    {
        if (!victory && _victoryRevealRoutine != null)
        {
            StopCoroutine(_victoryRevealRoutine);
            _victoryRevealRoutine = null;
        }

        if (_deathPanel   != null) _deathPanel.SetActive(death);
        if (_victoryPanel != null) _victoryPanel.SetActive(victory);
        ApplyMenuFont();
    }

    private void StartVictoryReveal()
    {
        if (_victoryRevealRoutine != null)
            StopCoroutine(_victoryRevealRoutine);

        if (_deathPanel != null)
            _deathPanel.SetActive(false);

        if (_victoryPanel == null)
            return;

        _victoryPanel.SetActive(true);
        EnsureUiInputReady();
        ApplyMenuFont();
        PrepareVictoryRevealReferences();
        ApplyMenuFont();
        PlayVictoryMusic();
        _victoryRevealRoutine = StartCoroutine(VictoryRevealRoutine());
    }

    private IEnumerator VictoryRevealRoutine()
    {
        SetVictoryRevealState(whiteAlpha: 0f, labelAlpha: 0f, buttonAlpha: 0f, buttonsInteractable: false);

        yield return FadeImageAlpha(_victoryWhiteOverlay, 0f, 1f, _victoryWhiteFadeDuration);
        yield return FadeCanvasGroupAlpha(_victoryLabelGroup, 0f, 1f, _victoryTextFadeDuration);

        float delay = Mathf.Max(0f, _victoryAutoReturnDelay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        yield return FadeVictoryButtons(0f, 1f, _victoryTextFadeDuration);
        SetVictoryButtonsInteractable(true);
        _victoryRevealRoutine = null;
    }

    private void PrepareVictoryRevealReferences()
    {
        _victoryWhiteOverlay = EnsureVictoryWhiteOverlay();
        TMP_Text victoryLabel = EnsureVictoryLabel();
        ConfigureVictoryLabel(victoryLabel);
        EnsureVictoryMainMenuButton();
        _victoryLabelGroup = EnsureCanvasGroup(victoryLabel != null ? victoryLabel.transform : _victoryPanel.transform);
        _victoryButtons = _victoryPanel.GetComponentsInChildren<Button>(true);
        _victoryButtonGroups = new CanvasGroup[_victoryButtons.Length];

        for (int i = 0; i < _victoryButtons.Length; i++)
        {
            ConfigureVictoryButton(_victoryButtons[i]);
            _victoryButtonGroups[i] = EnsureCanvasGroup(_victoryButtons[i].transform);
        }

        MenuButtonHoverEffect.AttachToButtons(_victoryPanel.transform);
    }

    private Image EnsureVictoryWhiteOverlay()
    {
        Transform existing = _victoryPanel.transform.Find("VictoryWhiteFadeOverlay");
        GameObject overlayObject = existing != null
            ? existing.gameObject
            : new GameObject("VictoryWhiteFadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        overlayObject.transform.SetParent(_victoryPanel.transform, false);
        overlayObject.transform.SetAsFirstSibling();

        RectTransform rect = overlayObject.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image overlay = overlayObject.GetComponent<Image>();
        overlay.color = Color.clear;
        overlay.raycastTarget = false;
        return overlay;
    }

    private TMP_Text EnsureVictoryLabel()
    {
        TMP_Text existing = FindVictoryLabel();
        if (existing != null)
            return existing;

        GameObject labelObject = new("VictoryAutoReturnLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(_victoryPanel.transform, false);
        return labelObject.GetComponent<TextMeshProUGUI>();
    }

    private TMP_Text FindVictoryLabel()
    {
        TMP_Text[] labels = _victoryPanel.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            string labelText = label != null && label.text != null ? label.text : string.Empty;
            string normalizedText = labelText.ToLowerInvariant();
            if (label != null && (label.name.Contains("Victory") || normalizedText.Contains("victory") || normalizedText.Contains("complete")))
                return label;
        }

        return null;
    }

    private static void ConfigureVictoryLabel(TMP_Text label)
    {
        if (label == null)
            return;

        label.text = "LEVEL COMPLETE";
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 36f;
        label.fontSizeMax = 96f;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.08f, 0.56f);
        rect.anchorMax = new Vector2(0.92f, 0.78f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private Button EnsureVictoryMainMenuButton()
    {
        Button existing = FindVictoryMainMenuButton();
        if (existing != null)
            return existing;

        GameObject buttonObject = new("VictoryMainMenuButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(_victoryPanel.transform, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 1f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ConfigureSolidButtonColors(button);
        button.onClick.AddListener(OnMainMenu);

        RectTransform rect = buttonObject.transform as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.38f);
        rect.anchorMax = new Vector2(0.5f, 0.38f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 78f);
        rect.anchoredPosition = Vector2.zero;
        buttonObject.transform.SetAsLastSibling();

        GameObject textObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.transform as RectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = "BACK TO MAIN MENU";
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 34f;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        return button;
    }

    private Button FindVictoryMainMenuButton()
    {
        if (_victoryPanel == null)
            return null;

        Button[] buttons = _victoryPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            string buttonName = button.name.ToLowerInvariant();
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            string labelText = label != null && label.text != null ? label.text.ToLowerInvariant() : string.Empty;
            if (buttonName.Contains("mainmenu") ||
                buttonName.Contains("main menu") ||
                labelText.Contains("main menu"))
            {
                button.onClick.RemoveListener(OnMainMenu);
                button.onClick.AddListener(OnMainMenu);
                return button;
            }
        }

        return null;
    }

    private void ConfigureVictoryButton(Button button)
    {
        if (button == null)
            return;

        TMP_Text label = FindButtonLabel(button);
        EnsureButtonLabelIsChild(button, ref label);
        if (label != null)
        {
            label.text = "BACK TO MAIN MENU";
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = Mathf.Max(label.fontSizeMax, 34f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            StretchLabelToButton(label);
        }

        Image image = EnsureButtonBackgroundImage(button);
        image.color = new Color(0f, 0f, 0f, 1f);
        image.raycastTarget = true;
        button.targetGraphic = image;

        ConfigureSolidButtonColors(button);
        PositionVictoryButton(button);
        button.onClick.RemoveListener(OnMainMenu);
        button.onClick.AddListener(OnMainMenu);
        button.transform.SetAsLastSibling();
    }

    private static void StretchLabelToButton(TMP_Text label)
    {
        if (label == null || label.transform is not RectTransform rect)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        label.margin = Vector4.zero;
        label.alignment = TextAlignmentOptions.Center;
    }

    private static Image EnsureButtonBackgroundImage(Button button)
    {
        Image image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        return image;
    }

    private static TMP_Text FindButtonLabel(Button button)
    {
        if (button == null)
            return null;

        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text fallback = null;
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text candidate = labels[i];
            if (candidate == null)
                continue;

            fallback ??= candidate;
            if (candidate.enabled && candidate.transform != button.transform)
                return candidate;
        }

        return fallback;
    }

    private static void EnsureButtonLabelIsChild(Button button, ref TMP_Text label)
    {
        if (button == null || label == null || label.transform != button.transform)
            return;

        RectTransform buttonRect = button.transform as RectTransform;
        Vector2 labelSize = buttonRect != null ? buttonRect.sizeDelta : new Vector2(420f, 78f);

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(button.transform, false);

        RectTransform labelRect = labelObject.transform as RectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI childLabel = labelObject.GetComponent<TextMeshProUGUI>();
        childLabel.text = label.text;
        childLabel.font = label.font;
        childLabel.fontStyle = label.fontStyle;
        childLabel.fontSize = label.fontSize;
        childLabel.enableAutoSizing = label.enableAutoSizing;
        childLabel.fontSizeMin = label.fontSizeMin;
        childLabel.fontSizeMax = label.fontSizeMax;
        childLabel.alignment = TextAlignmentOptions.Center;
        childLabel.color = Color.white;
        childLabel.raycastTarget = false;
        StretchLabelToButton(childLabel);

        label.enabled = false;
        label.rectTransform.sizeDelta = labelSize;
        label = childLabel;
    }

    private static void ConfigureSolidButtonColors(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 1f);
        colors.highlightedColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        colors.pressedColor = new Color(0.32f, 0.08f, 0.02f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0f, 0f, 0f, 0.35f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static void PositionVictoryButton(Button button)
    {
        if (button == null || button.transform is not RectTransform rect)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.38f);
        rect.anchorMax = new Vector2(0.5f, 0.38f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 78f);
        rect.anchoredPosition = Vector2.zero;
    }

    private static CanvasGroup EnsureCanvasGroup(Transform target)
    {
        if (target == null)
            return null;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.gameObject.AddComponent<CanvasGroup>();

        return group;
    }

    private void SetVictoryRevealState(float whiteAlpha, float labelAlpha, float buttonAlpha, bool buttonsInteractable)
    {
        SetImageAlpha(_victoryWhiteOverlay, whiteAlpha);
        SetCanvasGroupAlpha(_victoryLabelGroup, labelAlpha);

        if (_victoryButtonGroups != null)
        {
            for (int i = 0; i < _victoryButtonGroups.Length; i++)
                SetCanvasGroupAlpha(_victoryButtonGroups[i], buttonAlpha);
        }

        SetVictoryButtonsInteractable(buttonsInteractable);
    }

    private void SetVictoryButtonsInteractable(bool interactable)
    {
        if (_victoryButtons == null)
            return;

        for (int i = 0; i < _victoryButtons.Length; i++)
        {
            if (_victoryButtons[i] != null)
            {
                _victoryButtons[i].interactable = interactable;
                _victoryButtons[i].gameObject.SetActive(interactable);
            }
        }
    }

    private IEnumerator FadeVictoryButtons(float from, float to, float duration)
    {
        if (_victoryButtonGroups == null || _victoryButtonGroups.Length == 0)
            yield break;

        for (int i = 0; i < _victoryButtons.Length; i++)
        {
            if (_victoryButtons[i] != null)
                _victoryButtons[i].gameObject.SetActive(true);
        }

        yield return FadeCanvasGroupsAlpha(_victoryButtonGroups, from, to, duration);
    }

    private static IEnumerator FadeImageAlpha(Image image, float from, float to, float duration)
    {
        if (image == null)
            yield break;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetImageAlpha(image, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetImageAlpha(image, to);
    }

    private static IEnumerator FadeCanvasGroupAlpha(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetCanvasGroupAlpha(group, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetCanvasGroupAlpha(group, to);
    }

    private static IEnumerator FadeCanvasGroupsAlpha(CanvasGroup[] groups, float from, float to, float duration)
    {
        if (groups == null || groups.Length == 0)
            yield break;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < groups.Length; i++)
                SetCanvasGroupAlpha(groups[i], alpha);

            yield return null;
        }

        for (int i = 0; i < groups.Length; i++)
            SetCanvasGroupAlpha(groups[i], to);
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = Color.white;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static void SetCanvasGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group == null)
            return;

        group.alpha = Mathf.Clamp01(alpha);
        group.blocksRaycasts = alpha > 0.99f;
        group.interactable = alpha > 0.99f;
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
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            PauseManager.Instance.SetPaused(false);
        MusicManager.Instance?.Stop();

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadScene("MainMenu");
            return;
        }

        GameManager.Instance?.SetState(GameState.MainMenu);
        SceneManager.LoadScene("MainMenu");
    }

    public void PlayMenuHoverSound()
    {
        AudioManager.Instance?.PlayUi(_hoverSounds, _hoverSoundVolume);
    }

    private void PlayClickSound()
    {
        AudioManager.Instance?.PlayUi(_clickSounds, _clickSoundVolume);
    }

    private void PlayVictoryMusic()
    {
        if (_victoryMusic == null)
            return;

        MusicManager.Instance?.PlayTemporaryClip(
            _victoryMusic,
            _victoryMusicVolume,
            _victoryMusicFadeOutDuration,
            _victoryMusicFadeInDuration);
    }

    private void EnsureUiInputReady()
    {
        Canvas canvas = _victoryPanel != null ? _victoryPanel.GetComponentInParent<Canvas>(true) : null;
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            InputManager.Instance?.ConfigureUIInputModule();
        }
    }

    private void ApplyMenuFont()
    {
        _menuFont = MenuFontUtility.ResolveMenuFont(transform, _menuFont);
        MenuFontUtility.ApplyFont(transform, _menuFont);
    }
}
