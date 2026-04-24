using UnityEngine;

/// <summary>
/// Contract for all attack behaviours composed onto <see cref="EnemyBase"/>.
/// Define additional implementations alongside this interface rather than creating new files
/// until the attack module count exceeds two, at which point extract to /Assets/Scripts/Core/.
/// </summary>
public interface IAttackModule
{
    /// <summary>Called by <see cref="EnemyBase"/> when transitioning into the Attack state.</summary>
    void OnAttackEnter();

    /// <summary>Called by <see cref="EnemyBase"/> each Update tick while in the Attack state.</summary>
    void Tick();
}

/// <summary>Implemented by attack modules that trigger their own attack animations.</summary>
public interface IManualAttackAnimationModule { }

/// <summary>
/// Melee attack module that validates reach against the cached player collider.
/// This avoids false negatives from the grunt's own colliders filling overlap queries.
/// </summary>
public class MeleeAttackModule : MonoBehaviour, IAttackModule
{
    private const float ATTACK_HEIGHT_OFFSET = 0.9f;
    private const float ATTACK_FORWARD_BIAS  = 0.45f;

    private EnemyData   _data;
    private EnemyBase   _enemyBase;
    private Transform   _playerTransform;
    private Collider    _playerCollider;
    private IDamageable _playerDamageable;
    private float       _attackTimer;

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
            _attackTimer = 1f / _data.AttackRate;
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
            Amount = _data.AttackDamage,
            Type   = _data.AttackDamageType,
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
            attackOrigin += toPlayer.normalized * Mathf.Min(ATTACK_FORWARD_BIAS, _data.AttackRange * 0.5f);

        return attackOrigin;
    }

    private bool IsPlayerInRange(Vector3 attackOrigin)
    {
        float attackRangeSqr = _data.AttackRange * _data.AttackRange;

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
                             + transform.forward * Mathf.Min(ATTACK_FORWARD_BIAS, _data.AttackRange * 0.5f);

        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(attackOrigin, Vector3.up, _data.AttackRange);
    }
#endif
}
