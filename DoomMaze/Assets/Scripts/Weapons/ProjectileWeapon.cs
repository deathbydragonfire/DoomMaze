using UnityEngine;

/// <summary>
/// <see cref="WeaponBase"/> subclass for the rocket launcher.
/// Retrieves a <see cref="Rocket"/> from an <see cref="ObjectPool{T}"/>, positions it at the
/// camera origin, and calls <see cref="Rocket.Launch"/> toward the camera's forward direction.
/// </summary>
public class ProjectileWeapon : WeaponBase
{
    private const int POOL_SIZE = 4;

    [SerializeField] private LayerMask  _hitMask;
    [SerializeField] private Rocket     _rocketPrefab;

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

        Vector3    origin    = cam.transform.position;
        Vector3    direction = cam.transform.forward;
        Quaternion rotation  = Quaternion.LookRotation(direction);

        Rocket rocket = _rocketPool.Get(origin, rotation);
        rocket.Init(_rocketPool);
        rocket.Launch(direction, _data.Damage, _data.Range, _hitMask);

        if (_data != null)
            EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
            {
                Magnitude = _data.ShakeMagnitude,
                Duration  = _data.ShakeDuration
            });
    }
}
