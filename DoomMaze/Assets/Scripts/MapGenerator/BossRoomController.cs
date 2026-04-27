using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.Video;

public enum BossRoomObjective
{
    EliminateAll
}

/// <summary>
/// Fresh procedural combat-room spawner used by generated maze boss rooms.
/// </summary>
public class BossRoomController : MonoBehaviour
{
    [Header("Objective")]
    [SerializeField] private BossRoomObjective _objective = BossRoomObjective.EliminateAll;

    [Header("Boss Prefabs")]
    [SerializeField] private GameObject _bossPrefab;
    [SerializeField] private GameObject _summonMeleeEnemyPrefab;
    [SerializeField] private GameObject _summonRangedEnemyPrefab;

    [Header("Eliminate All")]
    [SerializeField] private int _eliminateBossCount = 1;

    [Header("Spawn Sampling")]
    [SerializeField] private int _spawnCandidateTarget = 36;
    [SerializeField] private int _spawnSampleAttempts = 240;
    [SerializeField] private float _navMeshSampleRadius = 2f;
    [SerializeField] private float _doorExclusionRadius = 7f;
    [SerializeField] private float _centerExclusionRadius = 5f;
    [SerializeField] private float _spawnPlayableHeightTolerance = 3.5f;

    [Header("Room Activation")]
    [SerializeField] private float _activationInset = 8f;
    [SerializeField] private float _activationDoorClearanceRadius = 10f;
    [SerializeField] private float _activationPlayableHeightTolerance = 5f;
    [SerializeField] private float _entryActivationDepth = 4f;
    [SerializeField] private float _entryActivationDelay = 0.25f;
    [SerializeField] private float _entryOutsideResetDepth = -1f;
    [SerializeField] private float _playerFloorRayHeight = 3f;
    [SerializeField] private float _playerFloorRayDistance = 8f;

    [Header("Doors")]
    [SerializeField] private Door _doorPrefab;
    [SerializeField] private Vector3 _generatedDoorScale = new(8f, 5f, 0.5f);
    [SerializeField] private float _doorVerticalOffset = 2.5f;
    [SerializeField] private float _doorFadeDuration = 0.4f;

    [Header("Room Splash")]
    [SerializeField] private TMP_FontAsset _splashFont;
    [SerializeField] private float _splashDuration = 1.65f;
    [SerializeField] private float _splashMaxFontSize = 150f;
    [SerializeField] private Color _eliminateSplashColor = new(1f, 0.08f, 0.02f, 1f);
    [SerializeField] private Color _proceedSplashColor = new(0.25f, 1f, 0.22f, 1f);
    [SerializeField] private Color _phaseSplashColor = new(1f, 0.75f, 0.08f, 1f);

    [Header("Boss Audio")]
    [SerializeField] private AudioClip _bossTeleportSound;
    [SerializeField] [Range(0f, 1f)] private float _bossTeleportSoundVolume = 1f;
    [SerializeField] private AudioClip _bossSummonSound;
    [SerializeField] [Range(0f, 1f)] private float _bossSummonSoundVolume = 1f;
    [SerializeField] private AudioClip _bossMeleeImpactSound;
    [SerializeField] private AudioClip _bossDashWindupSound;
    [SerializeField] private AudioClip _bossDashMoveSound;
    [SerializeField] private AudioClip _bossSpikeCastSound;
    [SerializeField] private AudioClip _bossSpikeEruptSound;
    [SerializeField] private AudioClip _bossArenaSweepChargeSound;
    [SerializeField] private AudioClip _bossArenaSweepMoveSound;
    [SerializeField] private AudioClip _bossArenaSweepHitSound;
    [SerializeField] private AudioClip _bossSummonSpawnSound;
    [SerializeField] private AudioClip _bossShieldActivateSound;
    [SerializeField] private AudioClip _bossShieldCrystalHitSound;
    [SerializeField] private AudioClip _bossShieldCrystalDestroyedSound;
    [SerializeField] private AudioClip _bossShieldBreakSound;
    [SerializeField] [Range(0f, 1f)] private float _bossAttackSoundVolume = 1f;

    [Header("Boss Health Bar")]
    [SerializeField] private Color _bossHealthFillColor = new(1f, 0.08f, 0.02f, 1f);
    [SerializeField] private Color _bossHealthBackColor = new(0f, 0f, 0f, 0.72f);
    [SerializeField] private Vector2 _bossHealthBarSize = new(760f, 34f);
    [SerializeField] private float _bossHealthBarTopOffset = 42f;

    [Header("Boss Intro Cinematic")]
    [SerializeField] private bool _enableBossIntroCinematic = true;
    [SerializeField] private float _bossIntroMoveInDuration = 1.4f;
    [SerializeField] private float _bossIntroHoldDuration = 1.7f;
    [SerializeField] private float _bossIntroReturnDuration = 1.4f;
    [SerializeField] private float _bossIntroCameraDistance = 15f;
    [SerializeField] private float _bossIntroCameraHeight = 7f;
    [SerializeField] private float _bossIntroCameraFov = 46f;
    [SerializeField] private float _bossIntroFadeDuration = 2.4f;
    [SerializeField] private float _bossIntroShakeMagnitude = 0.06f;
    [SerializeField] private Color _bossIntroTitleColor = new(1f, 0.08f, 0.02f, 1f);
    [SerializeField] private Color _bossIntroFogPulseColor = new(1f, 0.14f, 0.1f, 1f);

    [Header("Boss Intro Cutscene")]
    [SerializeField] private bool _playBossIntroCutscene = true;
    [SerializeField] private VideoClip _bossIntroCutsceneVideo;
    [SerializeField] private AudioClip _bossIntroCutsceneAudio;
    [SerializeField] [Range(0f, 1f)] private float _bossIntroCutsceneAudioVolume = 1f;
    [SerializeField] private float _bossIntroCutsceneFadeToBlackDuration = 0.45f;
    [SerializeField] private float _bossIntroCutsceneFadeFromBlackDuration = 0.45f;
    [SerializeField] private float _bossIntroCutscenePrepareTimeout = 3f;

    [Header("Boss Teleport")]
    [SerializeField] private bool _enableBossTeleport = true;
    [SerializeField] private float _bossTeleportWhenFartherThan = 34f;
    [SerializeField] private Vector2 _bossTeleportNearPlayerDistance = new(5f, 9f);
    [SerializeField] private Vector2 _bossTeleportCooldownRange = new(20f, 30f);
    [SerializeField] [Range(0f, 1f)] private float _bossTeleportChance = 0.25f;
    [SerializeField] private float _bossTeleportFadeDuration = 0.28f;
    [SerializeField] private float _bossTeleportHiddenDuration = 0.18f;
    [SerializeField] private float _bossPostTeleportAttackLock = 2.5f;

    [Header("Boss Summon Attack")]
    [SerializeField] private bool _enableBossSummonAttack = true;
    [SerializeField] private int _bossSummonCount = 5;
    [SerializeField] private int _bossMaxActiveSummons = 7;
    [SerializeField] private Vector2 _bossSummonCooldownRange = new(16f, 24f);
    [SerializeField] [Range(0f, 1f)] private float _bossSummonMeleeWeight = 0.6f;
    [SerializeField] private float _bossSummonFadeDuration = 0.65f;

    [Header("Boss Phases")]
    [SerializeField] private int _bossPhaseCount = 3;
    [SerializeField] private float _bossPhaseTransitionDelay = 1.25f;
    [SerializeField] private int _phaseTwoShieldCrystalCount = 4;
    [SerializeField] private int _phaseTwoShieldCrystalHealth = 150;
    [SerializeField] [Range(0f, 1f)] private float _phaseTwoCrystalBossDamageFraction = 0.15f;
    [SerializeField] private float _phaseTwoCrystalSpawnHeight = 5.5f;
    [SerializeField] private float _phaseTwoCrystalScale = 2.8f;
    [SerializeField] private int _phaseTwoSummonCountBonus = 5;
    [SerializeField] private Vector2 _phaseTwoSummonCooldownRange = new(6f, 10f);
    [SerializeField] private float _phaseTwoSummonMaxRange = 45f;
    [SerializeField] private float _phaseThreeSpeedMultiplier = 1.55f;
    [SerializeField] private Vector2 _phaseThreeTeleportCooldownRange = new(4.5f, 7f);
    [SerializeField] [Range(0f, 1f)] private float _phaseThreeTeleportChance = 0.75f;
    [SerializeField] private int _phaseThreeSummonCount = 4;
    [SerializeField] private Vector2 _phaseThreeSummonCooldownRange = new(6f, 9f);
    [SerializeField] private Vector2 _phaseThreeComboCooldownRange = new(3f, 4.5f);
    [SerializeField] private Vector2 _phaseOneSpikeCooldownRange = new(9f, 13f);
    [SerializeField] private Vector2 _phaseThreeSpikeCooldownRange = new(5f, 8f);
    [SerializeField] private Vector2 _phaseOneBossMeleeCooldownRange = new(6f, 9f);
    [SerializeField] private Vector2 _phaseThreeBossMeleeCooldownRange = new(4f, 6f);
    [SerializeField] private Vector2 _phaseOneDashSlamCooldownRange = new(12f, 16f);
    [SerializeField] private Vector2 _phaseThreeDashSlamCooldownRange = new(6f, 9f);
    [SerializeField] private Vector2 _phaseOneArenaSweepCooldownRange = new(14f, 18f);
    [SerializeField] private Vector2 _phaseTwoArenaSweepCooldownRange = new(11f, 15f);
    [SerializeField] private Vector2 _phaseThreeArenaSweepCooldownRange = new(7f, 10f);
    [SerializeField] private Vector2 _phaseOneGridCooldownRange = new(13f, 17f);
    [SerializeField] private Vector2 _phaseTwoGridCooldownRange = new(10f, 14f);
    [SerializeField] private Vector2 _phaseThreeGridCooldownRange = new(7f, 10f);
    [SerializeField] private Vector2 _phaseOneTrackingSpikeCooldownRange = new(11f, 15f);
    [SerializeField] private Vector2 _phaseTwoTrackingSpikeCooldownRange = new(8f, 12f);
    [SerializeField] private Vector2 _phaseThreeTrackingSpikeCooldownRange = new(6f, 9f);

    [Header("Boss Room Pressure")]
    [SerializeField] [Range(0.05f, 1f)] private float _bossRoomPlayerDecayMultiplier = 0.35f;
    [SerializeField] [Range(0.05f, 1f)] private float _bossRoomDecayWallSpeedMultiplier = 0.35f;
    [SerializeField] private bool _refillAmmoOnBossFightStart = true;

    [Header("Cleared Fog")]
    [SerializeField] private Color _unclearedFogColor = new(0.7f, 0.03f, 0.02f, 1f);
    [SerializeField] private Color _clearedFogColor = new(0.05f, 0.85f, 0.18f, 1f);
    [SerializeField] private float _clearedFogCrossfadeDuration = 1.5f;

    [Header("Victory Cinematic")]
    [SerializeField] private float _victoryCameraIntroDuration = 1.1f;
    [SerializeField] private float _victoryCameraOrbitRadius = 18f;
    [SerializeField] private float _victoryCameraOrbitHeight = 8f;
    [SerializeField] private float _victoryCameraOrbitDegreesPerSecond = 16f;
    [SerializeField] private float _victoryAnimationFallbackDuration = 7f;
    [SerializeField] private float _victoryMinimumCinematicDuration = 2.75f;

    private readonly List<Vector3> _spawnCandidates = new();
    private readonly List<Vector3> _doorPositions = new();
    private readonly List<float> _playableLocalHeights = new();
    private readonly List<Collider> _roomColliders = new();
    private readonly List<RuntimeDoor> _doors = new();
    private readonly List<Vector3> _recentSpawnPositions = new();
    private readonly List<GameObject> _activeSummons = new();
    private readonly List<BossShieldCrystal> _shieldCrystals = new();
    private readonly List<GameObject> _activePhaseHazards = new();
    private readonly HashSet<GameObject> _trackedEnemies = new();

    private static BossRoomController _activeRoom;
    private static BossRoomController _clearedFogOwner;
    private static MonoBehaviour _fogRoutineHost;
    private static Coroutine _fogCrossfadeRoutine;
    private static Color _currentFogTarget;
    private static bool _hasFogTarget;

    private const string BossMeleeImpactSoundPath = "Assets/Audio/Enemies/Boss/ESM_Asylum_Time_Strike_Impact_Tone_1_Hit_Scary_Horror_Creepy.wav";
    private const string BossIntroCutsceneAssetPath = "Assets/Animations/Cutscenes/IntroSceneBoss.mp4";
    private const string BossIntroCutsceneAudioPath = "Assets/Audio/Music/pre-fight video.wav";
    private const string BossDashWindupSoundPath = "Assets/Audio/Enemies/Boss/RKU_SBS_140_fx_loop_short_reverse.wav";
    private const string BossDashMoveSoundPath = "Assets/Audio/Enemies/Boss/ESM_AG_Cinematic_FX_movement_one_shot_fast_dash_leaves_shuffling_07.wav";
    private const string BossSpikeCastSoundPath = "Assets/Audio/Enemies/Boss/ESM_Hybrid_Appear_Hybrid_Mobile_Collect_Special_Power_Up_Buff.wav";
    private const string BossSpikeEruptSoundPath = "Assets/Audio/Enemies/Boss/ESM_Mobile_Game_One_Shot_Action_Spike_Trap_Trigger_2.wav";
    private const string BossArenaSweepChargeSoundPath = "Assets/Audio/Enemies/Boss/ESM_Cinematic_Power_Charge_2_Impact_Hybrid_Tech_.wav";
    private const string BossArenaSweepMoveSoundPath = "Assets/Audio/Enemies/Boss/ESM_FG2_FX_magic_one_shot_water_ESM_FG2_FX_magic_one_shot_blood_spell_impact_charge_fast_cast_blast_4.wav";
    private const string BossSummonSpawnSoundPath = "Assets/Audio/Enemies/Boss/ESM_Hybrid_Game_Transition_Window_Appear_Swoosh_Pass_By_Fly_Futuristic_Robotic_Technology_Hi_Tech_Game_Tone_Science_UFO_Space_Processed_Glitch_Hybrid_Sound_Air.wav";
    private const string BossShieldActivateSoundPath = "Assets/Audio/Enemies/Boss/ESM_Mobile_Game_One_Shot_Special_Tech_Shields_Up_1.wav";
    private const string BossShieldCrystalHitSoundPath = "Assets/Audio/Enemies/Boss/ESM_FG2_FX_combat_one_shot_shield_thunk_wood_bump_01.wav";
    private const string BossShieldCrystalDestroyedSoundPath = "Assets/Audio/Enemies/Boss/ESM_Airy_Force_Field_Shield_Power_Up_3.wav";
    private const string BossShieldBreakSoundPath = "Assets/Audio/Enemies/Boss/ESM_MVG_fx_effect_one_shot_energy_shield_break_1_drop_whoosh_sweep_slow.wav";

    private MazePrefab _mazePrefab;
    private Bounds _localBounds;
    private Coroutine _combatRoutine;
    private Coroutine _splashRoutine;
    private Coroutine _victoryRoutine;
    private Transform _playerTransform;
    private GameObject _splashCanvasObject;
    private GameObject _bossHealthCanvasObject;
    private Image _bossHealthFillImage;
    private RectTransform _bossHealthFillRect;
    private TextMeshProUGUI _bossHealthLabel;
    private HealthComponent _bossHealth;
    private GameObject _timerCanvasObject;
    private RectTransform _timerRect;
    private TextMeshProUGUI _timerLabel;
    private float _timerPulseRemaining;
    private bool _configured;
    private bool _started;
    private bool _completed;
    private bool _isPlayerInClearedRoom;
    private bool _armed;
    private bool _wasOutsideEntry = true;
    private bool _loggedMissingSpawnPoint;
    private bool _useTestingSpawnFallback;
    private bool _victoryDeathAnimationComplete;
    private bool _isBossPhaseTransitioning;
    private GameObject _victoryTargetBoss;
    private GameObject _currentBoss;
    private BossShieldVisual _currentBossShieldVisual;
    private float _entryActivationStartedAt = -1f;
    private float _minimumSpawnSpacing = 5f;
    private int _spawnedCount;
    private int _bossPhaseIndex = 1;
    private Coroutine _phaseTransitionRoutine;

    public BossRoomObjective Objective => _objective;
    public static bool IsBossFightActive => _activeRoom != null && _activeRoom._started && !_activeRoom._completed;
    public static float ActiveBossRoomPlayerDecayMultiplier => IsBossFightActive ? Mathf.Clamp(_activeRoom._bossRoomPlayerDecayMultiplier, 0.05f, 1f) : 1f;
    public static float ActiveBossRoomDecayWallSpeedMultiplier => IsBossFightActive ? Mathf.Clamp(_activeRoom._bossRoomDecayWallSpeedMultiplier, 0.05f, 1f) : 1f;

    private sealed class BehaviourEnabledState
    {
        public Behaviour Behaviour;
        public bool WasEnabled;
    }

    private sealed class RuntimeDoor
    {
        public Door Door;
        public Renderer[] Renderers;
        public Collider[] Colliders;
        public Coroutine FadeRoutine;
    }

    private sealed class ComponentEnabledState
    {
        public Behaviour Behaviour;
        public bool WasEnabled;
    }

    private sealed class ColliderEnabledState
    {
        public Collider Collider;
        public bool WasEnabled;
    }

    private sealed class BossIntroRuntimeState
    {
        public ComponentEnabledState[] Components;
        public ColliderEnabledState[] Colliders;
        public NavMeshAgent Agent;
        public Vector3 Position;
    }

    private struct MaterialColorState
    {
        public Material Material;
        public Color BaseColor;
        public bool HasBaseColor;
        public Color Color;
        public bool HasColor;
    }

    private void OnEnable()
    {
        EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
        EventBus<EnemyDeathAnimationCompletedEvent>.Subscribe(OnEnemyDeathAnimationCompleted);
    }

    private void OnDisable()
    {
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
        EventBus<EnemyDeathAnimationCompletedEvent>.Unsubscribe(OnEnemyDeathAnimationCompleted);

        if (_activeRoom == this)
            _activeRoom = null;

        if (_clearedFogOwner == this)
        {
            _clearedFogOwner = null;
            _isPlayerInClearedRoom = false;
            RenderSettings.fog = true;
            RenderSettings.fogColor = _unclearedFogColor;
            _currentFogTarget = _unclearedFogColor;
            _hasFogTarget = true;
        }

        if (_fogRoutineHost == this && _fogCrossfadeRoutine != null)
        {
            StopCoroutine(_fogCrossfadeRoutine);
            _fogCrossfadeRoutine = null;
            _fogRoutineHost = null;
        }

        if (_victoryRoutine != null)
        {
            StopCoroutine(_victoryRoutine);
            _victoryRoutine = null;
        }

        if (_phaseTransitionRoutine != null)
        {
            StopCoroutine(_phaseTransitionRoutine);
            _phaseTransitionRoutine = null;
        }

        CleanupBossPhaseRuntime(destroyCurrentBoss: false);
        StopBossHealthBar();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_bossIntroCutsceneVideo == null)
            _bossIntroCutsceneVideo = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(BossIntroCutsceneAssetPath);
        if (_bossIntroCutsceneAudio == null)
            _bossIntroCutsceneAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(BossIntroCutsceneAudioPath);
    }
#endif

    public void Configure(
        MazePrefab mazePrefab,
        BossRoomObjective objective,
        GameObject bossPrefab,
        GameObject summonMeleeEnemyPrefab,
        GameObject summonRangedEnemyPrefab,
        Door doorPrefab,
        Vector3 generatedDoorScale,
        float doorVerticalOffset,
        TMP_FontAsset splashFont,
        AudioClip bossTeleportSound,
        AudioClip bossSummonSound,
        int eliminateBossCount,
        int bossPhaseCount = 3,
        VideoClip bossIntroCutsceneVideo = null)
    {
        _mazePrefab = mazePrefab;
        _objective = objective;
        _bossPrefab = bossPrefab;
        _summonMeleeEnemyPrefab = summonMeleeEnemyPrefab;
        _summonRangedEnemyPrefab = summonRangedEnemyPrefab;
        _doorPrefab = doorPrefab;
        _generatedDoorScale = generatedDoorScale;
        _doorVerticalOffset = doorVerticalOffset;
        _splashFont = splashFont;
        _bossTeleportSound = bossTeleportSound;
        _bossSummonSound = bossSummonSound;
        _eliminateBossCount = Mathf.Max(0, eliminateBossCount);
        _bossPhaseCount = Mathf.Max(1, bossPhaseCount);
        if (bossIntroCutsceneVideo != null)
            _bossIntroCutsceneVideo = bossIntroCutsceneVideo;

        _configured = true;

        CacheLocalBounds();
        CacheDoorPositions();
        CachePlayableLocalHeights();
        CacheRoomColliders();
        GenerateSpawnCandidates();
    }

    public void ArmFromPlayerStart(Vector3 playerPosition)
    {
        float entryDepth = GetEntryInsideDepth(playerPosition);
        _wasOutsideEntry = entryDepth <= _entryOutsideResetDepth;
        _armed = _wasOutsideEntry;
        _entryActivationStartedAt = -1f;
    }

    public void BeginCombatForTesting()
    {
        if (!_configured)
            return;

        _armed = true;
        _wasOutsideEntry = true;
        _useTestingSpawnFallback = true;
        _entryActivationStartedAt = -1f;
        StartCombat();
    }

    private void Update()
    {
        if (!_configured)
            return;

        if (_completed)
        {
            UpdateClearedRoomFogPresence();
            return;
        }

        if (_started)
            return;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        if (_playerTransform != null)
            UpdateEntryPlaneActivation(_playerTransform.position);
    }

    private void UpdateEntryPlaneActivation(Vector3 playerPosition)
    {
        float entryDepth = GetEntryInsideDepth(playerPosition);
        if (entryDepth <= _entryOutsideResetDepth)
        {
            _armed = true;
            _wasOutsideEntry = true;
            _entryActivationStartedAt = -1f;
            return;
        }

        if (!_armed || !_wasOutsideEntry)
            return;

        bool crossedIntoRoom = entryDepth >= _entryActivationDepth;
        if (!crossedIntoRoom ||
            !IsInsideRoomActivationZone(playerPosition) ||
            !IsPlayerStandingOnThisRoom(playerPosition))
        {
            _entryActivationStartedAt = -1f;
            return;
        }

        if (_entryActivationStartedAt < 0f)
        {
            _entryActivationStartedAt = Time.time;
            return;
        }

        if (Time.time - _entryActivationStartedAt >= _entryActivationDelay)
            StartCombat();
    }

    private float GetEntryInsideDepth(Vector3 worldPosition)
    {
        if (_mazePrefab == null || !_mazePrefab.HasEntrySocket)
            return float.NegativeInfinity;

        MazeSocket entrySocket = _mazePrefab.EntrySocket;
        Vector3 entryPosition = transform.TransformPoint(entrySocket.Position);
        Vector3 entryForward = transform.TransformDirection(entrySocket.Forward).normalized;
        if (entryForward.sqrMagnitude <= 0.001f)
            entryForward = transform.forward;

        Vector3 inward = -entryForward;
        return Vector3.Dot(worldPosition - entryPosition, inward);
    }

    private bool IsInsideRoomActivationZone(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        float inset = Mathf.Max(0f, _activationInset);

        float minX = _localBounds.min.x + inset;
        float maxX = _localBounds.max.x - inset;
        float minZ = _localBounds.min.z + inset;
        float maxZ = _localBounds.max.z - inset;

        if (minX > maxX)
        {
            minX = _localBounds.center.x - 0.5f;
            maxX = _localBounds.center.x + 0.5f;
        }

        if (minZ > maxZ)
        {
            minZ = _localBounds.center.z - 0.5f;
            maxZ = _localBounds.center.z + 0.5f;
        }

        if (localPosition.x < minX ||
            localPosition.x > maxX ||
            localPosition.z < minZ ||
            localPosition.z > maxZ)
            return false;

        return IsOnPlayableHeight(worldPosition, _activationPlayableHeightTolerance) &&
               !IsNearDoorSocket(worldPosition, _activationDoorClearanceRadius);
    }

    private bool IsPlayerStandingOnThisRoom(Vector3 playerPosition)
    {
        if (_roomColliders.Count == 0)
            return true;

        Vector3 rayOrigin = playerPosition + Vector3.up * Mathf.Max(0.25f, _playerFloorRayHeight);
        Ray ray = new(rayOrigin, Vector3.down);
        float maxDistance = Mathf.Max(0.5f, _playerFloorRayDistance);

        for (int i = 0; i < _roomColliders.Count; i++)
        {
            Collider collider = _roomColliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (!collider.Raycast(ray, out RaycastHit hit, maxDistance))
                continue;

            if (!IsWithinLocalBounds(hit.point))
                continue;

            if (!IsOnPlayableHeight(hit.point, _activationPlayableHeightTolerance))
                continue;

            return true;
        }

        return false;
    }

    private bool IsNearDoorSocket(Vector3 worldPosition, float radius)
    {
        float sqrRadius = radius * radius;
        foreach (Vector3 doorPosition in _doorPositions)
        {
            Vector3 offset = Vector3.ProjectOnPlane(worldPosition - doorPosition, Vector3.up);
            if (offset.sqrMagnitude < sqrRadius)
                return true;
        }

        return false;
    }

    private void StartCombat()
    {
        if (_started || _completed)
            return;

        if (_activeRoom != null && _activeRoom != this && !_activeRoom._completed)
            return;

        _activeRoom = this;
        _started = true;
        _bossPhaseIndex = 1;
        _spawnedCount = 0;
        _currentBoss = null;
        _isBossPhaseTransitioning = false;

        RefillPlayerAmmoForBossFight();
        ShowPhaseSplash(_bossPhaseIndex);
        ShowDoors();

        _combatRoutine = StartCoroutine(EliminateAllRoutine());
    }

    private void RefillPlayerAmmoForBossFight()
    {
        if (!_refillAmmoOnBossFightStart)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        PlayerInventory inventory = player.GetComponentInParent<PlayerInventory>();
        WeaponBase[] weapons = player.GetComponentsInChildren<WeaponBase>(true);

        if (inventory != null)
        {
            FillBossAmmoType(inventory, "pistol_rounds", 200);
            FillBossAmmoType(inventory, "shells", 50);
            FillBossAmmoType(inventory, "rockets", 20);
            FillBossAmmoType(inventory, "mg_rounds", 300);
            FillBossAmmoType(inventory, "flame_fuel", 200);

            HashSet<string> weaponAmmoTypes = new();
            for (int i = 0; i < weapons.Length; i++)
            {
                string ammoType = weapons[i] != null && weapons[i].Data != null ? weapons[i].Data.AmmoTypeId : null;
                if (string.IsNullOrEmpty(ammoType) || !weaponAmmoTypes.Add(ammoType))
                    continue;

                int maxCarry = GetBossAmmoCarryCap(ammoType);
                FillBossAmmoType(inventory, ammoType, maxCarry);
            }
        }

        for (int i = 0; i < weapons.Length; i++)
            weapons[i]?.RefillMagazine();
    }

    private static void FillBossAmmoType(PlayerInventory inventory, string ammoType, int maxCarry)
    {
        if (inventory == null || string.IsNullOrEmpty(ammoType) || maxCarry <= 0)
            return;

        inventory.AddAmmo(ammoType, maxCarry, maxCarry);
    }

    private static int GetBossAmmoCarryCap(string ammoType)
    {
        return ammoType switch
        {
            "pistol_rounds" => 200,
            "shells" => 50,
            "rockets" => 20,
            "mg_rounds" => 300,
            "flame_fuel" => 200,
            _ => 999
        };
    }

    private IEnumerator EliminateAllRoutine()
    {
        if (_bossPhaseCount <= 0)
        {
            CompleteRoom();
            yield break;
        }

        bool spawnedBoss = SpawnBoss(out GameObject boss, showHealthBar: !_enableBossIntroCinematic);
        if (spawnedBoss && boss != null && _enableBossIntroCinematic)
            yield return StartCoroutine(BossIntroCinematicRoutine(boss));

        TryCompleteEliminateAll();
    }

    private bool SpawnBoss()
    {
        return SpawnBoss(out _, showHealthBar: true);
    }

    private bool SpawnBoss(out GameObject boss, bool showHealthBar)
    {
        boss = null;

        if (_activeRoom != this || !_started || _completed)
            return false;

        GameObject prefab = _bossPrefab;
        if (prefab == null)
            return false;

        if (!TryPickBossSpawnPosition(out Vector3 position) &&
            (!_useTestingSpawnFallback || !TryPickTestingSpawnPosition(out position)))
        {
            if (!_loggedMissingSpawnPoint)
            {
                Debug.LogWarning($"[BossRoomController] Could not find a NavMesh spawn point for {gameObject.name}. Skipping boss spawns until a valid point exists.", this);
                _loggedMissingSpawnPoint = true;
            }

            if (_useTestingSpawnFallback)
                return false;

            _spawnedCount = Mathf.Max(_spawnedCount + 1, _bossPhaseCount);
            TryCompleteEliminateAll();
            return false;
        }

        Quaternion rotation = Quaternion.LookRotation(GetDirectionToRoomCenter(position), Vector3.up);
        boss = Instantiate(prefab, position, rotation, transform);

        NavMeshAgent agent = boss.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Warp(position);
        }
        else if (_useTestingSpawnFallback && agent != null && agent.enabled)
        {
            agent.enabled = false;
            Debug.LogWarning($"[BossRoomController] Spawned test boss for {gameObject.name} without NavMesh support. The boss may be stationary, but can still be killed for victory-screen testing.", boss);
        }

        _trackedEnemies.Add(boss);
        _currentBoss = boss;
        EnemyBase bossEnemyBase = boss.GetComponent<EnemyBase>();
        if (bossEnemyBase != null)
            bossEnemyBase.SetUseBossSfxVolume(true);

        if (showHealthBar)
            ShowBossHealthBar(boss);

        ConfigureBossModelHitFlash(boss);
        ConfigureBossRangedProjectiles(boss);
        ConfigureBossAttackVisualPolish(boss, _bossPhaseIndex);
        ConfigureBossTeleport(boss, _bossPhaseIndex);
        ConfigureBossSummonAttack(boss, _bossPhaseIndex);
        ConfigureBossPhase(boss, _bossPhaseIndex);
        RememberSpawnPosition(position);
        _spawnedCount++;
        return true;
    }

    private void ConfigureBossRangedProjectiles(GameObject boss)
    {
        if (boss == null)
            return;

        RangedAttackModule[] rangedModules = boss.GetComponents<RangedAttackModule>();
        for (int i = 0; i < rangedModules.Length; i++)
        {
            if (rangedModules[i] != null)
                rangedModules[i].ConfigureProjectileReach(maxAttackRange: 140f, projectileMaxDistance: 220f, projectileRadius: 0.85f, projectileSpeed: 42f);
        }
    }

    private void ConfigureBossAttackVisualPolish(GameObject boss, int phaseIndex)
    {
        if (boss == null)
            return;

        float intensity = phaseIndex >= 3 ? 1.45f : phaseIndex == 2 ? 1.12f : 1f;
        Color rangedColor = phaseIndex >= 3
            ? new Color(0.35f, 0.98f, 1f, 1f)
            : new Color(0.24f, 0.9f, 1f, 1f);
        Color rangedEmission = phaseIndex >= 3
            ? new Color(0.9f, 5.2f, 6f, 1f)
            : new Color(0.65f, 3.9f, 4.8f, 1f);
        Color rangedPulse = phaseIndex >= 3
            ? new Color(0.28f, 0.95f, 1f, 0.56f)
            : new Color(0.18f, 0.85f, 1f, 0.42f);

        RangedAttackModule[] rangedModules = boss.GetComponents<RangedAttackModule>();
        for (int i = 0; i < rangedModules.Length; i++)
        {
            if (rangedModules[i] != null)
                rangedModules[i].ConfigureBossVisualPolish(rangedColor, rangedEmission, rangedPulse, intensity);
        }

        Color aoeColor = phaseIndex >= 3
            ? new Color(1f, 0.34f, 0.04f, 0.86f)
            : new Color(1f, 0.58f, 0.08f, 0.74f);
        Color aoeEmission = phaseIndex >= 3
            ? new Color(5.2f, 1.45f, 0.25f, 0.86f)
            : new Color(4f, 2.25f, 0.35f, 0.74f);
        Color aoePulse = phaseIndex >= 3
            ? new Color(1f, 0.18f, 0.02f, 0.58f)
            : new Color(1f, 0.38f, 0.03f, 0.48f);

        AreaOfEffectAttackModule[] areaModules = boss.GetComponents<AreaOfEffectAttackModule>();
        for (int i = 0; i < areaModules.Length; i++)
        {
            if (areaModules[i] != null)
                areaModules[i].ConfigureBossVisualPolish(aoeColor, aoeEmission, aoePulse, intensity);
        }
    }

    private bool TryPickBossSpawnPosition(out Vector3 position)
    {
        Vector3 center = GetBossRoomWorldCenter();
        float searchRadius = Mathf.Max(2f, _navMeshSampleRadius * 2f);

        if (_playableLocalHeights.Count > 0)
        {
            for (int i = 0; i < _playableLocalHeights.Count; i++)
            {
                Vector3 localCenter = transform.InverseTransformPoint(center);
                localCenter.y = _playableLocalHeights[i];
                Vector3 worldCenter = transform.TransformPoint(localCenter);

                if (TryFindRoomColliderFloor(worldCenter, out Vector3 floorPoint))
                    worldCenter = floorPoint;

                if (NavMesh.SamplePosition(worldCenter, out NavMeshHit hit, searchRadius, NavMesh.AllAreas) &&
                    IsWithinLocalBounds(hit.position))
                {
                    position = hit.position;
                    return true;
                }

                if (IsWithinLocalBounds(worldCenter))
                {
                    position = worldCenter;
                    return true;
                }
            }
        }

        if (TryFindRoomColliderFloor(center, out Vector3 centerFloorPoint))
            center = centerFloorPoint;

        if (NavMesh.SamplePosition(center, out NavMeshHit centerHit, searchRadius, NavMesh.AllAreas) &&
            IsWithinLocalBounds(centerHit.position))
        {
            position = centerHit.position;
            return true;
        }

        if (IsWithinLocalBounds(center))
        {
            position = center;
            return true;
        }

        position = default;
        return false;
    }

    private Vector3 GetBossRoomWorldCenter()
    {
        if (TryGetRoomColliderWorldBounds(out Bounds colliderBounds))
            return colliderBounds.center;

        if (_spawnCandidates.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _spawnCandidates.Count; i++)
                sum += _spawnCandidates[i];

            return sum / _spawnCandidates.Count;
        }

        return transform.TransformPoint(_localBounds.center);
    }

    private bool TryGetRoomColliderWorldBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < _roomColliders.Count; i++)
        {
            Collider collider = _roomColliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private IEnumerator BossIntroCinematicRoutine(GameObject boss)
    {
        if (boss == null)
            yield break;

        BossIntroRuntimeState bossState = PrepareBossForIntro(boss);
        MaterialColorState[] materialStates = CacheMaterialColorStates(boss.GetComponentsInChildren<Renderer>(true));
        ApplyMaterialColor(materialStates, Color.white);

        StopRoomSplash();
        StopBossHealthBar();

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Cinematic)
            GameManager.Instance.SetState(GameState.Cinematic);

        yield return StartCoroutine(BossIntroCutsceneRoutine());

        AudioManager.Instance?.PlayBossSfx(_bossSummonSound, _bossSummonSoundVolume);

        Camera cinematicCamera = Camera.main;
        Coroutine fadeRoutine = StartCoroutine(BossIntroFadeRoutine(materialStates));
        Coroutine fogRoutine = StartCoroutine(BossIntroFogPulseRoutine());
        Coroutine ringRoutine = StartCoroutine(BossIntroGroundRingRoutine(boss));
        Coroutine titleRoutine = StartCoroutine(BossIntroTitleRoutine(GetBossIntroTitle(boss)));

        if (cinematicCamera != null)
            yield return StartCoroutine(BossIntroCameraRoutine(cinematicCamera, boss));
        else
            yield return new WaitForSeconds(GetBossIntroTotalDuration());

        if (fadeRoutine != null)
            yield return fadeRoutine;

        RestoreMaterialColor(materialStates);

        if (ringRoutine != null)
            yield return ringRoutine;
        if (titleRoutine != null)
            yield return titleRoutine;
        if (fogRoutine != null)
            yield return fogRoutine;

        if (boss != null && !_completed)
        {
            ReleaseBossAfterIntro(bossState);
            ShowBossHealthBar(boss);
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Cinematic)
            GameManager.Instance.SetState(GameState.Playing);
    }

    private IEnumerator BossIntroCutsceneRoutine()
    {
        if (!_playBossIntroCutscene)
            yield break;

        bool hasVideoClip = _bossIntroCutsceneVideo != null;
        string fallbackUrl = GetBossIntroCutsceneFallbackUrl();
        bool hasFallbackUrl = !string.IsNullOrWhiteSpace(fallbackUrl);
        if (!hasVideoClip && !hasFallbackUrl)
            yield break;

        GameObject canvasObject = new("BossIntroCutsceneCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject videoObject = new("BossIntroCutsceneVideo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        videoObject.transform.SetParent(canvasObject.transform, false);
        RectTransform videoRect = videoObject.GetComponent<RectTransform>();
        StretchToParent(videoRect);
        RawImage videoImage = videoObject.GetComponent<RawImage>();
        videoImage.color = Color.white;
        videoImage.enabled = false;

        GameObject blackObject = new("BossIntroCutsceneBlack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        blackObject.transform.SetParent(canvasObject.transform, false);
        RectTransform blackRect = blackObject.GetComponent<RectTransform>();
        StretchToParent(blackRect);
        Image blackImage = blackObject.GetComponent<Image>();
        blackImage.color = Color.clear;

        GameObject playerObject = new("BossIntroCutscenePlayer", typeof(VideoPlayer));
        playerObject.transform.SetParent(canvasObject.transform, false);
        VideoPlayer videoPlayer = playerObject.GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.waitForFirstFrame = true;

        if (hasVideoClip)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = _bossIntroCutsceneVideo;
        }
        else
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = fallbackUrl;
        }

        bool finished = false;
        bool failed = false;
        videoPlayer.loopPointReached += OnBossIntroVideoFinished;
        videoPlayer.errorReceived += OnBossIntroVideoError;

        yield return StartCoroutine(FadeGraphicAlpha(blackImage, 0f, 1f, _bossIntroCutsceneFadeToBlackDuration));

        videoPlayer.Prepare();
        float prepareElapsed = 0f;
        float prepareTimeout = Mathf.Max(0.1f, _bossIntroCutscenePrepareTimeout);
        while (!videoPlayer.isPrepared && !failed && prepareElapsed < prepareTimeout)
        {
            prepareElapsed += Time.deltaTime;
            yield return null;
        }

        if (videoPlayer.isPrepared && !failed)
        {
            AudioClip cutsceneAudio = ResolveBossAudioClip(ref _bossIntroCutsceneAudio, BossIntroCutsceneAudioPath);
            bool startedCutsceneAudio = cutsceneAudio != null && MusicManager.Instance != null;
            if (startedCutsceneAudio)
            {
                MusicManager.Instance.PlayTemporaryClip(
                    cutsceneAudio,
                    _bossIntroCutsceneAudioVolume,
                    _bossIntroCutsceneFadeToBlackDuration,
                    _bossIntroCutsceneFadeFromBlackDuration,
                    loop: false);
            }

            videoPlayer.Play();

            if (videoPlayer.texture == null)
                yield return null;

            videoImage.texture = videoPlayer.texture;
            videoImage.enabled = videoImage.texture != null;
            yield return StartCoroutine(FadeGraphicAlpha(blackImage, 1f, 0f, _bossIntroCutsceneFadeFromBlackDuration));

            float playElapsed = 0f;
            float maxPlayDuration = videoPlayer.length > 0.1 ? (float)videoPlayer.length + 1f : 30f;
            while (!finished && !failed && playElapsed < maxPlayDuration)
            {
                playElapsed += Time.deltaTime;
                yield return null;
            }

            yield return StartCoroutine(FadeGraphicAlpha(blackImage, blackImage.color.a, 1f, _bossIntroCutsceneFadeToBlackDuration));

            if (startedCutsceneAudio)
                MusicManager.Instance.StopTemporaryClip(_bossIntroCutsceneFadeFromBlackDuration);
        }

        videoPlayer.loopPointReached -= OnBossIntroVideoFinished;
        videoPlayer.errorReceived -= OnBossIntroVideoError;
        videoPlayer.Stop();

        if (canvasObject != null)
        {
            if (blackImage != null)
                yield return StartCoroutine(FadeGraphicAlpha(blackImage, blackImage.color.a, 0f, _bossIntroCutsceneFadeFromBlackDuration));

            Destroy(canvasObject);
        }

        void OnBossIntroVideoFinished(VideoPlayer source)
        {
            finished = true;
        }

        void OnBossIntroVideoError(VideoPlayer source, string message)
        {
            failed = true;
            Debug.LogWarning($"[BossRoomController] Boss intro cutscene failed to play: {message}", this);
        }
    }

    private static string GetBossIntroCutsceneFallbackUrl()
    {
        string path = $"{Application.dataPath}/Animations/Cutscenes/IntroSceneBoss.mp4";
        return System.IO.File.Exists(path) ? path : string.Empty;
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static IEnumerator FadeGraphicAlpha(Graphic graphic, float from, float to, float duration)
    {
        if (graphic == null)
            yield break;

        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetGraphicAlpha(graphic, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetGraphicAlpha(graphic, to);
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = Mathf.Clamp01(alpha);
        graphic.color = color;
    }

    private BossIntroRuntimeState PrepareBossForIntro(GameObject boss)
    {
        BossIntroRuntimeState state = new()
        {
            Position = boss != null ? boss.transform.position : Vector3.zero,
            Agent = boss != null ? boss.GetComponent<NavMeshAgent>() : null
        };

        List<ComponentEnabledState> componentStates = new();
        AddIntroComponentState(componentStates, state.Agent);
        AddIntroComponentState(componentStates, boss != null ? boss.GetComponent<EnemyBase>() : null);

        if (boss != null)
        {
            IAttackModule[] attackModules = boss.GetComponents<IAttackModule>();
            for (int i = 0; i < attackModules.Length; i++)
            {
                if (attackModules[i] is Behaviour behaviour)
                    AddIntroComponentState(componentStates, behaviour);
            }

            AddIntroComponentState(componentStates, boss.GetComponent<BossTeleportAbility>());
            AddIntroComponentState(componentStates, boss.GetComponent<BossSummonAttackModule>());
        }

        state.Components = componentStates.ToArray();
        for (int i = 0; i < state.Components.Length; i++)
        {
            if (state.Components[i].Behaviour != null)
                state.Components[i].Behaviour.enabled = false;
        }

        Collider[] colliders = boss != null ? boss.GetComponentsInChildren<Collider>(true) : System.Array.Empty<Collider>();
        List<ColliderEnabledState> colliderStates = new();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            colliderStates.Add(new ColliderEnabledState
            {
                Collider = collider,
                WasEnabled = collider.enabled
            });

            collider.enabled = false;
        }

        state.Colliders = colliderStates.ToArray();
        return state;
    }

    private static void AddIntroComponentState(List<ComponentEnabledState> states, Behaviour behaviour)
    {
        if (states == null || behaviour == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Behaviour == behaviour)
                return;
        }

        states.Add(new ComponentEnabledState
        {
            Behaviour = behaviour,
            WasEnabled = behaviour.enabled
        });
    }

    private void ReleaseBossAfterIntro(BossIntroRuntimeState state)
    {
        if (state == null)
            return;

        if (state.Colliders != null)
        {
            for (int i = 0; i < state.Colliders.Length; i++)
            {
                ColliderEnabledState colliderState = state.Colliders[i];
                if (colliderState?.Collider != null)
                    colliderState.Collider.enabled = colliderState.WasEnabled;
            }
        }

        if (state.Components != null)
        {
            for (int i = 0; i < state.Components.Length; i++)
            {
                ComponentEnabledState componentState = state.Components[i];
                if (componentState?.Behaviour != null)
                    componentState.Behaviour.enabled = componentState.WasEnabled;
            }
        }

        if (state.Agent != null && state.Agent.enabled && state.Agent.isOnNavMesh)
            state.Agent.Warp(state.Position);
    }

    private IEnumerator BossIntroFadeRoutine(MaterialColorState[] materialStates)
    {
        float duration = Mathf.Max(0.01f, _bossIntroFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            LerpMaterialColor(materialStates, Color.white, t);
            yield return null;
        }
    }

    private IEnumerator BossIntroCameraRoutine(Camera cinematicCamera, GameObject boss)
    {
        Transform cameraTransform = cinematicCamera.transform;
        Transform startParent = cameraTransform.parent;
        int startSiblingIndex = startParent != null ? cameraTransform.GetSiblingIndex() : -1;
        Vector3 startWorldPosition = cameraTransform.position;
        Quaternion startWorldRotation = cameraTransform.rotation;
        Vector3 startLocalPosition = cameraTransform.localPosition;
        Quaternion startLocalRotation = cameraTransform.localRotation;
        float startFov = cinematicCamera.fieldOfView;

        List<BehaviourEnabledState> disabledBehaviours = DisableVictoryCameraBehaviours(cinematicCamera);
        DisableViewmodelCameras(cinematicCamera, disabledBehaviours);

        Bounds bossBounds = GetWorldBounds(boss);
        Vector3 lookPoint = bossBounds.center;
        Vector3 targetPosition = GetBossIntroCameraPosition(startWorldPosition, boss.transform.position, bossBounds);
        Quaternion targetRotation = GetLookRotation(targetPosition, lookPoint, startWorldRotation);
        float targetFov = Mathf.Clamp(_bossIntroCameraFov, 25f, 80f);

        cameraTransform.SetParent(null, true);

        try
        {
            float elapsed = 0f;
            float moveInDuration = Mathf.Max(0.01f, _bossIntroMoveInDuration);
            while (elapsed < moveInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveInDuration));
                Vector3 basePosition = Vector3.Lerp(startWorldPosition, targetPosition, t);
                cameraTransform.position = basePosition + GetBossIntroShakeOffset(1f - t);
                cameraTransform.rotation = Quaternion.Slerp(startWorldRotation, targetRotation, t);
                cinematicCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
                yield return null;
            }

            elapsed = 0f;
            float holdDuration = Mathf.Max(0f, _bossIntroHoldDuration);
            while (elapsed < holdDuration)
            {
                elapsed += Time.deltaTime;
                float shakeT = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, holdDuration));
                cameraTransform.position = targetPosition + GetBossIntroShakeOffset(shakeT);
                cameraTransform.rotation = GetLookRotation(cameraTransform.position, lookPoint, targetRotation);
                cinematicCamera.fieldOfView = targetFov;
                yield return null;
            }

            Vector3 returnStartPosition = cameraTransform.position;
            Quaternion returnStartRotation = cameraTransform.rotation;
            float returnStartFov = cinematicCamera.fieldOfView;
            elapsed = 0f;
            float returnDuration = Mathf.Max(0.01f, _bossIntroReturnDuration);
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));
                cameraTransform.position = Vector3.Lerp(returnStartPosition, startWorldPosition, t);
                cameraTransform.rotation = Quaternion.Slerp(returnStartRotation, startWorldRotation, t);
                cinematicCamera.fieldOfView = Mathf.Lerp(returnStartFov, startFov, t);
                yield return null;
            }
        }
        finally
        {
            cameraTransform.SetParent(startParent, true);
            if (startSiblingIndex >= 0)
                cameraTransform.SetSiblingIndex(startSiblingIndex);

            cameraTransform.localPosition = startLocalPosition;
            cameraTransform.localRotation = startLocalRotation;
            cinematicCamera.fieldOfView = startFov;

            RestoreVictoryCameraBehaviours(disabledBehaviours);
        }
    }

    private Vector3 GetBossIntroCameraPosition(Vector3 startPosition, Vector3 bossPosition, Bounds bossBounds)
    {
        Vector3 toStart = Vector3.ProjectOnPlane(startPosition - bossPosition, Vector3.up);
        if (toStart.sqrMagnitude <= 0.001f)
            toStart = -transform.forward;

        Vector3 direction = toStart.normalized;
        float distance = Mathf.Max(_bossIntroCameraDistance, Mathf.Max(bossBounds.extents.x, bossBounds.extents.z) + 8f);
        float height = Mathf.Max(_bossIntroCameraHeight, bossBounds.size.y * 0.55f);
        return bossPosition + direction * distance + Vector3.up * height;
    }

    private Vector3 GetBossIntroShakeOffset(float intensity)
    {
        float magnitude = Mathf.Max(0f, _bossIntroShakeMagnitude) * Mathf.Clamp01(intensity);
        if (magnitude <= 0f)
            return Vector3.zero;

        Vector2 random = Random.insideUnitCircle * magnitude;
        return new Vector3(random.x, random.y, 0f);
    }

    private IEnumerator BossIntroTitleRoutine(string titleText)
    {
        GameObject canvasObject = new("BossIntroTitleCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 21000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject titleObject = new("BossIntroTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = titleObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = titleObject.GetComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 72f;
        label.fontSizeMax = Mathf.Max(120f, _splashMaxFontSize);
        label.fontStyle = FontStyles.UpperCase;
        label.text = titleText;
        if (_splashFont != null)
            label.font = _splashFont;

        float duration = GetBossIntroTotalDuration();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float fadeIn = Mathf.Clamp01(t / 0.18f);
            float fadeOut = Mathf.Clamp01((t - 0.72f) / 0.28f);
            float alpha = Mathf.Lerp(0f, 1f, fadeIn) * Mathf.Lerp(1f, 0f, fadeOut);
            float scale = Mathf.Lerp(0.72f, 1.05f, EaseOutBack(fadeIn)) + Mathf.Sin(t * Mathf.PI * 12f) * 0.015f;

            rect.localScale = Vector3.one * scale;
            bool flash = Mathf.FloorToInt(elapsed * 18f) % 2 == 0;
            Color flashColor = flash ? Color.white : _phaseSplashColor;
            label.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            yield return null;
        }

        Destroy(canvasObject);
    }

    private IEnumerator BossIntroFogPulseRoutine()
    {
        StopActiveFogCrossfade();
        RenderSettings.fog = true;

        Color startColor = RenderSettings.fogColor;
        Color pulseColor = _bossIntroFogPulseColor;
        Color endColor = _unclearedFogColor;
        float duration = Mathf.Max(0.01f, GetBossIntroTotalDuration());
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(startColor, endColor, t), pulseColor, pulse * 0.65f);
            yield return null;
        }

        RenderSettings.fogColor = endColor;
        _currentFogTarget = endColor;
        _hasFogTarget = true;
    }

    private static void StopActiveFogCrossfade()
    {
        if (_fogRoutineHost != null && _fogCrossfadeRoutine != null)
            _fogRoutineHost.StopCoroutine(_fogCrossfadeRoutine);

        _fogRoutineHost = null;
        _fogCrossfadeRoutine = null;
    }

    private IEnumerator BossIntroGroundRingRoutine(GameObject boss)
    {
        if (boss == null)
            yield break;

        Bounds bounds = GetWorldBounds(boss);
        GameObject ringObject = new("BossIntroSummonRing", typeof(LineRenderer));
        LineRenderer line = ringObject.GetComponent<LineRenderer>();
        ConfigureBossIntroRing(line);

        const int segments = 96;
        line.positionCount = segments + 1;
        line.useWorldSpace = false;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0.035f, Mathf.Sin(angle)));
        }

        Vector3 ringPosition = new(bounds.center.x, boss.transform.position.y + 0.05f, bounds.center.z);
        ringObject.transform.position = ringPosition;

        float startRadius = Mathf.Max(1.5f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.45f);
        float endRadius = Mathf.Max(startRadius + 4f, Mathf.Max(bounds.extents.x, bounds.extents.z) + 6f);
        float duration = Mathf.Max(0.01f, _bossIntroMoveInDuration + _bossIntroHoldDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(startRadius, endRadius, Mathf.SmoothStep(0f, 1f, t));
            float alpha = Mathf.Sin(t * Mathf.PI);
            Color ringColor = new(1f, 1f, 1f, alpha * 0.85f);
            ringObject.transform.localScale = new Vector3(radius, 1f, radius);
            line.startColor = ringColor;
            line.endColor = ringColor;
            if (line.material != null)
                SetMaterialColor(line.material, ringColor);

            yield return null;
        }

        Destroy(ringObject);
    }

    private static void ConfigureBossIntroRing(LineRenderer line)
    {
        if (line == null)
            return;

        line.loop = true;
        line.widthMultiplier = 0.18f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader != null)
        {
            Material material = new(shader)
            {
                name = "BossIntroSummonRing_Runtime",
                color = Color.white
            };
            PrepareMaterialForFade(material);
            line.material = material;
        }
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private string GetBossIntroTitle(GameObject boss)
    {
        EnemyBase enemyBase = boss != null ? boss.GetComponent<EnemyBase>() : null;
        string displayName = enemyBase != null && enemyBase.Data != null ? enemyBase.Data.DisplayName : null;
        return string.IsNullOrWhiteSpace(displayName) ? "BOSS" : displayName.ToUpperInvariant();
    }

    private float GetBossIntroTotalDuration()
    {
        return Mathf.Max(0.01f, _bossIntroMoveInDuration) +
               Mathf.Max(0f, _bossIntroHoldDuration) +
               Mathf.Max(0.01f, _bossIntroReturnDuration);
    }

    private void ConfigureBossModelHitFlash(GameObject boss)
    {
        if (boss == null)
            return;

        if (boss.GetComponentInChildren<EnemyHitFlash>(true) != null)
            return;

        if (boss.GetComponent<EnemyModelHitFlash>() == null)
            boss.AddComponent<EnemyModelHitFlash>();
    }

    private void ConfigureBossTeleport(GameObject boss, int phaseIndex)
    {
        if (!_enableBossTeleport || boss == null)
            return;

        BossTeleportAbility teleport = boss.GetComponent<BossTeleportAbility>();
        if (teleport == null)
            teleport = boss.AddComponent<BossTeleportAbility>();

        Vector2 cooldownRange = phaseIndex >= 3 ? _phaseThreeTeleportCooldownRange : _bossTeleportCooldownRange;
        float teleportChance = phaseIndex >= 3 ? _phaseThreeTeleportChance : _bossTeleportChance;

        teleport.Configure(
            _bossTeleportWhenFartherThan,
            _bossTeleportNearPlayerDistance.x,
            _bossTeleportNearPlayerDistance.y,
            cooldownRange.x,
            cooldownRange.y,
            teleportChance,
            _bossTeleportFadeDuration,
            _bossTeleportHiddenDuration,
            _bossPostTeleportAttackLock,
            _bossTeleportSound,
            _bossTeleportSoundVolume);
    }

    private void ConfigureBossSummonAttack(GameObject boss, int phaseIndex)
    {
        if (!_enableBossSummonAttack || boss == null)
            return;

        if (_summonMeleeEnemyPrefab == null && _summonRangedEnemyPrefab == null)
            return;

        BossSummonAttackModule summon = boss.GetComponent<BossSummonAttackModule>();
        if (summon == null)
            summon = boss.AddComponent<BossSummonAttackModule>();

        int summonCount = _bossSummonCount;
        Vector2 cooldownRange = _bossSummonCooldownRange;
        float maxAttackRange = -1f;

        if (phaseIndex == 2)
        {
            summonCount = Mathf.Max(1, _bossSummonCount + _phaseTwoSummonCountBonus);
            cooldownRange = _phaseTwoSummonCooldownRange;
            maxAttackRange = _phaseTwoSummonMaxRange;
        }
        else if (phaseIndex >= 3)
        {
            summonCount = _phaseThreeSummonCount;
            cooldownRange = _phaseThreeSummonCooldownRange;
        }

        summon.Configure(
            this,
            summonCount,
            cooldownRange.x,
            cooldownRange.y,
            _bossSummonSound,
            _bossSummonSoundVolume,
            maxAttackRange);
    }

    private void ConfigureBossPhase(GameObject boss, int phaseIndex)
    {
        if (boss == null)
            return;

        DisableStandardBossMelee(boss);
        ConfigureBossTelegraphedMelee(boss, phaseIndex);
        ConfigureBossSpikeAttack(boss, phaseIndex);
        ConfigureBossDashSlam(boss, phaseIndex);
        ConfigureBossArenaSweep(boss, phaseIndex);
        ConfigureBossGridAttack(boss, phaseIndex);
        ConfigureBossTrackingSpikeAttack(boss, phaseIndex);

        if (phaseIndex == 2)
            ConfigurePhaseTwoShield(boss);

        if (phaseIndex >= 3)
        {
            BossProjectileBarrageModule oldBarrage = boss.GetComponent<BossProjectileBarrageModule>();
            if (oldBarrage != null)
                oldBarrage.enabled = false;

            BossGroundBurstModule oldGroundBurst = boss.GetComponent<BossGroundBurstModule>();
            if (oldGroundBurst != null)
                oldGroundBurst.enabled = false;

            BossPhaseThreeDirector director = boss.GetComponent<BossPhaseThreeDirector>();
            if (director == null)
                director = boss.AddComponent<BossPhaseThreeDirector>();
            director.Configure(this, _phaseThreeComboCooldownRange);

            StartCoroutine(ApplyPhaseThreeMovementNextFrame(boss));
        }
    }

    public void PlayBossMeleeImpactSound() => PlayBossAttackSound(ref _bossMeleeImpactSound, BossMeleeImpactSoundPath);
    public void PlayBossDashWindupSound() => PlayBossAttackSound(ref _bossDashWindupSound, BossDashWindupSoundPath);
    public void PlayBossDashMoveSound() => PlayBossAttackSound(ref _bossDashMoveSound, BossDashMoveSoundPath);
    public void PlayBossSpikeCastSound() => PlayBossAttackSound(ref _bossSpikeCastSound, BossSpikeCastSoundPath);
    public void PlayBossSpikeEruptSound() => PlayBossAttackSound(ref _bossSpikeEruptSound, BossSpikeEruptSoundPath);
    public void PlayBossGridEruptSound()
    {
        PlayBossSpikeEruptSound();
        PlayBossSpikeEruptSound();
    }
    public void PlayBossArenaSweepChargeSound() => PlayBossAttackSound(ref _bossArenaSweepChargeSound, BossArenaSweepChargeSoundPath);
    public void PlayBossArenaSweepMoveSound() => PlayBossAttackSound(ref _bossArenaSweepMoveSound, BossArenaSweepMoveSoundPath);
    public void PlayBossArenaSweepHitSound() => PlayBossAttackSound(ref _bossArenaSweepHitSound, BossMeleeImpactSoundPath);
    public void PlayBossSummonSpawnSound() => PlayBossAttackSound(ref _bossSummonSpawnSound, BossSummonSpawnSoundPath);
    public void PlayBossShieldActivateSound() => PlayBossAttackSound(ref _bossShieldActivateSound, BossShieldActivateSoundPath);
    public void PlayBossShieldCrystalHitSound() => PlayBossAttackSound(ref _bossShieldCrystalHitSound, BossShieldCrystalHitSoundPath);
    public void PlayBossShieldCrystalDestroyedSound() => PlayBossAttackSound(ref _bossShieldCrystalDestroyedSound, BossShieldCrystalDestroyedSoundPath);
    public void PlayBossShieldBreakSound() => PlayBossAttackSound(ref _bossShieldBreakSound, BossShieldBreakSoundPath);

    private void PlayBossAttackSound(ref AudioClip clip, string editorAssetPath)
    {
        AudioClip resolvedClip = ResolveBossAudioClip(ref clip, editorAssetPath);
        if (resolvedClip != null)
            AudioManager.Instance?.PlayBossSfx(resolvedClip, _bossAttackSoundVolume);
    }

    private static AudioClip ResolveBossAudioClip(ref AudioClip clip, string editorAssetPath)
    {
#if UNITY_EDITOR
        if (clip == null && !string.IsNullOrWhiteSpace(editorAssetPath))
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(editorAssetPath);
#endif
        return clip;
    }

    private void DisableStandardBossMelee(GameObject boss)
    {
        MeleeAttackModule melee = boss != null ? boss.GetComponent<MeleeAttackModule>() : null;
        if (melee != null)
            melee.enabled = false;
    }

    private void ConfigureBossTelegraphedMelee(GameObject boss, int phaseIndex)
    {
        BossTelegraphedMeleeModule telegraphedMelee = boss.GetComponent<BossTelegraphedMeleeModule>();

        if (phaseIndex == 2)
        {
            if (telegraphedMelee != null)
                telegraphedMelee.enabled = false;
            return;
        }

        if (phaseIndex != 1 && phaseIndex < 3)
            return;

        if (telegraphedMelee == null)
            telegraphedMelee = boss.AddComponent<BossTelegraphedMeleeModule>();

        Vector2 cooldownRange = phaseIndex >= 3 ? _phaseThreeBossMeleeCooldownRange : _phaseOneBossMeleeCooldownRange;
        telegraphedMelee.enabled = true;
        telegraphedMelee.Configure(this, phaseIndex, cooldownRange);
    }

    private void ConfigureBossSpikeAttack(GameObject boss, int phaseIndex)
    {
        BossSpikeAttackModule spikeAttack = boss.GetComponent<BossSpikeAttackModule>();

        if (phaseIndex == 2)
        {
            if (spikeAttack != null)
                spikeAttack.enabled = false;
            return;
        }

        if (phaseIndex != 1 && phaseIndex < 3)
            return;

        if (spikeAttack == null)
            spikeAttack = boss.AddComponent<BossSpikeAttackModule>();

        Vector2 cooldownRange = phaseIndex >= 3 ? _phaseThreeSpikeCooldownRange : _phaseOneSpikeCooldownRange;
        spikeAttack.enabled = true;
        spikeAttack.Configure(this, phaseIndex, cooldownRange);
    }

    private void ConfigureBossDashSlam(GameObject boss, int phaseIndex)
    {
        BossDashSlamModule dashSlam = boss.GetComponent<BossDashSlamModule>();

        if (phaseIndex == 2)
        {
            if (dashSlam != null)
                dashSlam.enabled = false;
            return;
        }

        if (phaseIndex != 1 && phaseIndex < 3)
            return;

        if (dashSlam == null)
            dashSlam = boss.AddComponent<BossDashSlamModule>();

        Vector2 cooldownRange = phaseIndex >= 3 ? _phaseThreeDashSlamCooldownRange : _phaseOneDashSlamCooldownRange;
        dashSlam.enabled = true;
        dashSlam.Configure(this, phaseIndex, cooldownRange);
    }

    private void ConfigureBossArenaSweep(GameObject boss, int phaseIndex)
    {
        BossArenaSweepModule arenaSweep = boss.GetComponent<BossArenaSweepModule>();

        if (arenaSweep == null)
            arenaSweep = boss.AddComponent<BossArenaSweepModule>();

        Vector2 cooldownRange = phaseIndex >= 3
            ? _phaseThreeArenaSweepCooldownRange
            : phaseIndex == 2 ? _phaseTwoArenaSweepCooldownRange : _phaseOneArenaSweepCooldownRange;
        arenaSweep.enabled = true;
        arenaSweep.Configure(this, phaseIndex, cooldownRange);
    }

    private void ConfigureBossGridAttack(GameObject boss, int phaseIndex)
    {
        BossGridAttackModule gridAttack = boss.GetComponent<BossGridAttackModule>();
        if (gridAttack == null)
            gridAttack = boss.AddComponent<BossGridAttackModule>();

        Vector2 cooldownRange = phaseIndex >= 3
            ? _phaseThreeGridCooldownRange
            : phaseIndex == 2 ? _phaseTwoGridCooldownRange : _phaseOneGridCooldownRange;
        gridAttack.enabled = true;
        gridAttack.Configure(this, phaseIndex, cooldownRange);
    }

    private void ConfigureBossTrackingSpikeAttack(GameObject boss, int phaseIndex)
    {
        BossTrackingSpikeAttackModule trackingSpike = boss.GetComponent<BossTrackingSpikeAttackModule>();
        if (trackingSpike == null)
            trackingSpike = boss.AddComponent<BossTrackingSpikeAttackModule>();

        Vector2 cooldownRange = phaseIndex >= 3
            ? _phaseThreeTrackingSpikeCooldownRange
            : phaseIndex == 2 ? _phaseTwoTrackingSpikeCooldownRange : _phaseOneTrackingSpikeCooldownRange;
        trackingSpike.enabled = true;
        trackingSpike.Configure(this, phaseIndex, cooldownRange);
    }

    private IEnumerator ApplyPhaseThreeMovementNextFrame(GameObject boss)
    {
        yield return null;

        if (boss == null || _completed)
            yield break;

        NavMeshAgent agent = boss.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.speed *= Mathf.Max(1f, _phaseThreeSpeedMultiplier);
    }

    private void ConfigurePhaseTwoShield(GameObject boss)
    {
        HealthComponent health = boss.GetComponent<HealthComponent>();
        if (health == null)
            return;

        health.SetInvulnerable(true);
        PlayBossShieldActivateSound();

        _currentBossShieldVisual = boss.GetComponent<BossShieldVisual>();
        if (_currentBossShieldVisual == null)
            _currentBossShieldVisual = boss.AddComponent<BossShieldVisual>();
        _currentBossShieldVisual.Configure(boss.transform);

        SpawnShieldCrystals(boss);

        if (_shieldCrystals.Count == 0)
        {
            PlayBossShieldBreakSound();
            EndPhaseTwoShield();
        }
    }

    private void SpawnShieldCrystals(GameObject boss)
    {
        _shieldCrystals.Clear();

        int crystalCount = Mathf.Max(4, _phaseTwoShieldCrystalCount);
        for (int i = 0; i < crystalCount; i++)
        {
            if (!TryGetShieldCrystalPosition(boss, out Vector3 position, out Vector3 floorPosition))
                GetFallbackShieldCrystalPosition(i, crystalCount, out position, out floorPosition);

            GameObject crystalObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystalObject.name = $"BossShieldCrystal_{i + 1}";
            crystalObject.transform.SetParent(transform, true);
            crystalObject.transform.position = position;
            crystalObject.transform.localScale = Vector3.one * Mathf.Max(1f, _phaseTwoCrystalScale);
            crystalObject.tag = "Enemy";
            ApplyLayerRecursively(crystalObject.transform, GetBossFightTargetLayer(boss));

            BossShieldCrystal crystal = crystalObject.AddComponent<BossShieldCrystal>();
            crystal.Configure(
                this,
                Mathf.Max(1, _phaseTwoShieldCrystalHealth));

            _shieldCrystals.Add(crystal);
        }
    }

    private static int GetBossFightTargetLayer(GameObject boss)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            return enemyLayer;

        return boss != null ? boss.layer : 0;
    }

    private static void ApplyLayerRecursively(Transform current, int layer)
    {
        if (current == null)
            return;

        current.gameObject.layer = layer;
        for (int i = 0; i < current.childCount; i++)
            ApplyLayerRecursively(current.GetChild(i), layer);
    }

    private void GetFallbackShieldCrystalPosition(int index, int count, out Vector3 position, out Vector3 floorPosition)
    {
        float angle = count > 0 ? (Mathf.PI * 2f * index) / count : 0f;
        Vector3 center = transform.TransformPoint(_localBounds.center);
        Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 8f;

        if (!IsWithinLocalBounds(candidate))
        {
            Vector3 local = transform.InverseTransformPoint(candidate);
            local.x = Mathf.Clamp(local.x, _localBounds.min.x + 2f, _localBounds.max.x - 2f);
            local.z = Mathf.Clamp(local.z, _localBounds.min.z + 2f, _localBounds.max.z - 2f);
            candidate = transform.TransformPoint(local);
        }

        if (TryFindRoomColliderFloor(candidate, out Vector3 roomFloor))
            floorPosition = roomFloor;
        else if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, Mathf.Max(4f, _navMeshSampleRadius * 2f), NavMesh.AllAreas))
            floorPosition = hit.position;
        else
            floorPosition = candidate;

        position = floorPosition + Vector3.up * Mathf.Max(2.5f, _phaseTwoCrystalSpawnHeight);
    }

    private bool TryGetShieldCrystalPosition(GameObject boss, out Vector3 position, out Vector3 floorPosition)
    {
        Vector3 centerWorld = boss != null ? boss.transform.position : transform.TransformPoint(_localBounds.center);

        for (int i = 0; i < 24; i++)
        {
            if (!TryPickRandomSpawnPosition(rejectNearDoors: true, rejectNearCenter: false, minimumSpacing: 6f, out Vector3 candidate))
                continue;

            if (Vector3.ProjectOnPlane(candidate - centerWorld, Vector3.up).sqrMagnitude < 7f * 7f)
                continue;

            floorPosition = candidate;
            position = floorPosition + Vector3.up * Mathf.Max(2.5f, _phaseTwoCrystalSpawnHeight);
            return true;
        }

        if (TryPickRandomSpawnPosition(rejectNearDoors: false, rejectNearCenter: false, minimumSpacing: 0f, out floorPosition))
        {
            position = floorPosition + Vector3.up * Mathf.Max(2.5f, _phaseTwoCrystalSpawnHeight);
            return true;
        }

        Vector2 fallbackDirection = Random.insideUnitCircle.normalized;
        if (fallbackDirection.sqrMagnitude <= 0.001f)
            fallbackDirection = Vector2.right;

        Vector3 world = centerWorld + new Vector3(fallbackDirection.x, 0f, fallbackDirection.y) * 10f;
        if (!IsWithinLocalBounds(world))
        {
            Vector3 local = transform.InverseTransformPoint(world);
            float xInset = Mathf.Min(6f, Mathf.Max(0f, _localBounds.extents.x * 0.35f));
            float zInset = Mathf.Min(6f, Mathf.Max(0f, _localBounds.extents.z * 0.35f));
            float minX = Mathf.Min(_localBounds.min.x + xInset, _localBounds.center.x);
            float maxX = Mathf.Max(_localBounds.max.x - xInset, _localBounds.center.x);
            float minZ = Mathf.Min(_localBounds.min.z + zInset, _localBounds.center.z);
            float maxZ = Mathf.Max(_localBounds.max.z - zInset, _localBounds.center.z);
            local.x = Mathf.Clamp(local.x, minX, maxX);
            local.z = Mathf.Clamp(local.z, minZ, maxZ);
            world = transform.TransformPoint(local);
        }

        if (TryFindRoomColliderFloor(world, out Vector3 floorPoint))
            world = floorPoint;
        else if (NavMesh.SamplePosition(world, out NavMeshHit hit, Mathf.Max(4f, _navMeshSampleRadius * 2f), NavMesh.AllAreas))
            world = hit.position;
        else
            world = centerWorld + new Vector3(fallbackDirection.x, 0f, fallbackDirection.y) * 6f;

        floorPosition = world;
        position = floorPosition + Vector3.up * Mathf.Max(2.5f, _phaseTwoCrystalSpawnHeight);
        return IsWithinLocalBounds(position);
    }

    public void OnBossShieldCrystalDestroyed(BossShieldCrystal crystal)
    {
        if (crystal == null || !_shieldCrystals.Remove(crystal))
            return;

        HealthComponent bossHealth = _currentBoss != null ? _currentBoss.GetComponent<HealthComponent>() : null;
        if (bossHealth != null && bossHealth.IsAlive)
        {
            int damage = Mathf.RoundToInt(bossHealth.MaxHealth * Mathf.Clamp01(_phaseTwoCrystalBossDamageFraction));
            if (damage > 0)
            {
                bossHealth.TakeDamage(new DamageInfo
                {
                    Amount = damage,
                    Type = DamageType.Energy,
                    Source = crystal.gameObject,
                    IgnoreInvulnerability = true,
                    IgnoreArmor = true
                });
            }
        }

        if (_shieldCrystals.Count == 0)
        {
            PlayBossShieldBreakSound();
            EndPhaseTwoShield();
        }
    }

    private void EndPhaseTwoShield()
    {
        HealthComponent bossHealth = _currentBoss != null ? _currentBoss.GetComponent<HealthComponent>() : null;
        if (bossHealth != null && bossHealth.IsAlive)
            bossHealth.SetInvulnerable(false);

        if (_currentBossShieldVisual != null)
        {
            Destroy(_currentBossShieldVisual);
            _currentBossShieldVisual = null;
        }
    }

    public void RegisterPhaseHazard(GameObject hazard)
    {
        if (hazard != null && !_activePhaseHazards.Contains(hazard))
            _activePhaseHazards.Add(hazard);
    }

    public bool TryPickPhaseThreeArenaPoint(Vector3 around, float minDistance, float maxDistance, out Vector3 point)
    {
        float min = Mathf.Max(0f, Mathf.Min(minDistance, maxDistance));
        float max = Mathf.Max(min + 0.1f, maxDistance);

        for (int i = 0; i < 36; i++)
        {
            Vector2 direction2D = Random.insideUnitCircle.normalized;
            if (direction2D.sqrMagnitude <= 0.001f)
                direction2D = Vector2.right;

            Vector3 candidate = around + new Vector3(direction2D.x, 0f, direction2D.y) * Random.Range(min, max);
            if (TryProjectToBossRoomFloor(candidate, out point))
                return true;
        }

        if (TryPickRandomSpawnPosition(rejectNearDoors: true, rejectNearCenter: false, minimumSpacing: 0f, out point))
            return true;

        if (TryPickRandomSpawnPosition(rejectNearDoors: false, rejectNearCenter: false, minimumSpacing: 0f, out point))
            return true;

        point = transform.TransformPoint(_localBounds.center);
        return TryProjectToBossRoomFloor(point, out point);
    }

    public bool TryProjectToBossRoomFloor(Vector3 candidate, out Vector3 point)
    {
        if (TryFindRoomColliderFloor(candidate, out Vector3 floorPoint))
            candidate = floorPoint;

        float sampleRadius = Mathf.Max(2f, _navMeshSampleRadius * 2f);
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            candidate = hit.position;

        if (!IsWithinLocalBounds(candidate) || !IsSupportedByThisRoomFloor(candidate))
        {
            point = default;
            return false;
        }

        point = candidate;
        return true;
    }

    public EnemyProjectile FireBossPhaseProjectile(
        GameObject owner,
        Vector3 origin,
        Vector3 direction,
        float damage,
        DamageType damageType,
        float maxDistance,
        float speed,
        float collisionRadius)
    {
        GameObject projectileObject = new($"{(owner != null ? owner.name : "Boss")}_PhaseProjectile");
        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Launch(owner, origin, direction, damage, damageType, maxDistance, speed, collisionRadius);
        ConfigureBossPhaseProjectileVisuals(projectile);
        RegisterPhaseHazard(projectileObject);
        return projectile;
    }

    private void ConfigureBossPhaseProjectileVisuals(EnemyProjectile projectile)
    {
        if (projectile == null)
            return;

        float intensity = _bossPhaseIndex >= 3 ? 1.5f : 1.1f;
        projectile.ConfigureBossVisuals(
            _bossPhaseIndex >= 3 ? new Color(0.4f, 1f, 1f, 1f) : new Color(0.24f, 0.9f, 1f, 1f),
            _bossPhaseIndex >= 3 ? new Color(1f, 5.5f, 6.2f, 1f) : new Color(0.65f, 3.9f, 4.8f, 1f),
            5.8f * intensity,
            4.8f * intensity,
            useTrail: true,
            impactPulse: true,
            impactPulseRadius: 1.8f,
            impactPulseColor: _bossPhaseIndex >= 3 ? new Color(0.32f, 0.95f, 1f, 0.58f) : new Color(0.2f, 0.85f, 1f, 0.42f),
            impactShake: 0.006f * intensity);
    }

    public BossGroundBurstHazard SpawnBossPhaseGroundBurst(
        GameObject owner,
        Vector3 position,
        float radius,
        float telegraphDuration,
        float lingerDuration,
        float damage,
        DamageType damageType)
    {
        GameObject hazardObject = new($"{(owner != null ? owner.name : "Boss")}_PhaseGroundBurst");
        BossGroundBurstHazard hazard = hazardObject.AddComponent<BossGroundBurstHazard>();
        hazard.Configure(owner, position, radius, telegraphDuration, lingerDuration, damage, damageType);
        RegisterPhaseHazard(hazardObject);
        return hazard;
    }

    public BossSpikeHazard SpawnBossSpikeHazard(
        GameObject owner,
        Vector3 position,
        float radius,
        float warningDuration,
        float damage,
        DamageType damageType,
        float startDelay = 0f,
        float spikeHeight = 4.2f)
    {
        if (!TryProjectToBossRoomFloor(position, out Vector3 floorPosition))
            return null;

        GameObject hazardObject = new($"{(owner != null ? owner.name : "Boss")}_SpikeHazard");
        BossSpikeHazard hazard = hazardObject.AddComponent<BossSpikeHazard>();
        hazard.Configure(this, owner, floorPosition, radius, warningDuration, damage, damageType, startDelay, spikeHeight);
        RegisterPhaseHazard(hazardObject);
        return hazard;
    }

    public BossCircleImpactHazard SpawnBossCircleImpactHazard(
        GameObject owner,
        Vector3 position,
        float radius,
        float warningDuration,
        float lingerDuration,
        float damage,
        DamageType damageType,
        Color warningColor,
        System.Action impactSound = null)
    {
        if (!TryProjectToBossRoomFloor(position, out Vector3 floorPosition))
            return null;

        GameObject hazardObject = new($"{(owner != null ? owner.name : "Boss")}_CircleImpact");
        BossCircleImpactHazard hazard = hazardObject.AddComponent<BossCircleImpactHazard>();
        hazard.Configure(owner, floorPosition, radius, warningDuration, lingerDuration, damage, damageType, warningColor, impactSound);
        RegisterPhaseHazard(hazardObject);
        return hazard;
    }

    public BossWaveAttackHazard SpawnBossWaveAttackHazard(
        GameObject owner,
        Vector3 start,
        Vector3 end,
        float width,
        float height,
        float depth,
        float warningDuration,
        float travelDuration,
        float damage,
        DamageType damageType,
        Color warningColor,
        System.Action activeSound = null,
        System.Action hitSound = null)
    {
        if (!TryProjectToBossRoomFloor(start, out Vector3 floorStart) ||
            !TryProjectToBossRoomFloor(end, out Vector3 floorEnd))
        {
            return null;
        }

        GameObject hazardObject = new($"{(owner != null ? owner.name : "Boss")}_WaveHazard");
        BossWaveAttackHazard hazard = hazardObject.AddComponent<BossWaveAttackHazard>();
        hazard.Configure(owner, floorStart, floorEnd, width, height, depth, warningDuration, travelDuration, damage, damageType, warningColor, activeSound, hitSound);
        RegisterPhaseHazard(hazardObject);
        return hazard;
    }

    public BossGridCellHazard SpawnBossGridCellHazard(
        GameObject owner,
        Vector3 position,
        float size,
        float warningDuration,
        float activeDuration,
        float damage,
        DamageType damageType,
        Color warningColor,
        System.Action impactSound = null)
    {
        if (!TryProjectToBossRoomFloor(position, out Vector3 floorPosition))
            return null;

        GameObject hazardObject = new($"{(owner != null ? owner.name : "Boss")}_GridCellHazard");
        BossGridCellHazard hazard = hazardObject.AddComponent<BossGridCellHazard>();
        hazard.Configure(owner, floorPosition, size, warningDuration, activeDuration, damage, damageType, warningColor, impactSound);
        RegisterPhaseHazard(hazardObject);
        return hazard;
    }

    public BossLineAttackHazard SpawnBossLineAttackHazard(
        GameObject owner,
        Vector3 start,
        Vector3 end,
        float width,
        float warningDuration,
        float activeDuration,
        float damage,
        DamageType damageType,
        Color warningColor,
        System.Action activeSound = null,
        System.Action hitSound = null)
    {
        if (!TryProjectToBossRoomFloor(start, out Vector3 floorStart) ||
            !TryProjectToBossRoomFloor(end, out Vector3 floorEnd))
        {
            return null;
        }

        GameObject hazardObject = new($"{(owner != null ? owner.name : "Boss")}_LineHazard");
        BossLineAttackHazard hazard = hazardObject.AddComponent<BossLineAttackHazard>();
        hazard.Configure(owner, floorStart, floorEnd, width, warningDuration, activeDuration, damage, damageType, warningColor, activeSound, hitSound);
        RegisterPhaseHazard(hazardObject);
        return hazard;
    }

    public bool IsShieldCrystalBoundaryCollider(Collider collider)
    {
        return collider != null && _roomColliders.Contains(collider);
    }

    public bool TryGetShieldCrystalFloor(Vector3 worldPosition, out Vector3 floorPoint)
    {
        floorPoint = default;

        if (!IsWithinLocalBounds(worldPosition))
            return false;

        if (!TryFindRoomColliderFloor(worldPosition, out floorPoint))
            return _roomColliders.Count == 0;

        return IsWithinLocalBounds(floorPoint) && IsOnPlayableHeight(floorPoint, _spawnPlayableHeightTolerance);
    }

    public bool TrySummonEnemies(GameObject source, int count)
    {
        if (!_started || _completed || count <= 0)
            return false;

        PruneActiveSummons();
        int availableSlots = Mathf.Max(0, _bossMaxActiveSummons - _activeSummons.Count);
        int spawnCount = Mathf.Min(count, availableSlots);
        if (spawnCount <= 0)
            return false;

        bool spawnedAny = false;
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = PickSummonedEnemyPrefab();
            if (prefab == null)
                continue;

            if (!TryPickSummonSpawnPosition(out Vector3 position))
                continue;

            Quaternion rotation = Quaternion.LookRotation(GetDirectionToRoomCenter(position), Vector3.up);
            GameObject enemy = Instantiate(prefab, position, rotation, transform);
            _activeSummons.Add(enemy);
            PrepareSummonedEnemy(enemy, position);
            PlayBossSummonSpawnSound();
            spawnedAny = true;
        }

        return spawnedAny;
    }

    private void PruneActiveSummons()
    {
        for (int i = _activeSummons.Count - 1; i >= 0; i--)
        {
            GameObject summon = _activeSummons[i];
            if (summon == null)
            {
                _activeSummons.RemoveAt(i);
                continue;
            }

            HealthComponent health = summon.GetComponent<HealthComponent>();
            if (health != null && !health.IsAlive)
                _activeSummons.RemoveAt(i);
        }
    }

    private GameObject PickSummonedEnemyPrefab()
    {
        if (_summonMeleeEnemyPrefab == null)
            return _summonRangedEnemyPrefab;

        if (_summonRangedEnemyPrefab == null)
            return _summonMeleeEnemyPrefab;

        return Random.value <= _bossSummonMeleeWeight
            ? _summonMeleeEnemyPrefab
            : _summonRangedEnemyPrefab;
    }

    private bool TryPickSummonSpawnPosition(out Vector3 position)
    {
        if (TryPickRandomSpawnPosition(rejectNearDoors: true, rejectNearCenter: false, minimumSpacing: 2f, out position))
            return true;

        if (TryPickRandomSpawnPosition(rejectNearDoors: false, rejectNearCenter: false, minimumSpacing: 0f, out position))
            return true;

        return TryPickSpawnPosition(out position);
    }

    private void PrepareSummonedEnemy(GameObject enemy, Vector3 position)
    {
        if (enemy == null)
            return;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.Warp(position);

        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase != null)
            enemyBase.enabled = false;

        IAttackModule[] attackModules = enemy.GetComponents<IAttackModule>();
        for (int i = 0; i < attackModules.Length; i++)
        {
            if (attackModules[i] is MonoBehaviour behaviour)
                behaviour.enabled = false;
        }

        if (agent != null)
            agent.enabled = false;

        Collider[] colliders = enemy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
                colliders[i].enabled = false;
        }

        StartCoroutine(SummonedEnemyFadeInRoutine(enemy, enemyBase, attackModules, agent, colliders, position));
    }

    private IEnumerator SummonedEnemyFadeInRoutine(
        GameObject enemy,
        EnemyBase enemyBase,
        IAttackModule[] attackModules,
        NavMeshAgent agent,
        Collider[] colliders,
        Vector3 position)
    {
        Renderer[] renderers = enemy != null ? enemy.GetComponentsInChildren<Renderer>(true) : null;
        MaterialColorState[] materialStates = CacheMaterialColorStates(renderers);
        ApplyMaterialColor(materialStates, Color.white);

        float duration = Mathf.Max(0.01f, _bossSummonFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / duration);
            LerpMaterialColor(materialStates, Color.white, t);
            yield return null;
        }

        RestoreMaterialColor(materialStates);

        if (enemy == null)
            yield break;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
                colliders[i].enabled = true;
        }

        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
                agent.Warp(position);
        }

        if (enemyBase != null)
            enemyBase.enabled = true;

        for (int i = 0; i < attackModules.Length; i++)
        {
            if (attackModules[i] is MonoBehaviour behaviour)
                behaviour.enabled = true;
        }
    }

    private static MaterialColorState[] CacheMaterialColorStates(Renderer[] renderers)
    {
        if (renderers == null)
            return System.Array.Empty<MaterialColorState>();

        List<MaterialColorState> states = new();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Material[] materials = renderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                    continue;

                MaterialColorState state = new()
                {
                    Material = material,
                    HasBaseColor = material.HasProperty("_BaseColor"),
                    HasColor = material.HasProperty("_Color")
                };

                if (state.HasBaseColor)
                    state.BaseColor = material.GetColor("_BaseColor");
                if (state.HasColor)
                    state.Color = material.GetColor("_Color");

                states.Add(state);
            }
        }

        return states.ToArray();
    }

    private static void LerpMaterialColor(MaterialColorState[] states, Color targetColor, float t)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Length; i++)
        {
            MaterialColorState state = states[i];
            if (state.Material == null)
                continue;

            if (state.HasBaseColor)
                state.Material.SetColor("_BaseColor", Color.Lerp(state.BaseColor, targetColor, t));
            if (state.HasColor)
                state.Material.SetColor("_Color", Color.Lerp(state.Color, targetColor, t));
        }
    }

    private static void ApplyMaterialColor(MaterialColorState[] states, Color color)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Length; i++)
        {
            MaterialColorState state = states[i];
            if (state.Material == null)
                continue;

            if (state.HasBaseColor)
                state.Material.SetColor("_BaseColor", color);
            if (state.HasColor)
                state.Material.SetColor("_Color", color);
        }
    }

    private static void RestoreMaterialColor(MaterialColorState[] states)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Length; i++)
        {
            MaterialColorState state = states[i];
            if (state.Material == null)
                continue;

            if (state.HasBaseColor)
                state.Material.SetColor("_BaseColor", state.BaseColor);
            if (state.HasColor)
                state.Material.SetColor("_Color", state.Color);
        }
    }

    private bool TryPickSpawnPosition(out Vector3 position)
    {
        float relaxedSpacing = Mathf.Max(1.5f, _minimumSpawnSpacing * 0.5f);

        if (TryPickRandomSpawnPosition(rejectNearDoors: true, rejectNearCenter: true, minimumSpacing: _minimumSpawnSpacing, out position))
            return true;

        if (TryPickRandomSpawnPosition(rejectNearDoors: true, rejectNearCenter: false, minimumSpacing: relaxedSpacing, out position))
            return true;

        if (TryPickRandomSpawnPosition(rejectNearDoors: false, rejectNearCenter: false, minimumSpacing: 0f, out position))
            return true;

        if (_spawnCandidates.Count > 0)
        {
            for (int i = 0; i < 16; i++)
            {
                Vector3 candidate = _spawnCandidates[Random.Range(0, _spawnCandidates.Count)];
                if (!IsTooCloseToRecentSpawn(candidate, relaxedSpacing))
                {
                    position = candidate;
                    return true;
                }
            }

            position = _spawnCandidates[Random.Range(0, _spawnCandidates.Count)];
            return true;
        }

        Vector3 center = transform.TransformPoint(_localBounds.center);
        float searchRadius = Mathf.Max(1f, Mathf.Min(_navMeshSampleRadius, _spawnPlayableHeightTolerance + 1f));
        for (int i = 0; i < _playableLocalHeights.Count; i++)
        {
            Vector3 localCenter = transform.InverseTransformPoint(center);
            localCenter.y = _playableLocalHeights[i];
            Vector3 worldCenter = transform.TransformPoint(localCenter);

            if (NavMesh.SamplePosition(worldCenter, out NavMeshHit centerHit, searchRadius, NavMesh.AllAreas) &&
                IsWithinLocalBounds(centerHit.position) &&
                IsOnPlayableHeight(centerHit.position, _spawnPlayableHeightTolerance))
            {
                position = centerHit.position;
                return true;
            }
        }

        if (_playableLocalHeights.Count == 0 &&
            NavMesh.SamplePosition(center, out NavMeshHit centerFallbackHit, searchRadius, NavMesh.AllAreas) &&
            IsWithinLocalBounds(centerFallbackHit.position))
        {
            position = centerFallbackHit.position;
            return true;
        }

        position = default;
        return false;
    }

    private bool TryPickTestingSpawnPosition(out Vector3 position)
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        Vector3 center = transform.TransformPoint(_localBounds.center);
        Vector3 preferred = center;

        if (_playerTransform != null)
        {
            Vector3 awayFromPlayer = Vector3.ProjectOnPlane(center - _playerTransform.position, Vector3.up);
            if (awayFromPlayer.sqrMagnitude <= 0.001f)
                awayFromPlayer = _playerTransform.forward;

            preferred = _playerTransform.position + awayFromPlayer.normalized * Mathf.Max(_centerExclusionRadius, 8f);
        }

        if (TryPickTestingSpawnPositionNear(preferred, out position))
            return true;

        return TryPickTestingSpawnPositionNear(center, out position);
    }

    private bool TryPickTestingSpawnPositionNear(Vector3 preferred, out Vector3 position)
    {
        if (NavMesh.SamplePosition(preferred, out NavMeshHit navHit, Mathf.Max(8f, _navMeshSampleRadius * 4f), NavMesh.AllAreas) &&
            IsWithinLocalBounds(navHit.position))
        {
            position = navHit.position;
            return true;
        }

        Vector3 floorProbe = preferred;
        if (!IsWithinLocalBounds(floorProbe))
        {
            Vector3 local = transform.InverseTransformPoint(floorProbe);
            local.x = Mathf.Clamp(local.x, _localBounds.min.x, _localBounds.max.x);
            local.z = Mathf.Clamp(local.z, _localBounds.min.z, _localBounds.max.z);
            floorProbe = transform.TransformPoint(local);
        }

        if (TryFindRoomColliderFloor(floorProbe, out Vector3 floorPoint))
        {
            position = floorPoint;
            return true;
        }

        Vector3 fallbackLocal = transform.InverseTransformPoint(floorProbe);
        fallbackLocal.y = _playableLocalHeights.Count > 0 ? _playableLocalHeights[0] : _localBounds.center.y;
        position = transform.TransformPoint(fallbackLocal);
        return IsWithinLocalBounds(position);
    }

    private bool TryPickRandomSpawnPosition(bool rejectNearDoors, bool rejectNearCenter, float minimumSpacing, out Vector3 position)
    {
        float sampleRadius = Mathf.Max(0.5f, Mathf.Min(_navMeshSampleRadius, _spawnPlayableHeightTolerance + 0.5f));
        int attempts = Mathf.Max(24, _spawnSampleAttempts / 3);

        for (int i = 0; i < attempts; i++)
        {
            float localY = _localBounds.center.y;
            if (_playableLocalHeights.Count > 0)
                localY = _playableLocalHeights[Random.Range(0, _playableLocalHeights.Count)];

            Vector3 local = new(
                Random.Range(_localBounds.min.x, _localBounds.max.x),
                localY,
                Random.Range(_localBounds.min.z, _localBounds.max.z));

            Vector3 world = transform.TransformPoint(local);
            if (TryFindRoomColliderFloor(world, out Vector3 floorPoint))
                world = floorPoint;

            if (!NavMesh.SamplePosition(world, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                continue;

            Vector3 candidate = hit.position;
            if (!IsValidSpawnCandidate(candidate, rejectNearDoors, rejectNearCenter))
                continue;

            if (minimumSpacing > 0f && IsTooCloseToRecentSpawn(candidate, minimumSpacing))
                continue;

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    private void RememberSpawnPosition(Vector3 position)
    {
        _recentSpawnPositions.Add(position);
        const int maxRecentSpawns = 10;
        if (_recentSpawnPositions.Count > maxRecentSpawns)
            _recentSpawnPositions.RemoveAt(0);
    }

    private bool IsTooCloseToRecentSpawn(Vector3 position, float minimumSpacing)
    {
        if (minimumSpacing <= 0f)
            return false;

        float minimumSpacingSqr = minimumSpacing * minimumSpacing;
        for (int i = 0; i < _recentSpawnPositions.Count; i++)
        {
            Vector3 offset = Vector3.ProjectOnPlane(position - _recentSpawnPositions[i], Vector3.up);
            if (offset.sqrMagnitude < minimumSpacingSqr)
                return true;
        }

        return false;
    }

    private Vector3 GetDirectionToRoomCenter(Vector3 position)
    {
        Vector3 center = transform.TransformPoint(_localBounds.center);
        Vector3 direction = Vector3.ProjectOnPlane(center - position, Vector3.up);
        return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
    }

    private void OnEnemyDied(EnemyDiedEvent evt)
    {
        if (evt.Enemy == null)
            return;

        _activeSummons.Remove(evt.Enemy);

        if (evt.Enemy == _currentBoss)
        {
            _trackedEnemies.Remove(evt.Enemy);
            StopBossHealthBar();
            CleanupBossPhaseRuntime(destroyCurrentBoss: false);

            if (_bossPhaseIndex < _bossPhaseCount)
            {
                if (!_isBossPhaseTransitioning)
                    _phaseTransitionRoutine = StartCoroutine(BossPhaseTransitionRoutine(evt.Enemy));
                return;
            }

            CompleteBossVictory(evt.Enemy);
            return;
        }

        if (!_trackedEnemies.Remove(evt.Enemy))
            return;

        if (_objective == BossRoomObjective.EliminateAll)
        {
            if (_spawnedCount >= _bossPhaseCount && CountLivingTrackedEnemies() == 0)
                CompleteRoom();
        }
    }

    private void TryCompleteEliminateAll()
    {
        if (_completed || _spawnedCount < _bossPhaseCount)
            return;

        if (CountLivingTrackedEnemies() == 0)
            CompleteRoom();
    }

    private IEnumerator BossPhaseTransitionRoutine(GameObject defeatedBoss)
    {
        _isBossPhaseTransitioning = true;

        float delay = Mathf.Max(0f, _bossPhaseTransitionDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (defeatedBoss != null)
            Destroy(defeatedBoss);

        if (_completed)
        {
            _isBossPhaseTransitioning = false;
            _phaseTransitionRoutine = null;
            yield break;
        }

        _bossPhaseIndex = Mathf.Min(_bossPhaseCount, _bossPhaseIndex + 1);
        ShowPhaseSplash(_bossPhaseIndex);

        bool spawnedBoss = SpawnBoss(out GameObject boss, showHealthBar: true);
        if (!spawnedBoss || boss == null)
            Debug.LogWarning($"[BossRoomController] Failed to spawn boss phase {_bossPhaseIndex} for {gameObject.name}.", this);

        _isBossPhaseTransitioning = false;
        _phaseTransitionRoutine = null;
    }

    private int CountLivingTrackedEnemies()
    {
        int count = 0;
        foreach (GameObject enemy in _trackedEnemies)
        {
            if (enemy == null)
                continue;

            HealthComponent health = enemy.GetComponent<HealthComponent>();
            if (health == null || health.IsAlive)
                count++;
        }

        return count;
    }

    private void CleanupBossPhaseRuntime(bool destroyCurrentBoss)
    {
        EndPhaseTwoShield();

        for (int i = _shieldCrystals.Count - 1; i >= 0; i--)
        {
            BossShieldCrystal crystal = _shieldCrystals[i];
            if (crystal != null)
                Destroy(crystal.gameObject);
        }
        _shieldCrystals.Clear();

        for (int i = _activeSummons.Count - 1; i >= 0; i--)
        {
            GameObject summon = _activeSummons[i];
            if (summon != null)
                Destroy(summon);
        }
        _activeSummons.Clear();

        for (int i = _activePhaseHazards.Count - 1; i >= 0; i--)
        {
            GameObject hazard = _activePhaseHazards[i];
            if (hazard != null)
                Destroy(hazard);
        }
        _activePhaseHazards.Clear();

        if (destroyCurrentBoss && _currentBoss != null)
        {
            Destroy(_currentBoss);
            _currentBoss = null;
        }
    }

    private void CompleteRoom()
    {
        if (_completed)
            return;

        _completed = true;
        if (_activeRoom == this)
            _activeRoom = null;

        if (_combatRoutine != null)
        {
            StopCoroutine(_combatRoutine);
            _combatRoutine = null;
        }

        SetDoorsOpen(true);
        CleanupBossPhaseRuntime(destroyCurrentBoss: false);
        StopBossHealthBar();
        ShowProceedSplash();
        SetClearedFogOwner(this);
    }

    private void CompleteBossVictory(GameObject defeatedBoss)
    {
        if (_completed)
            return;

        _completed = true;
        if (_activeRoom == this)
            _activeRoom = null;

        if (_combatRoutine != null)
        {
            StopCoroutine(_combatRoutine);
            _combatRoutine = null;
        }

        StopRoomSplash();
        CleanupBossPhaseRuntime(destroyCurrentBoss: false);
        StopBossHealthBar();
        ForceFinalBossDeathAnimation(defeatedBoss);

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Cinematic)
            GameManager.Instance.SetState(GameState.Cinematic);

        _victoryRoutine = StartCoroutine(BossVictoryRoutine(defeatedBoss));
    }

    private void ForceFinalBossDeathAnimation(GameObject defeatedBoss)
    {
        EnemyBase enemyBase = defeatedBoss != null ? defeatedBoss.GetComponent<EnemyBase>() : null;
        enemyBase?.ForceDeathAnimationNow();
    }

    private IEnumerator BossVictoryRoutine(GameObject defeatedBoss)
    {
        _victoryTargetBoss = defeatedBoss;
        _victoryDeathAnimationComplete = false;
        float cinematicStartedAt = Time.time;

        Camera cinematicCamera = Camera.main;
        if (cinematicCamera != null && defeatedBoss != null)
            yield return StartCoroutine(OrbitBossUntilDeathAnimationComplete(cinematicCamera, defeatedBoss, () => _victoryDeathAnimationComplete));
        else
            yield return WaitForBossDeathAnimationFallback(() => _victoryDeathAnimationComplete);

        float remainingMinimumTime = Mathf.Max(0f, _victoryMinimumCinematicDuration - (Time.time - cinematicStartedAt));
        if (remainingMinimumTime > 0f)
            yield return new WaitForSeconds(remainingMinimumTime);

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Victory)
            GameManager.Instance.SetState(GameState.Victory);

        _victoryTargetBoss = null;
        _victoryRoutine = null;
    }

    private void OnEnemyDeathAnimationCompleted(EnemyDeathAnimationCompletedEvent evt)
    {
        if (evt.Enemy != null && evt.Enemy == _victoryTargetBoss)
            _victoryDeathAnimationComplete = true;
    }

    private IEnumerator OrbitBossUntilDeathAnimationComplete(Camera cinematicCamera, GameObject defeatedBoss, System.Func<bool> isDeathAnimationComplete)
    {
        Transform cameraTransform = cinematicCamera.transform;
        Vector3 startWorldPosition = cameraTransform.position;
        Quaternion startWorldRotation = cameraTransform.rotation;
        float startFov = cinematicCamera.fieldOfView;

        List<BehaviourEnabledState> disabledBehaviours = DisableVictoryCameraBehaviours(cinematicCamera);
        DisableViewmodelCameras(cinematicCamera, disabledBehaviours);

        Bounds bossBounds = GetWorldBounds(defeatedBoss);
        Vector3 lookPoint = bossBounds.center;
        Vector3 orbitCenter = new(lookPoint.x, defeatedBoss.transform.position.y, lookPoint.z);
        float orbitRadius = Mathf.Max(_victoryCameraOrbitRadius, Mathf.Max(bossBounds.extents.x, bossBounds.extents.z) + 12f);
        float orbitHeight = Mathf.Max(_victoryCameraOrbitHeight, bossBounds.size.y * 0.45f);

        Vector3 startOffset = Vector3.ProjectOnPlane(startWorldPosition - orbitCenter, Vector3.up);
        if (startOffset.sqrMagnitude <= 0.001f)
            startOffset = -defeatedBoss.transform.forward;

        float orbitAngle = Mathf.Atan2(startOffset.z, startOffset.x) * Mathf.Rad2Deg;
        Vector3 targetPosition = GetOrbitPosition(orbitCenter, orbitAngle, orbitRadius, orbitHeight);
        Quaternion targetRotation = GetLookRotation(targetPosition, lookPoint, startWorldRotation);

        cameraTransform.SetParent(null, true);

        float introDuration = Mathf.Max(0.01f, _victoryCameraIntroDuration);
        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / introDuration));
            cameraTransform.position = Vector3.Lerp(startWorldPosition, targetPosition, t);
            cameraTransform.rotation = Quaternion.Slerp(startWorldRotation, targetRotation, t);
            yield return null;
        }

        float waiting = 0f;
        float fallbackDuration = Mathf.Max(0.1f, _victoryAnimationFallbackDuration);
        while (!isDeathAnimationComplete() && waiting < fallbackDuration)
        {
            waiting += Time.deltaTime;
            orbitAngle += _victoryCameraOrbitDegreesPerSecond * Time.deltaTime;
            Vector3 position = GetOrbitPosition(orbitCenter, orbitAngle, orbitRadius, orbitHeight);
            cameraTransform.position = position;
            cameraTransform.rotation = GetLookRotation(position, lookPoint, cameraTransform.rotation);
            yield return null;
        }

        cinematicCamera.fieldOfView = startFov;
    }

    private IEnumerator WaitForBossDeathAnimationFallback(System.Func<bool> isDeathAnimationComplete)
    {
        float elapsed = 0f;
        float fallbackDuration = Mathf.Max(0.1f, _victoryAnimationFallbackDuration);
        while (!isDeathAnimationComplete() && elapsed < fallbackDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static Vector3 GetOrbitPosition(Vector3 orbitCenter, float angleDegrees, float radius, float height)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector3 horizontal = new(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        return orbitCenter + horizontal * radius + Vector3.up * height;
    }

    private static Quaternion GetLookRotation(Vector3 cameraPosition, Vector3 lookPoint, Quaternion fallback)
    {
        Vector3 direction = lookPoint - cameraPosition;
        if (direction.sqrMagnitude <= 0.001f)
            return fallback;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Bounds GetWorldBounds(GameObject target)
    {
        bool hasBounds = false;
        Bounds bounds = new(target.transform.position + Vector3.up * 2f, new Vector3(4f, 4f, 4f));

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            return bounds;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    private List<BehaviourEnabledState> DisableVictoryCameraBehaviours(Camera cinematicCamera)
    {
        List<BehaviourEnabledState> states = new();

        if (_playerTransform != null)
        {
            AddEnabledBehaviourStates(states, _playerTransform.GetComponentsInChildren<PlayerHeadBob>(true));
            AddEnabledBehaviourStates(states, _playerTransform.GetComponentsInChildren<CameraSway>(true));
            AddEnabledBehaviourStates(states, _playerTransform.GetComponentsInChildren<CameraShaker>(true));
            AddEnabledBehaviourStates(states, _playerTransform.GetComponentsInChildren<FovKick>(true));
            AddEnabledBehaviourStates(states, _playerTransform.GetComponentsInChildren<ViewmodelController>(true));
        }

        if (cinematicCamera != null)
        {
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInParent<PlayerHeadBob>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInParent<CameraSway>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInParent<CameraShaker>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInParent<FovKick>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInParent<ViewmodelController>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInChildren<PlayerHeadBob>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInChildren<CameraSway>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInChildren<CameraShaker>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInChildren<FovKick>(true));
            AddEnabledBehaviourStates(states, cinematicCamera.GetComponentsInChildren<ViewmodelController>(true));
        }

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Behaviour != null)
                states[i].Behaviour.enabled = false;
        }

        return states;
    }

    private void DisableViewmodelCameras(Camera cinematicCamera, List<BehaviourEnabledState> states)
    {
        if (_playerTransform == null)
            return;

        Camera[] cameras = _playerTransform.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera == cinematicCamera || !camera.enabled)
                continue;

            AddEnabledBehaviourStates(states, camera);
            camera.enabled = false;
        }
    }

    private static void AddEnabledBehaviourStates<T>(List<BehaviourEnabledState> states, T[] behaviours) where T : Behaviour
    {
        if (behaviours == null)
            return;

        for (int i = 0; i < behaviours.Length; i++)
            AddEnabledBehaviourStates(states, behaviours[i]);
    }

    private static void AddEnabledBehaviourStates(List<BehaviourEnabledState> states, Behaviour behaviour)
    {
        if (states == null || behaviour == null || !behaviour.enabled)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Behaviour == behaviour)
                return;
        }

        states.Add(new BehaviourEnabledState
        {
            Behaviour = behaviour,
            WasEnabled = behaviour.enabled
        });
    }

    private static void RestoreVictoryCameraBehaviours(List<BehaviourEnabledState> states)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            BehaviourEnabledState state = states[i];
            if (state?.Behaviour != null)
                state.Behaviour.enabled = state.WasEnabled;
        }
    }

    private void UpdateClearedRoomFogPresence()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        bool playerInRoom = _playerTransform != null && IsPlayerStandingOnThisRoom(_playerTransform.position);
        if (playerInRoom)
        {
            _isPlayerInClearedRoom = true;
            SetClearedFogOwner(this);
            return;
        }

        if (!_isPlayerInClearedRoom && _clearedFogOwner != this)
            return;

        _isPlayerInClearedRoom = false;
        if (_clearedFogOwner == this)
        {
            _clearedFogOwner = null;
            RequestFogColor(_unclearedFogColor);
        }
    }

    private void SetClearedFogOwner(BossRoomController owner)
    {
        if (owner == null)
            return;

        _clearedFogOwner = owner;
        owner._isPlayerInClearedRoom = true;
        owner.RequestFogColor(owner._clearedFogColor);
    }

    private void RequestFogColor(Color targetColor)
    {
        RequestFogColor(this, targetColor, _clearedFogCrossfadeDuration);
    }

    public static void RequestFogColor(MonoBehaviour routineHost, Color targetColor, float crossfadeDuration)
    {
        if (routineHost == null)
            return;

        RenderSettings.fog = true;

        if (_hasFogTarget && ApproximatelySameColor(_currentFogTarget, targetColor))
            return;

        _currentFogTarget = targetColor;
        _hasFogTarget = true;

        if (_fogRoutineHost != null && _fogCrossfadeRoutine != null)
            _fogRoutineHost.StopCoroutine(_fogCrossfadeRoutine);

        if (!routineHost.isActiveAndEnabled)
        {
            RenderSettings.fogColor = targetColor;
            _fogRoutineHost = null;
            _fogCrossfadeRoutine = null;
            return;
        }

        _fogRoutineHost = routineHost;
        _fogCrossfadeRoutine = routineHost.StartCoroutine(FogCrossfadeRoutine(targetColor, crossfadeDuration, routineHost));
    }

    private static IEnumerator FogCrossfadeRoutine(Color targetColor, float crossfadeDuration, MonoBehaviour routineHost)
    {
        Color startColor = RenderSettings.fogColor;
        float duration = Mathf.Max(0.01f, crossfadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            RenderSettings.fogColor = Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        RenderSettings.fogColor = targetColor;
        if (_fogRoutineHost == routineHost)
        {
            _fogCrossfadeRoutine = null;
            _fogRoutineHost = null;
        }
    }

    private static bool ApproximatelySameColor(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) <= 0.001f &&
               Mathf.Abs(a.g - b.g) <= 0.001f &&
               Mathf.Abs(a.b - b.b) <= 0.001f &&
               Mathf.Abs(a.a - b.a) <= 0.001f;
    }

    private void ShowBossHealthBar(GameObject boss)
    {
        StopBossHealthBar();

        if (boss == null)
            return;

        _bossHealth = boss.GetComponent<HealthComponent>();
        if (_bossHealth == null)
            return;

        _bossHealth.OnDamaged += OnBossDamaged;
        _bossHealth.OnDied += StopBossHealthBar;

        GameObject canvasObject = new("BossHealthCanvas", typeof(Canvas), typeof(CanvasScaler));
        _bossHealthCanvasObject = canvasObject;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 19900;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObject = new("BossHealthRoot", typeof(RectTransform));
        rootObject.transform.SetParent(canvasObject.transform, false);
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -Mathf.Max(0f, _bossHealthBarTopOffset));
        root.sizeDelta = new Vector2(Mathf.Max(120f, _bossHealthBarSize.x), Mathf.Max(18f, _bossHealthBarSize.y));

        GameObject backObject = new("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backObject.transform.SetParent(root, false);
        RectTransform backRect = backObject.GetComponent<RectTransform>();
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = Vector2.zero;
        backRect.offsetMax = Vector2.zero;
        Image backImage = backObject.GetComponent<Image>();
        backImage.color = _bossHealthBackColor;
        backImage.raycastTarget = false;

        GameObject fillObject = new("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(root, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        _bossHealthFillRect = fillRect;
        _bossHealthFillImage = fillObject.GetComponent<Image>();
        _bossHealthFillImage.color = _bossHealthFillColor;
        _bossHealthFillImage.type = Image.Type.Simple;
        _bossHealthFillImage.raycastTarget = false;

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        _bossHealthLabel = labelObject.GetComponent<TextMeshProUGUI>();
        _bossHealthLabel.raycastTarget = false;
        _bossHealthLabel.alignment = TextAlignmentOptions.Center;
        _bossHealthLabel.fontSize = 24f;
        _bossHealthLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        _bossHealthLabel.color = Color.white;
        if (_splashFont != null)
            _bossHealthLabel.font = _splashFont;

        UpdateBossHealthBar();
    }

    private void OnBossDamaged(DamageInfo info)
    {
        UpdateBossHealthBar();
    }

    private void UpdateBossHealthBar()
    {
        if (_bossHealth == null)
            return;

        int maxHealth = Mathf.Max(1, _bossHealth.MaxHealth);
        float normalized = Mathf.Clamp01((float)_bossHealth.CurrentHealth / maxHealth);

        if (_bossHealthFillImage != null)
        {
            _bossHealthFillImage.enabled = normalized > 0f;
            _bossHealthFillImage.fillAmount = normalized;
        }

        if (_bossHealthFillRect != null)
        {
            _bossHealthFillRect.anchorMin = Vector2.zero;
            _bossHealthFillRect.anchorMax = new Vector2(normalized, 1f);
            _bossHealthFillRect.offsetMin = new Vector2(3f, 3f);
            _bossHealthFillRect.offsetMax = normalized > 0.01f ? new Vector2(-3f, -3f) : new Vector2(0f, -3f);
        }

        if (_bossHealthLabel != null)
            _bossHealthLabel.text = $"BOSS - PHASE {_bossPhaseIndex}/{Mathf.Max(1, _bossPhaseCount)}  {_bossHealth.CurrentHealth} / {maxHealth}";
    }

    private void StopBossHealthBar()
    {
        if (_bossHealth != null)
        {
            _bossHealth.OnDamaged -= OnBossDamaged;
            _bossHealth.OnDied -= StopBossHealthBar;
            _bossHealth = null;
        }

        _bossHealthFillImage = null;
        _bossHealthFillRect = null;
        _bossHealthLabel = null;

        if (_bossHealthCanvasObject != null)
        {
            Destroy(_bossHealthCanvasObject);
            _bossHealthCanvasObject = null;
        }
    }

    private void ShowRoomSplash()
    {
        StopRoomSplash();

        string text = "ELIMINATE ALL!";
        Color color = _eliminateSplashColor;
        _splashRoutine = StartCoroutine(RoomSplashRoutine(text, color));
    }

    private void ShowPhaseSplash(int phaseIndex)
    {
        StopRoomSplash();
        _splashRoutine = StartCoroutine(RoomSplashRoutine(GetBossPhaseSplashText(phaseIndex), _phaseSplashColor));
    }

    private string GetBossPhaseSplashText(int phaseIndex)
    {
        return phaseIndex switch
        {
            1 => "PHASE 1 - AWAKENING",
            2 => "PHASE 2 - SHIELD BREAK",
            3 => "PHASE 3 - FRENZY",
            _ => $"PHASE {phaseIndex}"
        };
    }

    private void ShowProceedSplash()
    {
        StopRoomSplash();

        _splashRoutine = StartCoroutine(RoomSplashRoutine("PROCEED", _proceedSplashColor));
    }

    private void StopRoomSplash()
    {
        if (_splashRoutine != null)
        {
            StopCoroutine(_splashRoutine);
            _splashRoutine = null;
        }

        if (_splashCanvasObject != null)
        {
            Destroy(_splashCanvasObject);
            _splashCanvasObject = null;
        }
    }

    private IEnumerator RoomSplashRoutine(string splashText, Color baseColor)
    {
        GameObject canvasObject = new("EnemyRoomSplashCanvas", typeof(Canvas), typeof(CanvasScaler));
        _splashCanvasObject = canvasObject;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new("RoomTypeSplash", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 64f;
        label.fontSizeMax = _splashMaxFontSize;
        label.text = splashText;
        label.fontStyle = FontStyles.UpperCase;

        if (_splashFont != null)
            label.font = _splashFont;

        float duration = Mathf.Max(0.1f, _splashDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float intro = Mathf.Clamp01(t / 0.18f);
            float outro = Mathf.Clamp01((t - 0.68f) / 0.32f);
            float alpha = t < 0.68f ? 1f : Mathf.Lerp(1f, 0f, outro);
            float punch = Mathf.Sin(Mathf.Clamp01(t / 0.32f) * Mathf.PI);
            float pulse = Mathf.Sin(t * Mathf.PI * 18f);
            float scale = Mathf.Lerp(0.45f, 1f, EaseOutBack(intro)) + punch * 0.18f + pulse * 0.025f;
            float shake = Mathf.Lerp(18f, 2f, t) * (1f - outro);

            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));

            bool flash = Mathf.FloorToInt(elapsed * 18f) % 2 == 0;
            Color flashColor = flash ? Color.white : baseColor;
            label.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

            yield return null;
        }

        Destroy(canvasObject);
        if (_splashCanvasObject == canvasObject)
            _splashCanvasObject = null;

        _splashRoutine = null;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    private void SetDoorsOpen(bool open)
    {
        if (open)
            HideDoors();
        else
            ShowDoors();
    }

    private void ShowDoors()
    {
        if (_doors.Count == 0)
            CreateDoors();

        foreach (RuntimeDoor runtimeDoor in _doors)
        {
            if (runtimeDoor?.Door == null)
                continue;

            runtimeDoor.Door.SetInteractionLocked(true);
            SetDoorCollidersEnabled(runtimeDoor, true);
            SetDoorRenderersEnabled(runtimeDoor, true);
            StartDoorFade(runtimeDoor, 1f, disableRenderersAfterFade: false, destroyAfterFade: false);
        }
    }

    private void HideDoors()
    {
        foreach (RuntimeDoor runtimeDoor in _doors)
        {
            if (runtimeDoor?.Door == null)
                continue;

            runtimeDoor.Door.SetInteractionLocked(true);
            SetDoorCollidersEnabled(runtimeDoor, false);
            SetDoorRenderersEnabled(runtimeDoor, true);
            StartDoorFade(runtimeDoor, 0f, disableRenderersAfterFade: true, destroyAfterFade: true);
        }
    }

    private void CacheLocalBounds()
    {
        bool hasBounds = false;
        _localBounds = new Bounds(Vector3.zero, Vector3.one);

        EncapsulateRenderers(ref hasBounds);
        EncapsulateColliders(ref hasBounds);

        if (!hasBounds)
            _localBounds = new Bounds(Vector3.zero, new Vector3(40f, 8f, 40f));

        _localBounds.Expand(new Vector3(-4f, 0f, -4f));
        if (_localBounds.size.x <= 1f || _localBounds.size.z <= 1f)
            _localBounds = new Bounds(Vector3.zero, new Vector3(40f, 8f, 40f));
    }

    private void EncapsulateRenderers(ref bool hasBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
            EncapsulateWorldBounds(renderer.bounds, ref hasBounds);
    }

    private void EncapsulateColliders(ref bool hasBounds)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger)
                continue;

            EncapsulateWorldBounds(collider.bounds, ref hasBounds);
        }
    }

    private void EncapsulateWorldBounds(Bounds worldBounds, ref bool hasBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        EncapsulateLocalPoint(new Vector3(min.x, min.y, min.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, min.y, max.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, max.y, min.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, max.y, max.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, min.y, min.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, min.y, max.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, max.y, min.z), ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, max.y, max.z), ref hasBounds);
    }

    private void EncapsulateLocalPoint(Vector3 worldPoint, ref bool hasBounds)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            _localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        _localBounds.Encapsulate(localPoint);
    }

    private void CacheDoorPositions()
    {
        _doorPositions.Clear();

        if (_mazePrefab == null)
            return;

        IReadOnlyList<MazeSocket> sockets = _mazePrefab.Sockets;
        for (int i = 0; i < sockets.Count; i++)
            _doorPositions.Add(transform.TransformPoint(sockets[i].Position));
    }

    private void CachePlayableLocalHeights()
    {
        _playableLocalHeights.Clear();

        if (_mazePrefab == null)
            return;

        IReadOnlyList<MazeSocket> sockets = _mazePrefab.Sockets;
        for (int i = 0; i < sockets.Count; i++)
            AddPlayableLocalHeight(sockets[i].Position.y);
    }

    private void AddPlayableLocalHeight(float height)
    {
        for (int i = 0; i < _playableLocalHeights.Count; i++)
        {
            if (Mathf.Abs(_playableLocalHeights[i] - height) <= 0.25f)
                return;
        }

        _playableLocalHeights.Add(height);
    }

    private void CacheRoomColliders()
    {
        _roomColliders.Clear();
        List<Collider> fallbackColliders = new();

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (IsLikelyPlayableFloorCollider(collider))
                _roomColliders.Add(collider);
            else
                fallbackColliders.Add(collider);
        }

        if (_roomColliders.Count == 0)
            _roomColliders.AddRange(fallbackColliders);
    }

    private bool IsLikelyPlayableFloorCollider(Collider collider)
    {
        if (_playableLocalHeights.Count == 0)
            return true;

        Bounds bounds = collider.bounds;
        if (bounds.size.y > Mathf.Max(1f, _spawnPlayableHeightTolerance * 0.75f))
            return false;

        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        for (int i = 0; i < _playableLocalHeights.Count; i++)
        {
            if (Mathf.Abs(localCenter.y - _playableLocalHeights[i]) <= _spawnPlayableHeightTolerance)
                return true;
        }

        return false;
    }

    private void CreateDoors()
    {
        if (_doorPrefab == null || _mazePrefab == null)
            return;

        IReadOnlyList<MazeSocket> sockets = _mazePrefab.Sockets;
        for (int i = 0; i < sockets.Count; i++)
        {
            MazeSocket socket = sockets[i];
            Vector3 socketPosition = transform.TransformPoint(socket.Position);
            Vector3 socketForward = transform.TransformDirection(socket.Forward).normalized;
            if (socketForward.sqrMagnitude <= 0.001f)
                socketForward = transform.forward;

            Vector3 doorPosition = socketPosition + Vector3.up * _doorVerticalOffset;
            Quaternion doorRotation = Quaternion.LookRotation(socketForward, Vector3.up);
            Door door = Instantiate(_doorPrefab, doorPosition, doorRotation, transform);
            door.transform.localScale = _generatedDoorScale;
            door.SetInteractionLocked(true);

            RuntimeDoor runtimeDoor = CreateRuntimeDoor(door);
            SetDoorCollidersEnabled(runtimeDoor, false);
            SetDoorAlpha(runtimeDoor, 0f);
            SetDoorRenderersEnabled(runtimeDoor, false);
            _doors.Add(runtimeDoor);
        }
    }

    private RuntimeDoor CreateRuntimeDoor(Door door)
    {
        var runtimeDoor = new RuntimeDoor
        {
            Door = door,
            Renderers = door.GetComponentsInChildren<Renderer>(true),
            Colliders = door.GetComponentsInChildren<Collider>(true),
        };

        foreach (Renderer renderer in runtimeDoor.Renderers)
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
                PrepareMaterialForFade(materials[i]);
        }

        return runtimeDoor;
    }

    private void StartDoorFade(RuntimeDoor runtimeDoor, float targetAlpha, bool disableRenderersAfterFade, bool destroyAfterFade)
    {
        if (runtimeDoor.FadeRoutine != null)
            StopCoroutine(runtimeDoor.FadeRoutine);

        runtimeDoor.FadeRoutine = StartCoroutine(DoorFadeRoutine(runtimeDoor, targetAlpha, disableRenderersAfterFade, destroyAfterFade));
    }

    private IEnumerator DoorFadeRoutine(RuntimeDoor runtimeDoor, float targetAlpha, bool disableRenderersAfterFade, bool destroyAfterFade)
    {
        float startingAlpha = GetDoorAlpha(runtimeDoor);
        float duration = Mathf.Max(0.01f, _doorFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetDoorAlpha(runtimeDoor, Mathf.Lerp(startingAlpha, targetAlpha, t));
            yield return null;
        }

        SetDoorAlpha(runtimeDoor, targetAlpha);

        if (disableRenderersAfterFade)
            SetDoorRenderersEnabled(runtimeDoor, false);

        runtimeDoor.FadeRoutine = null;

        if (destroyAfterFade)
        {
            if (runtimeDoor.Door != null)
                Destroy(runtimeDoor.Door.gameObject);

            _doors.Remove(runtimeDoor);
        }
    }

    private static void SetDoorRenderersEnabled(RuntimeDoor runtimeDoor, bool enabled)
    {
        foreach (Renderer renderer in runtimeDoor.Renderers)
        {
            if (renderer != null)
                renderer.enabled = enabled;
        }
    }

    private static void SetDoorCollidersEnabled(RuntimeDoor runtimeDoor, bool enabled)
    {
        foreach (Collider collider in runtimeDoor.Colliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    private static float GetDoorAlpha(RuntimeDoor runtimeDoor)
    {
        foreach (Renderer renderer in runtimeDoor.Renderers)
        {
            if (renderer == null)
                continue;

            foreach (Material material in renderer.materials)
            {
                if (material == null)
                    continue;

                if (material.HasProperty("_BaseColor"))
                    return material.GetColor("_BaseColor").a;

                if (material.HasProperty("_Color"))
                    return material.color.a;
            }
        }

        return 1f;
    }

    private static void SetDoorAlpha(RuntimeDoor runtimeDoor, float alpha)
    {
        foreach (Renderer renderer in runtimeDoor.Renderers)
        {
            if (renderer == null)
                continue;

            foreach (Material material in renderer.materials)
            {
                if (material == null)
                    continue;

                if (material.HasProperty("_BaseColor"))
                {
                    Color baseColor = material.GetColor("_BaseColor");
                    baseColor.a = alpha;
                    material.SetColor("_BaseColor", baseColor);
                }

                if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = alpha;
                    material.color = color;
                }
            }
        }
    }

    private static void PrepareMaterialForFade(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void GenerateSpawnCandidates()
    {
        _spawnCandidates.Clear();
        _recentSpawnPositions.Clear();
        _loggedMissingSpawnPoint = false;

        FillRandomSpawnCandidates(rejectNearDoors: true, rejectNearCenter: true);

        if (_spawnCandidates.Count < Mathf.Min(4, _spawnCandidateTarget))
            FillRandomSpawnCandidates(rejectNearDoors: true, rejectNearCenter: false);

        if (_spawnCandidates.Count == 0)
            FillRandomSpawnCandidates(rejectNearDoors: false, rejectNearCenter: false);

        if (_spawnCandidates.Count == 0)
            Debug.LogWarning($"[BossRoomController] No NavMesh spawn candidates found for {gameObject.name}. Boss spawning will skip until a valid point exists.", this);
    }

    private void FillRandomSpawnCandidates(bool rejectNearDoors, bool rejectNearCenter)
    {
        float sampleRadius = Mathf.Max(1f, Mathf.Min(_navMeshSampleRadius, _spawnPlayableHeightTolerance + 1f));

        for (int i = 0; i < _spawnSampleAttempts && _spawnCandidates.Count < _spawnCandidateTarget; i++)
        {
            float localY = _localBounds.center.y;
            if (_playableLocalHeights.Count > 0)
                localY = _playableLocalHeights[Random.Range(0, _playableLocalHeights.Count)];

            Vector3 local = new(
                Random.Range(_localBounds.min.x, _localBounds.max.x),
                localY,
                Random.Range(_localBounds.min.z, _localBounds.max.z));

            Vector3 world = transform.TransformPoint(local);
            if (TryFindRoomColliderFloor(world, out Vector3 floorPoint))
                world = floorPoint;

            if (!NavMesh.SamplePosition(world, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                continue;

            TryAddSpawnCandidate(hit.position, rejectNearDoors, rejectNearCenter);
        }
    }

    private bool TryFindRoomColliderFloor(Vector3 worldPosition, out Vector3 floorPoint)
    {
        floorPoint = default;
        if (_roomColliders.Count == 0)
            return false;

        Vector3 rayOrigin = worldPosition + Vector3.up * Mathf.Max(8f, _localBounds.size.y + 4f);
        Ray ray = new(rayOrigin, Vector3.down);
        float maxDistance = Mathf.Max(16f, _localBounds.size.y * 2f + 12f);
        bool found = false;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < _roomColliders.Count; i++)
        {
            Collider collider = _roomColliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (!collider.Raycast(ray, out RaycastHit hit, maxDistance))
                continue;

            if (!IsOnPlayableHeight(hit.point, _spawnPlayableHeightTolerance))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            floorPoint = hit.point;
            found = true;
        }

        return found;
    }

    private bool TryAddSpawnCandidate(Vector3 position, bool rejectNearDoors, bool rejectNearCenter)
    {
        if (!IsValidSpawnCandidate(position, rejectNearDoors, rejectNearCenter))
            return false;

        for (int i = 0; i < _spawnCandidates.Count; i++)
        {
            Vector3 offset = Vector3.ProjectOnPlane(position - _spawnCandidates[i], Vector3.up);
            if (offset.sqrMagnitude < 1f)
                return false;
        }

        _spawnCandidates.Add(position);
        return true;
    }

    private bool IsValidSpawnCandidate(Vector3 position, bool rejectNearDoors, bool rejectNearCenter)
    {
        if (!IsWithinLocalBounds(position))
            return false;

        if (!IsOnPlayableHeight(position, _spawnPlayableHeightTolerance))
            return false;

        if (!IsSupportedByThisRoomFloor(position))
            return false;

        if (rejectNearDoors && IsTooCloseToDoors(position))
            return false;

        if (rejectNearCenter && IsTooCloseToRoomCenter(position))
            return false;

        return true;
    }

    private bool IsSupportedByThisRoomFloor(Vector3 position)
    {
        if (_roomColliders.Count == 0)
            return true;

        if (!TryFindRoomColliderFloor(position, out Vector3 floorPoint))
            return false;

        Vector3 horizontalOffset = Vector3.ProjectOnPlane(position - floorPoint, Vector3.up);
        return horizontalOffset.sqrMagnitude <= 0.75f * 0.75f &&
               Mathf.Abs(position.y - floorPoint.y) <= _spawnPlayableHeightTolerance;
    }

    private bool IsWithinLocalBounds(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return local.x >= _localBounds.min.x && local.x <= _localBounds.max.x
            && local.z >= _localBounds.min.z && local.z <= _localBounds.max.z;
    }

    private bool IsOnPlayableHeight(Vector3 worldPosition, float tolerance)
    {
        if (_playableLocalHeights.Count == 0)
            return true;

        float localY = transform.InverseTransformPoint(worldPosition).y;
        float maxDistance = Mathf.Max(0.1f, tolerance);
        for (int i = 0; i < _playableLocalHeights.Count; i++)
        {
            if (Mathf.Abs(localY - _playableLocalHeights[i]) <= maxDistance)
                return true;
        }

        return false;
    }

    private bool IsTooCloseToDoors(Vector3 position)
    {
        return IsNearDoorSocket(position, _doorExclusionRadius);
    }

    private bool IsTooCloseToRoomCenter(Vector3 position)
    {
        Vector3 center = transform.TransformPoint(_localBounds.center);
        Vector3 offset = Vector3.ProjectOnPlane(position - center, Vector3.up);
        return offset.magnitude < _centerExclusionRadius;
    }
}

[DisallowMultipleComponent]
public class BossShieldCrystal : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _sharedMaterial;
    private static Material _flashMaterial;

    private BossRoomController _room;
    private HealthComponent _health;
    private Renderer _renderer;
    private Light _light;
    private GameObject _healthBarObject;
    private Image _healthBarFill;
    private RectTransform _healthBarFillRect;
    private TextMeshProUGUI _healthBarText;
    private Camera _mainCamera;
    private Vector3 _hoverBasePosition;
    private Vector3 _rotationAxis;
    private float _bobSeed;
    private float _damageFlashRemaining;
    private bool _notifiedDeath;

    public void Configure(
        BossRoomController room,
        int maxHealth)
    {
        _room = room;
        _hoverBasePosition = transform.position;
        _rotationAxis = Random.onUnitSphere;
        if (Mathf.Abs(Vector3.Dot(_rotationAxis, Vector3.up)) > 0.95f)
            _rotationAxis = Vector3.right;
        _bobSeed = Random.value * Mathf.PI * 2f;

        _health = GetComponent<HealthComponent>();
        if (_health == null)
            _health = gameObject.AddComponent<HealthComponent>();
        _health.SetMaxHealth(Mathf.Max(1, maxHealth), refill: true);
        _health.OnDamaged += OnCrystalDamaged;
        _health.OnDied += OnCrystalDied;

        EnsureVisuals();
        UpdateHealthBar();
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDamaged -= OnCrystalDamaged;
            _health.OnDied -= OnCrystalDied;
        }

        if (_healthBarObject != null)
            Destroy(_healthBarObject);
    }

    private void Update()
    {
        transform.position = _hoverBasePosition + Vector3.up * (Mathf.Sin(Time.time * 2.25f + _bobSeed) * 0.35f);
        transform.Rotate(_rotationAxis, 120f * Time.deltaTime, Space.World);

        if (_light != null)
            _light.intensity = 8f + Mathf.Sin(Time.time * 7f + _bobSeed) * 2.5f;

        UpdateDamageFlash();
        UpdateHealthBarTransform();
    }

    private void OnCrystalDamaged(DamageInfo info)
    {
        _damageFlashRemaining = 0.16f;
        UpdateHealthBar();

        if (_health == null || _health.CurrentHealth > 0)
            _room?.PlayBossShieldCrystalHitSound();

        if (_health != null && _health.IsAlive && _health.CurrentHealth <= 0)
            OnCrystalDied();
    }

    private void UpdateDamageFlash()
    {
        if (_renderer == null)
            return;

        if (_damageFlashRemaining > 0f)
        {
            _damageFlashRemaining -= Time.deltaTime;
            _renderer.sharedMaterial = GetFlashMaterial();
        }
        else if (_renderer.sharedMaterial != GetSharedMaterial())
        {
            _renderer.sharedMaterial = GetSharedMaterial();
        }
    }

    private void OnCrystalDied()
    {
        if (_notifiedDeath)
            return;

        _notifiedDeath = true;
        _room?.PlayBossShieldCrystalDestroyedSound();
        _room?.OnBossShieldCrystalDestroyed(this);
        Destroy(gameObject);
    }

    private void EnsureVisuals()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
            _renderer.sharedMaterial = GetSharedMaterial();

        _light = GetComponent<Light>();
        if (_light == null)
            _light = gameObject.AddComponent<Light>();

        _light.type = LightType.Point;
        _light.color = new Color(0.25f, 0.95f, 1f, 1f);
        _light.range = 16f;
        _light.shadows = LightShadows.None;

        EnsureHealthBar();
        UpdateHealthBarTransform();
    }

    private void EnsureHealthBar()
    {
        if (_healthBarObject != null)
            return;

        _healthBarObject = new GameObject($"{name}_HealthBar", typeof(Canvas), typeof(CanvasScaler));
        _healthBarObject.transform.SetParent(transform.parent, true);

        Canvas canvas = _healthBarObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 80;

        RectTransform canvasRect = _healthBarObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(130f, 18f);
        canvasRect.localScale = Vector3.one * 0.018f;

        GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(_healthBarObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0f, 0.04f, 0.07f, 0.82f);

        GameObject fillObject = new("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.06f, 0.24f);
        fillRect.anchorMax = new Vector2(0.94f, 0.76f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        _healthBarFillRect = fillRect;
        _healthBarFill = fillObject.GetComponent<Image>();
        _healthBarFill.color = new Color(0.1f, 0.95f, 1f, 0.95f);
        _healthBarFill.type = Image.Type.Simple;

        GameObject textObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(_healthBarObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        _healthBarText = textObject.GetComponent<TextMeshProUGUI>();
        _healthBarText.alignment = TextAlignmentOptions.Center;
        _healthBarText.fontSize = 10f;
        _healthBarText.color = Color.white;
        _healthBarText.raycastTarget = false;

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (_health == null)
            return;

        EnsureHealthBar();

        float ratio = _health.MaxHealth > 0 ? Mathf.Clamp01((float)_health.CurrentHealth / _health.MaxHealth) : 0f;
        if (_healthBarFill != null)
            _healthBarFill.enabled = ratio > 0f;
        if (_healthBarFillRect != null)
            _healthBarFillRect.anchorMax = new Vector2(Mathf.Lerp(0.06f, 0.94f, ratio), 0.76f);
        if (_healthBarText != null)
            _healthBarText.text = $"{Mathf.Max(0, _health.CurrentHealth)} / {_health.MaxHealth}";
    }

    private void UpdateHealthBarTransform()
    {
        if (_healthBarObject == null)
            return;

        _healthBarObject.transform.position = transform.position + Vector3.up * Mathf.Max(1.8f, transform.localScale.y * 0.9f);

        if (_mainCamera == null)
            _mainCamera = Camera.main;
        if (_mainCamera != null)
            _healthBarObject.transform.rotation = _mainCamera.transform.rotation;
    }

    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        _sharedMaterial = new Material(shader)
        {
            name = "BossShieldCrystal_Runtime"
        };

        Color baseColor = new(0.15f, 0.95f, 1f, 1f);
        Color emissionColor = new(0.6f, 4f, 4.6f, 1f);

        if (_sharedMaterial.HasProperty(BaseColorId))
            _sharedMaterial.SetColor(BaseColorId, baseColor);
        if (_sharedMaterial.HasProperty(ColorId))
            _sharedMaterial.SetColor(ColorId, baseColor);
        if (_sharedMaterial.HasProperty(EmissionColorId))
        {
            _sharedMaterial.EnableKeyword("_EMISSION");
            _sharedMaterial.SetColor(EmissionColorId, emissionColor);
        }

        _sharedMaterial.hideFlags = HideFlags.HideAndDontSave;
        return _sharedMaterial;
    }

    private static Material GetFlashMaterial()
    {
        if (_flashMaterial != null)
            return _flashMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return GetSharedMaterial();

        _flashMaterial = new Material(shader)
        {
            name = "BossShieldCrystal_Flash_Runtime"
        };

        Color baseColor = new(1f, 0.05f, 0.02f, 1f);
        Color emissionColor = new(5f, 0.1f, 0.05f, 1f);

        if (_flashMaterial.HasProperty(BaseColorId))
            _flashMaterial.SetColor(BaseColorId, baseColor);
        if (_flashMaterial.HasProperty(ColorId))
            _flashMaterial.SetColor(ColorId, baseColor);
        if (_flashMaterial.HasProperty(EmissionColorId))
        {
            _flashMaterial.EnableKeyword("_EMISSION");
            _flashMaterial.SetColor(EmissionColorId, emissionColor);
        }

        _flashMaterial.hideFlags = HideFlags.HideAndDontSave;
        return _flashMaterial;
    }

}

[DisallowMultipleComponent]
public class BossShieldVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

    private static Material _sharedMaterial;
    private static Material _outerGlowMaterial;
    private static Material _bodyGlowMaterial;

    private Transform _boss;
    private readonly List<GameObject> _outlineObjects = new();
    private readonly List<GameObject> _skinnedOutlineObjects = new();
    private readonly List<SkinnedOutlineState> _skinnedOutlines = new();
    private Light _outlineLight;
    private const float INNER_OUTLINE_SCALE = 1.13f;
    private const float OUTER_OUTLINE_SCALE = 1.22f;
    private const float OUTLINE_PULSE = 0.04f;

    public void Configure(Transform boss)
    {
        _boss = boss;
        EnsureOutlineObjects();
        EnsureOutlineLight();
    }

    private void OnDestroy()
    {
        for (int i = _outlineObjects.Count - 1; i >= 0; i--)
        {
            if (_outlineObjects[i] != null)
                Destroy(_outlineObjects[i]);
        }

        _outlineObjects.Clear();
        _skinnedOutlineObjects.Clear();

        for (int i = _skinnedOutlines.Count - 1; i >= 0; i--)
            _skinnedOutlines[i].Dispose();
        _skinnedOutlines.Clear();
    }

    private void LateUpdate()
    {
        if (_boss == null)
            return;

        float pulse = Mathf.Sin(Time.time * 5.5f) * OUTLINE_PULSE;
        UpdateSkinnedOutlines(pulse);

        for (int i = _outlineObjects.Count - 1; i >= 0; i--)
        {
            GameObject outline = _outlineObjects[i];
            if (outline == null)
            {
                _outlineObjects.RemoveAt(i);
                continue;
            }

            if (_skinnedOutlineObjects.Contains(outline))
                continue;

            float baseScale = outline.name.Contains("_OuterShieldOutline") ? OUTER_OUTLINE_SCALE : INNER_OUTLINE_SCALE;
            outline.transform.localScale = Vector3.one * (baseScale + pulse);
        }

        if (_outlineLight != null)
            _outlineLight.intensity = 8f + Mathf.Sin(Time.time * 6.5f) * 2f;
    }

    private void EnsureOutlineObjects()
    {
        if (_boss == null || _outlineObjects.Count > 0)
            return;

        Renderer[] renderers = _boss.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (renderer.GetComponentInParent<BossShieldVisual>() != this)
                continue;

            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null && skinned.sharedMesh != null)
            {
                CreateSkinnedBodyGlow(skinned);
                CreateBakedSkinnedOutline(skinned, inner: true);
                CreateBakedSkinnedOutline(skinned, inner: false);
                continue;
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                CreateMeshBodyGlow(renderer, meshFilter);
                CreateMeshOutline(renderer, meshFilter, inner: true);
                CreateMeshOutline(renderer, meshFilter, inner: false);
            }
        }
    }

    private void CreateSkinnedBodyGlow(SkinnedMeshRenderer source)
    {
        GameObject glow = new($"{source.gameObject.name}_FullBodyShieldGlow");
        glow.transform.SetParent(source.transform.parent, false);
        glow.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
        glow.transform.localScale = source.transform.localScale;

        SkinnedMeshRenderer glowRenderer = glow.AddComponent<SkinnedMeshRenderer>();
        glowRenderer.sharedMesh = source.sharedMesh;
        glowRenderer.bones = source.bones;
        glowRenderer.rootBone = source.rootBone;
        glowRenderer.quality = source.quality;
        glowRenderer.updateWhenOffscreen = true;
        glowRenderer.localBounds = source.localBounds;
        glowRenderer.sharedMaterials = CreateRepeatedMaterialArray(source.sharedMaterials.Length, GetBodyGlowMaterial());
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;
        glowRenderer.forceRenderingOff = false;

        _outlineObjects.Add(glow);
    }

    private void CreateMeshBodyGlow(Renderer source, MeshFilter sourceMeshFilter)
    {
        GameObject glow = new($"{source.gameObject.name}_FullBodyShieldGlow");
        glow.transform.SetParent(source.transform.parent, false);
        glow.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
        glow.transform.localScale = source.transform.localScale;

        MeshFilter glowMeshFilter = glow.AddComponent<MeshFilter>();
        glowMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        MeshRenderer glowRenderer = glow.AddComponent<MeshRenderer>();
        glowRenderer.sharedMaterials = CreateRepeatedMaterialArray(source.sharedMaterials.Length, GetBodyGlowMaterial());
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;
        glowRenderer.forceRenderingOff = false;

        _outlineObjects.Add(glow);
    }

    private static Material[] CreateRepeatedMaterialArray(int length, Material material)
    {
        int count = Mathf.Max(1, length);
        Material[] materials = new Material[count];
        for (int i = 0; i < count; i++)
            materials[i] = material;
        return materials;
    }

    private void CreateBakedSkinnedOutline(SkinnedMeshRenderer source, bool inner)
    {
        GameObject outline = new($"{source.gameObject.name}_{(inner ? "InnerShieldOutline" : "OuterShieldOutline")}");
        outline.transform.SetParent(transform, false);
        outline.transform.localScale = Vector3.one * (inner ? INNER_OUTLINE_SCALE : OUTER_OUTLINE_SCALE);

        Mesh bakedMesh = new()
        {
            name = $"{source.gameObject.name}_{(inner ? "Inner" : "Outer")}_ShieldBake"
        };
        bakedMesh.hideFlags = HideFlags.HideAndDontSave;

        MeshFilter outlineMeshFilter = outline.AddComponent<MeshFilter>();
        outlineMeshFilter.sharedMesh = bakedMesh;

        MeshRenderer outlineRenderer = outline.AddComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = inner ? GetSharedMaterial() : GetOuterGlowMaterial();
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;

        _skinnedOutlines.Add(new SkinnedOutlineState(source, outline.transform, bakedMesh, inner ? INNER_OUTLINE_SCALE : OUTER_OUTLINE_SCALE));
        _skinnedOutlineObjects.Add(outline);
        _outlineObjects.Add(outline);
    }

    private void UpdateSkinnedOutlines(float pulse)
    {
        for (int i = _skinnedOutlines.Count - 1; i >= 0; i--)
        {
            SkinnedOutlineState state = _skinnedOutlines[i];
            if (state.Source == null || state.Transform == null || state.Mesh == null)
            {
                state.Dispose();
                _skinnedOutlines.RemoveAt(i);
                continue;
            }

            state.Source.BakeMesh(state.Mesh);
            state.Transform.SetPositionAndRotation(state.Source.transform.position, state.Source.transform.rotation);
            Vector3 sourceScale = state.Source.transform.lossyScale;
            float outlineScale = state.BaseScale + pulse;
            state.Transform.localScale = new Vector3(
                sourceScale.x * outlineScale,
                sourceScale.y * outlineScale,
                sourceScale.z * outlineScale);
        }
    }

    private void CreateMeshOutline(Renderer source, MeshFilter sourceMeshFilter, bool inner)
    {
        GameObject outline = new($"{source.gameObject.name}_{(inner ? "InnerShieldOutline" : "OuterShieldOutline")}");
        outline.transform.SetParent(source.transform, false);
        outline.transform.localPosition = Vector3.zero;
        outline.transform.localRotation = Quaternion.identity;
        outline.transform.localScale = Vector3.one * (inner ? INNER_OUTLINE_SCALE : OUTER_OUTLINE_SCALE);

        MeshFilter outlineMeshFilter = outline.AddComponent<MeshFilter>();
        outlineMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        MeshRenderer outlineRenderer = outline.AddComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = inner ? GetSharedMaterial() : GetOuterGlowMaterial();
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;

        _outlineObjects.Add(outline);
    }

    private void EnsureOutlineLight()
    {
        if (_boss == null || _outlineLight != null)
            return;

        GameObject lightObject = new("BossShield_BlueOutlineLight");
        lightObject.transform.SetParent(_boss, false);
        lightObject.transform.localPosition = Vector3.up * 1.8f;

        _outlineLight = lightObject.AddComponent<Light>();
        _outlineLight.type = LightType.Point;
        _outlineLight.color = new Color(0.12f, 0.72f, 1f, 1f);
        _outlineLight.intensity = 8f;
        _outlineLight.range = 8f;
        _outlineLight.shadows = LightShadows.None;

        _outlineObjects.Add(lightObject);
    }

    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        _sharedMaterial = new Material(shader)
        {
            name = "BossShieldVisual_Runtime"
        };

        Color baseColor = new(0.05f, 0.75f, 1f, 0.72f);
        Color emissionColor = new(0.2f, 4.5f, 6f, 0.72f);

        if (_sharedMaterial.HasProperty(BaseColorId))
            _sharedMaterial.SetColor(BaseColorId, baseColor);
        if (_sharedMaterial.HasProperty(ColorId))
            _sharedMaterial.SetColor(ColorId, baseColor);
        if (_sharedMaterial.HasProperty(EmissionColorId))
        {
            _sharedMaterial.EnableKeyword("_EMISSION");
            _sharedMaterial.SetColor(EmissionColorId, emissionColor);
        }

        ConfigureTransparentMaterial(_sharedMaterial);
        _sharedMaterial.hideFlags = HideFlags.HideAndDontSave;
        return _sharedMaterial;
    }

    private static Material GetOuterGlowMaterial()
    {
        if (_outerGlowMaterial != null)
            return _outerGlowMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        _outerGlowMaterial = new Material(shader)
        {
            name = "BossShieldVisual_OuterGlow_Runtime"
        };

        Color baseColor = new(0.02f, 0.42f, 1f, 0.42f);
        Color emissionColor = new(0.12f, 3.2f, 8f, 0.42f);

        if (_outerGlowMaterial.HasProperty(BaseColorId))
            _outerGlowMaterial.SetColor(BaseColorId, baseColor);
        if (_outerGlowMaterial.HasProperty(ColorId))
            _outerGlowMaterial.SetColor(ColorId, baseColor);
        if (_outerGlowMaterial.HasProperty(EmissionColorId))
        {
            _outerGlowMaterial.EnableKeyword("_EMISSION");
            _outerGlowMaterial.SetColor(EmissionColorId, emissionColor);
        }

        ConfigureTransparentMaterial(_outerGlowMaterial);
        _outerGlowMaterial.hideFlags = HideFlags.HideAndDontSave;
        return _outerGlowMaterial;
    }

    private static Material GetBodyGlowMaterial()
    {
        if (_bodyGlowMaterial != null)
            return _bodyGlowMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        _bodyGlowMaterial = new Material(shader)
        {
            name = "BossShieldVisual_FullBodyGlow_Runtime"
        };

        Color baseColor = new(0.03f, 0.65f, 1f, 0.32f);
        Color emissionColor = new(0.12f, 4.4f, 7f, 0.32f);

        if (_bodyGlowMaterial.HasProperty(BaseColorId))
            _bodyGlowMaterial.SetColor(BaseColorId, baseColor);
        if (_bodyGlowMaterial.HasProperty(ColorId))
            _bodyGlowMaterial.SetColor(ColorId, baseColor);
        if (_bodyGlowMaterial.HasProperty(EmissionColorId))
        {
            _bodyGlowMaterial.EnableKeyword("_EMISSION");
            _bodyGlowMaterial.SetColor(EmissionColorId, emissionColor);
        }

        ConfigureTransparentBodyGlowMaterial(_bodyGlowMaterial);
        _bodyGlowMaterial.hideFlags = HideFlags.HideAndDontSave;
        return _bodyGlowMaterial;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
        if (material.HasProperty(ZTestId))
            material.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void ConfigureTransparentBodyGlowMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        if (material.HasProperty(ZTestId))
            material.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 2;
    }

    private struct SkinnedOutlineState
    {
        public SkinnedMeshRenderer Source;
        public Transform Transform;
        public Mesh Mesh;
        public float BaseScale;

        public SkinnedOutlineState(SkinnedMeshRenderer source, Transform transform, Mesh mesh, float baseScale)
        {
            Source = source;
            Transform = transform;
            Mesh = mesh;
            BaseScale = baseScale;
        }

        public void Dispose()
        {
            if (Mesh != null)
                Destroy(Mesh);
            Mesh = null;
        }
    }
}

public static class BossAttackVfx
{
    public static void SpawnImpactPulse(Vector3 position, float radius, Color color, float duration = 0.35f, float shakeMagnitude = 0f)
    {
        GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pulse.name = "BossAttack_ImpactPulse";
        pulse.transform.position = position + Vector3.up * 0.08f;

        Collider collider = pulse.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        BossRuntimePulseEffect effect = pulse.AddComponent<BossRuntimePulseEffect>();
        effect.Configure(Mathf.Max(0.25f, radius), Mathf.Max(0.05f, duration), color);

        if (shakeMagnitude > 0f)
        {
            EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
            {
                Magnitude = shakeMagnitude,
                Duration = Mathf.Min(0.2f, duration)
            });
        }
    }
}

public class BossRuntimePulseEffect : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Renderer _renderer;
    private Light _light;
    private Color _color;
    private float _radius;
    private float _duration;
    private float _elapsed;

    public void Configure(float radius, float duration, Color color)
    {
        _radius = radius;
        _duration = duration;
        _color = color;

        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
            _renderer.material = CreateMaterial(color);

        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = color;
        _light.range = radius * 4f;
        _light.intensity = 6f;
        _light.shadows = LightShadows.None;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _duration));
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        float diameter = Mathf.Lerp(_radius * 0.4f, _radius * 2.2f, eased);
        transform.localScale = new Vector3(diameter, 0.045f, diameter);

        if (_renderer != null)
        {
            Color color = _color;
            color.a = Mathf.Lerp(_color.a, 0f, t);
            if (_renderer.material.HasProperty(BaseColorId))
                _renderer.material.SetColor(BaseColorId, color);
            if (_renderer.material.HasProperty(ColorId))
                _renderer.material.SetColor(ColorId, color);
        }

        if (_light != null)
            _light.intensity = Mathf.Lerp(6f, 0f, t);

        if (t >= 1f)
            Destroy(gameObject);
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new(shader)
        {
            name = "BossRuntimePulse_Runtime"
        };

        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, color);
        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, color * 3f);
        }

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }
}

[DisallowMultipleComponent]
public class BossTelegraphedMeleeModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 0f;
    [SerializeField] private float _maxAttackRange = 11f;
    [SerializeField] private string _attackAnimTrigger = "Melee1";

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private Coroutine _attackRoutine;
    private Vector2 _cooldownRange = new(6f, 9f);
    private float _nextAttackTime;
    private float _damage = 18f;
    private float _warningDuration = 0.75f;
    private float _radius = 4.2f;
    private float _forwardOffset = 3.1f;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => _damage;
    public float AttackRate => 1f / Mathf.Max(0.1f, _cooldownRange.x);
    public DamageType AttackDamageType => DamageType.Physical;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => _room != null && Time.time >= _nextAttackTime && _attackRoutine == null;
    public bool IsExecuting => _attackRoutine != null;

    public void Configure(BossRoomController room, int phaseIndex, Vector2 cooldownRange)
    {
        _room = room;
        _cooldownRange = NormalizeRange(cooldownRange);

        if (phaseIndex >= 3)
        {
            _damage = 22f;
            _warningDuration = 0.55f;
            _radius = 5.2f;
            _forwardOffset = 3.8f;
            _maxAttackRange = 13f;
        }
        else
        {
            _damage = 18f;
            _warningDuration = 0.8f;
            _radius = 4.2f;
            _forwardOffset = 3.1f;
            _maxAttackRange = 11f;
        }

        CacheComponents();
        ScheduleNextAttack();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    private void CacheComponents()
    {
        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (!CanStartAttack)
            return;

        _attackRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        ScheduleNextAttack();
        CachePlayerReference();
        if (_room == null || _playerTransform == null)
        {
            _attackRoutine = null;
            yield break;
        }

        _enemyBase?.PlayAttackAnimationOneShot();

        Vector3 direction = Vector3.ProjectOnPlane(_playerTransform.position - transform.position, Vector3.up);
        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;
        direction.Normalize();

        Vector3 impactPosition = transform.position + direction * _forwardOffset;
        _room.SpawnBossCircleImpactHazard(
            gameObject,
            impactPosition,
            _radius,
            _warningDuration,
            0.18f,
            _damage,
            DamageType.Physical,
            new Color(1f, 0.08f, 0.02f, 0.5f),
            _room.PlayBossMeleeImpactSound);

        yield return new WaitForSeconds(_warningDuration + 0.1f);
        _attackRoutine = null;
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private void ScheduleNextAttack()
    {
        _nextAttackTime = Time.time + Random.Range(_cooldownRange.x, _cooldownRange.y);
    }

    private static Vector2 NormalizeRange(Vector2 range)
    {
        return new Vector2(
            Mathf.Max(0.1f, Mathf.Min(range.x, range.y)),
            Mathf.Max(0.1f, Mathf.Max(range.x, range.y)));
    }
}

[DisallowMultipleComponent]
public class BossDashSlamModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 7f;
    [SerializeField] private float _maxAttackRange = 70f;
    [SerializeField] private string _attackAnimTrigger = "Melee2";

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private NavMeshAgent _agent;
    private Transform _playerTransform;
    private Coroutine _attackRoutine;
    private Vector2 _cooldownRange = new(12f, 16f);
    private float _nextAttackTime;
    private float _damage = 18f;
    private float _warningDuration = 0.75f;
    private float _dashDuration = 0.24f;
    private float _laneWidth = 3.4f;
    private float _slamRadius = 5f;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => _damage;
    public float AttackRate => 1f / Mathf.Max(0.1f, _cooldownRange.x);
    public DamageType AttackDamageType => DamageType.Physical;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => _room != null && Time.time >= _nextAttackTime && _attackRoutine == null;
    public bool IsExecuting => _attackRoutine != null;

    public void Configure(BossRoomController room, int phaseIndex, Vector2 cooldownRange)
    {
        _room = room;
        _cooldownRange = NormalizeRange(cooldownRange);

        if (phaseIndex >= 3)
        {
            _damage = 20f;
            _warningDuration = 0.55f;
            _dashDuration = 0.18f;
            _laneWidth = 4f;
            _slamRadius = 6f;
        }
        else
        {
            _damage = 17f;
            _warningDuration = 0.85f;
            _dashDuration = 0.28f;
            _laneWidth = 3.2f;
            _slamRadius = 5f;
        }

        CacheComponents();
        ScheduleNextAttack();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    private void CacheComponents()
    {
        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (!CanStartAttack)
            return;

        _attackRoutine = StartCoroutine(DashSlamRoutine(scheduleCooldown: true));
    }

    public IEnumerator ExecuteDirectorPattern()
    {
        if (_attackRoutine != null)
            yield break;

        _attackRoutine = StartCoroutine(DashSlamRoutine(scheduleCooldown: false));
        while (_attackRoutine != null)
            yield return null;
    }

    private IEnumerator DashSlamRoutine(bool scheduleCooldown)
    {
        if (scheduleCooldown)
            ScheduleNextAttack();

        CachePlayerReference();
        if (_room == null || _playerTransform == null)
        {
            _attackRoutine = null;
            yield break;
        }

        Vector3 start = transform.position;
        Vector3 direction = Vector3.ProjectOnPlane(_playerTransform.position - start, Vector3.up);
        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;
        direction.Normalize();

        Vector3 desiredEnd = _playerTransform.position + direction * 6f;
        if (!_room.TryProjectToBossRoomFloor(desiredEnd, out Vector3 end))
            _room.TryPickPhaseThreeArenaPoint(_playerTransform.position, 8f, 14f, out end);

        _enemyBase?.PlayAttackAnimationOneShot();
        _enemyBase?.DisableAttacksFor(_warningDuration + _dashDuration + 0.45f);
        _room.PlayBossDashWindupSound();

        _room.SpawnBossLineAttackHazard(
            gameObject,
            start,
            end,
            _laneWidth,
            _warningDuration,
            _dashDuration,
            _damage,
            DamageType.Physical,
            new Color(1f, 0.18f, 0.02f, 0.48f),
            _room.PlayBossDashMoveSound);

        yield return new WaitForSeconds(_warningDuration);
        yield return MoveBoss(start, end);

        _room.SpawnBossCircleImpactHazard(
            gameObject,
            end,
            _slamRadius,
            0.35f,
            0.22f,
            _damage + 4f,
            DamageType.Physical,
            new Color(1f, 0.55f, 0.04f, 0.52f));

        yield return new WaitForSeconds(0.58f);
        _attackRoutine = null;
    }

    private IEnumerator MoveBoss(Vector3 start, Vector3 end)
    {
        bool agentWasEnabled = _agent != null && _agent.enabled;
        bool agentWasStopped = _agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.isStopped;
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = true;

        float elapsed = 0f;
        float nextTrailAt = 0f;
        while (elapsed < _dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _dashDuration));
            Vector3 position = Vector3.Lerp(start, end, 1f - Mathf.Pow(1f - t, 3f));

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.Warp(position);
            else
                transform.position = position;

            if (elapsed >= nextTrailAt)
            {
                BossAttackVfx.SpawnImpactPulse(position, _laneWidth * 0.6f, new Color(1f, 0.22f, 0.05f, 0.36f), 0.16f);
                nextTrailAt = elapsed + 0.055f;
            }

            yield return null;
        }

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Warp(end);
            _agent.isStopped = agentWasStopped;
        }
        else
        {
            transform.position = end;
        }
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private void ScheduleNextAttack()
    {
        _nextAttackTime = Time.time + Random.Range(_cooldownRange.x, _cooldownRange.y);
    }

    private static Vector2 NormalizeRange(Vector2 range)
    {
        return new Vector2(
            Mathf.Max(0.1f, Mathf.Min(range.x, range.y)),
            Mathf.Max(0.1f, Mathf.Max(range.x, range.y)));
    }
}

[DisallowMultipleComponent]
public class BossArenaSweepModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 0f;
    [SerializeField] private float _maxAttackRange = 90f;
    [SerializeField] private string _attackAnimTrigger = "AreaOfEffect";

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private Coroutine _attackRoutine;
    private Vector2 _cooldownRange = new(14f, 18f);
    private float _nextAttackTime;
    private float _damage = 18f;
    private float _warningDuration = 0.9f;
    private float _travelDuration = 1.2f;
    private float _stripWidth = 4f;
    private float _waveHeight = 5f;
    private float _waveDepth = 2.8f;
    private int _stripCount = 1;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => _damage;
    public float AttackRate => 1f / Mathf.Max(0.1f, _cooldownRange.x);
    public DamageType AttackDamageType => DamageType.Energy;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => _room != null && Time.time >= _nextAttackTime && _attackRoutine == null;
    public bool IsExecuting => _attackRoutine != null;

    public void Configure(BossRoomController room, int phaseIndex, Vector2 cooldownRange)
    {
        _room = room;
        _cooldownRange = NormalizeRange(cooldownRange);

        if (phaseIndex >= 3)
        {
            _damage = 20f;
            _warningDuration = 0.65f;
            _travelDuration = 0.85f;
            _stripWidth = 5f;
            _waveHeight = 6.2f;
            _waveDepth = 3.2f;
            _stripCount = 2;
        }
        else if (phaseIndex == 2)
        {
            _damage = 19f;
            _warningDuration = 0.85f;
            _travelDuration = 1.05f;
            _stripWidth = 4.8f;
            _waveHeight = 5.8f;
            _waveDepth = 3f;
            _stripCount = 1;
        }
        else
        {
            _damage = 17f;
            _warningDuration = 1f;
            _travelDuration = 1.25f;
            _stripWidth = 4.2f;
            _waveHeight = 5f;
            _waveDepth = 2.8f;
            _stripCount = 1;
        }

        CacheComponents();
        ScheduleNextAttack();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    private void CacheComponents()
    {
        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (!CanStartAttack)
            return;

        _attackRoutine = StartCoroutine(SweepRoutine(scheduleCooldown: true));
    }

    public IEnumerator ExecuteDirectorPattern()
    {
        if (_attackRoutine != null)
            yield break;

        _attackRoutine = StartCoroutine(SweepRoutine(scheduleCooldown: false));
        while (_attackRoutine != null)
            yield return null;
    }

    private IEnumerator SweepRoutine(bool scheduleCooldown)
    {
        if (scheduleCooldown)
            ScheduleNextAttack();

        CachePlayerReference();
        if (_room == null)
        {
            _attackRoutine = null;
            yield break;
        }

        _enemyBase?.PlayAttackAnimationOneShot();
        float staggerDuration = Mathf.Max(0, _stripCount - 1) * 0.18f;
        _enemyBase?.DisableAttacksFor(_warningDuration + staggerDuration + _travelDuration + 0.2f);
        _room.PlayBossArenaSweepChargeSound();

        Vector3 center = _playerTransform != null ? _playerTransform.position : transform.position;
        Vector3 direction = Random.value < 0.5f ? transform.right : transform.forward;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector3.right;

        Vector3 perpendicular = Vector3.Cross(Vector3.up, direction).normalized;
        int count = Mathf.Max(1, _stripCount);
        for (int i = 0; i < count; i++)
        {
            float offset = count == 1 ? 0f : (i == 0 ? -4.5f : 4.5f);
            Vector3 stripCenter = center + perpendicular * offset;
            Vector3 start = stripCenter - direction * 42f;
            Vector3 end = stripCenter + direction * 42f;
            _room.SpawnBossWaveAttackHazard(
                gameObject,
                start,
                end,
                _stripWidth,
                _waveHeight,
                _waveDepth,
                _warningDuration + i * 0.18f,
                _travelDuration,
                _damage,
                DamageType.Energy,
                new Color(0.08f, 0.85f, 1f, 0.5f),
                _room.PlayBossArenaSweepMoveSound,
                _room.PlayBossArenaSweepHitSound);
        }

        yield return new WaitForSeconds(_warningDuration + staggerDuration + _travelDuration + 0.2f);
        _attackRoutine = null;
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private void ScheduleNextAttack()
    {
        _nextAttackTime = Time.time + Random.Range(_cooldownRange.x, _cooldownRange.y);
    }

    private static Vector2 NormalizeRange(Vector2 range)
    {
        return new Vector2(
            Mathf.Max(0.1f, Mathf.Min(range.x, range.y)),
            Mathf.Max(0.1f, Mathf.Max(range.x, range.y)));
    }
}

[DisallowMultipleComponent]
public class BossGridAttackModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 0f;
    [SerializeField] private float _maxAttackRange = 80f;
    [SerializeField] private string _attackAnimTrigger = "AreaOfEffect";

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private Coroutine _attackRoutine;
    private Vector2 _cooldownRange = new(13f, 17f);
    private float _nextAttackTime;
    private float _damage = 14f;
    private float _warningDuration = 1.15f;
    private float _burstDuration = 0.55f;
    private float _cellSize = 3.8f;
    private int _rows = 4;
    private int _columns = 4;
    private int _passCount = 1;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => _damage;
    public float AttackRate => 1f / Mathf.Max(0.1f, _cooldownRange.x);
    public DamageType AttackDamageType => DamageType.Energy;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => _room != null && Time.time >= _nextAttackTime && _attackRoutine == null;
    public bool IsExecuting => _attackRoutine != null;

    public void Configure(BossRoomController room, int phaseIndex, Vector2 cooldownRange)
    {
        _room = room;
        _cooldownRange = NormalizeRange(cooldownRange);

        if (phaseIndex >= 3)
        {
            _damage = 18f;
            _warningDuration = 0.75f;
            _burstDuration = 0.65f;
            _cellSize = 3.6f;
            _rows = 6;
            _columns = 6;
            _passCount = 2;
        }
        else if (phaseIndex == 2)
        {
            _damage = 16f;
            _warningDuration = 0.95f;
            _burstDuration = 0.6f;
            _cellSize = 3.7f;
            _rows = 5;
            _columns = 5;
            _passCount = 1;
        }
        else
        {
            _damage = 14f;
            _warningDuration = 1.15f;
            _burstDuration = 0.55f;
            _cellSize = 3.8f;
            _rows = 4;
            _columns = 4;
            _passCount = 1;
        }

        CacheComponents();
        ScheduleNextAttack();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    private void CacheComponents()
    {
        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (!CanStartAttack)
            return;

        _attackRoutine = StartCoroutine(GridRoutine(scheduleCooldown: true));
    }

    private IEnumerator GridRoutine(bool scheduleCooldown)
    {
        if (scheduleCooldown)
            ScheduleNextAttack();

        CachePlayerReference();
        if (_room == null || _playerTransform == null)
        {
            _attackRoutine = null;
            yield break;
        }

        _enemyBase?.PlayAttackAnimationOneShot();
        _enemyBase?.DisableAttacksFor((_warningDuration + _burstDuration) * _passCount + 0.55f);
        _room.PlayBossSpikeCastSound();

        Vector3 center = _playerTransform.position;
        if (!_room.TryProjectToBossRoomFloor(center, out center))
            center = transform.position;

        int passes = Mathf.Max(1, _passCount);
        for (int pass = 0; pass < passes; pass++)
        {
            SpawnCheckerboard(center, pass % 2);
            if (pass < passes - 1)
                yield return new WaitForSeconds(_warningDuration + _burstDuration + 0.35f);
        }

        yield return new WaitForSeconds(_warningDuration + _burstDuration + 0.2f);
        _attackRoutine = null;
    }

    private void SpawnCheckerboard(Vector3 center, int parity)
    {
        int rows = Mathf.Max(2, _rows);
        int columns = Mathf.Max(2, _columns);
        float startX = -((columns - 1) * _cellSize) * 0.5f;
        float startZ = -((rows - 1) * _cellSize) * 0.5f;
        float hazardSize = _cellSize * 0.88f;
        bool playedImpactSound = false;
        System.Action playGridImpactSound = () =>
        {
            if (playedImpactSound)
                return;

            playedImpactSound = true;
            _room.PlayBossGridEruptSound();
        };

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (((row + column + parity) & 1) != 0)
                    continue;

                Vector3 candidate = center + new Vector3(startX + column * _cellSize, 0f, startZ + row * _cellSize);
                _room.SpawnBossGridCellHazard(
                    gameObject,
                    candidate,
                    hazardSize,
                    _warningDuration,
                    _burstDuration,
                    _damage,
                    DamageType.Energy,
                    new Color(1f, 0.62f, 0.05f, 0.52f),
                    playGridImpactSound);
            }
        }
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private void ScheduleNextAttack()
    {
        _nextAttackTime = Time.time + Random.Range(_cooldownRange.x, _cooldownRange.y);
    }

    private static Vector2 NormalizeRange(Vector2 range)
    {
        return new Vector2(
            Mathf.Max(0.1f, Mathf.Min(range.x, range.y)),
            Mathf.Max(0.1f, Mathf.Max(range.x, range.y)));
    }
}

[DisallowMultipleComponent]
public class BossTrackingSpikeAttackModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 0f;
    [SerializeField] private float _maxAttackRange = 80f;
    [SerializeField] private string _attackAnimTrigger = "AreaOfEffect";

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private Coroutine _attackRoutine;
    private Vector2 _cooldownRange = new(11f, 15f);
    private float _nextAttackTime;
    private float _damage = 18f;
    private float _warningDuration = 1.25f;
    private float _sampleInterval = 2.2f;
    private float _spikeRadius = 2f;
    private float _spikeHeight = 6f;
    private int _spikeCount = 3;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => _damage;
    public float AttackRate => 1f / Mathf.Max(0.1f, _cooldownRange.x);
    public DamageType AttackDamageType => DamageType.Physical;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => _room != null && Time.time >= _nextAttackTime && _attackRoutine == null;
    public bool IsExecuting => _attackRoutine != null;

    public void Configure(BossRoomController room, int phaseIndex, Vector2 cooldownRange)
    {
        _room = room;
        _cooldownRange = NormalizeRange(cooldownRange);

        if (phaseIndex >= 3)
        {
            _damage = 22f;
            _warningDuration = 0.82f;
            _sampleInterval = 2f;
            _spikeRadius = 2.45f;
            _spikeHeight = 7.2f;
            _spikeCount = 6;
        }
        else if (phaseIndex == 2)
        {
            _damage = 20f;
            _warningDuration = 1f;
            _sampleInterval = 2.1f;
            _spikeRadius = 2.25f;
            _spikeHeight = 6.6f;
            _spikeCount = 4;
        }
        else
        {
            _damage = 18f;
            _warningDuration = 1.25f;
            _sampleInterval = 2.2f;
            _spikeRadius = 2f;
            _spikeHeight = 6f;
            _spikeCount = 3;
        }

        CacheComponents();
        ScheduleNextAttack();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    private void CacheComponents()
    {
        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (!CanStartAttack)
            return;

        _attackRoutine = StartCoroutine(TrackingSpikeRoutine(scheduleCooldown: true));
    }

    private IEnumerator TrackingSpikeRoutine(bool scheduleCooldown)
    {
        if (scheduleCooldown)
            ScheduleNextAttack();

        CachePlayerReference();
        if (_room == null || _playerTransform == null)
        {
            _attackRoutine = null;
            yield break;
        }

        _enemyBase?.PlayAttackAnimationOneShot();
        _enemyBase?.DisableAttacksFor(_warningDuration + _spikeCount * _sampleInterval + 0.45f);
        _room.PlayBossSpikeCastSound();

        int count = Mathf.Max(1, _spikeCount);
        for (int i = 0; i < count; i++)
        {
            CachePlayerReference();
            if (_playerTransform == null)
                break;

            _room.SpawnBossSpikeHazard(
                gameObject,
                _playerTransform.position,
                _spikeRadius,
                _warningDuration,
                _damage,
                DamageType.Physical,
                0f,
                _spikeHeight);

            yield return new WaitForSeconds(_sampleInterval);
        }

        yield return new WaitForSeconds(_warningDuration + 0.25f);
        _attackRoutine = null;
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private void ScheduleNextAttack()
    {
        _nextAttackTime = Time.time + Random.Range(_cooldownRange.x, _cooldownRange.y);
    }

    private static Vector2 NormalizeRange(Vector2 range)
    {
        return new Vector2(
            Mathf.Max(0.1f, Mathf.Min(range.x, range.y)),
            Mathf.Max(0.1f, Mathf.Max(range.x, range.y)));
    }
}

[DisallowMultipleComponent]
public class BossCircleImpactHazard : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _warningMaterial;
    private static Material _activeMaterial;

    private GameObject _owner;
    private GameObject _visual;
    private Light _light;
    private Color _warningColor;
    private float _radius;
    private float _warningDuration;
    private float _lingerDuration;
    private float _damage;
    private DamageType _damageType;
    private float _elapsed;
    private bool _active;
    private bool _damagedPlayer;
    private System.Action _impactSound;

    public void Configure(
        GameObject owner,
        Vector3 position,
        float radius,
        float warningDuration,
        float lingerDuration,
        float damage,
        DamageType damageType,
        Color warningColor,
        System.Action impactSound = null)
    {
        _owner = owner;
        _radius = Mathf.Max(0.4f, radius);
        _warningDuration = Mathf.Max(0.05f, warningDuration);
        _lingerDuration = Mathf.Max(0.05f, lingerDuration);
        _damage = Mathf.Max(0f, damage);
        _damageType = damageType;
        _warningColor = warningColor;
        _impactSound = impactSound;
        transform.position = position;
        EnsureVisual();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (!_active)
        {
            float t = Mathf.Clamp01(_elapsed / _warningDuration);
            SetVisualScale(Mathf.Lerp(0.25f, 1f, t) * (1f + Mathf.Sin(Time.time * 18f) * 0.05f));
            if (_light != null)
                _light.intensity = Mathf.Lerp(1.5f, 6f, t);

            if (_elapsed >= _warningDuration)
                Activate();

            return;
        }

        float activeElapsed = _elapsed - _warningDuration;
        DamagePlayerIfInside();
        SetVisualScale(Mathf.Lerp(1.08f, 0.82f, Mathf.Clamp01(activeElapsed / _lingerDuration)));
        if (_light != null)
            _light.intensity = Mathf.Lerp(7f, 0f, Mathf.Clamp01(activeElapsed / _lingerDuration));

        if (activeElapsed >= _lingerDuration)
            Destroy(gameObject);
    }

    private void Activate()
    {
        _active = true;
        Renderer renderer = _visual != null ? _visual.GetComponent<Renderer>() : null;
        if (renderer != null)
            renderer.sharedMaterial = GetActiveMaterial();
        _impactSound?.Invoke();
        BossAttackVfx.SpawnImpactPulse(transform.position, _radius, new Color(1f, 0.65f, 0.08f, 0.62f), 0.3f, 0.025f);
        DamagePlayerIfInside();
    }

    private void DamagePlayerIfInside()
    {
        if (_damagedPlayer || _damage <= 0f)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, _radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            HealthComponent health = hits[i] != null ? hits[i].GetComponentInParent<HealthComponent>() : null;
            if (health == null || !health.IsPlayer || !health.IsAlive)
                continue;

            Vector3 offset = Vector3.ProjectOnPlane(health.transform.position - transform.position, Vector3.up);
            if (offset.sqrMagnitude > _radius * _radius)
                continue;

            health.TakeDamage(new DamageInfo
            {
                Amount = _damage,
                Type = _damageType,
                Source = _owner
            });
            _damagedPlayer = true;
            return;
        }
    }

    private void EnsureVisual()
    {
        _visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _visual.name = "BossCircleImpactVisual";
        _visual.transform.SetParent(transform, false);

        Collider collider = _visual.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        Renderer renderer = _visual.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = GetWarningMaterial(_warningColor);

        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = _warningColor;
        _light.range = _radius * 5f;
        _light.intensity = 1.5f;
        _light.shadows = LightShadows.None;

        SetVisualScale(0.25f);
    }

    private void SetVisualScale(float normalized)
    {
        if (_visual == null)
            return;

        float diameter = _radius * 2f * Mathf.Max(0.05f, normalized);
        _visual.transform.localPosition = Vector3.up * 0.05f;
        _visual.transform.localScale = new Vector3(diameter, 0.05f, diameter);
    }

    private static Material GetWarningMaterial(Color color)
    {
        if (_warningMaterial != null)
            return _warningMaterial;

        _warningMaterial = CreateMaterial("BossCircleImpact_Warning_Runtime", color, color * 4f);
        return _warningMaterial;
    }

    private static Material GetActiveMaterial()
    {
        if (_activeMaterial != null)
            return _activeMaterial;

        _activeMaterial = CreateMaterial("BossCircleImpact_Active_Runtime", new Color(1f, 0.75f, 0.1f, 0.68f), new Color(4f, 2f, 0.2f, 0.68f));
        return _activeMaterial;
    }

    private static Material CreateMaterial(string name, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new(shader) { name = name };
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, baseColor);
        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emissionColor);
        }
        ConfigureTransparentMaterial(material);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}

[DisallowMultipleComponent]
public class BossLineAttackHazard : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _warningMaterial;
    private static Material _activeMaterial;

    private GameObject _owner;
    private GameObject _visual;
    private Light _light;
    private Vector3 _start;
    private Vector3 _end;
    private Vector3 _direction;
    private float _length;
    private float _width;
    private float _warningDuration;
    private float _activeDuration;
    private float _damage;
    private DamageType _damageType;
    private float _elapsed;
    private bool _active;
    private bool _damagedPlayer;
    private System.Action _activeSound;
    private System.Action _hitSound;

    public void Configure(
        GameObject owner,
        Vector3 start,
        Vector3 end,
        float width,
        float warningDuration,
        float activeDuration,
        float damage,
        DamageType damageType,
        Color warningColor,
        System.Action activeSound = null,
        System.Action hitSound = null)
    {
        _owner = owner;
        _start = start;
        _end = end;
        _width = Mathf.Max(0.25f, width);
        _warningDuration = Mathf.Max(0.05f, warningDuration);
        _activeDuration = Mathf.Max(0.05f, activeDuration);
        _damage = Mathf.Max(0f, damage);
        _damageType = damageType;
        _activeSound = activeSound;
        _hitSound = hitSound;

        Vector3 line = Vector3.ProjectOnPlane(_end - _start, Vector3.up);
        _length = Mathf.Max(0.1f, line.magnitude);
        _direction = line.sqrMagnitude > 0.001f ? line.normalized : Vector3.forward;

        transform.position = Vector3.Lerp(_start, _end, 0.5f);
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        EnsureVisual(warningColor);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (!_active)
        {
            float t = Mathf.Clamp01(_elapsed / _warningDuration);
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.05f;
            SetVisualScale(_width * Mathf.Lerp(0.35f, 1f, t) * pulse, _length);
            if (_light != null)
                _light.intensity = Mathf.Lerp(1f, 5f, t);

            if (_elapsed >= _warningDuration)
                Activate();

            return;
        }

        float activeElapsed = _elapsed - _warningDuration;
        DamagePlayerIfInside();
        float sweepT = Mathf.Clamp01(activeElapsed / _activeDuration);
        SetVisualScale(_width * Mathf.Lerp(1.15f, 0.85f, sweepT), _length * Mathf.Lerp(0.35f, 1f, sweepT));
        if (_light != null)
            _light.intensity = Mathf.Lerp(6f, 0.5f, sweepT);

        if (activeElapsed >= _activeDuration)
            Destroy(gameObject);
    }

    private void Activate()
    {
        _active = true;
        Renderer renderer = _visual != null ? _visual.GetComponent<Renderer>() : null;
        if (renderer != null)
            renderer.sharedMaterial = GetActiveMaterial();
        _activeSound?.Invoke();
        BossAttackVfx.SpawnImpactPulse(transform.position, Mathf.Max(_width, 2f), new Color(1f, 0.82f, 0.12f, 0.55f), 0.28f, 0.018f);
        DamagePlayerIfInside();
    }

    private void DamagePlayerIfInside()
    {
        if (_damagedPlayer || _damage <= 0f)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        HealthComponent health = player != null ? player.GetComponentInParent<HealthComponent>() : null;
        if (health == null || !health.IsPlayer || !health.IsAlive)
            return;

        Vector3 toPlayer = Vector3.ProjectOnPlane(health.transform.position - _start, Vector3.up);
        float along = Vector3.Dot(toPlayer, _direction);
        if (along < 0f || along > _length)
            return;

        Vector3 closest = _start + _direction * along;
        Vector3 lateral = Vector3.ProjectOnPlane(health.transform.position - closest, Vector3.up);
        if (lateral.sqrMagnitude > (_width * 0.5f) * (_width * 0.5f))
            return;

        health.TakeDamage(new DamageInfo
        {
            Amount = _damage,
            Type = _damageType,
            Source = _owner
        });
        _hitSound?.Invoke();
        _damagedPlayer = true;
    }

    private void EnsureVisual(Color warningColor)
    {
        _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _visual.name = "BossLineAttackVisual";
        _visual.transform.SetParent(transform, false);

        Collider collider = _visual.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        Renderer renderer = _visual.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = GetWarningMaterial(warningColor);

        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = warningColor;
        _light.range = Mathf.Max(_width * 4f, 10f);
        _light.intensity = 1f;
        _light.shadows = LightShadows.None;

        SetVisualScale(_width * 0.35f, _length);
    }

    private void SetVisualScale(float width, float length)
    {
        if (_visual == null)
            return;

        _visual.transform.localPosition = Vector3.up * 0.08f;
        _visual.transform.localRotation = Quaternion.identity;
        _visual.transform.localScale = new Vector3(Mathf.Max(0.05f, width), 0.08f, Mathf.Max(0.05f, length));
    }

    private static Material GetWarningMaterial(Color color)
    {
        if (_warningMaterial != null)
            return _warningMaterial;

        _warningMaterial = CreateMaterial("BossLineAttack_Warning_Runtime", color, color * 4f);
        return _warningMaterial;
    }

    private static Material GetActiveMaterial()
    {
        if (_activeMaterial != null)
            return _activeMaterial;

        _activeMaterial = CreateMaterial("BossLineAttack_Active_Runtime", new Color(1f, 0.85f, 0.12f, 0.72f), new Color(5f, 2.6f, 0.4f, 0.72f));
        return _activeMaterial;
    }

    private static Material CreateMaterial(string name, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new(shader) { name = name };
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, baseColor);
        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emissionColor);
        }
        ConfigureTransparentMaterial(material);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}

[DisallowMultipleComponent]
public class BossWaveAttackHazard : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _warningMaterial;
    private static Material _waveMaterial;

    private GameObject _owner;
    private GameObject _warningVisual;
    private GameObject _waveVisual;
    private Light _light;
    private Vector3 _start;
    private Vector3 _end;
    private Vector3 _direction;
    private float _length;
    private float _width;
    private float _height;
    private float _depth;
    private float _warningDuration;
    private float _travelDuration;
    private float _damage;
    private DamageType _damageType;
    private float _elapsed;
    private bool _active;
    private bool _damagedPlayer;
    private System.Action _activeSound;
    private System.Action _hitSound;

    public void Configure(
        GameObject owner,
        Vector3 start,
        Vector3 end,
        float width,
        float height,
        float depth,
        float warningDuration,
        float travelDuration,
        float damage,
        DamageType damageType,
        Color warningColor,
        System.Action activeSound = null,
        System.Action hitSound = null)
    {
        _owner = owner;
        _start = start;
        _end = end;
        _width = Mathf.Max(0.5f, width);
        _height = Mathf.Max(1f, height);
        _depth = Mathf.Max(0.5f, depth);
        _warningDuration = Mathf.Max(0.05f, warningDuration);
        _travelDuration = Mathf.Max(0.05f, travelDuration);
        _damage = Mathf.Max(0f, damage);
        _damageType = damageType;
        _activeSound = activeSound;
        _hitSound = hitSound;

        Vector3 line = Vector3.ProjectOnPlane(_end - _start, Vector3.up);
        _length = Mathf.Max(0.1f, line.magnitude);
        _direction = line.sqrMagnitude > 0.001f ? line.normalized : Vector3.forward;

        transform.position = Vector3.Lerp(_start, _end, 0.5f);
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        EnsureVisuals(warningColor);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (!_active)
        {
            float t = Mathf.Clamp01(_elapsed / _warningDuration);
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.06f;
            SetWarningScale(_width * Mathf.Lerp(0.35f, 1f, t) * pulse);
            if (_light != null)
                _light.intensity = Mathf.Lerp(1.2f, 6f, t);

            if (_elapsed >= _warningDuration)
                Activate();

            return;
        }

        float activeElapsed = _elapsed - _warningDuration;
        float tActive = Mathf.Clamp01(activeElapsed / _travelDuration);
        UpdateWaveVisual(tActive);
        DamagePlayerIfInside();

        if (_light != null)
            _light.intensity = Mathf.Lerp(7f, 0.5f, tActive);

        if (activeElapsed >= _travelDuration)
            Destroy(gameObject);
    }

    private void Activate()
    {
        _active = true;
        _activeSound?.Invoke();

        if (_warningVisual != null)
            _warningVisual.SetActive(false);
        if (_waveVisual != null)
            _waveVisual.SetActive(true);

        BossAttackVfx.SpawnImpactPulse(_start, Mathf.Max(_width, 2f), new Color(0.1f, 0.9f, 1f, 0.55f), 0.28f, 0.018f);
        UpdateWaveVisual(0f);
        DamagePlayerIfInside();
    }

    private void EnsureVisuals(Color warningColor)
    {
        _warningVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _warningVisual.name = "BossWaveWarningLane";
        _warningVisual.transform.SetParent(transform, false);

        Collider warningCollider = _warningVisual.GetComponent<Collider>();
        if (warningCollider != null)
            warningCollider.enabled = false;

        Renderer warningRenderer = _warningVisual.GetComponent<Renderer>();
        if (warningRenderer != null)
            warningRenderer.sharedMaterial = GetWarningMaterial(warningColor);

        _waveVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _waveVisual.name = "BossWaveWall";
        _waveVisual.transform.SetParent(transform, false);
        _waveVisual.SetActive(false);

        Collider waveCollider = _waveVisual.GetComponent<Collider>();
        if (waveCollider != null)
            waveCollider.enabled = false;

        Renderer waveRenderer = _waveVisual.GetComponent<Renderer>();
        if (waveRenderer != null)
            waveRenderer.sharedMaterial = GetWaveMaterial();

        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = warningColor;
        _light.range = Mathf.Max(_width * 5f, 12f);
        _light.intensity = 1.2f;
        _light.shadows = LightShadows.None;

        SetWarningScale(_width * 0.35f);
        UpdateWaveVisual(0f);
    }

    private void SetWarningScale(float width)
    {
        if (_warningVisual == null)
            return;

        _warningVisual.transform.localPosition = Vector3.up * 0.08f;
        _warningVisual.transform.localRotation = Quaternion.identity;
        _warningVisual.transform.localScale = new Vector3(Mathf.Max(0.05f, width), 0.08f, _length);
    }

    private void UpdateWaveVisual(float normalized)
    {
        if (_waveVisual == null)
            return;

        float localZ = Mathf.Lerp(-_length * 0.5f, _length * 0.5f, normalized);
        float ripple = 1f + Mathf.Sin((normalized * Mathf.PI * 6f) + Time.time * 8f) * 0.06f;
        _waveVisual.transform.localPosition = new Vector3(0f, _height * 0.5f, localZ);
        _waveVisual.transform.localRotation = Quaternion.identity;
        _waveVisual.transform.localScale = new Vector3(_width * ripple, _height, _depth);
    }

    private void DamagePlayerIfInside()
    {
        if (_damagedPlayer || _damage <= 0f || _waveVisual == null || !_waveVisual.activeSelf)
            return;

        Vector3 center = _waveVisual.transform.position;
        Vector3 halfExtents = new(_width * 0.5f, _height * 0.5f, _depth * 0.5f);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            HealthComponent health = hits[i] != null ? hits[i].GetComponentInParent<HealthComponent>() : null;
            if (health == null || !health.IsPlayer || !health.IsAlive)
                continue;

            health.TakeDamage(new DamageInfo
            {
                Amount = _damage,
                Type = _damageType,
                Source = _owner
            });
            _hitSound?.Invoke();
            _damagedPlayer = true;
            return;
        }
    }

    private static Material GetWarningMaterial(Color color)
    {
        if (_warningMaterial != null)
            return _warningMaterial;

        _warningMaterial = CreateMaterial("BossWave_Warning_Runtime", color, color * 4f);
        return _warningMaterial;
    }

    private static Material GetWaveMaterial()
    {
        if (_waveMaterial != null)
            return _waveMaterial;

        _waveMaterial = CreateMaterial("BossWave_Active_Runtime", new Color(0.06f, 0.85f, 1f, 0.62f), new Color(0.25f, 4.5f, 6f, 0.75f));
        return _waveMaterial;
    }

    private static Material CreateMaterial(string name, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new(shader) { name = name };
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, baseColor);
        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emissionColor);
        }
        ConfigureTransparentMaterial(material);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}

[DisallowMultipleComponent]
public class BossGridCellHazard : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _warningMaterial;
    private static Material _activeMaterial;

    private GameObject _owner;
    private GameObject _warningVisual;
    private GameObject _burstVisual;
    private Light _light;
    private Color _warningColor;
    private float _size;
    private float _warningDuration;
    private float _activeDuration;
    private float _damage;
    private DamageType _damageType;
    private float _elapsed;
    private bool _active;
    private bool _damagedPlayer;
    private System.Action _impactSound;

    private const float BurstHeight = 5.2f;

    public void Configure(
        GameObject owner,
        Vector3 position,
        float size,
        float warningDuration,
        float activeDuration,
        float damage,
        DamageType damageType,
        Color warningColor,
        System.Action impactSound = null)
    {
        _owner = owner;
        _size = Mathf.Max(0.5f, size);
        _warningDuration = Mathf.Max(0.05f, warningDuration);
        _activeDuration = Mathf.Max(0.05f, activeDuration);
        _damage = Mathf.Max(0f, damage);
        _damageType = damageType;
        _warningColor = warningColor;
        _impactSound = impactSound;
        transform.position = position;
        EnsureVisuals();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (!_active)
        {
            float t = Mathf.Clamp01(_elapsed / _warningDuration);
            float pulse = 1f + Mathf.Sin(Time.time * 20f) * 0.05f;
            SetWarningScale(Mathf.Lerp(0.3f, 1f, t) * pulse);
            if (_light != null)
                _light.intensity = Mathf.Lerp(1.4f, 5.8f, t);

            if (_elapsed >= _warningDuration)
                Activate();

            return;
        }

        float activeElapsed = _elapsed - _warningDuration;
        float tActive = Mathf.Clamp01(activeElapsed / _activeDuration);
        SetBurstScale(tActive);
        DamagePlayerIfInside();
        if (_light != null)
            _light.intensity = Mathf.Lerp(7f, 0f, tActive);

        if (activeElapsed >= _activeDuration)
            Destroy(gameObject);
    }

    private void Activate()
    {
        _active = true;
        _impactSound?.Invoke();

        if (_warningVisual != null)
            _warningVisual.SetActive(false);
        if (_burstVisual != null)
            _burstVisual.SetActive(true);

        BossAttackVfx.SpawnImpactPulse(transform.position, _size * 0.55f, new Color(1f, 0.66f, 0.08f, 0.55f), 0.26f, 0.016f);
        SetBurstScale(0f);
        DamagePlayerIfInside();
    }

    private void EnsureVisuals()
    {
        _warningVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _warningVisual.name = "BossGridCellWarning";
        _warningVisual.transform.SetParent(transform, false);

        Collider warningCollider = _warningVisual.GetComponent<Collider>();
        if (warningCollider != null)
            warningCollider.enabled = false;

        Renderer warningRenderer = _warningVisual.GetComponent<Renderer>();
        if (warningRenderer != null)
            warningRenderer.sharedMaterial = GetWarningMaterial(_warningColor);

        _burstVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _burstVisual.name = "BossGridCellBurst";
        _burstVisual.transform.SetParent(transform, false);
        _burstVisual.SetActive(false);

        Collider burstCollider = _burstVisual.GetComponent<Collider>();
        if (burstCollider != null)
            burstCollider.enabled = false;

        Renderer burstRenderer = _burstVisual.GetComponent<Renderer>();
        if (burstRenderer != null)
            burstRenderer.sharedMaterial = GetActiveMaterial();

        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = _warningColor;
        _light.range = _size * 4f;
        _light.intensity = 1.4f;
        _light.shadows = LightShadows.None;

        SetWarningScale(0.3f);
        SetBurstScale(0f);
    }

    private void SetWarningScale(float normalized)
    {
        if (_warningVisual == null)
            return;

        float size = _size * Mathf.Max(0.05f, normalized);
        _warningVisual.transform.localPosition = Vector3.up * 0.06f;
        _warningVisual.transform.localScale = new Vector3(size, 0.06f, size);
    }

    private void SetBurstScale(float normalized)
    {
        if (_burstVisual == null)
            return;

        float rise = Mathf.Clamp01(normalized / 0.35f);
        float fade = normalized > 0.72f ? Mathf.InverseLerp(0.72f, 1f, normalized) : 0f;
        float height = BurstHeight * Mathf.Lerp(0.15f, 1f, 1f - Mathf.Pow(1f - rise, 3f));
        float width = _size * Mathf.Lerp(1f, 0.65f, fade);
        _burstVisual.transform.localPosition = Vector3.up * (height * 0.5f);
        _burstVisual.transform.localScale = new Vector3(width, height, width);
    }

    private void DamagePlayerIfInside()
    {
        if (_damagedPlayer || _damage <= 0f || !_active)
            return;

        Vector3 center = transform.position + Vector3.up * (BurstHeight * 0.5f);
        Vector3 halfExtents = new(_size * 0.45f, BurstHeight * 0.5f, _size * 0.45f);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            HealthComponent health = hits[i] != null ? hits[i].GetComponentInParent<HealthComponent>() : null;
            if (health == null || !health.IsPlayer || !health.IsAlive)
                continue;

            health.TakeDamage(new DamageInfo
            {
                Amount = _damage,
                Type = _damageType,
                Source = _owner
            });
            _damagedPlayer = true;
            return;
        }
    }

    private static Material GetWarningMaterial(Color color)
    {
        if (_warningMaterial != null)
            return _warningMaterial;

        _warningMaterial = CreateMaterial("BossGridCell_Warning_Runtime", color, color * 4f);
        return _warningMaterial;
    }

    private static Material GetActiveMaterial()
    {
        if (_activeMaterial != null)
            return _activeMaterial;

        _activeMaterial = CreateMaterial("BossGridCell_Active_Runtime", new Color(1f, 0.58f, 0.04f, 0.62f), new Color(5f, 2.2f, 0.12f, 0.72f));
        return _activeMaterial;
    }

    private static Material CreateMaterial(string name, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new(shader) { name = name };
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, baseColor);
        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emissionColor);
        }
        ConfigureTransparentMaterial(material);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}

[DisallowMultipleComponent]
public class BossSpikeAttackModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule, IAttackExecutionStatus
{
    public enum SpikePattern
    {
        Line,
        Ring,
        RandomCluster
    }

    [SerializeField] private float _minAttackRange = 0f;
    [SerializeField] private float _maxAttackRange = 70f;
    [SerializeField] private string _attackAnimTrigger = "AreaOfEffect";
    [SerializeField] private DamageType _damageType = DamageType.Physical;
    [SerializeField] private float _spikeRadius = 1.35f;
    [SerializeField] private float _lineSpacing = 3.1f;
    [SerializeField] private float _ringRadius = 7.5f;
    [SerializeField] private float _clusterRadius = 8f;

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private Coroutine _attackRoutine;
    private Vector2 _cooldownRange = new(9f, 13f);
    private float _nextAttackTime;
    private float _damage = 18f;
    private float _warningDuration = 1.05f;
    private int _lineCount = 7;
    private int _ringCount = 12;
    private int _clusterCount = 7;
    private float _lineWeight = 0.45f;
    private float _ringWeight = 0.4f;
    private float _clusterWeight = 0.15f;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => _damage;
    public float AttackRate => 1f / Mathf.Max(0.1f, _cooldownRange.x);
    public DamageType AttackDamageType => _damageType;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => _room != null && Time.time >= _nextAttackTime && _attackRoutine == null;
    public bool IsExecuting => _attackRoutine != null;

    public void Configure(BossRoomController room, int phaseIndex, Vector2 cooldownRange)
    {
        _room = room;
        _cooldownRange = new Vector2(
            Mathf.Max(0.1f, Mathf.Min(cooldownRange.x, cooldownRange.y)),
            Mathf.Max(0.1f, Mathf.Max(cooldownRange.x, cooldownRange.y)));

        if (phaseIndex >= 3)
        {
            _damage = 20f;
            _warningDuration = 0.75f;
            _lineCount = 9;
            _ringCount = 16;
            _clusterCount = 10;
            _lineWeight = 0.34f;
            _ringWeight = 0.33f;
            _clusterWeight = 0.33f;
        }
        else
        {
            _damage = 18f;
            _warningDuration = 1.05f;
            _lineCount = 7;
            _ringCount = 12;
            _clusterCount = 7;
            _lineWeight = 0.45f;
            _ringWeight = 0.4f;
            _clusterWeight = 0.15f;
        }

        CacheComponents();
        ScheduleNextAttack();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    private void CacheComponents()
    {
        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (!CanStartAttack)
            return;

        _attackRoutine = StartCoroutine(SpikeAttackRoutine(PickPattern(), scheduleCooldown: true));
    }

    public IEnumerator ExecuteDirectorPattern(SpikePattern pattern)
    {
        if (_attackRoutine != null)
            yield break;

        _attackRoutine = StartCoroutine(SpikeAttackRoutine(pattern, scheduleCooldown: false));
        while (_attackRoutine != null)
            yield return null;
    }

    public SpikePattern PickPattern()
    {
        float total = Mathf.Max(0.01f, _lineWeight + _ringWeight + _clusterWeight);
        float roll = Random.value * total;
        if (roll <= _lineWeight)
            return SpikePattern.Line;
        if (roll <= _lineWeight + _ringWeight)
            return SpikePattern.Ring;
        return SpikePattern.RandomCluster;
    }

    private IEnumerator SpikeAttackRoutine(SpikePattern pattern, bool scheduleCooldown)
    {
        CachePlayerReference();
        if (_playerTransform == null || _room == null)
        {
            _attackRoutine = null;
            yield break;
        }

        if (scheduleCooldown)
            ScheduleNextAttack();

        _enemyBase?.PlayAttackAnimationOneShot();
        _room.PlayBossSpikeCastSound();
        SpawnPattern(pattern);

        yield return new WaitForSeconds(Mathf.Max(0.25f, _warningDuration * 0.6f));
        _attackRoutine = null;
    }

    private void SpawnPattern(SpikePattern pattern)
    {
        switch (pattern)
        {
            case SpikePattern.Line:
                SpawnLinePattern();
                break;
            case SpikePattern.Ring:
                SpawnRingPattern();
                break;
            case SpikePattern.RandomCluster:
                SpawnRandomClusterPattern();
                break;
        }
    }

    private void SpawnLinePattern()
    {
        Vector3 origin = transform.position;
        Vector3 target = _playerTransform != null ? _playerTransform.position : origin + transform.forward * 10f;
        Vector3 direction = Vector3.ProjectOnPlane(target - origin, Vector3.up);
        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;
        direction.Normalize();

        Vector3 start = origin + direction * 4f;
        int count = Mathf.Max(1, _lineCount);
        for (int i = 0; i < count; i++)
        {
            Vector3 point = start + direction * (_lineSpacing * i);
            SpawnSpike(point, i * 0.12f);
        }
    }

    private void SpawnRingPattern()
    {
        Vector3 center = _playerTransform != null ? _playerTransform.position : transform.position;
        if (!_room.TryProjectToBossRoomFloor(center, out center))
            center = transform.position;

        int count = Mathf.Max(3, _ringCount);
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i) / count;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _ringRadius;
            SpawnSpike(point, i * 0.065f);
        }
    }

    private void SpawnRandomClusterPattern()
    {
        Vector3 center = _playerTransform != null ? _playerTransform.position : transform.position;
        int count = Mathf.Max(1, _clusterCount);
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * _clusterRadius;
            Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
            int wave = i / 3;
            SpawnSpike(point, wave * 0.16f);
        }
    }

    private void SpawnSpike(Vector3 position, float startDelay)
    {
        _room?.SpawnBossSpikeHazard(gameObject, position, _spikeRadius, _warningDuration, _damage, _damageType, startDelay);
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private void ScheduleNextAttack()
    {
        _nextAttackTime = Time.time + Random.Range(_cooldownRange.x, _cooldownRange.y);
    }
}

public class BossSpikeHazard : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _warningMaterial;
    private static Material _spikeMaterial;
    private static Material _flashMaterial;
    private static Mesh _spikeMesh;

    private BossRoomController _room;
    private GameObject _owner;
    private GameObject _warningVisual;
    private GameObject _spikeVisual;
    private Light _light;
    private float _radius;
    private float _warningDuration;
    private float _startDelay;
    private float _damage;
    private DamageType _damageType;
    private float _elapsed;
    private float _spikeHeight = DefaultSpikeHeight;
    private bool _erupted;
    private bool _damagedPlayer;

    private const float RiseDuration = 0.28f;
    private const float LingerDuration = 0.45f;
    private const float FadeDuration = 0.22f;
    private const float DefaultSpikeHeight = 4.2f;

    public void Configure(
        BossRoomController room,
        GameObject owner,
        Vector3 floorPosition,
        float radius,
        float warningDuration,
        float damage,
        DamageType damageType,
        float startDelay = 0f,
        float spikeHeight = DefaultSpikeHeight)
    {
        _room = room;
        _owner = owner;
        _radius = Mathf.Max(0.4f, radius);
        _warningDuration = Mathf.Max(0.1f, warningDuration);
        _startDelay = Mathf.Max(0f, startDelay);
        _damage = Mathf.Max(0f, damage);
        _damageType = damageType;
        _spikeHeight = Mathf.Max(1f, spikeHeight);
        transform.position = floorPosition;
        EnsureVisuals();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (_elapsed < _startDelay)
        {
            SetVisualAlpha(0.25f);
            return;
        }

        float activeElapsed = _elapsed - _startDelay;
        if (!_erupted)
        {
            UpdateWarningVisual(activeElapsed);
            if (activeElapsed >= _warningDuration)
                Erupt();
            return;
        }

        float afterErupt = activeElapsed - _warningDuration;
        UpdateSpikeVisual(afterErupt);

        if (afterErupt >= RiseDuration + LingerDuration + FadeDuration)
            Destroy(gameObject);
    }

    private void EnsureVisuals()
    {
        _warningVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _warningVisual.name = "SpikeWarning";
        _warningVisual.transform.SetParent(transform, false);
        _warningVisual.transform.localPosition = Vector3.up * 0.035f;
        _warningVisual.transform.localScale = new Vector3(_radius * 2f, 0.035f, _radius * 2f);

        Collider warningCollider = _warningVisual.GetComponent<Collider>();
        if (warningCollider != null)
            warningCollider.enabled = false;

        Renderer warningRenderer = _warningVisual.GetComponent<Renderer>();
        if (warningRenderer != null)
            warningRenderer.sharedMaterial = GetWarningMaterial();

        _spikeVisual = new GameObject("SpikeVisual", typeof(MeshFilter), typeof(MeshRenderer));
        _spikeVisual.transform.SetParent(transform, false);
        _spikeVisual.transform.localPosition = Vector3.down * _spikeHeight;
        _spikeVisual.transform.localScale = new Vector3(_radius * 1.45f, _spikeHeight, _radius * 1.45f);

        MeshFilter meshFilter = _spikeVisual.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = GetSpikeMesh();

        MeshRenderer spikeRenderer = _spikeVisual.GetComponent<MeshRenderer>();
        spikeRenderer.sharedMaterial = GetSpikeMaterial();

        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = new Color(1f, 0.16f, 0.05f, 1f);
        _light.range = _radius * 5f;
        _light.intensity = 1.2f;
        _light.shadows = LightShadows.None;
    }

    private void UpdateWarningVisual(float activeElapsed)
    {
        float t = Mathf.Clamp01(activeElapsed / _warningDuration);
        float pulse = 1f + Mathf.Sin(Time.time * 20f) * 0.08f;
        float scale = Mathf.Lerp(0.35f, 1f, t) * pulse;
        SetVisualAlpha(Mathf.Lerp(0.45f, 1f, t));

        if (_warningVisual != null)
            _warningVisual.transform.localScale = new Vector3(_radius * 2f * scale, 0.035f, _radius * 2f * scale);

        if (_light != null)
            _light.intensity = Mathf.Lerp(1.2f, 5.5f, t) + Mathf.Sin(Time.time * 24f) * 0.4f;
    }

    private void Erupt()
    {
        _erupted = true;
        _elapsed = _startDelay + _warningDuration;

        Renderer spikeRenderer = _spikeVisual != null ? _spikeVisual.GetComponent<Renderer>() : null;
        if (spikeRenderer != null)
            spikeRenderer.sharedMaterial = GetFlashMaterial();

        if (_warningVisual != null)
            _warningVisual.transform.localScale = new Vector3(_radius * 2.35f, 0.04f, _radius * 2.35f);

        if (_light != null)
            _light.intensity = 8f;

        _room?.PlayBossSpikeEruptSound();
        BossAttackVfx.SpawnImpactPulse(transform.position, _radius, new Color(1f, 0.2f, 0.04f, 0.58f), 0.26f, 0.016f);
        DamagePlayerIfInside();
    }

    private void SetVisualAlpha(float alpha)
    {
        if (_warningVisual == null)
            return;

        Renderer renderer = _warningVisual.GetComponent<Renderer>();
        if (renderer == null || renderer.material == null)
            return;

        Color color = renderer.material.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.material.color = color;
    }

    private void UpdateSpikeVisual(float afterErupt)
    {
        float riseT = Mathf.Clamp01(afterErupt / RiseDuration);
        float eased = 1f - Mathf.Pow(1f - riseT, 3f);

        if (_spikeVisual != null)
            _spikeVisual.transform.localPosition = Vector3.Lerp(Vector3.down * _spikeHeight, Vector3.zero, eased);

        Renderer spikeRenderer = _spikeVisual != null ? _spikeVisual.GetComponent<Renderer>() : null;
        if (spikeRenderer != null && afterErupt > 0.1f)
            spikeRenderer.sharedMaterial = GetSpikeMaterial();

        float fadeStart = RiseDuration + LingerDuration;
        if (afterErupt >= fadeStart)
        {
            float fadeT = Mathf.Clamp01((afterErupt - fadeStart) / FadeDuration);
            if (_spikeVisual != null)
                _spikeVisual.transform.localScale = new Vector3(_radius * 1.45f, _spikeHeight * (1f - fadeT * 0.65f), _radius * 1.45f);
            if (_warningVisual != null)
                _warningVisual.transform.localScale = Vector3.Lerp(_warningVisual.transform.localScale, Vector3.zero, fadeT);
            if (_light != null)
                _light.intensity = Mathf.Lerp(_light.intensity, 0f, fadeT);
        }
    }

    private void DamagePlayerIfInside()
    {
        if (_damagedPlayer || _damage <= 0f)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, _radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            HealthComponent health = hits[i] != null ? hits[i].GetComponentInParent<HealthComponent>() : null;
            if (health == null || !health.IsPlayer || !health.IsAlive)
                continue;

            Vector3 offset = Vector3.ProjectOnPlane(health.transform.position - transform.position, Vector3.up);
            if (offset.sqrMagnitude > _radius * _radius)
                continue;

            health.TakeDamage(new DamageInfo
            {
                Amount = _damage,
                Type = _damageType,
                Source = _owner
            });
            _damagedPlayer = true;
            return;
        }
    }

    private static Mesh GetSpikeMesh()
    {
        if (_spikeMesh != null)
            return _spikeMesh;

        const int segments = 18;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 6];

        vertices[0] = Vector3.up;
        vertices[1] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            vertices[i + 2] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
        }

        int index = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = i == segments - 1 ? 0 : i + 1;
            triangles[index++] = 0;
            triangles[index++] = i + 2;
            triangles[index++] = next + 2;

            triangles[index++] = 1;
            triangles[index++] = next + 2;
            triangles[index++] = i + 2;
        }

        _spikeMesh = new Mesh
        {
            name = "BossSpike_RuntimeMesh",
            vertices = vertices,
            triangles = triangles
        };
        _spikeMesh.RecalculateNormals();
        _spikeMesh.RecalculateBounds();
        _spikeMesh.hideFlags = HideFlags.HideAndDontSave;
        return _spikeMesh;
    }

    private static Material GetWarningMaterial()
    {
        if (_warningMaterial != null)
            return _warningMaterial;

        _warningMaterial = CreateMaterial("BossSpike_Warning_Runtime", new Color(1f, 0.08f, 0.02f, 0.5f), new Color(4f, 0.25f, 0.08f, 0.5f), transparent: true);
        return _warningMaterial;
    }

    private static Material GetSpikeMaterial()
    {
        if (_spikeMaterial != null)
            return _spikeMaterial;

        _spikeMaterial = CreateMaterial("BossSpike_Runtime", new Color(0.42f, 0.04f, 0.03f, 1f), new Color(1.2f, 0.08f, 0.04f, 1f), transparent: false);
        return _spikeMaterial;
    }

    private static Material GetFlashMaterial()
    {
        if (_flashMaterial != null)
            return _flashMaterial;

        _flashMaterial = CreateMaterial("BossSpike_Flash_Runtime", new Color(1f, 0.75f, 0.08f, 1f), new Color(5f, 2.2f, 0.25f, 1f), transparent: false);
        return _flashMaterial;
    }

    private static Material CreateMaterial(string name, Color baseColor, Color emissionColor, bool transparent)
    {
        Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find(transparent ? "Unlit/Color" : "Standard");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new(shader)
        {
            name = name
        };

        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, baseColor);
        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emissionColor);
        }

        if (transparent)
            ConfigureTransparentMaterial(material);

        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}

[DisallowMultipleComponent]
public class BossPhaseThreeDirector : MonoBehaviour
{
    private enum ComboType
    {
        BlinkBarrage,
        ChaseBursts,
        RingVolley,
        PressureSummon,
        SpikeSurge,
        DashSlam,
        ArenaSweep
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _telegraphMaterial;

    [SerializeField] private float _projectileDamage = 13f;
    [SerializeField] private float _projectileSpeed = 42f;
    [SerializeField] private float _projectileRadius = 0.85f;
    [SerializeField] private float _projectileMaxDistance = 220f;
    [SerializeField] private float _groundBurstDamage = 22f;
    [SerializeField] private float _groundBurstRadius = 4.2f;
    [SerializeField] private float _groundBurstTelegraph = 0.85f;
    [SerializeField] private float _groundBurstLinger = 0.28f;
    [SerializeField] private Vector3 _muzzleOffset = new(0f, 3f, 1f);

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private HealthComponent _health;
    private NavMeshAgent _agent;
    private BossSpikeAttackModule _spikeAttack;
    private BossDashSlamModule _dashSlam;
    private BossArenaSweepModule _arenaSweep;
    private Transform _playerTransform;
    private Vector2 _cooldownRange = new(3f, 4.5f);
    private Coroutine _routine;
    private ComboType _lastCombo = (ComboType)(-1);
    private Vector3 _lastPlayerPosition;

    public void Configure(BossRoomController room, Vector2 cooldownRange)
    {
        _room = room;
        _cooldownRange = new Vector2(
            Mathf.Max(0.2f, Mathf.Min(cooldownRange.x, cooldownRange.y)),
            Mathf.Max(0.2f, Mathf.Max(cooldownRange.x, cooldownRange.y)));
        CacheComponents();
        RestartRoutine();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        if (_room != null)
            RestartRoutine();
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private void CacheComponents()
    {
        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
        if (_health == null)
            _health = GetComponent<HealthComponent>();
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();
        if (_spikeAttack == null)
            _spikeAttack = GetComponent<BossSpikeAttackModule>();
        if (_dashSlam == null)
            _dashSlam = GetComponent<BossDashSlamModule>();
        if (_arenaSweep == null)
            _arenaSweep = GetComponent<BossArenaSweepModule>();
    }

    private void RestartRoutine()
    {
        if (!isActiveAndEnabled)
            return;

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(ComboLoop());
    }

    private IEnumerator ComboLoop()
    {
        yield return new WaitForSeconds(1f);

        while (_room != null && _health != null && _health.IsAlive)
        {
            CachePlayerReference();
            if (_playerTransform == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            ComboType combo = PickNextCombo();
            yield return ExecuteCombo(combo);

            float healthScale = GetHealthRatio() <= 0.3f ? 0.65f : GetHealthRatio() <= 0.6f ? 0.82f : 1f;
            float cooldown = Random.Range(_cooldownRange.x, _cooldownRange.y) * healthScale;
            yield return new WaitForSeconds(Mathf.Max(1.2f, cooldown));
        }

        _routine = null;
    }

    private ComboType PickNextCombo()
    {
        ComboType next;
        do
        {
            next = (ComboType)Random.Range(0, 7);
        }
        while (next == _lastCombo);

        _lastCombo = next;
        return next;
    }

    private IEnumerator ExecuteCombo(ComboType combo)
    {
        float lockDuration = combo == ComboType.ChaseBursts ? 1.25f : 1.05f;
        _enemyBase?.DisableAttacksFor(lockDuration);

        switch (combo)
        {
            case ComboType.BlinkBarrage:
                yield return BlinkBarrageRoutine();
                break;
            case ComboType.ChaseBursts:
                yield return ChaseBurstsRoutine();
                break;
            case ComboType.RingVolley:
                yield return RingVolleyRoutine();
                break;
            case ComboType.PressureSummon:
                yield return PressureSummonRoutine();
                break;
            case ComboType.SpikeSurge:
                yield return SpikeSurgeRoutine();
                break;
            case ComboType.DashSlam:
                yield return DashSlamRoutine();
                break;
            case ComboType.ArenaSweep:
                yield return ArenaSweepRoutine();
                break;
        }
    }

    private IEnumerator BlinkBarrageRoutine()
    {
        CachePlayerReference();
        if (_playerTransform == null)
            yield break;

        if (_room.TryPickPhaseThreeArenaPoint(_playerTransform.position, 9f, 16f, out Vector3 destination))
            yield return BlinkTo(destination, 0.22f);

        int volleys = GetHealthRatio() <= 0.6f ? 3 : 2;
        int projectileCount = GetHealthRatio() <= 0.3f ? 9 : 7;
        float spread = GetHealthRatio() <= 0.3f ? 62f : 52f;

        for (int i = 0; i < volleys; i++)
        {
            FireFan(projectileCount, spread, _projectileDamage);
            yield return new WaitForSeconds(0.22f);
        }
    }

    private IEnumerator ChaseBurstsRoutine()
    {
        CachePlayerReference();
        if (_playerTransform == null)
            yield break;

        int burstCount = GetHealthRatio() <= 0.3f ? 6 : GetHealthRatio() <= 0.6f ? 5 : 4;
        float telegraph = GetHealthRatio() <= 0.3f ? 0.68f : _groundBurstTelegraph;
        Vector3 previousPlayerPosition = _lastPlayerPosition.sqrMagnitude > 0.001f ? _lastPlayerPosition : _playerTransform.position;

        for (int i = 0; i < burstCount; i++)
        {
            CachePlayerReference();
            if (_playerTransform == null)
                yield break;

            Vector3 playerVelocity = (_playerTransform.position - previousPlayerPosition) / Mathf.Max(Time.deltaTime, 0.02f);
            previousPlayerPosition = _playerTransform.position;
            _lastPlayerPosition = _playerTransform.position;

            Vector3 predicted = _playerTransform.position + Vector3.ProjectOnPlane(playerVelocity, Vector3.up).normalized * Mathf.Lerp(1.8f, 4.2f, i / Mathf.Max(1f, burstCount - 1f));
            Vector2 scatter = Random.insideUnitCircle * 2.6f;
            predicted += new Vector3(scatter.x, 0f, scatter.y);

            if (_room.TryProjectToBossRoomFloor(predicted, out Vector3 burstPosition))
            {
                _room.SpawnBossPhaseGroundBurst(
                    gameObject,
                    burstPosition,
                    _groundBurstRadius,
                    telegraph,
                    _groundBurstLinger,
                    _groundBurstDamage,
                    DamageType.Energy);
            }

            yield return new WaitForSeconds(0.18f);
        }
    }

    private IEnumerator RingVolleyRoutine()
    {
        Vector3 origin = GetMuzzleOrigin();
        Vector3 telegraphPosition = transform.position;
        if (_room != null && !_room.TryProjectToBossRoomFloor(transform.position, out telegraphPosition))
            telegraphPosition = transform.position;
        yield return TelegraphPulse(telegraphPosition, 5.5f, 0.35f);
        BossAttackVfx.SpawnImpactPulse(telegraphPosition, 5.5f, new Color(0.2f, 0.9f, 1f, 0.46f), 0.28f, 0.012f);

        int count = GetHealthRatio() <= 0.3f ? 24 : GetHealthRatio() <= 0.6f ? 20 : 16;
        float damage = Mathf.Max(1f, _projectileDamage * 0.85f);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f * i) / count;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            _room.FireBossPhaseProjectile(gameObject, origin, direction, damage, DamageType.Energy, _projectileMaxDistance, _projectileSpeed * 0.9f, _projectileRadius);
        }

        if (GetHealthRatio() <= 0.3f)
        {
            yield return new WaitForSeconds(0.28f);
            FireFan(7, 42f, damage);
        }
    }

    private IEnumerator PressureSummonRoutine()
    {
        int summonCount = GetHealthRatio() <= 0.3f ? 4 : 3;
        _room.TrySummonEnemies(gameObject, summonCount);
        yield return new WaitForSeconds(0.35f);

        if (Random.value < 0.55f)
        {
            FireFan(GetHealthRatio() <= 0.3f ? 9 : 7, 48f, _projectileDamage);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                CachePlayerReference();
                if (_playerTransform == null)
                    yield break;

                Vector2 offset = Random.insideUnitCircle * 5f;
                Vector3 candidate = _playerTransform.position + new Vector3(offset.x, 0f, offset.y);
                if (_room.TryProjectToBossRoomFloor(candidate, out Vector3 burstPosition))
                {
                    _room.SpawnBossPhaseGroundBurst(
                        gameObject,
                        burstPosition,
                        _groundBurstRadius,
                        _groundBurstTelegraph,
                        _groundBurstLinger,
                        _groundBurstDamage,
                        DamageType.Energy);
                }

                yield return new WaitForSeconds(0.12f);
            }
        }
    }

    private IEnumerator SpikeSurgeRoutine()
    {
        if (_spikeAttack == null)
            _spikeAttack = GetComponent<BossSpikeAttackModule>();
        if (_spikeAttack == null)
            yield break;

        BossSpikeAttackModule.SpikePattern pattern = GetHealthRatio() <= 0.3f
            ? _spikeAttack.PickPattern()
            : Random.value < 0.5f ? BossSpikeAttackModule.SpikePattern.Line : BossSpikeAttackModule.SpikePattern.Ring;

        yield return _spikeAttack.ExecuteDirectorPattern(pattern);
    }

    private IEnumerator DashSlamRoutine()
    {
        if (_dashSlam == null)
            _dashSlam = GetComponent<BossDashSlamModule>();
        if (_dashSlam == null)
            yield break;

        yield return _dashSlam.ExecuteDirectorPattern();
    }

    private IEnumerator ArenaSweepRoutine()
    {
        if (_arenaSweep == null)
            _arenaSweep = GetComponent<BossArenaSweepModule>();
        if (_arenaSweep == null)
            yield break;

        yield return _arenaSweep.ExecuteDirectorPattern();
    }

    private IEnumerator BlinkTo(Vector3 destination, float windup)
    {
        BossAttackVfx.SpawnImpactPulse(transform.position, 3.2f, new Color(0.75f, 0.95f, 1f, 0.42f), 0.22f, 0.008f);
        yield return TelegraphPulse(destination + Vector3.up * 0.05f, 3.2f, windup);

        CachePlayerReference();
        Quaternion lookRotation = GetLookRotation(destination);
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Warp(destination);
            _agent.ResetPath();
            transform.rotation = lookRotation;
        }
        else
        {
            transform.SetPositionAndRotation(destination, lookRotation);
        }

        BossAttackVfx.SpawnImpactPulse(destination, 3.6f, new Color(0.75f, 0.95f, 1f, 0.5f), 0.24f, 0.012f);
    }

    private IEnumerator TelegraphPulse(Vector3 position, float radius, float duration)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = $"{gameObject.name}_PhaseThreeTelegraph";
        visual.transform.position = position;
        visual.transform.rotation = Quaternion.identity;

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = GetTelegraphMaterial();

        _room?.RegisterPhaseHazard(visual);

        float elapsed = 0f;
        while (elapsed < duration && visual != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float diameter = Mathf.Lerp(radius * 0.2f, radius * 2f, t);
            visual.transform.localScale = new Vector3(diameter, 0.04f, diameter);
            visual.transform.position = position + Vector3.up * 0.05f;
            yield return null;
        }

        if (visual != null)
            Destroy(visual);
    }

    private void FireFan(int count, float spreadDegrees, float damage)
    {
        CachePlayerReference();
        if (_playerTransform == null || _room == null)
            return;

        Vector3 origin = GetMuzzleOrigin();
        Vector3 target = _playerTransform.position + Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(target - origin, Vector3.up);
        if (forward.sqrMagnitude <= 0.001f)
            forward = transform.forward;
        forward.Normalize();

        int projectileCount = Mathf.Max(1, count);
        for (int i = 0; i < projectileCount; i++)
        {
            float t = projectileCount == 1 ? 0.5f : (float)i / (projectileCount - 1);
            float angle = Mathf.Lerp(-spreadDegrees * 0.5f, spreadDegrees * 0.5f, t);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            _room.FireBossPhaseProjectile(gameObject, origin, direction, damage, DamageType.Energy, _projectileMaxDistance, _projectileSpeed, _projectileRadius);
        }
    }

    private Vector3 GetMuzzleOrigin()
    {
        return transform.TransformPoint(_muzzleOffset);
    }

    private Quaternion GetLookRotation(Vector3 from)
    {
        if (_playerTransform == null)
            return transform.rotation;

        Vector3 direction = Vector3.ProjectOnPlane(_playerTransform.position - from, Vector3.up);
        return direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : transform.rotation;
    }

    private float GetHealthRatio()
    {
        if (_health == null || _health.MaxHealth <= 0)
            return 1f;

        return Mathf.Clamp01((float)_health.CurrentHealth / _health.MaxHealth);
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private static Material GetTelegraphMaterial()
    {
        if (_telegraphMaterial != null)
            return _telegraphMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        _telegraphMaterial = new Material(shader)
        {
            name = "BossPhaseThreeTelegraph_Runtime"
        };

        Color baseColor = new(0.1f, 0.8f, 1f, 0.45f);
        Color emissionColor = new(0.4f, 3.5f, 5f, 0.45f);

        if (_telegraphMaterial.HasProperty(BaseColorId))
            _telegraphMaterial.SetColor(BaseColorId, baseColor);
        if (_telegraphMaterial.HasProperty(ColorId))
            _telegraphMaterial.SetColor(ColorId, baseColor);
        if (_telegraphMaterial.HasProperty(EmissionColorId))
        {
            _telegraphMaterial.EnableKeyword("_EMISSION");
            _telegraphMaterial.SetColor(EmissionColorId, emissionColor);
        }

        ConfigureTransparentMaterial(_telegraphMaterial);
        _telegraphMaterial.hideFlags = HideFlags.HideAndDontSave;
        return _telegraphMaterial;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}

[DisallowMultipleComponent]
public class BossProjectileBarrageModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 6f;
    [SerializeField] private float _maxAttackRange = 140f;
    [SerializeField] private float _attackDamage = 14f;
    [SerializeField] private float _attackRate = 0.12f;
    [SerializeField] private DamageType _attackDamageType = DamageType.Energy;
    [SerializeField] private string _attackAnimTrigger = "Ranged";
    [SerializeField] private int _projectileCount = 7;
    [SerializeField] private float _spreadDegrees = 42f;
    [SerializeField] private float _projectileSpeed = 42f;
    [SerializeField] private float _projectileRadius = 0.85f;
    [SerializeField] private float _projectileMaxDistance = 220f;
    [SerializeField] private float _fireDelay = 0.35f;
    [SerializeField] private Vector3 _muzzleOffset = new(0f, 3f, 1f);

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private float _nextBarrageAt;
    private Coroutine _attackRoutine;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => _attackDamage;
    public float AttackRate => _attackRate;
    public DamageType AttackDamageType => _attackDamageType;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => Time.time >= _nextBarrageAt && _attackRoutine == null;
    public bool IsExecuting => _attackRoutine != null;

    public void Configure(BossRoomController room)
    {
        _room = room;
        ScheduleNextBarrage();
    }

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        ScheduleNextBarrage();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (_attackRoutine == null && Time.time >= _nextBarrageAt)
            _attackRoutine = StartCoroutine(BarrageRoutine());
    }

    private IEnumerator BarrageRoutine()
    {
        ScheduleNextBarrage();
        _enemyBase?.PlayAttackAnimationOneShot();

        yield return new WaitForSeconds(Mathf.Max(0f, _fireDelay));

        if (_enemyBase == null || !_enemyBase.IsAlive || _enemyBase.CurrentState != EnemyState.Attack || !ReferenceEquals(_enemyBase.CurrentAttack, this))
        {
            _attackRoutine = null;
            yield break;
        }

        FireBarrage();
        _attackRoutine = null;
    }

    private void FireBarrage()
    {
        CachePlayerReference();
        if (_playerTransform == null)
            return;

        Vector3 origin = transform.TransformPoint(_muzzleOffset);
        Vector3 target = _playerTransform.position + Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(target - origin, Vector3.up);
        if (forward.sqrMagnitude <= 0.001f)
            forward = transform.forward;
        forward.Normalize();

        int count = Mathf.Max(1, _projectileCount);
        float spread = Mathf.Max(0f, _spreadDegrees);
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float angle = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;

            GameObject projectileObject = new($"{gameObject.name}_BarrageProjectile");
            EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
            projectile.Launch(
                gameObject,
                origin,
                direction,
                AttackDamage,
                AttackDamageType,
                _projectileMaxDistance,
                _projectileSpeed,
                _projectileRadius);
        }
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private void ScheduleNextBarrage()
    {
        _nextBarrageAt = Time.time + (1f / Mathf.Max(0.01f, _attackRate));
    }
}

[DisallowMultipleComponent]
public class BossGroundBurstModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 0f;
    [SerializeField] private float _maxAttackRange = 55f;
    [SerializeField] private float _attackDamage = 24f;
    [SerializeField] private float _attackRate = 0.1f;
    [SerializeField] private DamageType _attackDamageType = DamageType.Energy;
    [SerializeField] private string _attackAnimTrigger = "AreaOfEffect";
    [SerializeField] private int _burstCount = 4;
    [SerializeField] private float _burstRadius = 4.5f;
    [SerializeField] private float _telegraphDuration = 1.05f;
    [SerializeField] private float _lingerDuration = 0.35f;
    [SerializeField] private float _scatterRadius = 9f;

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private float _nextBurstAt;
    private Coroutine _attackRoutine;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => _attackDamage;
    public float AttackRate => _attackRate;
    public DamageType AttackDamageType => _attackDamageType;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => Time.time >= _nextBurstAt && _attackRoutine == null;
    public bool IsExecuting => _attackRoutine != null;

    public void Configure(BossRoomController room)
    {
        _room = room;
        ScheduleNextBurst();
    }

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        ScheduleNextBurst();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (_attackRoutine == null && Time.time >= _nextBurstAt)
            _attackRoutine = StartCoroutine(BurstRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        ScheduleNextBurst();
        _enemyBase?.PlayAttackAnimationOneShot();

        yield return new WaitForSeconds(0.25f);

        if (_enemyBase == null || !_enemyBase.IsAlive || _enemyBase.CurrentState != EnemyState.Attack || !ReferenceEquals(_enemyBase.CurrentAttack, this))
        {
            _attackRoutine = null;
            yield break;
        }

        SpawnBursts();
        _attackRoutine = null;
    }

    private void SpawnBursts()
    {
        CachePlayerReference();
        if (_playerTransform == null)
            return;

        int count = Mathf.Max(1, _burstCount);
        for (int i = 0; i < count; i++)
        {
            Vector2 scatter = Random.insideUnitCircle * _scatterRadius;
            Vector3 position = _playerTransform.position + new Vector3(scatter.x, 0f, scatter.y);

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                position = hit.position;

            GameObject hazardObject = new($"{gameObject.name}_GroundBurst");
            BossGroundBurstHazard hazard = hazardObject.AddComponent<BossGroundBurstHazard>();
            hazard.Configure(
                gameObject,
                position,
                _burstRadius,
                _telegraphDuration,
                _lingerDuration,
                AttackDamage,
                AttackDamageType);

            _room?.RegisterPhaseHazard(hazardObject);
        }
    }

    private void CachePlayerReference()
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }
    }

    private void ScheduleNextBurst()
    {
        _nextBurstAt = Time.time + (1f / Mathf.Max(0.01f, _attackRate));
    }
}

public class BossGroundBurstHazard : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _telegraphMaterial;
    private static Material _detonateMaterial;

    private GameObject _owner;
    private GameObject _visual;
    private float _radius;
    private float _telegraphDuration;
    private float _lingerDuration;
    private float _damage;
    private DamageType _damageType;
    private float _elapsed;
    private bool _detonated;

    public void Configure(
        GameObject owner,
        Vector3 position,
        float radius,
        float telegraphDuration,
        float lingerDuration,
        float damage,
        DamageType damageType)
    {
        _owner = owner;
        _radius = Mathf.Max(0.5f, radius);
        _telegraphDuration = Mathf.Max(0.05f, telegraphDuration);
        _lingerDuration = Mathf.Max(0.05f, lingerDuration);
        _damage = damage;
        _damageType = damageType;
        transform.position = position;
        EnsureVisual();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (!_detonated)
        {
            float t = Mathf.Clamp01(_elapsed / _telegraphDuration);
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.06f;
            SetVisualScale(Mathf.Lerp(0.25f, 1f, t) * pulse);

            if (_elapsed >= _telegraphDuration)
                Detonate();

            return;
        }

        float lingerT = Mathf.Clamp01((_elapsed - _telegraphDuration) / _lingerDuration);
        SetVisualScale(Mathf.Lerp(1.08f, 0.75f, lingerT));

        if (_elapsed >= _telegraphDuration + _lingerDuration)
            Destroy(gameObject);
    }

    private void Detonate()
    {
        _detonated = true;

        Renderer renderer = _visual != null ? _visual.GetComponent<Renderer>() : null;
        if (renderer != null)
            renderer.sharedMaterial = GetDetonateMaterial();

        BossAttackVfx.SpawnImpactPulse(transform.position, _radius, new Color(1f, 0.58f, 0.08f, 0.55f), 0.32f, 0.02f);

        Collider[] hits = Physics.OverlapSphere(transform.position, _radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            HealthComponent health = hits[i] != null ? hits[i].GetComponentInParent<HealthComponent>() : null;
            if (health == null || !health.IsPlayer || !health.IsAlive)
                continue;

            Vector3 offset = Vector3.ProjectOnPlane(health.transform.position - transform.position, Vector3.up);
            if (offset.sqrMagnitude > _radius * _radius)
                continue;

            health.TakeDamage(new DamageInfo
            {
                Amount = _damage,
                Type = _damageType,
                Source = _owner
            });
            break;
        }
    }

    private void EnsureVisual()
    {
        if (_visual != null)
            return;

        _visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _visual.name = "GroundBurstTelegraph";
        _visual.transform.SetParent(transform, false);

        Collider collider = _visual.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        Renderer renderer = _visual.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = GetTelegraphMaterial();

        SetVisualScale(0.25f);
    }

    private void SetVisualScale(float normalized)
    {
        if (_visual == null)
            return;

        float diameter = _radius * 2f * Mathf.Max(0.05f, normalized);
        _visual.transform.localScale = new Vector3(diameter, 0.05f, diameter);
        _visual.transform.localPosition = Vector3.up * 0.05f;
    }

    private static Material GetTelegraphMaterial()
    {
        if (_telegraphMaterial != null)
            return _telegraphMaterial;

        _telegraphMaterial = CreateMaterial("BossGroundBurst_Telegraph", new Color(1f, 0.1f, 0.02f, 0.42f), new Color(3f, 0.35f, 0.1f, 0.42f));
        return _telegraphMaterial;
    }

    private static Material GetDetonateMaterial()
    {
        if (_detonateMaterial != null)
            return _detonateMaterial;

        _detonateMaterial = CreateMaterial("BossGroundBurst_Detonate", new Color(1f, 0.85f, 0.1f, 0.65f), new Color(4f, 2.5f, 0.4f, 0.65f));
        return _detonateMaterial;
    }

    private static Material CreateMaterial(string name, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new(shader)
        {
            name = name
        };

        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, baseColor);
        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emissionColor);
        }

        ConfigureTransparentMaterial(material);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}

[DisallowMultipleComponent]
public class BossTeleportAbility : MonoBehaviour
{
    private const float DEFAULT_TELEPORT_DISTANCE = 34f;
    private const float DEFAULT_MIN_PLAYER_DISTANCE = 5f;
    private const float DEFAULT_MAX_PLAYER_DISTANCE = 9f;
    private const float DEFAULT_MIN_COOLDOWN = 20f;
    private const float DEFAULT_MAX_COOLDOWN = 30f;
    private const float DEFAULT_CHANCE = 0.25f;
    private const float DEFAULT_FADE_DURATION = 0.28f;
    private const float DEFAULT_HIDDEN_DURATION = 0.18f;
    private const float DEFAULT_POST_TELEPORT_ATTACK_LOCK = 2.5f;
    private const int TELEPORT_SAMPLE_ATTEMPTS = 16;

    private struct MaterialColorState
    {
        public Material Material;
        public Color BaseColor;
        public bool HasBaseColor;
        public Color Color;
        public bool HasColor;
        public Color EmissionColor;
        public bool HasEmissionColor;
    }

    private struct RendererMaterialState
    {
        public Renderer Renderer;
        public Material[] SharedMaterials;
    }

    private EnemyBase _enemyBase;
    private HealthComponent _health;
    private NavMeshAgent _agent;
    private Renderer[] _renderers;
    private Collider[] _colliders;
    private MaterialColorState[] _materialStates;
    private RendererMaterialState[] _rendererMaterialStates;
    private Material _whiteTeleportMaterial;

    private float _teleportDistance = DEFAULT_TELEPORT_DISTANCE;
    private float _minPlayerDistance = DEFAULT_MIN_PLAYER_DISTANCE;
    private float _maxPlayerDistance = DEFAULT_MAX_PLAYER_DISTANCE;
    private float _minCooldown = DEFAULT_MIN_COOLDOWN;
    private float _maxCooldown = DEFAULT_MAX_COOLDOWN;
    private float _chance = DEFAULT_CHANCE;
    private float _fadeDuration = DEFAULT_FADE_DURATION;
    private float _hiddenDuration = DEFAULT_HIDDEN_DURATION;
    private float _postTeleportAttackLock = DEFAULT_POST_TELEPORT_ATTACK_LOCK;
    private AudioClip _teleportSound;
    private float _teleportSoundVolume = 1f;
    private float _nextTeleportAt;
    private bool _isTeleporting;
    private Coroutine _teleportRoutine;

    public void Configure(
        float teleportDistance,
        float minPlayerDistance,
        float maxPlayerDistance,
        float minCooldown,
        float maxCooldown,
        float chance,
        float fadeDuration,
        float hiddenDuration,
        float postTeleportAttackLock,
        AudioClip teleportSound,
        float teleportSoundVolume)
    {
        _teleportDistance = Mathf.Max(0f, teleportDistance);
        _minPlayerDistance = Mathf.Max(0f, minPlayerDistance);
        _maxPlayerDistance = Mathf.Max(_minPlayerDistance, maxPlayerDistance);
        _minCooldown = Mathf.Max(0.1f, minCooldown);
        _maxCooldown = Mathf.Max(_minCooldown, maxCooldown);
        _chance = Mathf.Clamp01(chance);
        _fadeDuration = Mathf.Max(0.01f, fadeDuration);
        _hiddenDuration = Mathf.Max(0f, hiddenDuration);
        _postTeleportAttackLock = Mathf.Max(0f, postTeleportAttackLock);
        _teleportSound = teleportSound;
        _teleportSoundVolume = Mathf.Clamp01(teleportSoundVolume);
        ScheduleNextTeleport();
    }

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        _health = GetComponent<HealthComponent>();
        _agent = GetComponent<NavMeshAgent>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        CacheMaterialStates();
        if (_health != null)
            _health.OnDied += CancelTeleport;
        ScheduleNextTeleport();
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnDied -= CancelTeleport;
    }

    private void OnDisable()
    {
        if (_teleportRoutine != null)
        {
            StopCoroutine(_teleportRoutine);
            _teleportRoutine = null;
        }

        SetRenderersVisible(true);
        SetCollidersEnabled(true);
        RestoreRendererMaterials();
        RestoreMaterialColors();
        _isTeleporting = false;
    }

    private void Update()
    {
        if (Time.time < _nextTeleportAt)
            return;

        TryStartTeleport(requireFarDistance: true);
    }

    private void TryStartTeleport(bool requireFarDistance)
    {
        if (_isTeleporting || _health == null || !_health.IsAlive)
            return;

        Transform player = ResolvePlayerTransform();
        if (player == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (requireFarDistance && distanceToPlayer < _teleportDistance)
            return;

        ScheduleNextTeleport();

        if (Random.value > _chance)
            return;

        if (TryFindTeleportPosition(player, out Vector3 destination))
            _teleportRoutine = StartCoroutine(TeleportRoutine(destination, player.position));
    }

    private Transform ResolvePlayerTransform()
    {
        if (_enemyBase != null && _enemyBase.PlayerTransform != null)
            return _enemyBase.PlayerTransform;

        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void ScheduleNextTeleport()
    {
        _nextTeleportAt = Time.time + Random.Range(_minCooldown, _maxCooldown);
    }

    private bool TryFindTeleportPosition(Transform player, out Vector3 destination)
    {
        float minDistance = Mathf.Min(_minPlayerDistance, _maxPlayerDistance);
        float maxDistance = Mathf.Max(_minPlayerDistance, _maxPlayerDistance);

        for (int i = 0; i < TELEPORT_SAMPLE_ATTEMPTS; i++)
        {
            Vector2 direction2D = Random.insideUnitCircle.normalized;
            if (direction2D.sqrMagnitude <= 0.001f)
                direction2D = Vector2.right;

            float distance = Random.Range(minDistance, maxDistance);
            Vector3 candidate = player.position + new Vector3(direction2D.x, 0f, direction2D.y) * distance;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                destination = hit.position;
                return true;
            }
        }

        Vector3 behindPlayer = player.position - player.forward * Mathf.Lerp(minDistance, maxDistance, 0.5f);
        if (NavMesh.SamplePosition(behindPlayer, out NavMeshHit fallbackHit, 8f, NavMesh.AllAreas))
        {
            destination = fallbackHit.position;
            return true;
        }

        destination = behindPlayer;
        return true;
    }

    private IEnumerator TeleportRoutine(Vector3 destination, Vector3 playerPosition)
    {
        _isTeleporting = true;

        bool agentWasEnabled = _agent != null && _agent.enabled;
        bool agentWasStopped = _agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.isStopped;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = true;

        AudioManager.Instance?.PlayBossSfx(_teleportSound, _teleportSoundVolume);
        SetCollidersEnabled(false);
        ApplyWhiteMaterialOverride();
        yield return FadeColor(Color.white, _fadeDuration);

        if (_health == null || !_health.IsAlive)
        {
            CancelTeleport();
            yield break;
        }

        SetRenderersVisible(false);
        yield return new WaitForSeconds(_hiddenDuration);

        if (_health == null || !_health.IsAlive)
        {
            CancelTeleport();
            yield break;
        }

        Quaternion lookRotation = GetLookRotation(destination, playerPosition);
        if (_agent != null && agentWasEnabled && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Warp(destination);
            transform.rotation = lookRotation;
        }
        else
        {
            transform.SetPositionAndRotation(destination, lookRotation);
        }

        SetRenderersVisible(true);
        RestoreRendererMaterials();
        ApplyColor(Color.white);
        SetCollidersEnabled(true);
        _enemyBase?.DisableAttacksFor(_postTeleportAttackLock);

        yield return FadeBack(_fadeDuration);

        if (_agent != null && agentWasEnabled && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = agentWasStopped;

        _isTeleporting = false;
        _teleportRoutine = null;
    }

    private void CancelTeleport()
    {
        if (_teleportRoutine != null)
        {
            StopCoroutine(_teleportRoutine);
            _teleportRoutine = null;
        }

        SetRenderersVisible(true);
        SetCollidersEnabled(true);
        RestoreRendererMaterials();
        RestoreMaterialColors();
        _isTeleporting = false;
    }

    private Quaternion GetLookRotation(Vector3 from, Vector3 target)
    {
        Vector3 direction = Vector3.ProjectOnPlane(target - from, Vector3.up);
        return direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : transform.rotation;
    }

    private IEnumerator FadeColor(Color targetColor, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            LerpMaterialColors(targetColor, t);
            yield return null;
        }
    }

    private IEnumerator FadeBack(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / duration);
            LerpMaterialColors(Color.white, t);
            yield return null;
        }

        RestoreMaterialColors();
    }

    private void CacheMaterialStates()
    {
        List<MaterialColorState> states = new();
        List<RendererMaterialState> rendererStates = new();
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            rendererStates.Add(new RendererMaterialState
            {
                Renderer = renderer,
                SharedMaterials = renderer.sharedMaterials
            });

            Material[] materials = renderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                    continue;

                MaterialColorState state = new()
                {
                    Material = material,
                    HasBaseColor = material.HasProperty("_BaseColor"),
                    HasColor = material.HasProperty("_Color"),
                    HasEmissionColor = material.HasProperty("_EmissionColor")
                };

                if (state.HasBaseColor)
                    state.BaseColor = material.GetColor("_BaseColor");
                if (state.HasColor)
                    state.Color = material.GetColor("_Color");
                if (state.HasEmissionColor)
                    state.EmissionColor = material.GetColor("_EmissionColor");

                states.Add(state);
            }
        }

        _materialStates = states.ToArray();
        _rendererMaterialStates = rendererStates.ToArray();
    }

    private void LerpMaterialColors(Color targetColor, float t)
    {
        if (_materialStates == null)
            return;

        for (int i = 0; i < _materialStates.Length; i++)
        {
            MaterialColorState state = _materialStates[i];
            if (state.Material == null)
                continue;

            if (state.HasBaseColor)
                state.Material.SetColor("_BaseColor", Color.Lerp(state.BaseColor, targetColor, t));
            if (state.HasColor)
                state.Material.SetColor("_Color", Color.Lerp(state.Color, targetColor, t));
            if (state.HasEmissionColor)
            {
                state.Material.EnableKeyword("_EMISSION");
                state.Material.SetColor("_EmissionColor", Color.Lerp(state.EmissionColor, targetColor * 1.3f, t));
            }
        }
    }

    private void ApplyColor(Color color)
    {
        if (_materialStates == null)
            return;

        for (int i = 0; i < _materialStates.Length; i++)
        {
            MaterialColorState state = _materialStates[i];
            if (state.Material == null)
                continue;

            if (state.HasBaseColor)
                state.Material.SetColor("_BaseColor", color);
            if (state.HasColor)
                state.Material.SetColor("_Color", color);
            if (state.HasEmissionColor)
            {
                state.Material.EnableKeyword("_EMISSION");
                state.Material.SetColor("_EmissionColor", color * 1.3f);
            }
        }
    }

    private void RestoreMaterialColors()
    {
        if (_materialStates == null)
            return;

        for (int i = 0; i < _materialStates.Length; i++)
        {
            MaterialColorState state = _materialStates[i];
            if (state.Material == null)
                continue;

            if (state.HasBaseColor)
                state.Material.SetColor("_BaseColor", state.BaseColor);
            if (state.HasColor)
                state.Material.SetColor("_Color", state.Color);
            if (state.HasEmissionColor)
                state.Material.SetColor("_EmissionColor", state.EmissionColor);
        }
    }

    private void ApplyWhiteMaterialOverride()
    {
        if (_rendererMaterialStates == null)
            return;

        Material whiteMaterial = GetWhiteTeleportMaterial();
        if (whiteMaterial == null)
            return;

        for (int i = 0; i < _rendererMaterialStates.Length; i++)
        {
            RendererMaterialState state = _rendererMaterialStates[i];
            if (state.Renderer == null)
                continue;

            int materialCount = Mathf.Max(1, state.SharedMaterials != null ? state.SharedMaterials.Length : 0);
            Material[] overrideMaterials = new Material[materialCount];
            for (int j = 0; j < overrideMaterials.Length; j++)
                overrideMaterials[j] = whiteMaterial;

            state.Renderer.sharedMaterials = overrideMaterials;
        }
    }

    private void RestoreRendererMaterials()
    {
        if (_rendererMaterialStates == null)
            return;

        for (int i = 0; i < _rendererMaterialStates.Length; i++)
        {
            RendererMaterialState state = _rendererMaterialStates[i];
            if (state.Renderer != null && state.SharedMaterials != null)
                state.Renderer.sharedMaterials = state.SharedMaterials;
        }
    }

    private Material GetWhiteTeleportMaterial()
    {
        if (_whiteTeleportMaterial != null)
            return _whiteTeleportMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        _whiteTeleportMaterial = new Material(shader)
        {
            color = Color.white
        };

        if (_whiteTeleportMaterial.HasProperty("_BaseColor"))
            _whiteTeleportMaterial.SetColor("_BaseColor", Color.white);
        if (_whiteTeleportMaterial.HasProperty("_Color"))
            _whiteTeleportMaterial.SetColor("_Color", Color.white);
        if (_whiteTeleportMaterial.HasProperty("_EmissionColor"))
        {
            _whiteTeleportMaterial.EnableKeyword("_EMISSION");
            _whiteTeleportMaterial.SetColor("_EmissionColor", Color.white * 1.5f);
        }

        return _whiteTeleportMaterial;
    }

    private void SetRenderersVisible(bool visible)
    {
        if (_renderers == null)
            return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].enabled = visible;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null)
            return;

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null && !_colliders[i].isTrigger)
                _colliders[i].enabled = enabled;
        }
    }
}

[DisallowMultipleComponent]
public class BossSummonAttackModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IConditionalAttackModule
{
    [SerializeField] private float _minAttackRange = 0f;
    [SerializeField] private float _maxAttackRange = 18f;
    [SerializeField] private string _attackAnimTrigger = "AreaOfEffect";

    private BossRoomController _room;
    private EnemyBase _enemyBase;
    private int _summonCount = 5;
    private float _minCooldown = 16f;
    private float _maxCooldown = 24f;
    private AudioClip _summonSound;
    private float _summonSoundVolume = 1f;
    private float _nextSummonTime;

    public float MinAttackRange => _minAttackRange;
    public float MaxAttackRange => _maxAttackRange;
    public float AttackDamage => 0f;
    public float AttackRate => 1f / Mathf.Max(0.1f, _minCooldown);
    public DamageType AttackDamageType => DamageType.Energy;
    public string AttackAnimTrigger => _attackAnimTrigger;
    public bool CanStartAttack => Time.time >= _nextSummonTime;

    public void Configure(
        BossRoomController room,
        int summonCount,
        float minCooldown,
        float maxCooldown,
        AudioClip summonSound,
        float summonSoundVolume,
        float maxAttackRange = -1f)
    {
        _room = room;
        _summonCount = Mathf.Max(1, summonCount);
        _minCooldown = Mathf.Max(0.1f, minCooldown);
        _maxCooldown = Mathf.Max(_minCooldown, maxCooldown);
        _summonSound = summonSound;
        _summonSoundVolume = Mathf.Clamp01(summonSoundVolume);
        if (maxAttackRange > 0f)
            _maxAttackRange = Mathf.Max(_minAttackRange + 0.1f, maxAttackRange);
        ScheduleNextSummon();
    }

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        ScheduleNextSummon();
    }

    public void OnAttackEnter()
    {
    }

    public void Tick()
    {
        if (_room == null || Time.time < _nextSummonTime)
            return;

        ScheduleNextSummon();

        if (_room.TrySummonEnemies(gameObject, _summonCount))
        {
            _enemyBase?.PlayAttackAnimationOneShot();
            if (_summonSound != null)
                AudioManager.Instance?.PlayBossSfx(_summonSound, _summonSoundVolume);
        }
    }

    private void ScheduleNextSummon()
    {
        _nextSummonTime = Time.time + Random.Range(_minCooldown, _maxCooldown);
    }
}
