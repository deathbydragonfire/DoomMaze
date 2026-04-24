using UnityEngine;

/// <summary>
/// Melee attack module that validates reach against the cached player collider.
/// This avoids false negatives from the grunt's own colliders filling overlap queries.
/// </summary>
public class MeleeAttackModule : MonoBehaviour, IAttackModule
{
    [SerializeField] protected float _attackRange = 2;
    [SerializeField] protected float _attackDamage = 15;
    [SerializeField] protected float _attackRate = 1;
    [SerializeField] protected DamageType _attackDamageType = DamageType.Physical;

    // ── IAttackModule ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public float AttackRange => _attackRange;

    /// <inheritdoc/>
    public float AttackDamage => _attackDamage;

    /// <inheritdoc/>
    public float AttackRate => _attackRate;

    /// <inheritdoc/>
    public DamageType AttackDamageType => _attackDamageType;

    // ── Constants ───────────────────────────────────────────────────────────────

    private const float ATTACK_HEIGHT_OFFSET = 0.9f;
    private const float ATTACK_FORWARD_BIAS  = 0.45f;

    // ── Cached refs ───────────────────────────────────────────────────────────────

    private EnemyData   _data;
    private EnemyBase   _enemyBase;
    private Transform   _playerTransform;
    private Collider    _playerCollider;
    private IDamageable _playerDamageable;
    private float       _attackTimer;

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
        if (_attackTimer <= 0f)
        {
            PerformAttack();
            _attackTimer = 1f / AttackRate;
        }
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

        AudioManager.Instance?.PlaySfx(_data.GetAttackClip(), _data.AttackVolume);
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
            attackOrigin += toPlayer.normalized * Mathf.Min(ATTACK_FORWARD_BIAS, AttackRange * 0.5f);

        return attackOrigin;
    }

    private bool IsPlayerInRange(Vector3 attackOrigin)
    {
        float attackRangeSqr = AttackRange * AttackRange;

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
                             + transform.forward * Mathf.Min(ATTACK_FORWARD_BIAS, AttackRange * 0.5f);

        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(attackOrigin, Vector3.up, AttackRange);
    }
#endif
}
