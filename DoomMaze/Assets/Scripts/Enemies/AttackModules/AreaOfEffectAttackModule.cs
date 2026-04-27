using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Area of effect attack module that generates a damaging trigger area at intervals defined by <see cref="AttackRate"/>.
/// </summary>
public class AreaOfEffectAttackModule : MonoBehaviour, IAttackModule, IManualAttackAnimationModule, IAttackExecutionStatus
{
    [SerializeField] private float _minAttackRange = 8;
    [SerializeField] private float _maxAttackRange = 10;
    [SerializeField] private float _attackDamage = 10;
    [SerializeField] private float _attackRate = 0.25f;
    [SerializeField] private DamageType _attackDamageType = DamageType.Energy;
    [SerializeField] private string _attackAnimTrigger = "AreaOfEffect";
    [SerializeField] private Transform _areaOfEffectPoint;
    [SerializeField] private Vector3 _areaOfEffectOffset = new Vector3(0.25f, 0f, 0.38f);
    [SerializeField] private float _expansionSpeed = 3f;
    [SerializeField] private float _effectMaxDistance = 0f;
    [SerializeField] private float _spawnDelay = 0.55f;
    [SerializeField] private AudioClip _attackSoundOverride;
    [SerializeField] [Range(0f, 1f)] private float _attackSoundOverrideVolume = 1f;
    [SerializeField] private bool _useBossVisualPolish;
    [SerializeField] private Color _bossRingColor = new(1f, 0.55f, 0.08f, 0.78f);
    [SerializeField] private Color _bossRingEmissionColor = new(4f, 2.2f, 0.35f, 0.78f);
    [SerializeField] private Color _bossPulseColor = new(1f, 0.35f, 0.04f, 0.5f);
    [SerializeField] private float _bossVisualIntensity = 1f;
    //TODO: Need something for starting size of area?

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

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    public void ConfigureBossVisualPolish(Color ringColor, Color emissionColor, Color pulseColor, float intensity)
    {
        _useBossVisualPolish = true;
        _bossRingColor = ringColor;
        _bossRingEmissionColor = emissionColor;
        _bossPulseColor = pulseColor;
        _bossVisualIntensity = Mathf.Max(0.1f, intensity);
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
        SpawnBossPrecastPulse();

        yield return new WaitForSeconds(Mathf.Max(0f, _spawnDelay));

        if (_enemyBase == null || !_enemyBase.IsAlive || _enemyBase.CurrentState != EnemyState.Attack || !ReferenceEquals(_enemyBase.CurrentAttack, this))
        {
            _attackRoutine = null;
            yield break;
        }

        GenerateAreaOfEffect();
        PlayAttackSound();
        _attackRoutine = null;
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
            _effectMaxDistance > 0f ? _effectMaxDistance : MaxAttackRange,
            _expansionSpeed
        );

        if (_useBossVisualPolish)
        {
            areaOfEffect.ConfigureBossVisuals(
                _bossRingColor,
                _bossRingEmissionColor,
                _bossPulseColor,
                _bossVisualIntensity,
                0.008f * _bossVisualIntensity);
        }
    }

    private void SpawnBossPrecastPulse()
    {
        if (!_useBossVisualPolish)
            return;

        BossAttackVfx.SpawnImpactPulse(
            GetAreaOfEffectPosition(),
            2.4f * _bossVisualIntensity,
            _bossPulseColor,
            Mathf.Clamp(_spawnDelay, 0.18f, 0.45f),
            0f);
    }

    private void PlayAttackSound()
    {
        AudioClip clip = _attackSoundOverride != null ? _attackSoundOverride : _data.GetAttackClip();
        float volume = _attackSoundOverride != null ? _attackSoundOverrideVolume : _data.AttackVolume;

        if (_useBossVisualPolish || (_enemyBase != null && _enemyBase.UsesBossSfxVolume))
            AudioManager.Instance?.PlayBossSfx(clip, volume);
        else
            AudioManager.Instance?.PlaySfx(clip, volume);
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
