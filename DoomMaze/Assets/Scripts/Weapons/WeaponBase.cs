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
    public virtual bool CanBeSwitchedAway => _spriteSequencer == null || _spriteSequencer.CanSwitchWeapons;

    /// <inheritdoc/>
    public virtual bool CanFire()
    {
        return Time.time >= _nextFireTime && CurrentAmmo > 0 && !_isReloading;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    protected float _nextFireTime;
    private   bool  _isReloading;
    private   Coroutine _fireStopCoroutine;

    // ── Cached references ─────────────────────────────────────────────────────

    protected PlayerInventory   _playerInventory;
    protected ViewmodelAnimator   _viewmodelAnimator;
    protected PunchSpriteSequencer _spriteSequencer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _playerInventory = GetComponentInParent<PlayerInventory>();

        Transform viewmodelRoot = transform.parent;
        if (viewmodelRoot != null)
            _viewmodelAnimator = viewmodelRoot.GetComponentInChildren<ViewmodelAnimator>();

        _spriteSequencer = GetComponent<PunchSpriteSequencer>();
    }

    protected virtual void Start()
    {
        CurrentAmmo = _data != null ? _data.MagazineSize : 0;
    }

    // ── IWeapon implementation ────────────────────────────────────────────────

    /// <inheritdoc/>
    public virtual void Fire()
    {
        if (_isReloading)
        {
            StopFiring();
            return;
        }

        if (CurrentAmmo <= 0)
        {
            StopFiring();
            TryAutoReload();
            return;
        }

        if (!CanFire()) return;

        CurrentAmmo--;
        _nextFireTime = Time.time + 1f / _data.FireRate;

        ExecuteFire();

        _spriteSequencer?.StartFiring();

        if (_fireStopCoroutine != null)
            StopCoroutine(_fireStopCoroutine);
        _fireStopCoroutine = StartCoroutine(FireStopCoroutine());

        EventBus<WeaponFiredEvent>.Raise(new WeaponFiredEvent { Data = _data });
        RaiseAmmoChanged();

        PlayFireAudio();

        if (CurrentAmmo <= 0)
        {
            TryAutoReload();
            StopFiring();
        }
    }

    /// <inheritdoc/>
    public virtual void StopFiring()
    {
        if (_fireStopCoroutine != null)
        {
            StopCoroutine(_fireStopCoroutine);
            _fireStopCoroutine = null;
        }

        _spriteSequencer?.StopFiring();
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

        if (_data != null && _spriteSequencer != null)
            _spriteSequencer.ApplyLayout(_data.ViewmodelSpriteSize, _data.ViewmodelSpritePosition);
    }

    /// <inheritdoc/>
    public virtual void OnUnequip()
    {
        _isReloading = false;
        StopFiring();
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

        AudioManager.Instance?.PlaySfx(_data.ReloadSounds);

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

    private IEnumerator FireStopCoroutine()
    {
        float interval = _data != null && _data.FireRate > 0f ? 1f / _data.FireRate : 0.5f;
        yield return new WaitForSeconds(interval * 2f);
        _spriteSequencer?.StopFiring();
        _fireStopCoroutine = null;
    }

    // ── Abstract ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Subclass-specific attack implementation (raycast, overlap sphere, projectile, etc.).
    /// Called by <see cref="Fire"/> after all guards and ammo consumption pass.
    /// </summary>
    protected virtual void PlayFireAudio()
    {
        if (_data == null)
            return;

        AudioManager.Instance?.PlaySfx(_data.FireSounds);
    }

    protected abstract void ExecuteFire();
}
