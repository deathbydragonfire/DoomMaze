using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
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
    [SerializeField] private Collider _enemyAndSuperActivationTrigger;
    [SerializeField] private Collider _fogTrigger;

    [Header("Scene References")]
    [SerializeField] private HUDController _tutorialHud;
    [SerializeField] private PlayerCombat _playerCombat;
    [SerializeField] private Transform _playerRoot;
    [SerializeField] private CharacterController _playerCharacterController;
    [SerializeField] private Transform _initialRespawnAnchor;

    [Header("Scene Start Audio")]
    [SerializeField] private AudioClip _sceneStartSound;
    [Range(0f, 1f)] [SerializeField] private float _sceneStartSoundVolume = 1f;

    [Header("Enemy And Super Activation")]
    [SerializeField] private GameObject[] _enemyAndSuperActivationEnemies;
    [SerializeField] private AudioClip _enemyAndSuperActivationSound;
    [Range(0f, 1f)] [SerializeField] private float _enemyAndSuperActivationSoundVolume = 1f;
    [SerializeField] private Image _enemyAndSuperActivationTintOverlay;
    [SerializeField] private Color _enemyAndSuperActivationTintColor = new Color(1f, 0f, 0f, 0.35f);
    [SerializeField] private float _enemyAndSuperActivationTintFadeInDuration = 0.08f;
    [SerializeField] private float _enemyAndSuperActivationTintFadeOutDuration = 0.35f;
    [SerializeField] private TMP_Text _enemyAndSuperActivationCompleteText;
    [SerializeField] private float _enemyAndSuperActivationCompleteTextFadeInDuration = 0.5f;

    [Header("Checkpoint Feedback")]
    [SerializeField] private AudioClip[] _checkpointSounds;
    [Range(0f, 1f)] [SerializeField] private float _checkpointSoundVolume = 1f;
    [SerializeField] private Renderer _combatAndHudUnlockGlowRenderer;
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

    [Header("Tutorial Music")]
    [SerializeField] private AudioSource _tutorialMusicSource;
    [SerializeField] private AudioClip _tutorialMusicTrack;
    [Range(0f, 1f)] [SerializeField] private float _tutorialMusicVolume = 1f;
    [SerializeField] private float _tutorialMusicStartDelay = 2f;
    [SerializeField] private float _tutorialMusicFadeInDuration = 2f;
    [FormerlySerializedAs("_normalMusicFadeOutDuration")]
    [SerializeField] private float _tutorialMusicFadeOutDuration = 1f;

    [Header("Fog Audio")]
    [SerializeField] private AudioClip _fogPointLoop;
    [Range(0f, 1f)] [SerializeField] private float _fogPointVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _fogPointSpatialBlend = 1f;
    [SerializeField] private float _fogPointMinDistance = 2f;
    [SerializeField] private float _fogPointMaxDistance = 20f;
    [SerializeField] private AudioClip _fogMusicTrack;
    [Range(0f, 1f)] [SerializeField] private float _fogMusicVolume = 1f;
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
    private AudioSource _sceneStartSource;
    private AudioSource _fogPointSource;
    private AudioSource _fogMusicSource;
    private Coroutine _combatAndHudUnlockGlowRoutine;
    private Coroutine _enemyAndSuperActivationTintRoutine;
    private Coroutine _enemyAndSuperActivationCompleteTextRoutine;
    private Coroutine _tutorialMusicRoutine;
    private Coroutine _fogMusicRoutine;
    private Coroutine _fogMusicStopRoutine;
    private Coroutine _sceneFadeRoutine;
    private Coroutine _fogPointTransitionRoutine;
    private int _currentCheckpointIndex = -1;
    private bool _isCombatAndHudUnlocked;
    private bool _isFogZoneActive;
    private bool _hasReachedFogPoint;
    private bool _hasStartedFogAudio;
    private bool _hasStartedTutorialMusic;
    private bool _hasActivatedEnemyAndSuperTrigger;
    private bool _hasShownEnemyAndSuperActivationCompleteText;
    private bool _isLoadingGameplay;
    private bool _hasCachedMusicMixerVolume;
    private float _cachedMusicMixerVolume = 1f;
    private float _enemyAndSuperActivationCompleteTextTargetAlpha = 1f;
    private bool[] _enemyAndSuperActivationEnemyEliminated;

    private void Awake()
    {
        CaptureOriginalFogSettings();
        ResolveReferences();
        ConfigureRelays();
        CaptureInitialRespawnPoint();
        ApplyInitialGateState();
        ApplyInitialEnemyAndSuperActivationState();
        EnsureTransitionUi();
        PrimeSceneFadeOverlay();

        _checkpointActivated = new bool[_checkpoints != null ? _checkpoints.Length : 0];
        _glowRoutines = new Coroutine[_checkpointActivated.Length];
    }

    private IEnumerator Start()
    {
        yield return null;
        PlaySceneStartSound();
        PlayTutorialMusic(true);
        BeginSceneFadeIn();
    }

    private void OnEnable()
    {
        EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
    }

    private void Update()
    {
        UpdateFogZone();
    }

    private void OnDisable()
    {
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);

        if (!_isLoadingGameplay)
            RestoreOriginalFogSettings();

        StopTutorialMusicImmediate();
        RestoreMusicMixerVolume();
    }

    private void OnDestroy()
    {
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);

        if (!_isLoadingGameplay)
            RestoreOriginalFogSettings();

        StopTutorialMusicImmediate();
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

            case TutorialTriggerType.EnemyAndSuperActivation:
                ActivateEnemiesAndFillSuper();
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
        ConfigureRelay(_enemyAndSuperActivationTrigger, TutorialTriggerType.EnemyAndSuperActivation, -1);
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

    private void ApplyInitialEnemyAndSuperActivationState()
    {
        int enemyCount = _enemyAndSuperActivationEnemies != null ? _enemyAndSuperActivationEnemies.Length : 0;
        _enemyAndSuperActivationEnemyEliminated = new bool[enemyCount];

        if (_enemyAndSuperActivationEnemies == null)
        {
            HideEnemyAndSuperActivationCompleteText();
            return;
        }

        for (int i = 0; i < _enemyAndSuperActivationEnemies.Length; i++)
        {
            if (_enemyAndSuperActivationEnemies[i] != null)
                _enemyAndSuperActivationEnemies[i].SetActive(false);
        }

        HideEnemyAndSuperActivationCompleteText();
    }

    private void HideEnemyAndSuperActivationCompleteText()
    {
        if (_enemyAndSuperActivationCompleteText == null)
            return;

        _enemyAndSuperActivationCompleteTextTargetAlpha = Mathf.Clamp01(_enemyAndSuperActivationCompleteText.color.a);
        if (_enemyAndSuperActivationCompleteTextTargetAlpha <= 0f)
            _enemyAndSuperActivationCompleteTextTargetAlpha = 1f;

        SetEnemyAndSuperActivationCompleteTextAlpha(0f);
        _enemyAndSuperActivationCompleteText.gameObject.SetActive(false);
    }

    private void PlaySceneStartSound()
    {
        if (_sceneStartSound == null)
            return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(_sceneStartSound, _sceneStartSoundVolume);
            return;
        }

        AudioSource source = EnsureSceneStartSource();
        source.PlayOneShot(_sceneStartSound, Mathf.Clamp01(_sceneStartSoundVolume));
    }

    private AudioSource EnsureSceneStartSource()
    {
        if (_sceneStartSource != null)
            return _sceneStartSource;

        GameObject sourceObject = new GameObject("TutorialSceneStartSource");
        sourceObject.transform.SetParent(transform, false);

        _sceneStartSource = sourceObject.AddComponent<AudioSource>();
        _sceneStartSource.playOnAwake = false;
        _sceneStartSource.loop = false;
        _sceneStartSource.spatialBlend = 0f;
        _sceneStartSource.ignoreListenerPause = true;
        return _sceneStartSource;
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

            _glowRoutines[checkpointIndex] = StartCoroutine(PulseGlow(
                glowRenderer,
                () => _glowRoutines[checkpointIndex] = null));
        }

        AudioManager.Instance?.PlaySfx(_checkpointSounds, _checkpointSoundVolume);
    }

    private void PlayCombatAndHudUnlockFeedback()
    {
        Renderer glowRenderer = ResolveGlowRenderer(_combatAndHudUnlockTrigger, _combatAndHudUnlockGlowRenderer);
        if (glowRenderer != null)
        {
            if (_combatAndHudUnlockGlowRoutine != null)
                StopCoroutine(_combatAndHudUnlockGlowRoutine);

            _combatAndHudUnlockGlowRoutine = StartCoroutine(PulseGlow(
                glowRenderer,
                () => _combatAndHudUnlockGlowRoutine = null));
        }

        AudioManager.Instance?.PlaySfx(_checkpointSounds, _checkpointSoundVolume);
    }

    private Renderer ResolveGlowRenderer(CheckpointDefinition checkpoint)
    {
        if (checkpoint == null)
            return null;

        return ResolveGlowRenderer(checkpoint.Trigger, checkpoint.GlowRenderer);
    }

    private Renderer ResolveGlowRenderer(Collider trigger, Renderer explicitRenderer)
    {
        if (explicitRenderer != null)
            return explicitRenderer;

        if (trigger != null && trigger.transform.parent != null)
        {
            Renderer parentRenderer = trigger.transform.parent.GetComponent<Renderer>();
            if (parentRenderer != null)
                return parentRenderer;

            parentRenderer = trigger.transform.parent.GetComponentInChildren<Renderer>();
            if (parentRenderer != null)
                return parentRenderer;
        }

        if (trigger != null)
        {
            Renderer childRenderer = trigger.GetComponentInChildren<Renderer>();
            if (childRenderer != null)
                return childRenderer;
        }

        return trigger != null ? trigger.GetComponent<Renderer>() : null;
    }

    private IEnumerator PulseGlow(Renderer targetRenderer, Action onComplete)
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
        onComplete?.Invoke();
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
        PlayCombatAndHudUnlockFeedback();
    }

    private void ActivateEnemiesAndFillSuper()
    {
        if (_hasActivatedEnemyAndSuperTrigger)
            return;

        _hasActivatedEnemyAndSuperTrigger = true;

        if (_enemyAndSuperActivationEnemies != null)
        {
            for (int i = 0; i < _enemyAndSuperActivationEnemies.Length; i++)
            {
                if (_enemyAndSuperActivationEnemies[i] != null)
                    _enemyAndSuperActivationEnemies[i].SetActive(true);
            }
        }

        _playerCombat?.FillSuperMeter();
        AudioManager.Instance?.PlaySfx(_enemyAndSuperActivationSound, _enemyAndSuperActivationSoundVolume);
        PlayEnemyAndSuperActivationTint();
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (!_hasActivatedEnemyAndSuperTrigger || _hasShownEnemyAndSuperActivationCompleteText)
            return;

        if (_enemyAndSuperActivationEnemies == null || _enemyAndSuperActivationEnemies.Length == 0)
            return;

        if (_enemyAndSuperActivationEnemyEliminated == null ||
            _enemyAndSuperActivationEnemyEliminated.Length != _enemyAndSuperActivationEnemies.Length)
        {
            _enemyAndSuperActivationEnemyEliminated = new bool[_enemyAndSuperActivationEnemies.Length];
        }

        bool matchedEnemy = false;
        for (int i = 0; i < _enemyAndSuperActivationEnemies.Length; i++)
        {
            if (_enemyAndSuperActivationEnemyEliminated[i])
                continue;

            if (!IsTrackedEnemy(e.Enemy, _enemyAndSuperActivationEnemies[i]))
                continue;

            _enemyAndSuperActivationEnemyEliminated[i] = true;
            matchedEnemy = true;
            break;
        }

        if (matchedEnemy && AreEnemyAndSuperActivationEnemiesEliminated())
            ShowEnemyAndSuperActivationCompleteText();
    }

    private bool AreEnemyAndSuperActivationEnemiesEliminated()
    {
        if (_enemyAndSuperActivationEnemies == null || _enemyAndSuperActivationEnemies.Length == 0)
            return false;

        for (int i = 0; i < _enemyAndSuperActivationEnemies.Length; i++)
        {
            if (_enemyAndSuperActivationEnemies[i] == null)
                continue;

            if (_enemyAndSuperActivationEnemyEliminated == null ||
                i >= _enemyAndSuperActivationEnemyEliminated.Length ||
                !_enemyAndSuperActivationEnemyEliminated[i])
            {
                return false;
            }
        }

        return true;
    }

    private bool IsTrackedEnemy(GameObject deadEnemy, GameObject trackedEnemy)
    {
        if (deadEnemy == null || trackedEnemy == null)
            return false;

        if (deadEnemy == trackedEnemy)
            return true;

        Transform deadTransform = deadEnemy.transform;
        Transform trackedTransform = trackedEnemy.transform;
        return deadTransform.IsChildOf(trackedTransform) || trackedTransform.IsChildOf(deadTransform);
    }

    private void ShowEnemyAndSuperActivationCompleteText()
    {
        if (_enemyAndSuperActivationCompleteText == null)
            return;

        _hasShownEnemyAndSuperActivationCompleteText = true;

        if (_enemyAndSuperActivationCompleteTextRoutine != null)
            StopCoroutine(_enemyAndSuperActivationCompleteTextRoutine);

        _enemyAndSuperActivationCompleteTextRoutine = StartCoroutine(EnemyAndSuperActivationCompleteTextFadeInRoutine());
    }

    private IEnumerator EnemyAndSuperActivationCompleteTextFadeInRoutine()
    {
        _enemyAndSuperActivationCompleteText.gameObject.SetActive(true);
        _enemyAndSuperActivationCompleteText.enabled = true;

        float targetAlpha = Mathf.Clamp01(_enemyAndSuperActivationCompleteTextTargetAlpha);
        float duration = Mathf.Max(0f, _enemyAndSuperActivationCompleteTextFadeInDuration);

        if (duration <= 0f)
        {
            SetEnemyAndSuperActivationCompleteTextAlpha(targetAlpha);
            _enemyAndSuperActivationCompleteTextRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetEnemyAndSuperActivationCompleteTextAlpha(Mathf.Lerp(0f, targetAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetEnemyAndSuperActivationCompleteTextAlpha(targetAlpha);
        _enemyAndSuperActivationCompleteTextRoutine = null;
    }

    private void PlayEnemyAndSuperActivationTint()
    {
        EnsureTransitionUi();

        if (_enemyAndSuperActivationTintOverlay == null)
            return;

        if (_enemyAndSuperActivationTintRoutine != null)
            StopCoroutine(_enemyAndSuperActivationTintRoutine);

        _enemyAndSuperActivationTintRoutine = StartCoroutine(EnemyAndSuperActivationTintRoutine());
    }

    private IEnumerator EnemyAndSuperActivationTintRoutine()
    {
        _enemyAndSuperActivationTintOverlay.gameObject.SetActive(true);
        _enemyAndSuperActivationTintOverlay.enabled = true;
        _enemyAndSuperActivationTintOverlay.raycastTarget = false;
        _enemyAndSuperActivationTintOverlay.transform.SetAsLastSibling();

        float peakAlpha = Mathf.Clamp01(_enemyAndSuperActivationTintColor.a);
        yield return StartCoroutine(FadeEnemyAndSuperActivationTintRoutine(0f, peakAlpha, _enemyAndSuperActivationTintFadeInDuration));
        yield return StartCoroutine(FadeEnemyAndSuperActivationTintRoutine(peakAlpha, 0f, _enemyAndSuperActivationTintFadeOutDuration));

        SetEnemyAndSuperActivationTintAlpha(0f);
        _enemyAndSuperActivationTintRoutine = null;
    }

    private IEnumerator FadeEnemyAndSuperActivationTintRoutine(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetEnemyAndSuperActivationTintAlpha(Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / safeDuration)));
            yield return null;
        }

        SetEnemyAndSuperActivationTintAlpha(toAlpha);
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
        BeginTutorialMusicFadeOut();
        BeginFogAudio();
    }

    private void ExitFogZone()
    {
        if (_hasReachedFogPoint)
            return;

        _isFogZoneActive = false;
        RestoreOriginalFogSettings();
        StopFogAudio();
        PlayTutorialMusic(false);
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

    private void PlayTutorialMusic(bool includeStartDelay)
    {
        if (_hasReachedFogPoint)
            return;

        AudioClip clip = _tutorialMusicTrack != null
            ? _tutorialMusicTrack
            : _tutorialMusicSource != null
                ? _tutorialMusicSource.clip
                : null;

        if (clip == null)
            return;

        if (_tutorialMusicRoutine != null)
        {
            StopCoroutine(_tutorialMusicRoutine);
            _tutorialMusicRoutine = null;
        }

        AudioSource source = EnsureTutorialMusicSource();
        if (source == null)
            return;

        _tutorialMusicRoutine = StartCoroutine(TutorialMusicStartRoutine(source, clip, includeStartDelay));
    }

    private IEnumerator TutorialMusicStartRoutine(AudioSource source, AudioClip clip, bool includeStartDelay)
    {
        if (includeStartDelay)
        {
            float delay = Mathf.Max(0f, _tutorialMusicStartDelay);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }

        if (_hasReachedFogPoint || _isFogZoneActive || source == null || clip == null)
        {
            _tutorialMusicRoutine = null;
            yield break;
        }

        MusicManager.Instance?.Stop();

        if (source.isPlaying && source.clip != clip)
            source.Stop();

        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 0f;

        if (!source.isPlaying)
            source.Play();

        _hasStartedTutorialMusic = true;

        float targetVolume = Mathf.Clamp01(_tutorialMusicVolume);
        float fadeDuration = Mathf.Max(0f, _tutorialMusicFadeInDuration);

        if (fadeDuration <= 0f)
        {
            source.volume = targetVolume;
            _tutorialMusicRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            if (_hasReachedFogPoint || _isFogZoneActive || source == null)
            {
                _tutorialMusicRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        source.volume = targetVolume;
        _tutorialMusicRoutine = null;
    }

    private void BeginTutorialMusicFadeOut()
    {
        if (!_hasStartedTutorialMusic || _tutorialMusicSource == null)
            return;

        if (_tutorialMusicRoutine != null)
            StopCoroutine(_tutorialMusicRoutine);

        _tutorialMusicRoutine = StartCoroutine(TutorialMusicFadeOutRoutine());
    }

    private IEnumerator TutorialMusicFadeOutRoutine()
    {
        AudioSource source = _tutorialMusicSource;
        if (source == null)
        {
            _tutorialMusicRoutine = null;
            yield break;
        }

        float startingVolume = source.volume;
        float duration = Mathf.Max(0f, _tutorialMusicFadeOutDuration);

        if (duration <= 0f)
        {
            source.volume = 0f;
            source.Stop();
            _hasStartedTutorialMusic = false;
            _tutorialMusicRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startingVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
        _hasStartedTutorialMusic = false;
        _tutorialMusicRoutine = null;
    }

    private AudioSource EnsureTutorialMusicSource()
    {
        if (_tutorialMusicSource != null)
            return _tutorialMusicSource;

        GameObject sourceObject = new GameObject("TutorialMusicSource");
        sourceObject.transform.SetParent(transform, false);

        _tutorialMusicSource = sourceObject.AddComponent<AudioSource>();
        _tutorialMusicSource.playOnAwake = false;
        _tutorialMusicSource.loop = true;
        _tutorialMusicSource.spatialBlend = 0f;
        _tutorialMusicSource.ignoreListenerPause = true;
        return _tutorialMusicSource;
    }

    private void StopTutorialMusicImmediate()
    {
        if (_tutorialMusicRoutine != null)
        {
            StopCoroutine(_tutorialMusicRoutine);
            _tutorialMusicRoutine = null;
        }

        if (_tutorialMusicSource != null)
        {
            _tutorialMusicSource.volume = 0f;
            _tutorialMusicSource.Stop();
        }

        _hasStartedTutorialMusic = false;
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
            float fadeOutDuration = Mathf.Max(0f, _tutorialMusicFadeOutDuration);
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
        EnsureEnemyAndSuperActivationTintOverlay();
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

    private void EnsureEnemyAndSuperActivationTintOverlay()
    {
        if (_transitionCanvas == null)
            return;

        if (_enemyAndSuperActivationTintOverlay == null)
        {
            GameObject overlayObject = new GameObject("EnemyAndSuperActivationTintOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(_transitionCanvas.transform, false);

            RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            _enemyAndSuperActivationTintOverlay = overlayObject.GetComponent<Image>();
        }

        _enemyAndSuperActivationTintOverlay.gameObject.SetActive(true);
        _enemyAndSuperActivationTintOverlay.enabled = true;
        SetEnemyAndSuperActivationTintAlpha(0f);
        _enemyAndSuperActivationTintOverlay.raycastTarget = false;
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

    private void SetEnemyAndSuperActivationTintAlpha(float alpha)
    {
        if (_enemyAndSuperActivationTintOverlay == null)
            return;

        _enemyAndSuperActivationTintOverlay.color = WithAlpha(_enemyAndSuperActivationTintColor, alpha);
    }

    private void SetEnemyAndSuperActivationCompleteTextAlpha(float alpha)
    {
        if (_enemyAndSuperActivationCompleteText == null)
            return;

        Color color = _enemyAndSuperActivationCompleteText.color;
        color.a = Mathf.Clamp01(alpha);
        _enemyAndSuperActivationCompleteText.color = color;
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
