using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DecayWallController : MonoBehaviour
{
    private const string DefaultDecayClipPath = "Assets/Audio/Environment/FF_IE_fx_boom_resonate.wav";

    [Header("Scene")]
    [SerializeField] private bool _decayEnabled = true;
    [SerializeField] private bool _gameplaySceneOnly = true;
    [SerializeField] private string _gameplaySceneName = "Gameplay";

    [Header("Movement")]
    [SerializeField] private float _movementSpeed = 0.35f;
    [SerializeField] private float _startOffsetBehindStart = 42f;
    [SerializeField] private float _endOffsetPastExit = 24f;
    [SerializeField] private float _killPadding = 1.5f;

    [Header("Wall Shape")]
    [SerializeField] private float _widthPadding = 96f;
    [SerializeField] private float _heightPadding = 24f;
    [SerializeField] private float _minimumWallWidth = 90f;
    [SerializeField] private float _minimumWallHeight = 44f;
    [SerializeField] private float _particleDepth = 18f;
    [SerializeField] private float _floorOffset = 2f;

    [Header("Particles")]
    [SerializeField] private int _darkParticleRate = 16000;
    [SerializeField] private int _grayParticleRate = 9000;
    [SerializeField] private int _maxParticlesPerLayer = 65000;
    [SerializeField] private float _particleLifetime = 10f;
    [SerializeField] private float _particleTurbulence = 7.5f;
    [SerializeField] private Vector2 _darkParticleSizeRange = new Vector2(0.08f, 0.22f);
    [SerializeField] private Vector2 _grayParticleSizeRange = new Vector2(0.06f, 0.18f);

    [Header("Dense Curtain")]
    [SerializeField] [Range(0f, 1f)] private float _curtainAlpha = 0.72f;
    [SerializeField] private int _curtainLayerCount = 5;

    [Header("Fog Volume")]
    [SerializeField] [Range(0f, 1f)] private float _fogVolumeAlpha = 0.62f;
    [SerializeField] private int _fogVolumeLayerCount = 4;

    [Header("Audio")]
    [SerializeField] private AudioClip _decayLoopClip;
    [SerializeField] [Range(0f, 1f)] private float _maxAudioVolume = 0.9f;
    [SerializeField] private float _audioNearDistance = 14f;
    [SerializeField] private float _audioFarDistance = 80f;
    [SerializeField] private float _audioFadeSpeed = 3.5f;

    private readonly HashSet<UpgradeRoomController> _upgradePauseRooms = new();
    private readonly List<ParticleSystem> _particleSystems = new();
    private readonly List<Renderer> _curtainRenderers = new();
    private readonly List<Renderer> _fogVolumeRenderers = new();

    private Transform _wallRoot;
    private Transform _visualRoot;
    private BoxCollider _killCollider;
    private AudioSource _audioSource;
    private Transform _playerTransform;
    private HealthComponent _playerHealth;
    private Vector3 _origin;
    private Vector3 _direction;
    private Vector3 _right;
    private Vector3 _up = Vector3.up;
    private Bounds _mapBounds;
    private float _wallWidth;
    private float _wallHeight;
    private float _maxTravelDistance;
    private float _currentTravelDistance;
    private float _currentAudioIntensity;
    private bool _initialized;
    private bool _playerKilled;

    private bool IsGameplayScene =>
        !_gameplaySceneOnly ||
        string.IsNullOrWhiteSpace(_gameplaySceneName) ||
        SceneManager.GetActiveScene().name == _gameplaySceneName;

    public bool DecayEnabled => _decayEnabled && isActiveAndEnabled;

    private void OnEnable()
    {
        EventBus<MazePopulatedEvent>.Subscribe(OnMazePopulated);
        EventBus<UpgradeRoomPresenceChangedEvent>.Subscribe(OnUpgradeRoomPresenceChanged);
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
    }

    private void OnDisable()
    {
        EventBus<MazePopulatedEvent>.Unsubscribe(OnMazePopulated);
        EventBus<UpgradeRoomPresenceChangedEvent>.Unsubscribe(OnUpgradeRoomPresenceChanged);
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        AudioManager.Instance?.SetDecayDuckIntensity(0f);

        if (_wallRoot != null)
            _wallRoot.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (_initialized || !DecayEnabled || !IsGameplayScene)
            return;

        MazePopulator populator = GetComponent<MazePopulator>();
        if (populator == null)
            populator = FindFirstObjectByType<MazePopulator>();

        if (populator != null && populator.GeneratedRooms.Count > 0)
            InitializeFromPopulator(populator);
    }

    private void Update()
    {
        if (!_decayEnabled)
        {
            DisableRuntimeWall();
            return;
        }

        if (!_initialized || !IsGameplayScene)
            return;

        CachePlayer();

        bool isPlaying = GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing;
        if (isPlaying && _upgradePauseRooms.Count == 0)
            AdvanceWall();

        UpdateTransform();
        UpdateAudio(isPlaying);

        if (isPlaying)
            KillPlayerIfCaught();
    }

    public void InitializeFromPopulator(MazePopulator populator)
    {
        if (populator == null || !DecayEnabled || !IsGameplayScene)
        {
            DisableRuntimeWall();
            return;
        }

        IReadOnlyList<MazePopulator.GeneratedRoomPlacement> rooms = populator.GeneratedRooms;
        if (rooms == null || rooms.Count == 0)
            return;

        if (!TryGetKeyPlacements(rooms, out MazePopulator.GeneratedRoomPlacement start, out MazePopulator.GeneratedRoomPlacement target))
        {
            Debug.LogWarning("[DecayWallController] Could not find a start and boss/exit target in generated rooms.", this);
            return;
        }

        _up = InferMapUpAxis(rooms);
        Vector3 targetHeading = Vector3.ProjectOnPlane(target.Position - start.Position, _up);
        Vector3 startExitHeading = GetStartExitDirection(start);
        Vector3 heading = startExitHeading.sqrMagnitude > 0.001f &&
                          (targetHeading.sqrMagnitude <= 0.001f || Vector3.Dot(startExitHeading.normalized, targetHeading.normalized) > 0f)
            ? startExitHeading
            : targetHeading;
        if (heading.sqrMagnitude <= 0.001f)
            heading = Vector3.ProjectOnPlane(transform.forward, _up);
        if (heading.sqrMagnitude <= 0.001f)
            heading = Vector3.forward;

        _direction = heading.normalized;
        _right = Vector3.Cross(_up, _direction).normalized;
        if (_right.sqrMagnitude <= 0.001f)
            _right = Vector3.right;

        _mapBounds = BuildMapBounds(rooms, start.Position, target.Position);
        _origin = start.Position - _direction * Mathf.Max(0f, _startOffsetBehindStart);
        float minUp = GetMinProjectedMapAxis(rooms, _up);
        _origin += _up * (minUp + _floorOffset - Vector3.Dot(_origin, _up));

        float targetDistance = Vector3.Dot(target.Position - _origin, _direction);
        _maxTravelDistance = Mathf.Max(1f, targetDistance + Mathf.Max(0f, _endOffsetPastExit));
        _currentTravelDistance = 0f;
        _playerKilled = false;
        _upgradePauseRooms.Clear();

        CalculateWallSize(rooms);
        EnsureWallRoot();
        _wallRoot.gameObject.SetActive(true);
        EnsureVisuals();
        EnsureAudioSource();
        UpdateTransform();

        _initialized = true;
    }

    private void OnMazePopulated(MazePopulatedEvent e)
    {
        if (e.Populator != null)
            InitializeFromPopulator(e.Populator);
    }

    private void OnUpgradeRoomPresenceChanged(UpgradeRoomPresenceChangedEvent e)
    {
        if (e.Room == null)
            return;

        if (e.IsPlayerInside)
            _upgradePauseRooms.Add(e.Room);
        else
            _upgradePauseRooms.Remove(e.Room);
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Dead || e.NewState == GameState.MainMenu || e.NewState == GameState.Loading)
        {
            _currentAudioIntensity = 0f;
            if (_audioSource != null)
                _audioSource.volume = 0f;

            AudioManager.Instance?.SetDecayDuckIntensity(0f);
        }
    }

    public void SetDecayEnabled(bool enabled)
    {
        _decayEnabled = enabled;
        if (!_decayEnabled)
            DisableRuntimeWall();
    }

    private void DisableRuntimeWall()
    {
        _currentAudioIntensity = 0f;

        if (_audioSource != null)
        {
            _audioSource.volume = 0f;
            _audioSource.Stop();
        }

        if (_wallRoot != null)
            _wallRoot.gameObject.SetActive(false);

        AudioManager.Instance?.SetDecayDuckIntensity(0f);
    }

    private void AdvanceWall()
    {
        _currentTravelDistance = Mathf.Min(
            _maxTravelDistance,
            _currentTravelDistance + Mathf.Max(0f, _movementSpeed) * Time.deltaTime);
    }

    private void UpdateTransform()
    {
        Vector3 center = GetWallCenter();
        Quaternion rotation = Quaternion.LookRotation(_direction, _up);

        if (_wallRoot != null)
            _wallRoot.SetPositionAndRotation(center, rotation);

        if (_visualRoot != null)
        {
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
        }
    }

    private void UpdateAudio(bool isPlaying)
    {
        float targetIntensity = isPlaying && _playerTransform != null
            ? GetAudioIntensity(_playerTransform.position)
            : 0f;

        _currentAudioIntensity = Mathf.MoveTowards(
            _currentAudioIntensity,
            targetIntensity,
            Mathf.Max(0.01f, _audioFadeSpeed) * Time.deltaTime);

        if (_audioSource != null)
        {
            _audioSource.volume = _currentAudioIntensity * _maxAudioVolume;
            if (!_audioSource.isPlaying && _audioSource.clip != null)
                _audioSource.Play();

            if (_playerTransform != null)
                _audioSource.transform.position = GetClosestPointOnWall(_playerTransform.position);
            else
                _audioSource.transform.position = GetWallCenter();
        }

        AudioManager.Instance?.SetDecayDuckIntensity(_currentAudioIntensity);
    }

    private void KillPlayerIfCaught()
    {
        if (_playerKilled || _playerTransform == null)
            return;

        float playerDistance = Vector3.Dot(_playerTransform.position - _origin, _direction);
        if (playerDistance > _currentTravelDistance + _particleDepth * 0.5f + _killPadding)
            return;

        KillPlayer(_playerHealth);
    }

    public void TryKillFromContact(Collider other)
    {
        if (!_initialized || _playerKilled || other == null)
            return;

        HealthComponent health = other.GetComponentInParent<HealthComponent>();
        if (health == null || !health.IsPlayer)
            return;

        KillPlayer(health);
    }

    private void KillPlayer(HealthComponent health)
    {
        if (health == null || !health.IsAlive)
            return;

        _playerKilled = true;
        health.Kill();
    }

    private void CachePlayer()
    {
        if (_playerTransform != null && _playerHealth != null)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        _playerTransform = player.transform;
        _playerHealth = player.GetComponentInParent<HealthComponent>();
    }

    private Vector3 GetWallCenter()
    {
        return _origin + _direction * _currentTravelDistance + _up * (_wallHeight * 0.5f);
    }

    private float GetAudioIntensity(Vector3 worldPosition)
    {
        float distance = Vector3.Distance(worldPosition, GetClosestPointOnWall(worldPosition));
        float far = Mathf.Max(_audioNearDistance + 0.01f, _audioFarDistance);
        return Mathf.InverseLerp(far, Mathf.Max(0.01f, _audioNearDistance), distance);
    }

    private Vector3 GetClosestPointOnWall(Vector3 worldPosition)
    {
        Vector3 center = GetWallCenter();
        Vector3 offset = worldPosition - center;
        float lateral = Mathf.Clamp(Vector3.Dot(offset, _right), _wallWidth * -0.5f, _wallWidth * 0.5f);
        float vertical = Mathf.Clamp(Vector3.Dot(offset, _up), _wallHeight * -0.5f, _wallHeight * 0.5f);
        float depth = Mathf.Clamp(Vector3.Dot(offset, _direction), _particleDepth * -0.5f, _particleDepth * 0.5f);
        return center + _right * lateral + _up * vertical + _direction * depth;
    }

    private bool TryGetKeyPlacements(
        IReadOnlyList<MazePopulator.GeneratedRoomPlacement> rooms,
        out MazePopulator.GeneratedRoomPlacement start,
        out MazePopulator.GeneratedRoomPlacement target)
    {
        start = default;
        target = default;
        bool hasStart = false;
        bool hasTarget = false;

        for (int i = 0; i < rooms.Count; i++)
        {
            MazePopulator.GeneratedRoomPlacement room = rooms[i];
            if (!room.HasNodeType)
                continue;

            if (!hasStart && room.Type == MapGenerator.RoomType.Start)
            {
                start = room;
                hasStart = true;
            }

            if (room.Type == MapGenerator.RoomType.Exit)
            {
                target = room;
                hasTarget = true;
            }
        }

        if (!hasTarget)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                MazePopulator.GeneratedRoomPlacement room = rooms[i];
                if (room.HasNodeType && room.Type == MapGenerator.RoomType.Boss)
                {
                    target = room;
                    hasTarget = true;
                    break;
                }
            }
        }

        if (!hasTarget && hasStart)
        {
            float bestDistance = -1f;
            for (int i = 0; i < rooms.Count; i++)
            {
                float distance = Vector3.SqrMagnitude(rooms[i].Position - start.Position);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    target = rooms[i];
                    hasTarget = true;
                }
            }
        }

        return hasStart && hasTarget;
    }

    private Vector3 GetStartExitDirection(MazePopulator.GeneratedRoomPlacement start)
    {
        if (start.Prefab == null)
            return Vector3.zero;

        IReadOnlyList<MazeSocket> exits = start.Prefab.ExitSockets;
        if (exits == null || exits.Count == 0)
            return Vector3.zero;

        Vector3 localForward = exits[0].Forward;
        if (localForward.sqrMagnitude <= 0.001f)
            return Vector3.zero;

        return Vector3.ProjectOnPlane(start.Rotation * localForward.normalized, _up);
    }

    private static Vector3 InferMapUpAxis(IReadOnlyList<MazePopulator.GeneratedRoomPlacement> rooms)
    {
        if (rooms == null || rooms.Count <= 1)
            return Vector3.up;

        Bounds bounds = new Bounds(rooms[0].Position, Vector3.zero);
        for (int i = 1; i < rooms.Count; i++)
            bounds.Encapsulate(rooms[i].Position);

        Vector3 size = bounds.size;
        if (size.x <= size.y && size.x <= size.z)
            return Vector3.right;
        if (size.y <= size.x && size.y <= size.z)
            return Vector3.up;
        return Vector3.forward;
    }

    private static float GetMinProjectedMapAxis(IReadOnlyList<MazePopulator.GeneratedRoomPlacement> rooms, Vector3 axis)
    {
        float min = float.PositiveInfinity;
        if (rooms != null)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                Bounds bounds = rooms[i].Bounds;
                min = Mathf.Min(min, Vector3.Dot(bounds.min, axis));
                min = Mathf.Min(min, Vector3.Dot(bounds.max, axis));
            }
        }

        return IsFinite(min) ? min : 0f;
    }

    private Bounds BuildMapBounds(
        IReadOnlyList<MazePopulator.GeneratedRoomPlacement> rooms,
        Vector3 startPosition,
        Vector3 targetPosition)
    {
        Bounds bounds = new Bounds(startPosition, Vector3.one);
        bounds.Encapsulate(targetPosition);

        for (int i = 0; i < rooms.Count; i++)
            bounds.Encapsulate(rooms[i].Bounds);

        return bounds;
    }

    private void CalculateWallSize(IReadOnlyList<MazePopulator.GeneratedRoomPlacement> rooms)
    {
        float minLateral = float.PositiveInfinity;
        float maxLateral = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < rooms.Count; i++)
        {
            Bounds bounds = rooms[i].Bounds;
            EncapsulateProjectedBounds(bounds, ref minLateral, ref maxLateral, ref minY, ref maxY);
        }

        if (!IsFinite(minLateral) || !IsFinite(maxLateral))
        {
            minLateral = -_minimumWallWidth * 0.5f;
            maxLateral = _minimumWallWidth * 0.5f;
        }

        if (!IsFinite(minY) || !IsFinite(maxY))
        {
            minY = _mapBounds.min.y;
            maxY = _mapBounds.max.y;
        }

        _wallWidth = Mathf.Max(_minimumWallWidth, maxLateral - minLateral + _widthPadding);
        _wallHeight = Mathf.Max(_minimumWallHeight, maxY - minY + _heightPadding);
    }

    private void EncapsulateProjectedBounds(
        Bounds bounds,
        ref float minLateral,
        ref float maxLateral,
        ref float minY,
        ref float maxY)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        EncapsulateProjectedPoint(new Vector3(min.x, min.y, min.z), ref minLateral, ref maxLateral, ref minY, ref maxY);
        EncapsulateProjectedPoint(new Vector3(min.x, min.y, max.z), ref minLateral, ref maxLateral, ref minY, ref maxY);
        EncapsulateProjectedPoint(new Vector3(min.x, max.y, min.z), ref minLateral, ref maxLateral, ref minY, ref maxY);
        EncapsulateProjectedPoint(new Vector3(min.x, max.y, max.z), ref minLateral, ref maxLateral, ref minY, ref maxY);
        EncapsulateProjectedPoint(new Vector3(max.x, min.y, min.z), ref minLateral, ref maxLateral, ref minY, ref maxY);
        EncapsulateProjectedPoint(new Vector3(max.x, min.y, max.z), ref minLateral, ref maxLateral, ref minY, ref maxY);
        EncapsulateProjectedPoint(new Vector3(max.x, max.y, min.z), ref minLateral, ref maxLateral, ref minY, ref maxY);
        EncapsulateProjectedPoint(new Vector3(max.x, max.y, max.z), ref minLateral, ref maxLateral, ref minY, ref maxY);
    }

    private void EncapsulateProjectedPoint(
        Vector3 point,
        ref float minLateral,
        ref float maxLateral,
        ref float minY,
        ref float maxY)
    {
        float lateral = Vector3.Dot(point - _origin, _right);
        float height = Vector3.Dot(point - _origin, _up);
        minLateral = Mathf.Min(minLateral, lateral);
        maxLateral = Mathf.Max(maxLateral, lateral);
        minY = Mathf.Min(minY, height);
        maxY = Mathf.Max(maxY, height);
    }

    private void EnsureVisuals()
    {
        EnsureWallRoot();

        if (_visualRoot == null)
        {
            GameObject visualObject = new GameObject("DecayWallRuntimeFX");
            visualObject.transform.SetParent(_wallRoot, false);
            _visualRoot = visualObject.transform;
        }

        if (_particleSystems.Count == 0)
        {
            CreateParticleLayer("DecayBlackSmoke", new Color(0f, 0f, 0f, 0.92f), _darkParticleRate, _darkParticleSizeRange);
            CreateParticleLayer("DecayGraySmoke", new Color(0.12f, 0.12f, 0.12f, 0.7f), _grayParticleRate, _grayParticleSizeRange);
        }

        EnsureFogVolumeLayers();
        EnsureCurtainLayers();

        for (int i = 0; i < _particleSystems.Count; i++)
            ConfigureParticleShape(_particleSystems[i]);

        UpdateFogVolumeLayers();
        UpdateCurtainLayers();
        UpdateKillCollider();
    }

    private void CreateParticleLayer(string objectName, Color color, int rateOverTime, Vector2 sizeRange)
    {
        GameObject layerObject = new GameObject(objectName);
        layerObject.transform.SetParent(_visualRoot, false);

        ParticleSystem particles = layerObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = layerObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateParticleMaterial(color);
        renderer.sortingFudge = 8f;

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = Mathf.Max(256, _maxParticlesPerLayer);
        main.startLifetime = new ParticleSystem.MinMaxCurve(_particleLifetime * 0.7f, _particleLifetime * 1.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 1.15f);
        float minSize = Mathf.Max(0.02f, Mathf.Min(sizeRange.x, sizeRange.y));
        float maxSize = Mathf.Max(minSize, Mathf.Max(sizeRange.x, sizeRange.y));
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0, rateOverTime);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = _particleTurbulence;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.35f;
        noise.octaveCount = 3;
        noise.quality = ParticleSystemNoiseQuality.High;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-1.1f, 1.1f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.2f, 1.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 0.75f),
                new GradientColorKey(new Color(color.r * 0.65f, color.g * 0.65f, color.b * 0.65f, color.a), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.08f),
                new GradientAlphaKey(color.a, 0.78f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ConfigureParticleShape(particles);
        particles.Emit(Mathf.RoundToInt(Mathf.Max(0, _maxParticlesPerLayer) * 0.85f));
        particles.Play();
        _particleSystems.Add(particles);
    }

    private void ConfigureParticleShape(ParticleSystem particles)
    {
        if (particles == null)
            return;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(_wallWidth, _wallHeight, Mathf.Max(1f, _particleDepth));
        shape.position = Vector3.zero;
    }

    private void EnsureFogVolumeLayers()
    {
        int targetCount = Mathf.Max(0, _fogVolumeLayerCount);
        while (_fogVolumeRenderers.Count < targetCount)
        {
            int index = _fogVolumeRenderers.Count;
            GameObject volumeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volumeObject.name = $"DecayFogVolume{index + 1}";
            volumeObject.transform.SetParent(_visualRoot, false);

            Collider collider = volumeObject.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = volumeObject.GetComponent<Renderer>();
            renderer.material = CreateFogVolumeMaterial(index);
            _fogVolumeRenderers.Add(renderer);
        }

        for (int i = 0; i < _fogVolumeRenderers.Count; i++)
        {
            if (_fogVolumeRenderers[i] != null)
                _fogVolumeRenderers[i].gameObject.SetActive(i < targetCount);
        }
    }

    private void UpdateFogVolumeLayers()
    {
        int activeCount = Mathf.Max(1, _fogVolumeLayerCount);
        float totalDepth = Mathf.Max(1f, _particleDepth);
        float layerDepth = totalDepth / activeCount;

        for (int i = 0; i < _fogVolumeRenderers.Count; i++)
        {
            Renderer renderer = _fogVolumeRenderers[i];
            if (renderer == null || !renderer.gameObject.activeSelf)
                continue;

            float normalized = activeCount <= 1 ? 0.5f : i / (activeCount - 1f);
            Transform layerTransform = renderer.transform;
            layerTransform.localPosition = new Vector3(0f, 0f, Mathf.Lerp(-totalDepth * 0.4f, totalDepth * 0.4f, normalized));
            layerTransform.localRotation = Quaternion.identity;
            layerTransform.localScale = new Vector3(_wallWidth, _wallHeight, Mathf.Max(0.5f, layerDepth * 1.35f));
        }
    }

    private Material CreateFogVolumeMaterial(int layerIndex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        float shade = layerIndex % 2 == 0 ? 0.015f : 0.08f;
        float alpha = Mathf.Clamp01(_fogVolumeAlpha / Mathf.Max(1, _fogVolumeLayerCount));
        Color color = new Color(shade, shade, shade, alpha);
        Material material = new Material(shader)
        {
            color = color,
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent,
        };

        ConfigureTransparentMaterial(material, color);
        return material;
    }

    private void EnsureCurtainLayers()
    {
        int targetCount = Mathf.Max(0, _curtainLayerCount);
        while (_curtainRenderers.Count < targetCount)
        {
            int index = _curtainRenderers.Count;
            GameObject curtainObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            curtainObject.name = $"DecayCurtainLayer{index + 1}";
            curtainObject.transform.SetParent(_visualRoot, false);

            Collider collider = curtainObject.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = curtainObject.GetComponent<Renderer>();
            renderer.material = CreateCurtainMaterial(index);
            _curtainRenderers.Add(renderer);
        }

        for (int i = 0; i < _curtainRenderers.Count; i++)
        {
            if (_curtainRenderers[i] != null)
                _curtainRenderers[i].gameObject.SetActive(i < targetCount);
        }
    }

    private void UpdateCurtainLayers()
    {
        int activeCount = Mathf.Max(1, _curtainLayerCount);
        float depth = Mathf.Max(1f, _particleDepth);

        for (int i = 0; i < _curtainRenderers.Count; i++)
        {
            Renderer renderer = _curtainRenderers[i];
            if (renderer == null || !renderer.gameObject.activeSelf)
                continue;

            float normalized = activeCount <= 1 ? 0.5f : i / (activeCount - 1f);
            Transform layerTransform = renderer.transform;
            layerTransform.localPosition = new Vector3(0f, 0f, Mathf.Lerp(-depth * 0.45f, depth * 0.45f, normalized));
            layerTransform.localRotation = Quaternion.identity;
            layerTransform.localScale = new Vector3(_wallWidth, _wallHeight, 1f);
        }
    }

    private Material CreateCurtainMaterial(int layerIndex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        float shade = layerIndex % 2 == 0 ? 0f : 0.08f;
        Color color = new Color(shade, shade, shade, _curtainAlpha);
        Material material = new Material(shader)
        {
            color = color,
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent,
        };

        ConfigureTransparentMaterial(material, color);
        return material;
    }

    private Material CreateParticleMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            color = color,
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent,
        };

        ConfigureTransparentMaterial(material, color);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
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
    }

    private void EnsureAudioSource()
    {
        ResolveDefaultDecayClip();
        EnsureWallRoot();

        if (_audioSource == null)
        {
            GameObject audioObject = new GameObject("DecayWallAudio");
            audioObject.transform.SetParent(_wallRoot, false);
            _audioSource = audioObject.AddComponent<AudioSource>();
        }

        _audioSource.clip = _decayLoopClip;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = Mathf.Max(1f, _audioNearDistance);
        _audioSource.maxDistance = Mathf.Max(_audioNearDistance + 1f, _audioFarDistance);
        _audioSource.volume = 0f;
        AudioManager.Instance?.ConfigureDecaySource(_audioSource);

        if (_audioSource.clip != null && !_audioSource.isPlaying)
            _audioSource.Play();
    }

    private void EnsureWallRoot()
    {
        if (_wallRoot != null)
            return;

        GameObject wallObject = new GameObject("DecayWallRuntimeRoot");
        wallObject.transform.SetParent(transform, false);
        _wallRoot = wallObject.transform;

        Rigidbody wallBody = wallObject.AddComponent<Rigidbody>();
        wallBody.isKinematic = true;
        wallBody.useGravity = false;

        _killCollider = wallObject.AddComponent<BoxCollider>();
        _killCollider.isTrigger = true;
        DecayWallKillTrigger killTrigger = wallObject.AddComponent<DecayWallKillTrigger>();
        killTrigger.Configure(this);
        UpdateKillCollider();
    }

    private void UpdateKillCollider()
    {
        if (_killCollider == null)
            return;

        _killCollider.center = Vector3.zero;
        _killCollider.size = new Vector3(
            Mathf.Max(1f, _wallWidth),
            Mathf.Max(1f, _wallHeight),
            Mathf.Max(1f, _particleDepth + _killPadding * 2f));
    }

    private void ResolveDefaultDecayClip()
    {
#if UNITY_EDITOR
        if (_decayLoopClip == null)
            _decayLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultDecayClipPath);
#endif
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public class DecayWallKillTrigger : MonoBehaviour
{
    private DecayWallController _controller;

    public void Configure(DecayWallController controller)
    {
        _controller = controller;
    }

    private void OnTriggerEnter(Collider other)
    {
        _controller?.TryKillFromContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        _controller?.TryKillFromContact(other);
    }
}
