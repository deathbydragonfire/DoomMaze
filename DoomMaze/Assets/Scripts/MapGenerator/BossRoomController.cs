using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

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

    [Header("Boss Audio")]
    [SerializeField] private AudioClip _bossTeleportSound;
    [SerializeField] [Range(0f, 1f)] private float _bossTeleportSoundVolume = 1f;
    [SerializeField] private AudioClip _bossSummonSound;
    [SerializeField] [Range(0f, 1f)] private float _bossSummonSoundVolume = 1f;

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
    [SerializeField] private Vector2 _bossSummonCooldownRange = new(16f, 24f);
    [SerializeField] [Range(0f, 1f)] private float _bossSummonMeleeWeight = 0.6f;
    [SerializeField] private float _bossSummonFadeDuration = 0.65f;

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
    private readonly HashSet<GameObject> _trackedEnemies = new();

    private static BossRoomController _activeRoom;
    private static BossRoomController _clearedFogOwner;
    private static MonoBehaviour _fogRoutineHost;
    private static Coroutine _fogCrossfadeRoutine;
    private static Color _currentFogTarget;
    private static bool _hasFogTarget;

    private MazePrefab _mazePrefab;
    private Bounds _localBounds;
    private Coroutine _combatRoutine;
    private Coroutine _splashRoutine;
    private Coroutine _victoryRoutine;
    private Transform _playerTransform;
    private GameObject _splashCanvasObject;
    private GameObject _bossHealthCanvasObject;
    private Image _bossHealthFillImage;
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
    private GameObject _victoryTargetBoss;
    private float _entryActivationStartedAt = -1f;
    private float _minimumSpawnSpacing = 5f;
    private int _spawnedCount;

    public BossRoomObjective Objective => _objective;

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

        StopBossHealthBar();
    }

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
        int eliminateBossCount)
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

        ShowRoomSplash();
        ShowDoors();

        _combatRoutine = StartCoroutine(EliminateAllRoutine());
    }

    private IEnumerator EliminateAllRoutine()
    {
        if (_eliminateBossCount <= 0)
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

        if (!TryPickSpawnPosition(out Vector3 position) &&
            (!_useTestingSpawnFallback || !TryPickTestingSpawnPosition(out position)))
        {
            if (!_loggedMissingSpawnPoint)
            {
                Debug.LogWarning($"[BossRoomController] Could not find a NavMesh spawn point for {gameObject.name}. Skipping boss spawns until a valid point exists.", this);
                _loggedMissingSpawnPoint = true;
            }

            if (_useTestingSpawnFallback)
                return false;

            _spawnedCount++;
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
        if (showHealthBar)
            ShowBossHealthBar(boss);

        ConfigureBossModelHitFlash(boss);
        ConfigureBossTeleport(boss);
        ConfigureBossSummonAttack(boss);
        RememberSpawnPosition(position);
        _spawnedCount++;
        return true;
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

        AudioManager.Instance?.PlaySfx(_bossSummonSound, _bossSummonSoundVolume);

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
            label.color = new Color(_bossIntroTitleColor.r, _bossIntroTitleColor.g, _bossIntroTitleColor.b, alpha);
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

    private void ConfigureBossTeleport(GameObject boss)
    {
        if (!_enableBossTeleport || boss == null)
            return;

        BossTeleportAbility teleport = boss.GetComponent<BossTeleportAbility>();
        if (teleport == null)
            teleport = boss.AddComponent<BossTeleportAbility>();

        teleport.Configure(
            _bossTeleportWhenFartherThan,
            _bossTeleportNearPlayerDistance.x,
            _bossTeleportNearPlayerDistance.y,
            _bossTeleportCooldownRange.x,
            _bossTeleportCooldownRange.y,
            _bossTeleportChance,
            _bossTeleportFadeDuration,
            _bossTeleportHiddenDuration,
            _bossPostTeleportAttackLock,
            _bossTeleportSound,
            _bossTeleportSoundVolume);
    }

    private void ConfigureBossSummonAttack(GameObject boss)
    {
        if (!_enableBossSummonAttack || boss == null)
            return;

        if (_summonMeleeEnemyPrefab == null && _summonRangedEnemyPrefab == null)
            return;

        BossSummonAttackModule summon = boss.GetComponent<BossSummonAttackModule>();
        if (summon == null)
            summon = boss.AddComponent<BossSummonAttackModule>();

        summon.Configure(
            this,
            _bossSummonCount,
            _bossSummonCooldownRange.x,
            _bossSummonCooldownRange.y,
            _bossSummonSound,
            _bossSummonSoundVolume);
    }

    public bool TrySummonEnemies(GameObject source, int count)
    {
        if (!_started || _completed || count <= 0)
            return false;

        bool spawnedAny = false;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = PickSummonedEnemyPrefab();
            if (prefab == null)
                continue;

            if (!TryPickSummonSpawnPosition(out Vector3 position))
                continue;

            Quaternion rotation = Quaternion.LookRotation(GetDirectionToRoomCenter(position), Vector3.up);
            GameObject enemy = Instantiate(prefab, position, rotation, transform);
            PrepareSummonedEnemy(enemy, position);
            spawnedAny = true;
        }

        return spawnedAny;
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
        if (evt.Enemy == null || !_trackedEnemies.Remove(evt.Enemy))
            return;

        if (_objective == BossRoomObjective.EliminateAll)
        {
            StopBossHealthBar();

            if (_spawnedCount >= _eliminateBossCount && CountLivingTrackedEnemies() == 0)
            {
                CompleteBossVictory(evt.Enemy);
                return;
            }

            TryCompleteEliminateAll();
        }
    }

    private void TryCompleteEliminateAll()
    {
        if (_completed || _spawnedCount < _eliminateBossCount)
            return;

        if (CountLivingTrackedEnemies() == 0)
            CompleteRoom();
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
        StopBossHealthBar();

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Cinematic)
            GameManager.Instance.SetState(GameState.Cinematic);

        _victoryRoutine = StartCoroutine(BossVictoryRoutine(defeatedBoss));
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
        _bossHealthFillImage = fillObject.GetComponent<Image>();
        _bossHealthFillImage.color = _bossHealthFillColor;
        _bossHealthFillImage.type = Image.Type.Filled;
        _bossHealthFillImage.fillMethod = Image.FillMethod.Horizontal;
        _bossHealthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
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
            _bossHealthFillImage.fillAmount = normalized;

        if (_bossHealthLabel != null)
            _bossHealthLabel.text = $"BOSS  {_bossHealth.CurrentHealth} / {maxHealth}";
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

        AudioManager.Instance?.PlaySfx(_teleportSound, _teleportSoundVolume);
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
        float summonSoundVolume)
    {
        _room = room;
        _summonCount = Mathf.Max(1, summonCount);
        _minCooldown = Mathf.Max(0.1f, minCooldown);
        _maxCooldown = Mathf.Max(_minCooldown, maxCooldown);
        _summonSound = summonSound;
        _summonSoundVolume = Mathf.Clamp01(summonSoundVolume);
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
                AudioManager.Instance?.PlaySfx(_summonSound, _summonSoundVolume);
        }
    }

    private void ScheduleNextSummon()
    {
        _nextSummonTime = Time.time + Random.Range(_minCooldown, _maxCooldown);
    }
}
