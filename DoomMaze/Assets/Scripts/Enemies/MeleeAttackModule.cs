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

/// <summary>
/// Melee attack module. Uses <see cref="Physics.OverlapSphereNonAlloc"/> to hit
/// <see cref="IDamageable"/> targets within <see cref="EnemyData.AttackRange"/>.
/// Reads all stats from the shared <see cref="EnemyData"/> asset.
/// </summary>
public class MeleeAttackModule : MonoBehaviour, IAttackModule
{
    private const int OVERLAP_BUFFER_SIZE = 4;
    private readonly Collider[] _overlapBuffer = new Collider[OVERLAP_BUFFER_SIZE];

    private EnemyData _data;
    private EnemyBase _enemyBase;
    private float     _attackTimer;

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
            Debug.LogError("[MeleeAttackModule] EnemyData is null — assign EnemyData to EnemyBase.");
    }

    // ── IAttackModule ─────────────────────────────────────────────────────────

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

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            PerformAttack();
            _attackTimer = 1f / _data.AttackRate;
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void PerformAttack()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _data.AttackRange, _overlapBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            if (_overlapBuffer[i].gameObject == gameObject) continue;

            IDamageable damageable = _overlapBuffer[i].GetComponent<IDamageable>();
            damageable?.TakeDamage(new DamageInfo
            {
                Amount = _data.AttackDamage,
                Type   = _data.AttackDamageType,
                Source = gameObject
            });
        }

        AudioManager.Instance.PlaySfx(_data.AttackSound);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, _data.AttackRange);
    }
#endif
}
