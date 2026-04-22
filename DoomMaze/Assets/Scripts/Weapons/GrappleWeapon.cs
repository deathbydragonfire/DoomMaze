using System.Collections;
using UnityEngine;

/// <summary>
/// <see cref="WeaponBase"/> subclass implementing the grapple gun with a 7-state
/// enum-based state machine. Performs an immediate raycast on fire, freezes hooked
/// enemies via <see cref="GrappledState"/>, and resolves the pull via
/// <see cref="UnityEngine.AI.NavMeshAgent.destination"/>.
/// Left-click fires the hook; repeated left-clicks during <c>EnemyHooked</c> act as mash input.
/// </summary>
public class GrappleWeapon : WeaponBase
{
    private enum GrappleState
    {
        Idle,
        MissRetract,
        EnemyHooked,
        MashRetract,
        MashFail,
        Cooldown
    }

    [SerializeField] private GrappleWeaponData _grappleData;
    [SerializeField] private LayerMask         _hitMask;

    private GrappleState _state = GrappleState.Idle;
    private float        _mashProgress;
    private IDamageable  _hitEnemy;
    private GrappledState _grappledState;
    private Transform    _hookedTransform;
    private Camera       _camera;

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[1];

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _camera = Camera.main;

        if (_grappleData == null)
            Debug.LogWarning($"[GrappleWeapon] GrappleWeaponData not assigned on {gameObject.name}.");
    }

    // ── IWeapon overrides ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override bool CanFire()
    {
        return base.CanFire() && _state == GrappleState.Idle;
    }

    /// <summary>
    /// Left-click during <see cref="GrappleState.EnemyHooked"/> routes to <see cref="Mash"/>.
    /// Otherwise delegates to <see cref="WeaponBase.Fire"/>.
    /// </summary>
    public override void Fire()
    {
        if (_state == GrappleState.EnemyHooked)
        {
            Mash();
            return;
        }

        base.Fire();
    }

    /// <inheritdoc/>
    public override void OnUnequip()
    {
        if (_state != GrappleState.Idle && _state != GrappleState.Cooldown)
        {
            _grappledState?.Release();
        }

        _state           = GrappleState.Idle;
        _grappledState   = null;
        _hookedTransform = null;

        base.OnUnequip();
    }

    // ── WeaponBase abstract ───────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void ExecuteFire()
    {
        _spriteSequencer?.PlayNextPunch();

        EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
        {
            Magnitude = _data != null ? _data.ShakeMagnitude : 0.05f,
            Duration  = _data != null ? _data.ShakeDuration  : 0.12f
        });

        bool didHit = TryRaycast(out RaycastHit hit);

        if (didHit)
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                EnterEnemyHooked(hit, damageable);
                return;
            }
        }

        EnterMissRetract(didHit ? hit.point : GetMissPoint());
    }

    // ── Mash ──────────────────────────────────────────────────────────────────

    private void Mash()
    {
        if (_grappleData == null) return;

        _mashProgress += _grappleData.MashProgressPerPress;
        _mashProgress  = Mathf.Clamp01(_mashProgress);

        EventBus<GrappleMashProgressEvent>.Raise(new GrappleMashProgressEvent { Progress = _mashProgress });

        if (_mashProgress >= 1f)
            EnterMashRetract();
    }

    // ── State entry ───────────────────────────────────────────────────────────

    private void EnterMissRetract(Vector3 missPoint)
    {
        _state = GrappleState.MissRetract;
        EventBus<GrappleMissedEvent>.Raise(new GrappleMissedEvent());
        StartCoroutine(MissRetractCoroutine());
    }

    private void EnterEnemyHooked(RaycastHit hit, IDamageable damageable)
    {
        EnemyBase enemyBase = hit.collider.GetComponentInParent<EnemyBase>();

        if (enemyBase != null && enemyBase.Data != null && enemyBase.Data.IsHookImmune)
        {
            EnterMissRetract(hit.point);
            return;
        }

        _state           = GrappleState.EnemyHooked;
        _hitEnemy        = damageable;
        _hookedTransform = hit.collider.transform;
        _mashProgress    = 0f;

        _grappledState = _hookedTransform.gameObject.GetComponent<GrappledState>();
        if (_grappledState == null)
            _grappledState = _hookedTransform.gameObject.AddComponent<GrappledState>();

        _grappledState.Hook();

        EventBus<GrappleHookedEvent>.Raise(new GrappleHookedEvent { Enemy = _hookedTransform.gameObject });

        _spriteSequencer?.PlayNextPunch();

        StartCoroutine(MashWindowCoroutine());
    }

    private void EnterMashRetract()
    {
        _state = GrappleState.MashRetract;

        Vector3 grabPoint = _camera != null
            ? _camera.transform.position + _camera.transform.forward * (_grappleData != null ? _grappleData.GrabPointDistance : 1.5f)
            : transform.position + Vector3.forward * 1.5f;

        _grappledState?.Pull(grabPoint);

        StartCoroutine(PullCoroutine());
    }

    private void EnterMashFail()
    {
        _state = GrappleState.MashFail;

        _grappledState?.Release();
        _grappledState   = null;
        _hookedTransform = null;

        EventBus<GrappleReleasedEvent>.Raise(new GrappleReleasedEvent());

        EnterCooldown();
    }

    private void EnterCooldown()
    {
        float cooldown = _grappleData != null ? _grappleData.CooldownSeconds : 1.5f;
        _nextFireTime = Time.time + cooldown;
        _state        = GrappleState.Idle;
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private System.Collections.IEnumerator MissRetractCoroutine()
    {
        float duration = _grappleData != null ? _grappleData.TetherDuration : 0.3f;
        yield return new UnityEngine.WaitForSeconds(duration);
        EnterCooldown();
    }

    private System.Collections.IEnumerator MashWindowCoroutine()
    {
        float window = _grappleData != null ? _grappleData.MashWindowSeconds : 0.75f;
        yield return new UnityEngine.WaitForSeconds(window);

        if (_state == GrappleState.EnemyHooked)
            EnterMashFail();
    }

    private System.Collections.IEnumerator PullCoroutine()
    {
        float duration = _grappleData != null ? _grappleData.PullDurationSeconds : 0.25f;
        yield return new UnityEngine.WaitForSeconds(duration);

        if (_hitEnemy != null)
        {
            _hitEnemy.TakeDamage(new DamageInfo
            {
                Amount = _data != null ? _data.Damage : 40f,
                Type   = DamageType.Physical,
                Source = gameObject
            });
        }

        EventBus<GrapplePulledEvent>.Raise(new GrapplePulledEvent
        {
            Enemy = _hookedTransform != null ? _hookedTransform.gameObject : null
        });

        float stunDuration = _grappleData != null ? _grappleData.PullStunDuration : 1.5f;
        _grappledState?.ReleaseWithStun(stunDuration);
        _grappledState   = null;
        _hookedTransform = null;

        EnterCooldown();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryRaycast(out RaycastHit hit)
    {
        hit = default;
        if (_camera == null) return false;

        Ray   ray   = new Ray(_camera.transform.position, _camera.transform.forward);
        float range = _data != null ? _data.Range : 30f;
        int   count = Physics.RaycastNonAlloc(ray, _hitBuffer, range, _hitMask);

        if (count == 0) return false;

        hit = _hitBuffer[0];
        return true;
    }

    private Vector3 GetMissPoint()
    {
        float range = _data != null ? _data.Range : 30f;

        if (_camera != null)
            return _camera.transform.position + _camera.transform.forward * range;

        return transform.position + transform.forward * range;
    }
}
