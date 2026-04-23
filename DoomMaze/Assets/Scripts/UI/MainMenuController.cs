using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Owns the Main Menu scene canvas. Wires New Game, Continue, Settings, and Quit buttons.
/// Interactability of Continue is determined at runtime via <see cref="SaveManager"/>.
/// </summary>
public class MainMenuController : MonoBehaviour, IMenuHoverAudioProvider
{
    [SerializeField] private Button     _continueButton;
    [SerializeField] private GameObject _settingsPanel;
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

    private void Awake()
    {
        if (_continueButton == null) Debug.LogError("[MainMenuController] _continueButton is not assigned.");
        if (_settingsPanel  == null) Debug.LogError("[MainMenuController] _settingsPanel is not assigned.");

        _menuButtons = GetComponentsInChildren<Button>(true);
        _menuButtonInteractableStates = _menuButtons != null ? new bool[_menuButtons.Length] : null;
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

        ResetTransitionVisuals();
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
        if (_isNewGameTransitioning)
            return;

        _newGameTransitionCoroutine = StartCoroutine(NewGameTransitionRoutine());
    }

    /// <summary>Loads the existing save and transitions to the Gameplay scene.</summary>
    public void OnContinue()
    {
        if (_isNewGameTransitioning)
            return;

        PlayClickSound();
        SaveManager.Instance?.LoadGame();
        SceneFlowManager.Instance?.LoadScene("Gameplay");
    }

    /// <summary>Opens the settings panel.</summary>
    public void OnSettings()
    {
        if (_isNewGameTransitioning)
            return;

        PlayClickSound();
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
        if (_isNewGameTransitioning)
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

        SceneFlowManager.Instance?.LoadScene("Gameplay");
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

        _newGameGlitchController = FindObjectOfType<HypeVolumeController>();
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
}
