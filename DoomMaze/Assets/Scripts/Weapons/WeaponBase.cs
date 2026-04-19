using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract base class for all weapon MonoBehaviours.
/// Owns magazine ammo state, fire-rate gating, and auto-reload on empty.
/// Subclasses implement <see cref="ExecuteFire"/> with their specific attack logic.
/// </summary>
public abstract class WeaponBase : MonoBehaviour, IWeapon
{
    [SerializeField] protected WeaponData _data;

    // ── IWeapon ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public WeaponData Data => _data;

    /// <inheritdoc/>
    public int CurrentAmmo { get; protected set; }

    /// <inheritdoc/>
    public virtual bool CanFire()
    {
        return Time.time >= _nextFireTime && CurrentAmmo > 0 && !_isReloading;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    protected float _nextFireTime;
    private   bool  _isReloading;

    // ── Cached references ─────────────────────────────────────────────────────

    protected PlayerInventory   _playerInventory;
    protected ViewmodelAnimator _viewmodelAnimator;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _playerInventory   = GetComponentInParent<PlayerInventory>();
        _viewmodelAnimator = GetComponentInParent<ViewmodelAnimator>();
    }

    protected virtual void Start()
    {
        CurrentAmmo = _data != null ? _data.MagazineSize : 0;
    }

    // ── IWeapon implementation ────────────────────────────────────────────────

    /// <inheritdoc/>
    public virtual void Fire()
    {
        if (_isReloading) return;

        if (CurrentAmmo <= 0)
        {
            TryAutoReload();
            return;
        }

        if (!CanFire()) return;

        CurrentAmmo--;
        _nextFireTime = Time.time + 1f / _data.FireRate;

        ExecuteFire();

        EventBus<WeaponFiredEvent>.Raise(new WeaponFiredEvent { Data = _data });
        RaiseAmmoChanged();

        AudioManager.Instance?.PlaySfx(_data.FireSound);

        if (CurrentAmmo <= 0)
            TryAutoReload();
    }

    /// <inheritdoc/>
    public virtual void AltFire() { }

    /// <inheritdoc/>
    public virtual void Reload()
    {
        if (_isReloading || string.IsNullOrEmpty(_data.AmmoTypeId)) return;
        if (_playerInventory == null) return;
        if (CurrentAmmo >= _data.MagazineSize) return;
        if (_playerInventory.GetAmmo(_data.AmmoTypeId) <= 0) return;

        StartCoroutine(ReloadCoroutine());
    }

    /// <inheritdoc/>
    public virtual void OnEquip()
    {
        gameObject.SetActive(true);
    }

    /// <inheritdoc/>
    public virtual void OnUnequip()
    {
        _isReloading = false;
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    // ── Reload ────────────────────────────────────────────────────────────────

    private void TryAutoReload()
    {
        if (string.IsNullOrEmpty(_data.AmmoTypeId)) return;
        if (_playerInventory == null) return;
        if (_playerInventory.GetAmmo(_data.AmmoTypeId) <= 0) return;

        Reload();
    }

    private IEnumerator ReloadCoroutine()
    {
        _isReloading = true;

        AudioManager.Instance?.PlaySfx(_data.ReloadSound);

        float reloadTime = _data.ReloadTime > 0f ? _data.ReloadTime : 1f;
        yield return new WaitForSeconds(reloadTime);

        int needed    = _data.MagazineSize - CurrentAmmo;
        int available = _playerInventory.GetAmmo(_data.AmmoTypeId);
        int toLoad    = Mathf.Min(needed, available);

        if (toLoad > 0)
        {
            _playerInventory.SpendAmmo(_data.AmmoTypeId, toLoad);
            CurrentAmmo += toLoad;
            RaiseAmmoChanged();
        }

        _isReloading = false;
    }

    private void RaiseAmmoChanged()
    {
        EventBus<AmmoChangedEvent>.Raise(new AmmoChangedEvent
        {
            AmmoTypeId  = _data.AmmoTypeId,
            CurrentAmmo = CurrentAmmo,
            CarriedAmmo = string.IsNullOrEmpty(_data.AmmoTypeId) ? 999
                        : (_playerInventory != null ? _playerInventory.GetAmmo(_data.AmmoTypeId) : 0)
        });
    }

    // ── Abstract ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Subclass-specific attack implementation (raycast, overlap sphere, projectile, etc.).
    /// Called by <see cref="Fire"/> after all guards and ammo consumption pass.
    /// </summary>
    protected abstract void ExecuteFire();
}
