using UnityEngine;
using UnityEngine.AI;

/// <summary>All possible states in the enemy state machine.</summary>
public enum EnemyState { Idle, Alert, Chase, Attack, Hurt, Dead }

/// <summary>
/// Core enemy MonoBehaviour. Owns the 6-state state machine, <see cref="NavMeshAgent"/>
/// movement, <see cref="HealthComponent"/> integration, aggro detection, and death flow.
/// Attack behaviour is composed via <see cref="IAttackModule"/> — attach either
/// <see cref="MeleeAttackModule"/> or <see cref="RangedAttackModule"/> to the same GameObject.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthComponent))]
public class EnemyBase : MonoBehaviour
{
    [SerializeField] private EnemyData _data;

    // Alert state dwell time before transitioning to Chase.
    private const float ALERT_DWELL_TIME    = 0.3f;
    // Multiplier on AggroRange to determine when a chasing enemy gives up.
    private const float LEASH_MULTIPLIER    = 1.5f;
    // Duration of the hurt stagger.
    private const float HURT_RECOVERY_TIME  = 0.4f;

    public EnemyState CurrentState     { get; private set; }
    public EnemyData  Data             => _data;
    public bool       IsAlive          => _healthComponent.IsAlive;
    public bool       IsGrappled       { get; private set; }

    /// <summary>Cached player transform, exposed to attack modules.</summary>
    public Transform  PlayerTransform  { get; private set; }

    // ── Cached components ─────────────────────────────────────────────────────

    private NavMeshAgent         _agent;
    private HealthComponent      _healthComponent;
    private IAttackModule        _attackModule;
    private EnemySpriteBillboard _billboard;
    private EnemyHitFlash        _hitFlash;

    // ── State timers (no coroutines — floats decremented in Update) ───────────

    private float _alertTimer;
    private float _hurtTimer;
    private float _distanceToPlayer;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _agent           = GetComponent<NavMeshAgent>();
        _healthComponent = GetComponent<HealthComponent>();
        _attackModule    = GetComponent<IAttackModule>();
        _billboard       = GetComponentInChildren<EnemySpriteBillboard>();
        _hitFlash        = GetComponentInChildren<EnemyHitFlash>();

        if (_data == null)
            Debug.LogWarning($"[EnemyBase] EnemyData is not assigned on {gameObject.name}. Assign in the Inspector.");

        if (_attackModule == null)
            Debug.LogError($"[EnemyBase] No IAttackModule found on {gameObject.name}. " +
                           "Attach MeleeAttackModule or RangedAttackModule.");
    }

    private void Start()
    {
        // One-time Find at startup — cached immediately; never called again.
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
            Debug.LogWarning($"[EnemyBase] No GameObject with tag 'Player' found in scene.");
        else
            PlayerTransform = playerObj.transform;

        _healthComponent.OnDied    += OnDeath;
        _healthComponent.OnDamaged += OnHurt;

        if (_data != null)
        {
            _agent.speed             = _data.MoveSpeed;
            _agent.stoppingDistance  = _data.StoppingDistance;
            _agent.radius            = _data.AgentRadius;
            _agent.height            = _data.AgentHeight;
        }

        _billboard?.Initialize(_data);
        SetState(EnemyState.Idle);
    }

    private void OnDestroy()
    {
        if (_healthComponent != null)
        {
            _healthComponent.OnDied    -= OnDeath;
            _healthComponent.OnDamaged -= OnHurt;
        }
    }

    private void Update()
    {
        if (!IsAlive) return;
        if (IsGrappled) return;
        TickStateMachine();
    }

    // ── Grapple API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="GrappledState"/> to pause the state machine while
    /// the enemy is hooked or being pulled.
    /// </summary>
    public void SetGrappled(bool grappled)
    {
        IsGrappled = grappled;
    }

    /// <summary>
    /// Forces the enemy into the Hurt state for <paramref name="duration"/> seconds,
    /// suppressing the normal 0.4s timer. Used by the grapple pull landing.
    /// </summary>
    public void Stun(float duration)
    {
        if (!IsAlive) return;
        IsGrappled = false;
        CurrentState = EnemyState.Hurt;
        _agent.isStopped = true;
        _hurtTimer = duration;
        _billboard?.SetAnimation(_data?.HurtSprites, loop: false);
        AudioManager.Instance?.PlaySfx(_data?.HurtSound);
    }

    // ── State Machine ─────────────────────────────────────────────────────────

    private void TickStateMachine()
    {
        if (CurrentState == EnemyState.Dead) return;

        if (PlayerTransform != null)
            _distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        // ── Timed-state resolution (handled before distance checks) ────────────

        if (CurrentState == EnemyState.Hurt)
        {
            _hurtTimer -= Time.deltaTime;
            if (_hurtTimer <= 0f)
                SetState(EnemyState.Chase);
            return;
        }

        if (CurrentState == EnemyState.Alert)
        {
            _alertTimer -= Time.deltaTime;
            if (_alertTimer <= 0f)
                SetState(EnemyState.Chase);
            return;
        }

        if (CurrentState == EnemyState.Attack)
        {
            _agent.isStopped = true;
            _attackModule?.Tick();

            // Re-evaluate: leave Attack if player moved outside AttackRange.
            if (PlayerTransform != null && _distanceToPlayer > _data.AttackRange)
                SetState(EnemyState.Chase);
            return;
        }

        // ── Distance-based transitions ─────────────────────────────────────────

        if (PlayerTransform != null && _data != null)
        {
            if (_distanceToPlayer <= _data.AttackRange)
            {
                SetState(EnemyState.Attack);
                return;
            }

            if (_distanceToPlayer <= _data.AggroRange)
            {
                if (CurrentState != EnemyState.Chase)
                    SetState(EnemyState.Alert);
                else
                    _agent.SetDestination(PlayerTransform.position);
                return;
            }

            if (CurrentState == EnemyState.Chase && _distanceToPlayer > _data.AggroRange * LEASH_MULTIPLIER)
            {
                SetState(EnemyState.Idle);
                return;
            }
        }

        // ── Default: remain Idle ───────────────────────────────────────────────
        if (CurrentState != EnemyState.Idle)
            SetState(EnemyState.Idle);
    }

    // ── State Entry ───────────────────────────────────────────────────────────

    private void SetState(EnemyState next)
    {
        if (CurrentState == next) return;
        CurrentState = next;

        switch (next)
        {
            case EnemyState.Idle:
                _agent.isStopped = true;
                _agent.ResetPath();
                _billboard?.SetAnimation(_data?.IdleSprites);
                break;

            case EnemyState.Alert:
                _agent.isStopped = true;
                _alertTimer = ALERT_DWELL_TIME;
                _billboard?.SetAnimation(_data?.IdleSprites);
                AudioManager.Instance?.PlaySfx(_data.AggroSound);
                break;
            case EnemyState.Chase:
                _agent.isStopped = false;
                if (PlayerTransform != null)
                    _agent.SetDestination(PlayerTransform.position);
                _billboard?.SetAnimation(_data?.WalkSprites);
                break;

            case EnemyState.Attack:
                _agent.isStopped = true;
                _attackModule?.OnAttackEnter();
                _billboard?.SetAnimation(_data?.AttackSprites);
                break;

            case EnemyState.Hurt:
                _agent.isStopped = true;
                _hurtTimer = HURT_RECOVERY_TIME;
                _billboard?.SetAnimation(_data?.HurtSprites, loop: false);
                AudioManager.Instance?.PlaySfx(_data.HurtSound);
                break;

            case EnemyState.Dead:
                _agent.isStopped  = true;
                _agent.enabled    = false;
                _billboard?.SetAnimationOneShot(_data?.DeathSprites, OnDeathAnimationComplete);
                AudioManager.Instance?.PlaySfx(_data.DeathSound);
                break;
        }
    }

    // ── HealthComponent Callbacks ─────────────────────────────────────────────

    private void OnDeath()
    {
        SetState(EnemyState.Dead);

        EventBus<EnemyDiedEvent>.Raise(new EnemyDiedEvent
        {
            Enemy   = gameObject,
            EnemyId = _data != null ? _data.EnemyId : string.Empty
        });
    }

    private void OnHurt(DamageInfo info)
    {
        if (CurrentState == EnemyState.Dead || CurrentState == EnemyState.Hurt) return;

        _hitFlash?.Flash();

        EventBus<EnemyDamagedEvent>.Raise(new EnemyDamagedEvent
        {
            Enemy         = gameObject,
            Info          = info,
            CurrentHealth = _healthComponent.CurrentHealth
        });

        SetState(EnemyState.Hurt);
    }

    // ── Death Flow ────────────────────────────────────────────────────────────

    private void OnDeathAnimationComplete()
    {
        TryDropLoot();
        gameObject.SetActive(false); // Phase 6 EnemySpawner recycles via ObjectPool
    }

    private void TryDropLoot()
    {
        if (_data == null || _data.PossibleDrops == null || _data.PossibleDrops.Length == 0) return;
        if (Random.value > _data.DropChance) return;

        // TODO Phase 6: Replace Instantiate with ObjectPool<T>.Get()
        GameObject drop = _data.PossibleDrops[Random.Range(0, _data.PossibleDrops.Length)];
        if (drop != null)
            Instantiate(drop, transform.position, Quaternion.identity);
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;

        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _data.AggroRange);

        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _data.AttackRange);
    }
#endif
}
