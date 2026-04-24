using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    private const int TransitionCanvasSortingOrder = 5000;

    [Serializable]
    public sealed class CheckpointDefinition
    {
        public Collider Trigger;
        public Transform RespawnAnchor;
        public Renderer GlowRenderer;
    }

    [Header("Triggers")]
    [SerializeField] private CheckpointDefinition[] _checkpoints;
    [SerializeField] private Collider[] _failTriggers;
    [SerializeField] private Collider _combatAndHudUnlockTrigger;
    [SerializeField] private Collider _fogTrigger;

    [Header("Scene References")]
    [SerializeField] private HUDController _tutorialHud;
    [SerializeField] private PlayerCombat _playerCombat;
    [SerializeField] private Transform _playerRoot;
    [SerializeField] private CharacterController _playerCharacterController;
    [SerializeField] private Transform _initialRespawnAnchor;

    [Header("Checkpoint Feedback")]
    [SerializeField] private AudioClip[] _checkpointSounds;
    [Range(0f, 1f)] [SerializeField] private float _checkpointSoundVolume = 1f;
    [ColorUsage(true, true)] [SerializeField] private Color _checkpointGlowColor = new Color(0.2f, 2f, 0.2f, 1f);
    [SerializeField] private float _checkpointGlowDuration = 0.7f;
    [SerializeField] private float _checkpointEmissionIntensity = 8f;
    [Range(0f, 1f)] [SerializeField] private float _checkpointTintStrength = 1f;

    [Header("Fog Zone")]
    [SerializeField] private Transform _fogPoint;
    [SerializeField] private Color _fogColor = Color.black;
    [SerializeField] private FogMode _fogMode = FogMode.ExponentialSquared;
    [SerializeField] private float _fogMinDensity = 0f;
    [SerializeField] private float _fogMaxDensity = 0.18f;
    [SerializeField] private float _fogMaxDistance = 35f;
    [SerializeField] private float _fogFullyBlackDistance = 8f;
    [SerializeField] private float _fogDensitySmoothingSpeed = 2.5f;
    [SerializeField] private float _fogPointReachedRadius = 2f;

    [Header("Fog Audio")]
    [SerializeField] private AudioClip _fogPointLoop;
    [Range(0f, 1f)] [SerializeField] private float _fogPointVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _fogPointSpatialBlend = 1f;
    [SerializeField] private float _fogPointMinDistance = 2f;
    [SerializeField] private float _fogPointMaxDistance = 20f;
    [SerializeField] private AudioClip _fogMusicTrack;
    [Range(0f, 1f)] [SerializeField] private float _fogMusicVolume = 1f;
    [SerializeField] private float _normalMusicFadeOutDuration = 1f;
    [SerializeField] private float _fogMusicFadeInDuration = 1f;

    [Header("Tutorial Transition UI")]
    [SerializeField] private Canvas _transitionCanvas;
    [SerializeField] private Image _fadeOverlay;
    [SerializeField] private CanvasGroup _proceedPanel;
    [SerializeField] private Button _proceedButton;
    [SerializeField] private TextMeshProUGUI _proceedPromptLabel;
    [SerializeField] private TextMeshProUGUI _proceedButtonLabel;
    [SerializeField] private TMP_FontAsset _proceedFont;
    [SerializeField] private Color _fadeColor = Color.black;
    [SerializeField] private float _sceneFadeInDelay = 1f;
    [SerializeField] private float _sceneFadeInDuration = 0.8f;
    [SerializeField] private float _fogPointFadeToBlackDuration = 1f;
    [SerializeField] private string _proceedPromptText = "Proceed?";
    [SerializeField] private string _proceedButtonText = "Proceed";

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private PlayerMovement _playerMovement;
    private bool[] _checkpointActivated;
    private Coroutine[] _glowRoutines;
    private RespawnPoint _initialRespawnPoint;
    private FogSettings _originalFogSettings;
    private AudioSource _fogPointSource;
    private AudioSource _fogMusicSource;
    private Coroutine _fogMusicRoutine;
    private Coroutine _fogMusicStopRoutine;
    private Coroutine _sceneFadeRoutine;
    private Coroutine _fogPointTransitionRoutine;
    private int _currentCheckpointIndex = -1;
    private bool _isCombatAndHudUnlocked;
    private bool _isFogZoneActive;
    private bool _hasReachedFogPoint;
    private bool _hasStartedFogAudio;
    private bool _isLoadingGameplay;
    private bool _hasCachedMusicMixerVolume;
    private float _cachedMusicMixerVolume = 1f;

    private void Awake()
    {
        CaptureOriginalFogSettings();
        ResolveReferences();
        ConfigureRelays();
        CaptureInitialRespawnPoint();
        ApplyInitialGateState();
        EnsureTransitionUi();
        PrimeSceneFadeOverlay();

        _checkpointActivated = new bool[_checkpoints != null ? _checkpoints.Length : 0];
        _glowRoutines = new Coroutine[_checkpointActivated.Length];
    }

    private IEnumerator Start()
    {
        yield return null;
        BeginSceneFadeIn();
    }

    private void Update()
    {
        UpdateFogZone();
    }

    private void OnDisable()
    {
        if (!_isLoadingGameplay)
            RestoreOriginalFogSettings();

        RestoreMusicMixerVolume();
    }

    private void OnDestroy()
    {
        if (!_isLoadingGameplay)
            RestoreOriginalFogSettings();

        RestoreMusicMixerVolume();
    }

    public void HandleTrigger(TutorialTriggerRelay relay, Collider other)
    {
        if (relay == null || !IsPlayerCollider(other))
            return;

        switch (relay.TriggerType)
        {
            case TutorialTriggerType.Checkpoint:
                ActivateCheckpoint(relay.CheckpointIndex);
                break;

            case TutorialTriggerType.FailRespawn:
                RespawnPlayer();
                break;

            case TutorialTriggerType.CombatAndHudUnlock:
                UnlockCombatAndHud();
                break;

            case TutorialTriggerType.FogZone:
                EnterFogZone();
                break;
        }
    }

    public void HandleTriggerExit(TutorialTriggerRelay relay, Collider other)
    {
        if (relay == null || !IsPlayerCollider(other))
            return;

        if (relay.TriggerType == TutorialTriggerType.FogZone)
            ExitFogZone();
    }

    private void ResolveReferences()
    {
        if (_playerRoot == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerRoot = player.transform;
        }

        if (_playerRoot != null)
        {
            if (_playerCombat == null)
                _playerCombat = _playerRoot.GetComponent<PlayerCombat>();

            if (_playerCharacterController == null)
                _playerCharacterController = _playerRoot.GetComponent<CharacterController>();

            _playerMovement = _playerRoot.GetComponent<PlayerMovement>();
        }

        if (_tutorialHud == null)
            _tutorialHud = FindFirstObjectByType<HUDController>();
    }

    private void ConfigureRelays()
    {
        if (_checkpoints != null)
        {
            for (int i = 0; i < _checkpoints.Length; i++)
                ConfigureRelay(_checkpoints[i]?.Trigger, TutorialTriggerType.Checkpoint, i);
        }

        if (_failTriggers != null)
        {
            for (int i = 0; i < _failTriggers.Length; i++)
                ConfigureRelay(_failTriggers[i], TutorialTriggerType.FailRespawn, -1);
        }

        ConfigureRelay(_combatAndHudUnlockTrigger, TutorialTriggerType.CombatAndHudUnlock, -1);
        ConfigureRelay(_fogTrigger, TutorialTriggerType.FogZone, -1);
    }

    private void ConfigureRelay(Collider collider, TutorialTriggerType triggerType, int checkpointIndex)
    {
        if (collider == null)
            return;

        if (!collider.isTrigger)
        {
            Debug.LogWarning($"[TutorialManager] Collider '{collider.name}' is assigned as a tutorial trigger but Is Trigger is disabled.", collider);
        }

        TutorialTriggerRelay relay = collider.GetComponent<TutorialTriggerRelay>();
        if (relay == null)
            relay = collider.gameObject.AddComponent<TutorialTriggerRelay>();

        relay.Configure(this, triggerType, checkpointIndex);
    }

    private void CaptureInitialRespawnPoint()
    {
        if (_initialRespawnAnchor != null)
        {
            _initialRespawnPoint = new RespawnPoint(_initialRespawnAnchor.position, _initialRespawnAnchor.rotation);
            return;
        }

        if (_playerRoot != null)
            _initialRespawnPoint = new RespawnPoint(_playerRoot.position, _playerRoot.rotation);
    }

    private void ApplyInitialGateState()
    {
        if (_combatAndHudUnlockTrigger == null)
        {
            _isCombatAndHudUnlocked = true;
            _playerCombat?.SetCombatEnabled(true);
            _tutorialHud?.ClearLocalVisibilityOverride();
            return;
        }

        _playerCombat?.SetCombatEnabled(false);
        _tutorialHud?.SetLocalVisibilityOverride(false);
        _isCombatAndHudUnlocked = false;
    }

    private void ActivateCheckpoint(int checkpointIndex)
    {
        if (_checkpoints == null || checkpointIndex < 0 || checkpointIndex >= _checkpoints.Length)
            return;

        if (_checkpointActivated[checkpointIndex])
            return;

        _checkpointActivated[checkpointIndex] = true;
        _currentCheckpointIndex = checkpointIndex;

        PlayCheckpointFeedback(checkpointIndex);
    }

    private void PlayCheckpointFeedback(int checkpointIndex)
    {
        Renderer glowRenderer = ResolveGlowRenderer(_checkpoints[checkpointIndex]);
        if (glowRenderer != null)
        {
            if (_glowRoutines[checkpointIndex] != null)
                StopCoroutine(_glowRoutines[checkpointIndex]);

            _glowRoutines[checkpointIndex] = StartCoroutine(PulseCheckpointGlow(glowRenderer, checkpointIndex));
        }

        AudioManager.Instance?.PlaySfx(_checkpointSounds, _checkpointSoundVolume);
    }

    private Renderer ResolveGlowRenderer(CheckpointDefinition checkpoint)
    {
        if (checkpoint == null)
            return null;

        if (checkpoint.GlowRenderer != null)
            return checkpoint.GlowRenderer;

        if (checkpoint.Trigger != null && checkpoint.Trigger.transform.parent != null)
        {
            Renderer parentRenderer = checkpoint.Trigger.transform.parent.GetComponent<Renderer>();
            if (parentRenderer != null)
                return parentRenderer;

            parentRenderer = checkpoint.Trigger.transform.parent.GetComponentInChildren<Renderer>();
            if (parentRenderer != null)
                return parentRenderer;
        }

        if (checkpoint.Trigger != null)
        {
            Renderer childRenderer = checkpoint.Trigger.GetComponentInChildren<Renderer>();
            if (childRenderer != null)
                return childRenderer;
        }

        return checkpoint.Trigger != null ? checkpoint.Trigger.GetComponent<Renderer>() : null;
    }

    private IEnumerator PulseCheckpointGlow(Renderer targetRenderer, int checkpointIndex)
    {
        Material[] materials = targetRenderer.materials;
        var originalState = CaptureMaterialState(materials);
        float duration = Mathf.Max(0.01f, _checkpointGlowDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float intensity = normalized < 0.5f
                ? Mathf.SmoothStep(0f, 1f, normalized / 0.5f)
                : Mathf.SmoothStep(1f, 0f, (normalized - 0.5f) / 0.5f);

            ApplyGlow(materials, originalState, intensity);
            yield return null;
        }

        RestoreGlow(materials, originalState);
        _glowRoutines[checkpointIndex] = null;
    }

    private MaterialState[] CaptureMaterialState(Material[] materials)
    {
        var state = new MaterialState[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            bool hasBaseColor = material.HasProperty(BaseColorId);
            bool hasColor = material.HasProperty(ColorId);
            bool hasEmission = material.HasProperty(EmissionColorId);
            state[i] = new MaterialState(
                hasBaseColor,
                hasBaseColor ? material.GetColor(BaseColorId) : default,
                hasColor,
                hasColor ? material.GetColor(ColorId) : default,
                hasEmission,
                hasEmission ? material.GetColor(EmissionColorId) : default,
                material.IsKeywordEnabled("_EMISSION"));
        }

        return state;
    }

    private void ApplyGlow(Material[] materials, MaterialState[] originalState, float intensity)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            MaterialState state = originalState[i];
            Color glowTint = GetGlowTint(state, intensity);

            if (state.HasEmission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, _checkpointGlowColor * (intensity * _checkpointEmissionIntensity));
            }

            if (state.HasBaseColor)
                material.SetColor(BaseColorId, glowTint);

            if (state.HasColor)
                material.SetColor(ColorId, glowTint);
        }
    }

    private void RestoreGlow(Material[] materials, MaterialState[] originalState)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            MaterialState state = originalState[i];
            if (state.HasBaseColor)
                material.SetColor(BaseColorId, state.BaseColor);

            if (state.HasColor)
                material.SetColor(ColorId, state.Color);

            if (state.HasEmission)
                material.SetColor(EmissionColorId, state.EmissionColor);

            if (state.EmissionKeywordEnabled)
                material.EnableKeyword("_EMISSION");
            else
                material.DisableKeyword("_EMISSION");
        }
    }

    private Color GetGlowTint(MaterialState state, float intensity)
    {
        Color sourceColor = state.HasBaseColor
            ? state.BaseColor
            : state.HasColor
                ? state.Color
                : Color.white;

        Color targetTint = new Color(0.05f, 1f, 0.05f, sourceColor.a);
        return Color.Lerp(sourceColor, targetTint, Mathf.Clamp01(intensity * _checkpointTintStrength));
    }

    private void RespawnPlayer()
    {
        RespawnPoint respawnPoint = GetCurrentRespawnPoint();
        if (_playerRoot == null)
            return;

        if (_playerMovement != null)
        {
            _playerMovement.TeleportTo(respawnPoint.Position, respawnPoint.Rotation);
            return;
        }

        bool wasControllerEnabled = _playerCharacterController != null && _playerCharacterController.enabled;
        if (wasControllerEnabled)
            _playerCharacterController.enabled = false;

        _playerRoot.position = respawnPoint.Position;
        _playerRoot.rotation = Quaternion.Euler(0f, respawnPoint.Rotation.eulerAngles.y, 0f);

        if (wasControllerEnabled)
            _playerCharacterController.enabled = true;
    }

    private RespawnPoint GetCurrentRespawnPoint()
    {
        if (_checkpoints != null && _currentCheckpointIndex >= 0 && _currentCheckpointIndex < _checkpoints.Length)
        {
            CheckpointDefinition checkpoint = _checkpoints[_currentCheckpointIndex];
            Transform respawnAnchor = checkpoint.RespawnAnchor != null
                ? checkpoint.RespawnAnchor
                : checkpoint.Trigger != null
                    ? checkpoint.Trigger.transform
                    : null;

            if (respawnAnchor != null)
                return new RespawnPoint(respawnAnchor.position, respawnAnchor.rotation);
        }

        return _initialRespawnPoint;
    }

    private void UnlockCombatAndHud()
    {
        if (_isCombatAndHudUnlocked)
            return;

        _isCombatAndHudUnlocked = true;
        _playerCombat?.SetCombatEnabled(true);
        _tutorialHud?.SetLocalVisibilityOverride(true);
    }

    private void CaptureOriginalFogSettings()
    {
        _originalFogSettings = new FogSettings(
            RenderSettings.fog,
            RenderSettings.fogColor,
            RenderSettings.fogMode,
            RenderSettings.fogDensity,
            RenderSettings.fogStartDistance,
            RenderSettings.fogEndDistance);
    }

    private void EnterFogZone()
    {
        if (_hasReachedFogPoint)
            return;

        _isFogZoneActive = true;
        ApplyFogSettings(0f);
        BeginFogAudio();
    }

    private void ExitFogZone()
    {
        if (_hasReachedFogPoint)
            return;

        _isFogZoneActive = false;
        RestoreOriginalFogSettings();
        StopFogAudio();
    }

    private void UpdateFogZone()
    {
        if (!_isFogZoneActive || _hasReachedFogPoint || _playerRoot == null || _fogPoint == null)
            return;

        float distanceToFogPoint = Vector3.Distance(_playerRoot.position, _fogPoint.position);
        float densityIntensity = GetFogIntensity(distanceToFogPoint, 0f);
        float colorIntensity = GetFogIntensity(distanceToFogPoint, Mathf.Max(0f, _fogFullyBlackDistance));
        float targetDensity = Mathf.Lerp(_originalFogSettings.Density, Mathf.Max(0f, _fogMaxDensity), densityIntensity);

        if (densityIntensity > 0f && _fogMinDensity > 0f)
            targetDensity = Mathf.Max(targetDensity, _fogMinDensity * densityIntensity);

        float smoothing = 1f - Mathf.Exp(-Mathf.Max(0.01f, _fogDensitySmoothingSpeed) * Time.unscaledDeltaTime);
        Color targetColor = Color.Lerp(_originalFogSettings.Color, _fogColor, colorIntensity);

        RenderSettings.fog = true;
        RenderSettings.fogMode = _fogMode;
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetColor, smoothing);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetDensity, smoothing);

        if (distanceToFogPoint <= Mathf.Max(0.01f, _fogPointReachedRadius))
            BeginFogPointReached();
    }

    private float GetFogIntensity(float distanceToFogPoint, float fullIntensityDistance)
    {
        float maxDistance = Mathf.Max(0.01f, _fogMaxDistance);
        float fullDistance = Mathf.Clamp(fullIntensityDistance, 0f, maxDistance - 0.01f);
        float normalized = 1f - Mathf.Clamp01((distanceToFogPoint - fullDistance) / (maxDistance - fullDistance));
        return Mathf.SmoothStep(0f, 1f, normalized);
    }

    private void ApplyFogSettings(float intensity)
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = Color.Lerp(_originalFogSettings.Color, _fogColor, Mathf.Clamp01(intensity));
        RenderSettings.fogMode = _fogMode;
        RenderSettings.fogDensity = Mathf.Lerp(_originalFogSettings.Density, Mathf.Max(0f, _fogMaxDensity), Mathf.Clamp01(intensity));
    }

    private void RestoreOriginalFogSettings()
    {
        RenderSettings.fog = _originalFogSettings.Enabled;
        RenderSettings.fogColor = _originalFogSettings.Color;
        RenderSettings.fogMode = _originalFogSettings.Mode;
        RenderSettings.fogDensity = _originalFogSettings.Density;
        RenderSettings.fogStartDistance = _originalFogSettings.StartDistance;
        RenderSettings.fogEndDistance = _originalFogSettings.EndDistance;
    }

    private void BeginFogPointReached()
    {
        if (_hasReachedFogPoint)
            return;

        _hasReachedFogPoint = true;
        _isFogZoneActive = false;
        ApplyFogSettings(1f);
        RenderSettings.fogDensity = Mathf.Max(_fogMinDensity, _fogMaxDensity);

        if (_playerCombat != null)
            _playerCombat.SetCombatEnabled(false);

        if (_fogPointTransitionRoutine != null)
            StopCoroutine(_fogPointTransitionRoutine);

        _fogPointTransitionRoutine = StartCoroutine(FogPointReachedRoutine());
    }

    private IEnumerator FogPointReachedRoutine()
    {
        EnsureTransitionUi();
        HideProceedPanel();

        if (_fadeOverlay != null)
        {
            _fadeOverlay.raycastTarget = true;
            yield return StartCoroutine(FadeOverlayRoutine(GetFadeOverlayAlpha(), 1f, _fogPointFadeToBlackDuration));
        }

        ShowProceedPanel();
        _fogPointTransitionRoutine = null;
    }

    private void BeginFogAudio()
    {
        if (_hasStartedFogAudio)
            return;

        _hasStartedFogAudio = true;
        PlayFogPointLoop();
        StartFogMusicTransition();
    }

    private void PlayFogPointLoop()
    {
        if (_fogPointLoop == null || _fogPoint == null)
            return;

        AudioSource source = EnsureFogPointSource();
        if (source == null)
            return;

        source.clip = _fogPointLoop;
        source.volume = Mathf.Clamp01(_fogPointVolume);
        source.spatialBlend = Mathf.Clamp01(_fogPointSpatialBlend);
        source.minDistance = Mathf.Max(0.01f, _fogPointMinDistance);
        source.maxDistance = Mathf.Max(source.minDistance, _fogPointMaxDistance);
        source.loop = true;

        if (!source.isPlaying)
            source.Play();
    }

    private AudioSource EnsureFogPointSource()
    {
        if (_fogPointSource != null)
            return _fogPointSource;

        if (_fogPoint == null)
            return null;

        _fogPointSource = _fogPoint.GetComponent<AudioSource>();
        if (_fogPointSource == null)
            _fogPointSource = _fogPoint.gameObject.AddComponent<AudioSource>();

        _fogPointSource.playOnAwake = false;
        _fogPointSource.loop = true;
        _fogPointSource.rolloffMode = AudioRolloffMode.Logarithmic;
        return _fogPointSource;
    }

    private void StartFogMusicTransition()
    {
        if (_fogMusicTrack == null)
            return;

        if (_fogMusicRoutine != null)
            StopCoroutine(_fogMusicRoutine);

        if (_fogMusicStopRoutine != null)
        {
            StopCoroutine(_fogMusicStopRoutine);
            _fogMusicStopRoutine = null;
        }

        _fogMusicRoutine = StartCoroutine(FogMusicTransitionRoutine());
    }

    private IEnumerator FogMusicTransitionRoutine()
    {
        CacheMusicMixerVolume();

        if (AudioManager.Instance != null)
        {
            float fadeOutDuration = Mathf.Max(0f, _normalMusicFadeOutDuration);
            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeOutDuration));
                AudioManager.Instance.SetMusicVolume(Mathf.Lerp(_cachedMusicMixerVolume, 0f, t));
                yield return null;
            }

            AudioManager.Instance.SetMusicVolume(0f);
        }

        MusicManager.Instance?.Stop();
        RestoreMusicMixerVolume();

        AudioSource source = EnsureFogMusicSource();
        source.clip = _fogMusicTrack;
        source.loop = true;
        source.volume = 0f;
        source.Play();

        float targetVolume = Mathf.Clamp01(_fogMusicVolume);
        float fadeInDuration = Mathf.Max(0f, _fogMusicFadeInDuration);
        float fadeElapsed = 0f;

        while (fadeElapsed < fadeInDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeElapsed / Mathf.Max(0.01f, fadeInDuration));
            source.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        source.volume = targetVolume;
        _fogMusicRoutine = null;
    }

    private AudioSource EnsureFogMusicSource()
    {
        if (_fogMusicSource != null)
            return _fogMusicSource;

        GameObject sourceObject = new GameObject("TutorialFogMusicSource");
        sourceObject.transform.SetParent(transform, false);

        _fogMusicSource = sourceObject.AddComponent<AudioSource>();
        _fogMusicSource.playOnAwake = false;
        _fogMusicSource.loop = true;
        _fogMusicSource.spatialBlend = 0f;
        _fogMusicSource.ignoreListenerPause = true;
        return _fogMusicSource;
    }

    private void StopFogAudio()
    {
        _hasStartedFogAudio = false;

        if (_fogPointSource != null)
            _fogPointSource.Stop();

        if (_fogMusicRoutine != null)
        {
            StopCoroutine(_fogMusicRoutine);
            _fogMusicRoutine = null;
        }

        RestoreMusicMixerVolume();

        if (_fogMusicSource == null || !_fogMusicSource.isPlaying)
            return;

        if (_fogMusicStopRoutine != null)
            StopCoroutine(_fogMusicStopRoutine);

        _fogMusicStopRoutine = StartCoroutine(FadeOutAndStopAudioSource(_fogMusicSource, 0.35f));
    }

    private IEnumerator FadeOutAndStopAudioSource(AudioSource source, float duration)
    {
        if (source == null)
            yield break;

        float startingVolume = source.volume;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startingVolume, 0f, Mathf.Clamp01(elapsed / safeDuration));
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
        _fogMusicStopRoutine = null;
    }

    private void CacheMusicMixerVolume()
    {
        if (_hasCachedMusicMixerVolume)
            return;

        _cachedMusicMixerVolume = GetConfiguredMusicVolume();
        _hasCachedMusicMixerVolume = true;
    }

    private void RestoreMusicMixerVolume()
    {
        if (!_hasCachedMusicMixerVolume || AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetMusicVolume(_cachedMusicMixerVolume);
        _hasCachedMusicMixerVolume = false;
    }

    private float GetConfiguredMusicVolume()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.CurrentSettings != null)
            return Mathf.Clamp01(SaveManager.Instance.CurrentSettings.MusicVolume);

        return 1f;
    }

    private void EnsureTransitionUi()
    {
        EnsureEventSystem();
        EnsureTransitionCanvas();
        EnsureFadeOverlay();
        EnsureProceedPanel();
        ApplyProceedFont();
        ApplyProceedText();
        HideProceedPanel();
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        InputManager.Instance?.ConfigureUIInputModule();
    }

    private void EnsureTransitionCanvas()
    {
        if (_transitionCanvas == null)
        {
            Canvas existingCanvas = null;
            if (_fadeOverlay != null)
                existingCanvas = _fadeOverlay.GetComponentInParent<Canvas>();
            else if (_proceedPanel != null)
                existingCanvas = _proceedPanel.GetComponentInParent<Canvas>();
            else if (_proceedButton != null)
                existingCanvas = _proceedButton.GetComponentInParent<Canvas>();

            if (existingCanvas != null)
            {
                _transitionCanvas = existingCanvas;
            }
            else
            {
                GameObject canvasObject = new GameObject("TutorialTransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _transitionCanvas = canvasObject.GetComponent<Canvas>();

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        if (_transitionCanvas == null)
            return;

        _transitionCanvas.gameObject.SetActive(true);
        _transitionCanvas.enabled = true;
        _transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _transitionCanvas.overrideSorting = true;
        _transitionCanvas.sortingOrder = TransitionCanvasSortingOrder;

        if (_transitionCanvas.GetComponent<GraphicRaycaster>() == null)
            _transitionCanvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private void EnsureFadeOverlay()
    {
        if (_transitionCanvas == null)
            return;

        if (_fadeOverlay == null)
        {
            GameObject overlayObject = new GameObject("TutorialFadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(_transitionCanvas.transform, false);

            RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            _fadeOverlay = overlayObject.GetComponent<Image>();
        }

        _fadeOverlay.gameObject.SetActive(true);
        _fadeOverlay.enabled = true;
        _fadeOverlay.transform.SetAsLastSibling();
        _fadeOverlay.color = WithAlpha(_fadeColor, GetFadeOverlayAlpha());
        _fadeOverlay.raycastTarget = false;
    }

    private void EnsureProceedPanel()
    {
        if (_proceedPanel != null && _proceedButton != null)
        {
            _proceedButton.onClick.RemoveListener(OnProceedClicked);
            _proceedButton.onClick.AddListener(OnProceedClicked);
            _proceedPanel.transform.SetAsLastSibling();
            return;
        }

        GameObject panelObject = new GameObject("ProceedPanel", typeof(RectTransform), typeof(CanvasGroup));
        panelObject.transform.SetParent(_transitionCanvas.transform, false);
        _proceedPanel = panelObject.GetComponent<CanvasGroup>();

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject promptObject = new GameObject("PromptLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        promptObject.transform.SetParent(panelObject.transform, false);
        _proceedPromptLabel = promptObject.GetComponent<TextMeshProUGUI>();
        _proceedPromptLabel.alignment = TextAlignmentOptions.Center;
        _proceedPromptLabel.fontSize = 64f;
        _proceedPromptLabel.color = Color.white;

        RectTransform promptRect = promptObject.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(0f, 80f);
        promptRect.sizeDelta = new Vector2(700f, 110f);

        GameObject buttonObject = new GameObject("ProceedButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panelObject.transform, false);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        _proceedButton = buttonObject.GetComponent<Button>();
        _proceedButton.targetGraphic = buttonImage;
        _proceedButton.onClick.AddListener(OnProceedClicked);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -55f);
        buttonRect.sizeDelta = new Vector2(280f, 72f);

        GameObject buttonLabelObject = new GameObject("ButtonLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        buttonLabelObject.transform.SetParent(buttonObject.transform, false);
        _proceedButtonLabel = buttonLabelObject.GetComponent<TextMeshProUGUI>();
        _proceedButtonLabel.alignment = TextAlignmentOptions.Center;
        _proceedButtonLabel.fontSize = 34f;
        _proceedButtonLabel.color = Color.white;

        RectTransform buttonLabelRect = buttonLabelObject.GetComponent<RectTransform>();
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;

        _proceedPanel.transform.SetAsLastSibling();
        MenuButtonHoverEffect.AttachToButtons(panelObject.transform);
    }

    private void ApplyProceedText()
    {
        if (_proceedPromptLabel != null)
            _proceedPromptLabel.text = _proceedPromptText;

        if (_proceedButtonLabel != null)
            _proceedButtonLabel.text = _proceedButtonText;
    }

    private void ApplyProceedFont()
    {
        if (_proceedFont == null)
            return;

        if (_proceedPromptLabel != null)
            _proceedPromptLabel.font = _proceedFont;

        if (_proceedButtonLabel != null)
            _proceedButtonLabel.font = _proceedFont;
    }

    private void BeginSceneFadeIn()
    {
        if (_fadeOverlay == null)
            return;

        if (_sceneFadeRoutine != null)
            StopCoroutine(_sceneFadeRoutine);

        SetFadeOverlayAlpha(1f);
        _fadeOverlay.raycastTarget = true;
        _sceneFadeRoutine = StartCoroutine(SceneFadeInRoutine());
    }

    private void PrimeSceneFadeOverlay()
    {
        if (_fadeOverlay == null)
            return;

        _fadeOverlay.transform.SetAsLastSibling();
        SetFadeOverlayAlpha(1f);
        _fadeOverlay.raycastTarget = true;
    }

    private IEnumerator SceneFadeInRoutine()
    {
        float delay = Mathf.Max(0f, _sceneFadeInDelay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        yield return StartCoroutine(FadeOverlayRoutine(1f, 0f, _sceneFadeInDuration));

        if (_fadeOverlay != null)
            _fadeOverlay.raycastTarget = false;

        _sceneFadeRoutine = null;
    }

    private IEnumerator FadeOverlayRoutine(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFadeOverlayAlpha(Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / safeDuration)));
            yield return null;
        }

        SetFadeOverlayAlpha(toAlpha);
    }

    private void SetFadeOverlayAlpha(float alpha)
    {
        if (_fadeOverlay == null)
            return;

        _fadeOverlay.color = WithAlpha(_fadeColor, alpha);
    }

    private float GetFadeOverlayAlpha()
    {
        return _fadeOverlay != null ? _fadeOverlay.color.a : 0f;
    }

    private void HideProceedPanel()
    {
        if (_proceedPanel == null)
            return;

        _proceedPanel.alpha = 0f;
        _proceedPanel.interactable = false;
        _proceedPanel.blocksRaycasts = false;
    }

    private void ShowProceedPanel()
    {
        if (_proceedPanel == null)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        InputManager.Instance?.EnableUIControls();

        _proceedPanel.alpha = 1f;
        _proceedPanel.interactable = true;
        _proceedPanel.blocksRaycasts = true;

        if (_proceedButton != null)
            _proceedButton.Select();
    }

    private void OnProceedClicked()
    {
        if (_isLoadingGameplay)
            return;

        _isLoadingGameplay = true;
        SaveManager.Instance?.DeleteSave();
        InputManager.Instance?.EnablePlayerControls();

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadScene("Gameplay");
            return;
        }

        SceneManager.LoadScene("Gameplay");
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (_playerRoot != null)
            return other.transform.root == _playerRoot;

        return other.CompareTag("Player");
    }

    private readonly struct RespawnPoint
    {
        public RespawnPoint(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }

    private readonly struct FogSettings
    {
        public FogSettings(bool enabled, Color color, FogMode mode, float density, float startDistance, float endDistance)
        {
            Enabled = enabled;
            Color = color;
            Mode = mode;
            Density = density;
            StartDistance = startDistance;
            EndDistance = endDistance;
        }

        public bool Enabled { get; }
        public Color Color { get; }
        public FogMode Mode { get; }
        public float Density { get; }
        public float StartDistance { get; }
        public float EndDistance { get; }
    }

    private readonly struct MaterialState
    {
        public MaterialState(
            bool hasBaseColor,
            Color baseColor,
            bool hasColor,
            Color color,
            bool hasEmission,
            Color emissionColor,
            bool emissionKeywordEnabled)
        {
            HasBaseColor = hasBaseColor;
            BaseColor = baseColor;
            HasColor = hasColor;
            Color = color;
            HasEmission = hasEmission;
            EmissionColor = emissionColor;
            EmissionKeywordEnabled = emissionKeywordEnabled;
        }

        public bool HasBaseColor { get; }
        public Color BaseColor { get; }
        public bool HasColor { get; }
        public Color Color { get; }
        public bool HasEmission { get; }
        public Color EmissionColor { get; }
        public bool EmissionKeywordEnabled { get; }
    }
}
