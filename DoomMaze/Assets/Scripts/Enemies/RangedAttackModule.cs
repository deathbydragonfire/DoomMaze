using UnityEngine;

/// <summary>
/// Ranged attack module that spawns a simple projectile toward the player at
/// intervals defined by <see cref="EnemyData.AttackRate"/>.
/// </summary>
public class RangedAttackModule : MonoBehaviour, IAttackModule
{
    [SerializeField] private Vector3 _muzzleOffset = new Vector3(0f, 0.95f, 0.55f);
    [SerializeField] private float _projectileSpeed = 16f;
    [SerializeField] private float _projectileRadius = 0.22f;

    private EnemyData _data;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private float _attackTimer;

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        if (_enemyBase == null)
            Debug.LogError("[RangedAttackModule] EnemyBase not found on this GameObject.");
    }

    private void Start()
    {
        if (_enemyBase == null)
            return;

        _data = _enemyBase.Data;
        if (_data == null)
            Debug.LogError("[RangedAttackModule] EnemyData is null - assign EnemyData to EnemyBase.");

        CachePlayerReference(logWarnings: true);
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
        if (_data == null)
            return;

        CachePlayerReference(logWarnings: false);

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            FireProjectile();
            _attackTimer = 1f / _data.AttackRate;
        }
    }

    private void FireProjectile()
    {
        CachePlayerReference(logWarnings: false);

        if (_playerTransform == null)
            return;

        Vector3 origin = transform.TransformPoint(_muzzleOffset);
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
            _data.AttackDamage,
            _data.AttackDamageType,
            _data.AttackRange,
            _projectileSpeed,
            _projectileRadius
        );

        AudioManager.Instance?.PlaySfx(_data.GetAttackClip(), _data.AttackVolume);
    }

    private void CachePlayerReference(bool logWarnings)
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null && logWarnings)
            Debug.LogWarning("[RangedAttackModule] Player transform not cached on EnemyBase.");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null || _playerTransform == null)
            return;

        UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.6f);
        UnityEditor.Handles.DrawLine(transform.TransformPoint(_muzzleOffset), _playerTransform.position + Vector3.up);
    }
#endif
}
