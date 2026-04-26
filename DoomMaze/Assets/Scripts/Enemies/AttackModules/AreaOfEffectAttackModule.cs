using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Area of effect attack module that generates a damaging trigger area at intervals defined by <see cref="AttackRate"/>.
/// </summary>
public class AreaOfEffectAttackModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule
{
    [SerializeField] private float _attackRange = 10;
    [SerializeField] private float _attackDamage = 10;
    [SerializeField] private float _attackRate = 1;
    [SerializeField] private DamageType _attackDamageType = DamageType.Energy;
    [SerializeField] private string _attackAnimTrigger = "AreaOfEffect";
    [SerializeField] private Transform _areaOfEffectPoint;
    [SerializeField] private Vector3 _areaOfEffectOffset = new Vector3(0.25f, 0f, 0.38f);
    [SerializeField] private float _expansionSpeed = 1f;
    //TODO: Need something for starting size of area?

    // ── IAttackModule ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public float AttackRange => _attackRange;         // Distance at which enemy can attack

    /// <inheritdoc/>
    public float AttackDamage => _attackDamage;

    /// <inheritdoc/>
    public float AttackRate => _attackRate;          // Attacks per second

    /// <inheritdoc/>
    public DamageType AttackDamageType => _attackDamageType;

    /// <inheritdoc/>
    public string AttackAnimTrigger => _attackAnimTrigger;

    // ── Cached references ─────────────────────────────────────────────────────

    private EnemyData _data;
    private EnemyBase _enemyBase;
    private Transform _playerTransform;
    private float _attackTimer;

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        if (_enemyBase == null)
            Debug.LogError("[AreaOfEffectAttackModule] EnemyBase not found on this GameObject.");

        if (_areaOfEffectPoint == null)
            _areaOfEffectPoint = FindAreaOfEffectPoint();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_enemyBase == null)
            return;

        _data = _enemyBase.Data;
        if (_data == null)
            Debug.LogError("[AreaOfEffectAttackModule] EnemyData is null - assign EnemyData to EnemyBase.");

        CachePlayerReference(logWarnings: true);
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
        if (_attackTimer <= 0f)
        {
            GenerateAreaOfEffect();
            _attackTimer = 1f / AttackRate;
        }
    }

    private void GenerateAreaOfEffect()
    {
        CachePlayerReference(logWarnings: false);

        if (_playerTransform == null)
            return;

        Vector3 origin = GetAreaOfEffectPosition();
        Vector3 targetPosition = _playerTransform.position + Vector3.up;
        Vector3 direction = targetPosition - origin;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        GameObject areaOfEffectObject = new GameObject($"{gameObject.name}_AreaOfEffect");
        EnemyAreaOfEffect areaOfEffect = areaOfEffectObject.AddComponent<EnemyAreaOfEffect>();
        areaOfEffect.Generate(
            gameObject,
            origin,
            direction.normalized,
            AttackDamage,
            AttackDamageType,
            AttackRange,
            _expansionSpeed
        );

        _enemyBase?.PlayAttackAnimationOneShot();
        AudioManager.Instance?.PlaySfx(_data.GetAttackClip(), _data.AttackVolume);
    }

    private void CachePlayerReference(bool logWarnings)
    {
        if (_playerTransform == null && _enemyBase != null)
            _playerTransform = _enemyBase.PlayerTransform;

        if (_playerTransform == null && logWarnings)
            Debug.LogWarning("[RangedAttackModule] Player transform not cached on EnemyBase.");
    }

    private Vector3 GetAreaOfEffectPosition()
    {
        return _areaOfEffectPoint != null ? _areaOfEffectPoint.position : transform.TransformPoint(_areaOfEffectOffset);
    }

    private Transform FindAreaOfEffectPoint()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != transform && children[i].name == "AreaOfEffect")
                return children[i];
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null || _playerTransform == null)
            return;

        // UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.6f);
        // UnityEditor.Handles.DrawLine(GetAreaOfEffectPosition(), _playerTransform.position + Vector3.up);
    }
#endif
}

