using UnityEngine;
using UnityEngine.AI;

/// <summary>All possible states in the enemy state machine.</summary>
public enum EnemyState { Idle, Alert, Chase, Attack, Hurt, Dead }

/// <summary>
/// Core enemy MonoBehaviour. Owns the 6-state state machine, <see cref="NavMeshAgent"/>
/// movement, <see cref="HealthComponent"/> integration, aggro detection, and death flow.
/// Attack behaviour is composed via <see cref="IAttackModule"/> - attach either
/// <see cref="MeleeAttackModule"/> or <see cref="RangedAttackModule"/> to the same GameObject.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthComponent))]
public class EnemyBase : MonoBehaviour
{
    private const string EnemyLayerName = "Enemy";
    private const string EnemyTagName = "Enemy";
    private const float DefaultPrimaryHitboxRadius = 0.9f;
    private const float DefaultPrimaryHitboxHeight = 3.14f;
    private static readonly Vector3 DefaultPrimaryHitboxCenter = new Vector3(0f, 0.9f, 0f);
    private const float DefaultSecondaryHitboxRadius = 0.97f;
    private const float DefaultSecondaryHitboxHeight = 3.65f;
    private static readonly Vector3 DefaultSecondaryHitboxCenter = new Vector3(0f, 0.8f, 0f);
    private static readonly Vector3 DefaultBoxHitboxCenter = new Vector3(0f, 0.95f, 0f);
    private static readonly Vector3 DefaultBoxHitboxSize = new Vector3(1.9f, 2.2f, 1.9f);

    [SerializeField] private EnemyData _data;
    [SerializeField] private float _knockbackDecay = 18f;
    [SerializeField] private EnemyDeathBurst _deathBurst;

    private const float AlertDwellTime = 0.3f;
    private const float LeashMultiplier = 1.5f;
    private const float HurtRecoveryTime = 0.4f;

    public EnemyState CurrentState { get; private set; }
    public EnemyData Data => _data;
    public bool IsAlive => _healthComponent.IsAlive;
    public bool IsGrappled { get; private set; }
    public Transform PlayerTransform { get; private set; }

    private NavMeshAgent _agent;
    private HealthComponent _healthComponent;
    private IAttackModule _attackModule;
    private EnemySpriteBillboard _billboard;
    private EnemyHitFlash _hitFlash;

    private float _alertTimer;
    private float _hurtTimer;
    private float _distanceToPlayer;
    private Vector3 _externalVelocity;
    private int _lineOfSightMask;

    private void Awake()
    {
        EnsureEnemyIdentification();
        EnsureRootHitboxes();

        _agent = GetComponent<NavMeshAgent>();
        _healthComponent = GetComponent<HealthComponent>();
        _attackModule = GetComponent<IAttackModule>();
        _billboard = GetComponentInChildren<EnemySpriteBillboard>();
        _hitFlash = GetComponentInChildren<EnemyHitFlash>();
        _lineOfSightMask = GetLineOfSightMask();

        if (_data == null)
            Debug.LogWarning($"[EnemyBase] EnemyData is not assigned on {gameObject.name}. Assign in the Inspector.");

        if (_attackModule == null)
        {
            Debug.LogError(
                $"[EnemyBase] No IAttackModule found on {gameObject.name}. " +
                "Attach MeleeAttackModule or RangedAttackModule.");
        }
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
            Debug.LogWarning("[EnemyBase] No GameObject with tag 'Player' found in scene.");
        else
            PlayerTransform = playerObj.transform;

        _healthComponent.OnDied += OnDeath;
        _healthComponent.OnDamaged += OnHurt;

        if (_data != null)
        {
            _agent.speed = _data.MoveSpeed;
            _agent.stoppingDistance = _data.StoppingDistance;
            _agent.radius = _data.AgentRadius;
            _agent.height = _data.AgentHeight;
        }

        _billboard?.Initialize(_data);
        SetState(EnemyState.Idle);
    }

    private void OnDestroy()
    {
        if (_healthComponent != null)
        {
            _healthComponent.OnDied -= OnDeath;
            _healthComponent.OnDamaged -= OnHurt;
        }
    }

    private void Update()
    {
        if (!IsAlive || IsGrappled)
            return;

        TickStateMachine();
        ApplyExternalVelocity();
    }

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
        if (!IsAlive)
            return;

        IsGrappled = false;
        CurrentState = EnemyState.Hurt;
        _agent.isStopped = true;
        _hurtTimer = duration;
        _billboard?.SetAnimation(_data?.HurtSprites, loop: false);
        AudioManager.Instance?.PlaySfx(_data != null ? _data.GetHurtClip() : null, _data != null ? _data.HurtVolume : 1f);
    }

    public void ApplyExplosionKnockback(Vector3 impulse)
    {
        if (!IsAlive)
            return;

        Vector3 horizontalImpulse = Vector3.ProjectOnPlane(impulse, Vector3.up);
        if (horizontalImpulse.sqrMagnitude <= 0.0001f)
            return;

        _externalVelocity += horizontalImpulse;
    }

    private void TickStateMachine()
    {
        if (CurrentState == EnemyState.Dead)
            return;

        if (PlayerTransform != null)
            _distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        bool canDetectPlayer = CanDetectPlayer();
        bool shouldPursuePlayer = ShouldPursuePlayer(canDetectPlayer);
        bool canAttackPlayer = CanAttackPlayer(canDetectPlayer, shouldPursuePlayer);

        if (CurrentState == EnemyState.Hurt)
        {
            _hurtTimer -= Time.deltaTime;
            if (_hurtTimer <= 0f)
                SetState(shouldPursuePlayer ? EnemyState.Chase : EnemyState.Idle);
            return;
        }

        if (CurrentState == EnemyState.Alert)
        {
            if (!canDetectPlayer)
            {
                SetState(EnemyState.Idle);
                return;
            }

            _alertTimer -= Time.deltaTime;
            if (_alertTimer <= 0f)
                SetState(EnemyState.Chase);
            return;
        }

        if (CurrentState == EnemyState.Attack)
        {
            _agent.isStopped = true;

            if (!canAttackPlayer)
            {
                SetState(shouldPursuePlayer ? EnemyState.Chase : EnemyState.Idle);
                return;
            }

            _attackModule?.Tick();

            return;
        }

        if (PlayerTransform != null && _data != null)
        {
            if (canAttackPlayer)
            {
                SetState(EnemyState.Attack);
                return;
            }

            if (canDetectPlayer || (CurrentState == EnemyState.Chase && shouldPursuePlayer))
            {
                if (CurrentState != EnemyState.Chase)
                    SetState(EnemyState.Alert);
                else
                    _agent.SetDestination(PlayerTransform.position);

                return;
            }
        }

        if (CurrentState != EnemyState.Idle)
            SetState(EnemyState.Idle);
    }

    private bool CanDetectPlayer()
    {
        if (PlayerTransform == null || _data == null || _distanceToPlayer > _data.AggroRange)
            return false;

        return _data.AggroDetectionMode != EnemyAggroDetectionMode.LineOfSight || HasLineOfSightToPlayer();
    }

    private bool ShouldPursuePlayer(bool canDetectPlayer)
    {
        if (canDetectPlayer || PlayerTransform == null || _data == null)
            return canDetectPlayer;

        bool isTrackingPlayer =
            CurrentState == EnemyState.Chase ||
            CurrentState == EnemyState.Attack ||
            CurrentState == EnemyState.Hurt;

        return isTrackingPlayer && _distanceToPlayer <= _data.AggroRange * LeashMultiplier;
    }

    private bool CanAttackPlayer(bool canDetectPlayer, bool shouldPursuePlayer)
    {
        if (PlayerTransform == null || _data == null || _distanceToPlayer > _data.AttackRange)
            return false;

        if (_data.AggroDetectionMode == EnemyAggroDetectionMode.LineOfSight)
            return canDetectPlayer;

        return shouldPursuePlayer;
    }

    private bool HasLineOfSightToPlayer()
    {
        Vector3 origin = GetLineOfSightOrigin();
        Vector3 target = GetLineOfSightTarget();
        Vector3 toTarget = target - origin;
        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget <= 0.001f)
            return true;

        if (Physics.Raycast(origin, toTarget / distanceToTarget, out RaycastHit hit, distanceToTarget, _lineOfSightMask, QueryTriggerInteraction.Ignore))
            return IsPlayerHit(hit.collider);

        return true;
    }

    private Vector3 GetLineOfSightOrigin()
    {
        float eyeHeight = _data != null
            ? Mathf.Max(0.9f, _data.AgentHeight * 0.5f)
            : 1f;

        return transform.position + Vector3.up * eyeHeight;
    }

    private Vector3 GetLineOfSightTarget()
    {
        Collider playerCollider = PlayerTransform != null ? PlayerTransform.GetComponent<Collider>() : null;
        if (playerCollider != null)
            return playerCollider.bounds.center;

        return PlayerTransform.position + Vector3.up;
    }

    private bool IsPlayerHit(Collider collider)
    {
        if (collider == null || PlayerTransform == null)
            return false;

        return collider.transform == PlayerTransform || collider.transform.IsChildOf(PlayerTransform);
    }

    private static int GetLineOfSightMask()
    {
        int mask = Physics.DefaultRaycastLayers;
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);

        if (enemyLayer >= 0)
            mask &= ~(1 << enemyLayer);

        return mask;
    }

    private void SetState(EnemyState next)
    {
        if (CurrentState == next)
            return;

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
                _alertTimer = AlertDwellTime;
                _billboard?.SetAnimation(_data?.IdleSprites);
                AudioManager.Instance?.PlaySfx(_data != null ? _data.GetAggroClip() : null, _data != null ? _data.AggroVolume : 1f);
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
                _hurtTimer = HurtRecoveryTime;
                _billboard?.SetAnimation(_data?.HurtSprites, loop: false);
                AudioManager.Instance?.PlaySfx(_data != null ? _data.GetHurtClip() : null, _data != null ? _data.HurtVolume : 1f);
                break;

            case EnemyState.Dead:
                _agent.isStopped = true;
                _agent.enabled = false;
                _billboard?.SetAnimationOneShot(_data?.DeathSprites, OnDeathAnimationComplete);
                AudioManager.Instance?.PlaySfx(_data != null ? _data.GetDeathClip() : null, _data != null ? _data.DeathVolume : 1f);
                break;
        }
    }

    private void OnDeath()
    {
        SetState(EnemyState.Dead);

        EventBus<EnemyDiedEvent>.Raise(new EnemyDiedEvent
        {
            Enemy = gameObject,
            EnemyId = _data != null ? _data.EnemyId : string.Empty
        });

        _deathBurst?.Burst(transform.position);
    }

    private void OnHurt(DamageInfo info)
    {
        if (CurrentState == EnemyState.Dead)
            return;

        _hitFlash?.Flash();

        EventBus<EnemyDamagedEvent>.Raise(new EnemyDamagedEvent
        {
            Enemy = gameObject,
            Info = info,
            CurrentHealth = _healthComponent.CurrentHealth
        });

        if (CurrentState != EnemyState.Hurt)
            SetState(EnemyState.Hurt);
    }

    private void OnDeathAnimationComplete()
    {
        TryDropLoot();
        gameObject.SetActive(false);
    }

    private void ApplyExternalVelocity()
    {
        if (_externalVelocity.sqrMagnitude <= 0.0001f)
        {
            _externalVelocity = Vector3.zero;
            return;
        }

        if (_agent != null && _agent.enabled)
            _agent.Move(_externalVelocity * Time.deltaTime);

        float decay = _knockbackDecay > 0f ? _knockbackDecay : 18f;
        _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, decay * Time.deltaTime);
    }

    private void TryDropLoot()
    {
        if (_data == null || _data.PossibleDrops == null || _data.PossibleDrops.Length == 0)
            return;

        if (Random.value > _data.DropChance)
            return;

        GameObject dropPrefab = _data.PossibleDrops[Random.Range(0, _data.PossibleDrops.Length)];
        if (dropPrefab == null)
            return;

        GameObject dropInstance = Instantiate(dropPrefab, transform.position, Quaternion.identity);
        PickupDropMotion dropMotion = dropInstance.GetComponent<PickupDropMotion>();
        if (dropMotion != null)
            dropMotion.DropFrom(transform.position);
    }

    private void EnsureRootHitboxes()
    {
        CapsuleCollider[] rootCapsules = GetComponents<CapsuleCollider>();
        int activeRootCapsuleCount = 0;

        for (int i = 0; i < rootCapsules.Length; i++)
        {
            CapsuleCollider capsule = rootCapsules[i];
            if (capsule != null && capsule.enabled && !capsule.isTrigger)
                activeRootCapsuleCount++;
        }

        if (activeRootCapsuleCount >= 2)
        {
            EnsureRootBoxHitbox();
            return;
        }

        EnsureCapsuleCollider(rootCapsules, 0, DefaultPrimaryHitboxRadius, DefaultPrimaryHitboxHeight, DefaultPrimaryHitboxCenter);
        EnsureCapsuleCollider(rootCapsules, 1, DefaultSecondaryHitboxRadius, DefaultSecondaryHitboxHeight, DefaultSecondaryHitboxCenter);
        EnsureRootBoxHitbox();
    }

    private void EnsureCapsuleCollider(
        CapsuleCollider[] existingCapsules,
        int capsuleIndex,
        float radius,
        float height,
        Vector3 center)
    {
        CapsuleCollider capsule = existingCapsules != null && capsuleIndex < existingCapsules.Length
            ? existingCapsules[capsuleIndex]
            : null;

        if (capsule == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        capsule.isTrigger = false;
        capsule.enabled = true;
        capsule.direction = 1;
        capsule.radius = radius;
        capsule.height = height;
        capsule.center = center;
    }

    private void EnsureRootBoxHitbox()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
            boxCollider = gameObject.AddComponent<BoxCollider>();

        boxCollider.isTrigger = false;
        boxCollider.enabled = true;
        boxCollider.center = DefaultBoxHitboxCenter;
        boxCollider.size = DefaultBoxHitboxSize;
    }

    private void EnsureEnemyIdentification()
    {
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        if (enemyLayer >= 0)
            ApplyLayerRecursively(transform, enemyLayer);

        if (!CompareTag(EnemyTagName))
            gameObject.tag = EnemyTagName;
    }

    private static void ApplyLayerRecursively(Transform current, int layer)
    {
        if (current == null)
            return;

        current.gameObject.layer = layer;

        for (int i = 0; i < current.childCount; i++)
            ApplyLayerRecursively(current.GetChild(i), layer);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null)
            return;

        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _data.AggroRange);

        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _data.AttackRange);
    }
#endif
}
