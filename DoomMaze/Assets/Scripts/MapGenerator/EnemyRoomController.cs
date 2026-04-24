using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public enum EnemyRoomObjective
{
    EliminateAll,
    WaveSurvival
}

/// <summary>
/// Fresh procedural combat-room spawner used by generated maze enemy rooms.
/// This intentionally does not use the legacy EnemySpawner component.
/// </summary>
public class EnemyRoomController : MonoBehaviour
{
    [Header("Objective")]
    [SerializeField] private EnemyRoomObjective _objective = EnemyRoomObjective.EliminateAll;

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject _meleeEnemyPrefab;
    [SerializeField] private GameObject _rangedEnemyPrefab;

    [Header("Eliminate All")]
    [SerializeField] private int _eliminateEnemyCount = 18;
    [SerializeField] private float _eliminateSpawnDuration = 8f;
    [SerializeField] [Range(0f, 1f)] private float _eliminateMeleeWeight = 0.85f;

    [Header("Wave Survival")]
    [SerializeField] private float _waveDuration = 30f;
    [SerializeField] private float _waveSpawnInterval = 0.9f;
    [SerializeField] private int _waveMaxAlive = 14;
    [SerializeField] [Range(0f, 1f)] private float _waveMeleeWeight = 0.75f;

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
    [SerializeField] private Color _surviveSplashColor = new(1f, 0.85f, 0.05f, 1f);
    [SerializeField] private Color _proceedSplashColor = new(0.25f, 1f, 0.22f, 1f);

    [Header("Cleared Fog")]
    [SerializeField] private Color _unclearedFogColor = new(0.7f, 0.03f, 0.02f, 1f);
    [SerializeField] private Color _clearedFogColor = new(0.05f, 0.85f, 0.18f, 1f);
    [SerializeField] private float _clearedFogCrossfadeDuration = 1.5f;

    [Header("Survival Timer")]
    [SerializeField] private float _timerFontSize = 54f;
    [SerializeField] private Color _timerColor = new(1f, 0.92f, 0.2f, 1f);
    [SerializeField] private Color _timerWarningColor = new(1f, 0.08f, 0.02f, 1f);

    private readonly List<Vector3> _spawnCandidates = new();
    private readonly List<Vector3> _doorPositions = new();
    private readonly List<float> _playableLocalHeights = new();
    private readonly List<Collider> _roomColliders = new();
    private readonly List<RuntimeDoor> _doors = new();
    private readonly List<Vector3> _recentSpawnPositions = new();
    private readonly HashSet<GameObject> _trackedEnemies = new();

    private static EnemyRoomController _activeRoom;
    private static EnemyRoomController _clearedFogOwner;
    private static MonoBehaviour _fogRoutineHost;
    private static Coroutine _fogCrossfadeRoutine;
    private static Color _currentFogTarget;
    private static bool _hasFogTarget;

    private MazePrefab _mazePrefab;
    private Bounds _localBounds;
    private Coroutine _combatRoutine;
    private Coroutine _splashRoutine;
    private Transform _playerTransform;
    private GameObject _timerCanvasObject;
    private RectTransform _timerRect;
    private TextMeshProUGUI _timerLabel;
    private float _timerPulseRemaining;
    private int _lastWarningSecond = -1;
    private bool _configured;
    private bool _started;
    private bool _completed;
    private bool _isPlayerInClearedRoom;
    private bool _armed;
    private bool _wasOutsideEntry = true;
    private bool _loggedMissingSpawnPoint;
    private float _entryActivationStartedAt = -1f;
    private float _minimumSpawnSpacing = 5f;
    private int _spawnedCount;

    public EnemyRoomObjective Objective => _objective;

    private sealed class RuntimeDoor
    {
        public Door Door;
        public Renderer[] Renderers;
        public Collider[] Colliders;
        public Coroutine FadeRoutine;
    }

    private void OnEnable()
    {
        EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
    }

    private void OnDisable()
    {
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);

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
    }

    public void Configure(
        MazePrefab mazePrefab,
        EnemyRoomObjective objective,
        GameObject meleeEnemyPrefab,
        GameObject rangedEnemyPrefab,
        Door doorPrefab,
        Vector3 generatedDoorScale,
        float doorVerticalOffset,
        TMP_FontAsset splashFont,
        int eliminateEnemyCount,
        float eliminateSpawnDuration,
        float eliminateMeleeWeight,
        float waveDuration,
        float waveSpawnInterval,
        int waveMaxAlive,
        float waveMeleeWeight)
    {
        _mazePrefab = mazePrefab;
        _objective = objective;
        _meleeEnemyPrefab = meleeEnemyPrefab;
        _rangedEnemyPrefab = rangedEnemyPrefab;
        _doorPrefab = doorPrefab;
        _generatedDoorScale = generatedDoorScale;
        _doorVerticalOffset = doorVerticalOffset;
        _splashFont = splashFont;
        _eliminateEnemyCount = Mathf.Max(0, eliminateEnemyCount);
        _eliminateSpawnDuration = Mathf.Max(0f, eliminateSpawnDuration);
        _eliminateMeleeWeight = Mathf.Clamp01(eliminateMeleeWeight);
        _waveDuration = Mathf.Max(0f, waveDuration);
        _waveSpawnInterval = Mathf.Max(0.05f, waveSpawnInterval);
        _waveMaxAlive = Mathf.Max(1, waveMaxAlive);
        _waveMeleeWeight = Mathf.Clamp01(waveMeleeWeight);

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

        if (_objective == EnemyRoomObjective.EliminateAll)
            HideSurvivalTimer();

        ShowRoomSplash();
        ShowDoors();

        _combatRoutine = StartCoroutine(_objective == EnemyRoomObjective.WaveSurvival
            ? WaveSurvivalRoutine()
            : EliminateAllRoutine());
    }

    private IEnumerator EliminateAllRoutine()
    {
        if (_eliminateEnemyCount <= 0)
        {
            CompleteRoom();
            yield break;
        }

        float interval = _eliminateEnemyCount > 1
            ? _eliminateSpawnDuration / (_eliminateEnemyCount - 1)
            : 0f;

        for (int i = 0; i < _eliminateEnemyCount; i++)
        {
            SpawnEnemy(_eliminateMeleeWeight);

            if (interval > 0f && i < _eliminateEnemyCount - 1)
                yield return new WaitForSeconds(interval);
        }

        TryCompleteEliminateAll();
    }

    private IEnumerator WaveSurvivalRoutine()
    {
        float remaining = _waveDuration;
        float spawnTimer = 0f;
        ShowSurvivalTimer(remaining);

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            spawnTimer -= Time.deltaTime;
            UpdateSurvivalTimer(Mathf.Max(0f, remaining));

            if (spawnTimer <= 0f && CountLivingTrackedEnemies() < _waveMaxAlive)
            {
                bool spawned = SpawnEnemy(_waveMeleeWeight);
                spawnTimer = spawned ? _waveSpawnInterval : 0.1f;
            }

            yield return null;
        }

        KillRemainingEnemies();
        HideSurvivalTimer();
        CompleteRoom();
    }

    private bool SpawnEnemy(float meleeWeight)
    {
        if (_activeRoom != this || !_started || _completed)
            return false;

        GameObject prefab = PickEnemyPrefab(meleeWeight);
        if (prefab == null)
            return false;

        if (!TryPickSpawnPosition(out Vector3 position))
        {
            if (!_loggedMissingSpawnPoint)
            {
                Debug.LogWarning($"[EnemyRoomController] Could not find a NavMesh spawn point for {gameObject.name}. Skipping enemy spawns until a valid point exists.", this);
                _loggedMissingSpawnPoint = true;
            }

            _spawnedCount++;
            TryCompleteEliminateAll();
            return false;
        }

        Quaternion rotation = Quaternion.LookRotation(GetDirectionToRoomCenter(position), Vector3.up);
        GameObject enemy = Instantiate(prefab, position, rotation, transform);

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.Warp(position);

        _trackedEnemies.Add(enemy);
        RememberSpawnPosition(position);
        _spawnedCount++;
        return true;
    }

    private GameObject PickEnemyPrefab(float meleeWeight)
    {
        bool useMelee = Random.value <= meleeWeight;
        GameObject preferred = useMelee ? _meleeEnemyPrefab : _rangedEnemyPrefab;
        GameObject fallback = useMelee ? _rangedEnemyPrefab : _meleeEnemyPrefab;
        return preferred != null ? preferred : fallback;
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

        if (_objective == EnemyRoomObjective.EliminateAll)
            TryCompleteEliminateAll();
    }

    private void TryCompleteEliminateAll()
    {
        if (_completed || _spawnedCount < _eliminateEnemyCount)
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

    private void KillRemainingEnemies()
    {
        var enemies = new List<GameObject>(_trackedEnemies);
        foreach (GameObject enemy in enemies)
        {
            if (enemy == null)
                continue;

            HealthComponent health = enemy.GetComponent<HealthComponent>();
            if (health != null && health.IsAlive)
                health.Kill();
            else
                enemy.SetActive(false);
        }

        _trackedEnemies.Clear();
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

        HideSurvivalTimer();
        SetDoorsOpen(true);
        ShowProceedSplash();
        SetClearedFogOwner(this);
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

    private void SetClearedFogOwner(EnemyRoomController owner)
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

    private void ShowSurvivalTimer(float remaining)
    {
        HideSurvivalTimer();
        _lastWarningSecond = -1;
        _timerPulseRemaining = 0f;

        _timerCanvasObject = new GameObject("SurvivalTimerCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = _timerCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 19900;

        CanvasScaler scaler = _timerCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject labelObject = new("SurvivalTimerLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(_timerCanvasObject.transform, false);

        _timerRect = labelObject.GetComponent<RectTransform>();
        _timerRect.anchorMin = new Vector2(0f, 1f);
        _timerRect.anchorMax = new Vector2(0f, 1f);
        _timerRect.pivot = new Vector2(0f, 1f);
        _timerRect.anchoredPosition = new Vector2(36f, -30f);
        _timerRect.sizeDelta = new Vector2(520f, 110f);

        _timerLabel = labelObject.GetComponent<TextMeshProUGUI>();
        _timerLabel.raycastTarget = false;
        _timerLabel.alignment = TextAlignmentOptions.Left;
        _timerLabel.fontSize = _timerFontSize;
        _timerLabel.enableAutoSizing = false;
        _timerLabel.color = _timerColor;

        if (_splashFont != null)
            _timerLabel.font = _splashFont;

        UpdateSurvivalTimer(remaining);
    }

    private void UpdateSurvivalTimer(float remaining)
    {
        if (_timerLabel == null || _timerRect == null)
            return;

        int displaySeconds = Mathf.CeilToInt(remaining);
        _timerLabel.text = $"SURVIVE: {displaySeconds:00}";

        if (displaySeconds <= 5 && displaySeconds > 0 && displaySeconds != _lastWarningSecond)
        {
            _lastWarningSecond = displaySeconds;
            _timerPulseRemaining = 0.32f;
        }

        if (_timerPulseRemaining > 0f)
        {
            _timerPulseRemaining = Mathf.Max(0f, _timerPulseRemaining - Time.deltaTime);
            float pulseT = _timerPulseRemaining / 0.32f;
            float shake = Mathf.Lerp(0f, 18f, pulseT);
            bool flash = Mathf.FloorToInt(Time.time * 24f) % 2 == 0;

            _timerRect.localScale = Vector3.one * (1f + pulseT * 0.18f);
            _timerRect.anchoredPosition = new Vector2(36f + Random.Range(-shake, shake), -30f + Random.Range(-shake, shake));
            _timerLabel.color = flash ? Color.white : _timerWarningColor;
        }
        else
        {
            _timerRect.localScale = Vector3.one;
            _timerRect.anchoredPosition = new Vector2(36f, -30f);
            _timerLabel.color = displaySeconds <= 5 ? _timerWarningColor : _timerColor;
        }
    }

    private void HideSurvivalTimer()
    {
        if (_timerCanvasObject != null)
            Destroy(_timerCanvasObject);

        _timerCanvasObject = null;
        _timerRect = null;
        _timerLabel = null;
        _timerPulseRemaining = 0f;
    }

    private void ShowRoomSplash()
    {
        if (_splashRoutine != null)
            StopCoroutine(_splashRoutine);

        string text = _objective == EnemyRoomObjective.WaveSurvival ? "SURVIVE!" : "ELIMINATE ALL!";
        Color color = _objective == EnemyRoomObjective.WaveSurvival ? _surviveSplashColor : _eliminateSplashColor;
        _splashRoutine = StartCoroutine(RoomSplashRoutine(text, color));
    }

    private void ShowProceedSplash()
    {
        if (_splashRoutine != null)
            StopCoroutine(_splashRoutine);

        _splashRoutine = StartCoroutine(RoomSplashRoutine("PROCEED", _proceedSplashColor));
    }

    private IEnumerator RoomSplashRoutine(string splashText, Color baseColor)
    {
        GameObject canvasObject = new("EnemyRoomSplashCanvas", typeof(Canvas), typeof(CanvasScaler));
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
            Debug.LogWarning($"[EnemyRoomController] No NavMesh spawn candidates found for {gameObject.name}. Enemy spawning will skip until a valid point exists.", this);
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
