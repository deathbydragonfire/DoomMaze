using System;
using System.Collections;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
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

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private PlayerMovement _playerMovement;
    private bool[] _checkpointActivated;
    private Coroutine[] _glowRoutines;
    private RespawnPoint _initialRespawnPoint;
    private int _currentCheckpointIndex = -1;
    private bool _isCombatAndHudUnlocked;

    private void Awake()
    {
        ResolveReferences();
        ConfigureRelays();
        CaptureInitialRespawnPoint();
        ApplyInitialGateState();

        _checkpointActivated = new bool[_checkpoints != null ? _checkpoints.Length : 0];
        _glowRoutines = new Coroutine[_checkpointActivated.Length];
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
        }
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
