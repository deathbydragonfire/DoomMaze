using System.Collections;
using UnityEngine;

/// <summary>Implemented by attack modules that trigger their own attack animations.</summary>
public interface IManualAttackAnimationModule { }

/// <summary>
/// Melee attack module that validates reach against the cached player collider.
/// This avoids false negatives from the grunt's own colliders filling overlap queries.
/// </summary>
public class MeleeAttackModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 2;
    [SerializeField] private float _maxAttackRange = 3;
    [SerializeField] private float _attackDamage = 15;
    [SerializeField] private float _attackRate = 1;
    [SerializeField] private DamageType _attackDamageType = DamageType.Physical;
    [SerializeField] private string _attackAnimTrigger = "Melee";
    [SerializeField] private float _impactDelay = 0.35f;

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

    // ── Constants ───────────────────────────────────────────────────────────────

    private const float ATTACK_HEIGHT_OFFSET = 0.9f;
    private const float ATTACK_FORWARD_BIAS  = 0.45f;

    // ── Cached references ─────────────────────────────────────────────────────

    private EnemyData   _data;
    private EnemyBase   _enemyBase;
    private Transform   _playerTransform;
    private Collider    _playerCollider;
    private IDamageable _playerDamageable;
    private float       _attackTimer;
    private Coroutine   _attackRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        if (_enemyBase == null)
            Debug.LogError("[MeleeAttackModule] EnemyBase not found on this GameObject.");
    }

    private void Start()
    {
        _data = _enemyBase != null ? _enemyBase.Data : null;
        if (_data == null)
            Debug.LogError("[MeleeAttackModule] EnemyData is null. Assign EnemyData to EnemyBase.");

        CachePlayerReferences(logWarnings: true);
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
        if (_data == null) return;

        CachePlayerReferences(logWarnings: false);

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
        if (_enemyBase != null && _enemyBase.UsesBossSfxVolume)
            AudioManager.Instance?.PlayBossSfx(_data.GetAttackClip(), _data.AttackVolume);
        else
            AudioManager.Instance?.PlaySfx(_data.GetAttackClip(), _data.AttackVolume);

        yield return new WaitForSeconds(Mathf.Max(0f, _impactDelay));

        if (_enemyBase == null || !_enemyBase.IsAlive || _enemyBase.CurrentState != EnemyState.Attack || !ReferenceEquals(_enemyBase.CurrentAttack, this))
        {
            _attackRoutine = null;
            yield break;
        }

        PerformAttack();
        _attackRoutine = null;
    }

    private void PerformAttack()
    {
        CachePlayerReferences(logWarnings: false);

        if (_data == null || _playerTransform == null || _playerDamageable == null || !_playerDamageable.IsAlive)
            return;

        Vector3 attackOrigin = GetAttackOrigin();
        if (!IsPlayerInRange(attackOrigin))
            return;

        _playerDamageable.TakeDamage(new DamageInfo
        {
            Amount = AttackDamage,
            Type   = AttackDamageType,
            Source = gameObject
        });

    }

    private void CachePlayerReferences(bool logWarnings)
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null)
        {
            if (logWarnings)
                Debug.LogWarning("[MeleeAttackModule] Player transform not cached on EnemyBase.");
            return;
        }

        if (_playerDamageable == null)
            _playerDamageable = _playerTransform.GetComponentInParent<IDamageable>();

        if (_playerCollider == null)
            _playerCollider = _playerTransform.GetComponent<Collider>();

        if (logWarnings && _playerDamageable == null)
            Debug.LogWarning("[MeleeAttackModule] No IDamageable found on the player hierarchy.");

        if (logWarnings && _playerCollider == null)
            Debug.LogWarning("[MeleeAttackModule] No Collider found on the player root. Falling back to transform distance checks.");
    }

    private Vector3 GetAttackOrigin()
    {
        Vector3 attackOrigin = transform.position + Vector3.up * ATTACK_HEIGHT_OFFSET;
        Vector3 toPlayer     = _playerTransform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude > 0.0001f)
            attackOrigin += toPlayer.normalized * Mathf.Min(ATTACK_FORWARD_BIAS, MaxAttackRange * 0.5f);

        return attackOrigin;
    }

    private bool IsPlayerInRange(Vector3 attackOrigin)
    {
        float attackRangeSqr = MaxAttackRange * MaxAttackRange;

        if (_playerCollider != null)
        {
            Vector3 closestPoint = _playerCollider.ClosestPoint(attackOrigin);
            return (closestPoint - attackOrigin).sqrMagnitude <= attackRangeSqr;
        }

        return (_playerTransform.position - attackOrigin).sqrMagnitude <= attackRangeSqr;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;

        Vector3 attackOrigin = transform.position
                             + Vector3.up * ATTACK_HEIGHT_OFFSET
                             + transform.forward * Mathf.Min(ATTACK_FORWARD_BIAS, MaxAttackRange * 0.5f);

        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(attackOrigin, Vector3.up, MaxAttackRange);
    }
#endif
}
