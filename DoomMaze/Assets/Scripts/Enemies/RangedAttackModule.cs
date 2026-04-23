using UnityEngine;

/// <summary>
/// Ranged hitscan attack module. Fires a single ray toward the player at intervals
/// defined by <see cref="EnemyData.AttackRate"/>. Uses <see cref="Physics.RaycastNonAlloc"/>
/// with a pre-allocated <see cref="RaycastHit"/> buffer — no per-shot heap allocations.
/// </summary>
public class RangedAttackModule : MonoBehaviour, IAttackModule
{
    [SerializeField] private LayerMask _playerLayerMask; // Inspector: assign the "Player" layer

    // Pre-allocated single-target buffer — enemies always aim at one target.
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[1];

    private EnemyData  _data;
    private EnemyBase  _enemyBase;
    private Transform  _playerTransform;
    private float      _attackTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        if (_enemyBase == null)
            Debug.LogError("[RangedAttackModule] EnemyBase not found on this GameObject.");
    }

    private void Start()
    {
        if (_enemyBase == null) return;

        _data            = _enemyBase.Data;
        _playerTransform = _enemyBase.PlayerTransform;

        if (_data == null)
            Debug.LogError("[RangedAttackModule] EnemyData is null — assign EnemyData to EnemyBase.");

        if (_playerTransform == null)
            Debug.LogWarning("[RangedAttackModule] Player transform not cached on EnemyBase.");
    }

    // ── IAttackModule ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void OnAttackEnter()
    {
        // Start with timer expired so the first shot fires immediately on entry.
        _attackTimer = 0f;
    }

    /// <inheritdoc/>
    public void Tick()
    {
        if (_data == null) return;

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            FireRay();
            _attackTimer = 1f / _data.AttackRate;
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void FireRay()
    {
        if (_playerTransform == null) return;

        // Aim at the player's center-of-mass offset (1 m up from feet).
        Vector3 targetPosition = _playerTransform.position + Vector3.up * 1f;
        Vector3 direction      = (targetPosition - transform.position).normalized;

        int hitCount = Physics.RaycastNonAlloc(
            transform.position,
            direction,
            _hitBuffer,
            _data.AttackRange,
            _playerLayerMask
        );

        if (hitCount > 0)
        {
            IDamageable damageable = _hitBuffer[0].collider.GetComponent<IDamageable>();
            damageable?.TakeDamage(new DamageInfo
            {
                Amount = _data.AttackDamage,
                Type   = _data.AttackDamageType,
                Source = gameObject
            });
        }

        AudioManager.Instance?.PlaySfx(_data.GetAttackClip(), _data.AttackVolume);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null || _playerTransform == null) return;
        UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.6f);
        UnityEditor.Handles.DrawLine(transform.position, _playerTransform.position + Vector3.up);
    }
#endif
}
