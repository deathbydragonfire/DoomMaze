using UnityEngine;

/// <summary>
/// <see cref="WeaponBase"/> subclass for the flamethrower.
/// Uses a wide <see cref="Physics.SphereCastNonAlloc"/> to simulate a flame cone,
/// applying <see cref="DamageType.Fire"/> damage to each <see cref="IDamageable"/> hit per tick.
/// </summary>
public class FlamethrowerWeapon : WeaponBase
{
    private const float SPHERE_CAST_RADIUS = 1.5f;

    [SerializeField] private LayerMask _hitMask;

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

    protected override void Awake()
    {
        base.Awake();

        if (_data == null)
            Debug.LogWarning("[FlamethrowerWeapon] No WeaponData assigned.", this);
    }

    /// <inheritdoc/>
    protected override void ExecuteFire()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 origin  = cam.transform.position;
        Vector3 forward = cam.transform.forward;

        int hits = Physics.SphereCastNonAlloc(origin, SPHERE_CAST_RADIUS, forward, _hitBuffer, _data.Range, _hitMask);

        for (int i = 0; i < hits; i++)
        {
            _hitBuffer[i].collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo
            {
                Amount = _data.Damage,
                Type   = DamageType.Fire,
                Source = gameObject
            });
        }

        if (_data != null)
            EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
            {
                Magnitude = _data.ShakeMagnitude,
                Duration  = _data.ShakeDuration
            });
    }
}
