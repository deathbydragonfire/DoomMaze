using UnityEngine;

/// <summary>
/// Concrete <see cref="WeaponBase"/> for melee weapons (Fists).
/// Uses <see cref="Physics.OverlapSphereNonAlloc"/> in front of the player to hit nearby targets.
/// Ammo is infinite — <see cref="WeaponData.AmmoTypeId"/> must be empty on the data asset.
/// </summary>
public class MeleeWeapon : WeaponBase
{
    [SerializeField] private LayerMask _hitMask;

    private const int OVERLAP_BUFFER_SIZE = 8;
    private readonly Collider[] _overlapBuffer = new Collider[OVERLAP_BUFFER_SIZE];

    // ── IWeapon overrides ─────────────────────────────────────────────────────

    /// <summary>Melee always reports it can fire — ammo is infinite.</summary>
    public override bool CanFire()
    {
        // Melee does not consume ammo; only the fire-rate timer matters.
        return Time.time >= _nextFireTime;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void Start()
    {
        // Keep CurrentAmmo non-zero so base-class logic is never surprised.
        CurrentAmmo = 999;
    }

    // ── WeaponBase ────────────────────────────────────────────────────────────

    public override void Fire()
    {
        if (!CanFire()) return;

        _nextFireTime = Time.time + 1f / _data.FireRate;

        ExecuteFire();

        EventBus<WeaponFiredEvent>.Raise(new WeaponFiredEvent { Data = _data });
    }

    /// <inheritdoc/>
    protected override void ExecuteFire()
    {
        Vector3 attackOrigin = transform.position + transform.forward * (_data.Range * 0.5f);

        int hitCount = Physics.OverlapSphereNonAlloc(attackOrigin, _data.Range, _overlapBuffer, _hitMask);

        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageable = _overlapBuffer[i].GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(new DamageInfo
            {
                Amount = _data.Damage,
                Type   = DamageType.Physical,
                Source = gameObject
            });
        }

        _viewmodelAnimator?.PlayMelee();
    }
}
