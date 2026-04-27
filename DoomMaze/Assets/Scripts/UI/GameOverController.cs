using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Listens to <see cref="GameStateChangedEvent"/> and activates the Death or Victory panel
/// accordingly. Unlocks the cursor when either panel is shown.
/// </summary>
public class GameOverController : MonoBehaviour, IMenuHoverAudioProvider
{
    [SerializeField] private GameObject _deathPanel;
    [SerializeField] private GameObject _victoryPanel;
    [SerializeField] private TMP_FontAsset _menuFont;
    [Header("Victory Reveal")]
    [SerializeField] private float _victoryWhiteFadeDuration = 1.8f;
    [SerializeField] private float _victoryTextFadeDuration = 1.1f;
    [SerializeField] private float _victoryAutoReturnDelay = 3.5f;
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
        ApplyMenuFont();
        PrepareVictoryRevealReferences();
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

        _victoryRevealRoutine = null;
        SceneFlowManager.Instance?.LoadScene("MainMenu");
    }

    private void PrepareVictoryRevealReferences()
    {
        _victoryWhiteOverlay = EnsureVictoryWhiteOverlay();
        TMP_Text victoryLabel = EnsureVictoryLabel();
        ConfigureVictoryLabel(victoryLabel);
        _victoryLabelGroup = EnsureCanvasGroup(victoryLabel != null ? victoryLabel.transform : _victoryPanel.transform);
        _victoryButtons = _victoryPanel.GetComponentsInChildren<Button>(true);
        _victoryButtonGroups = new CanvasGroup[_victoryButtons.Length];

        for (int i = 0; i < _victoryButtons.Length; i++)
            _victoryButtonGroups[i] = EnsureCanvasGroup(_victoryButtons[i].transform);
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
        label.transform.SetAsLastSibling();

        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
        if (group != null)
            group.alpha = Mathf.Clamp01(alpha);
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
