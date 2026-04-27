using System.Collections;
using UnityEngine;

/// <summary>
/// Ranged attack module that spawns a simple projectile toward the player at
/// intervals defined by <see cref="AttackRate"/>.
/// </summary>
public class RangedAttackModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 10;
    [SerializeField] private float _maxAttackRange = 12;
    [SerializeField] private float _attackDamage = 10;
    [SerializeField] private float _attackRate = 2;
    [SerializeField] private DamageType _attackDamageType = DamageType.Energy;
    [SerializeField] private string _attackAnimTrigger = "Ranged";
    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private Vector3 _muzzleOffset = new Vector3(0f, 0.95f, 0.55f);
    [SerializeField] private float _projectileSpeed = 16f;
    [SerializeField] private float _projectileRadius = 0.22f;
    [SerializeField] private float _projectileMaxDistance = 0f;
    [SerializeField] private float _fireDelay = 0.32f;
    [SerializeField] private AudioClip _attackSoundOverride;
    [SerializeField] [Range(0f, 1f)] private float _attackSoundOverrideVolume = 1f;

    // ── IAttackModule ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public float MinAttackRange => _minAttackRange;

    /// <inheritdoc/>
    public float MaxAttackRange => _maxAttackRange;

    /// <inheritdoc/>
    public float AttackDamage => _attackDamage;

    /// <inheritdoc/>
    public float AttackRate => _attackRate;          // Attacks per second

    /// <inheritdoc/>
    public DamageType AttackDamageType => _attackDamageType;

    /// <inheritdoc/>
    public string AttackAnimTrigger => _attackAnimTrigger;

    public bool IsExecuting => _attackRoutine != null;

    // ── Cached references ─────────────────────────────────────────────────────

    private EnemyData _data;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private float _attackTimer;
    private Coroutine _attackRoutine;

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        if (_enemyBase == null)
            Debug.LogError("[RangedAttackModule] EnemyBase not found on this GameObject.");

        if (_muzzlePoint == null)
            _muzzlePoint = FindMuzzlePoint();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_enemyBase == null)
            return;

        _data = _enemyBase.Data;
        if (_data == null)
            Debug.LogError("[RangedAttackModule] EnemyData is null - assign EnemyData to EnemyBase.");

        CachePlayerReference(logWarnings: true);
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    // ── IAttackModule implementation ────────────────────────────────────────────────

    /// <inheritdoc/>
    public void OnAttackEnter()
    {
        // Start with timer expired so the first attack fires immediately on entry.
        _attackTimer = 0f;
    }

    /// <inheritdoc/>
    public void Tick()
    {
        if (_data == null)
            return;

        CachePlayerReference(logWarnings: false);

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f && _attackRoutine == null)
        {
            _attackRoutine = StartCoroutine(AttackRoutine());
            _attackTimer = 1f / AttackRate;
        }
    }

    private IEnumerator AttackRoutine()
    {
        _enemyBase?.PlayAttackAnimationOneShot();
        PlayAttackSound();

        yield return new WaitForSeconds(Mathf.Max(0f, _fireDelay));

        if (_enemyBase == null || !_enemyBase.IsAlive || _enemyBase.CurrentState != EnemyState.Attack || !ReferenceEquals(_enemyBase.CurrentAttack, this))
        {
            _attackRoutine = null;
            yield break;
        }

        FireProjectile();
        _attackRoutine = null;
    }

    private void FireProjectile()
    {
        CachePlayerReference(logWarnings: false);

        if (_playerTransform == null)
            return;

        Vector3 origin = GetMuzzlePosition();
        Vector3 targetPosition = _playerTransform.position + Vector3.up;
        Vector3 direction = targetPosition - origin;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        GameObject projectileObject = new GameObject($"{gameObject.name}_Projectile");
        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Launch(
            gameObject,
            origin,
            direction.normalized,
            AttackDamage,
            AttackDamageType,
            _projectileMaxDistance > 0f ? _projectileMaxDistance : MaxAttackRange,
            _projectileSpeed,
            _projectileRadius
        );
    }

    private void PlayAttackSound()
    {
        if (_attackSoundOverride != null)
            AudioManager.Instance?.PlaySfx(_attackSoundOverride, _attackSoundOverrideVolume);
        else
            AudioManager.Instance?.PlaySfx(_data.GetAttackClip(), _data.AttackVolume);
    }

    private void CachePlayerReference(bool logWarnings)
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null && logWarnings)
            Debug.LogWarning("[RangedAttackModule] Player transform not cached on EnemyBase.");
    }

    private Vector3 GetMuzzlePosition()
    {
        return _muzzlePoint != null ? _muzzlePoint.position : transform.TransformPoint(_muzzleOffset);
    }

    private Transform FindMuzzlePoint()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != transform && children[i].name == "Muzzle")
                return children[i];
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null || _playerTransform == null)
            return;

        UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.6f);
        UnityEditor.Handles.DrawLine(GetMuzzlePosition(), _playerTransform.position + Vector3.up);
    }
#endif
}
