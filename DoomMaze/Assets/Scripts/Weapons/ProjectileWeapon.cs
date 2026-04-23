using UnityEngine;

/// <summary>
/// <see cref="WeaponBase"/> subclass for the rocket launcher.
/// Retrieves a <see cref="Rocket"/> from an <see cref="ObjectPool{T}"/>, positions it at the
/// camera origin, and calls <see cref="Rocket.Launch"/> toward the camera's forward direction.
/// </summary>
public class ProjectileWeapon : WeaponBase
{
    private const int POOL_SIZE = 4;
    private const float DEFAULT_SPAWN_DISTANCE = 0.75f;

    [SerializeField] private LayerMask  _hitMask;
    [SerializeField] private Rocket     _rocketPrefab;
    [SerializeField] private float      _spawnDistance = DEFAULT_SPAWN_DISTANCE;

    [Header("Audio")]
    [SerializeField] private AudioClip[] _fireSounds;
    [Range(0f, 1f)] [SerializeField] private float _fireSoundVolume = 1f;

    private ObjectPool<Rocket> _rocketPool;

    protected override void Awake()
    {
        base.Awake();

        if (_rocketPrefab == null)
        {
            Debug.LogWarning("[ProjectileWeapon] No rocket prefab assigned.", this);
            return;
        }

        _rocketPool = new ObjectPool<Rocket>(_rocketPrefab, POOL_SIZE);
    }

    /// <inheritdoc/>
    protected override void ExecuteFire()
    {
        if (_rocketPool == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        float launchRange = _data != null && _data.Range > 0f ? _data.Range : 80f;
        Vector3 origin = cam.transform.position + cam.transform.forward * GetSpawnDistance();
        Vector3 direction = cam.transform.forward;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit aimHit, launchRange, _hitMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 aimedDirection = aimHit.point - origin;
            if (aimedDirection.sqrMagnitude > 0.0001f)
                direction = aimedDirection.normalized;
        }

        Quaternion rotation  = Quaternion.LookRotation(direction);

        Rocket rocket = _rocketPool.Get(origin, rotation);
        rocket.Init(_rocketPool);
        rocket.Launch(transform.root.gameObject, direction, _data != null ? _data.Damage : 0f, launchRange, _hitMask);

        if (_data != null)
            EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
            {
                Magnitude = _data.ShakeMagnitude,
                Duration  = _data.ShakeDuration
            });
    }

    protected override void PlayFireAudio()
    {
        if (_fireSounds != null && _fireSounds.Length > 0)
        {
            AudioManager.Instance?.PlaySfx(_fireSounds, _fireSoundVolume);
            return;
        }

        base.PlayFireAudio();
    }

    private float GetSpawnDistance()
    {
        return _spawnDistance > 0f ? _spawnDistance : DEFAULT_SPAWN_DISTANCE;
    }
}
