using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

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
    private const float FallbackHitboxRadius = 0.5f;
    private const float FallbackHitboxHeight = 1.8f;
    private const float HitboxRadiusMultiplier = 1.75f;
    private const float HitboxHeightMultiplier = 1.35f;
    private const float MinimumHitboxRadius = 0.65f;
    private const float MinimumHitboxHeight = 2.4f;
    private const float NavMeshSpawnSampleDistance = 4f;
    private const float StuckRecoveryDelay = 1.25f;
    private const float StuckMoveThresholdSqr = 0.0004f;

    [SerializeField] private EnemyData _data;
    [SerializeField] private float _knockbackDecay = 18f;
    [SerializeField] private EnemyDeathBurst _deathBurst;
    [SerializeField] private bool _useModelAnimation = false;
    [SerializeField] private float _attackReselectInterval = 1.25f;
    [SerializeField] [Range(0f, 1f)] private float _chaseInsteadOfAttackChance = 0f;
    [SerializeField] private float _chaseInsteadOfAttackDuration = 1.25f;
    [SerializeField] private float _chaseInsteadOfAttackDecisionInterval = 1.75f;
    [SerializeField] private bool _deactivateAfterDeathAnimation = true;
    [SerializeField] private bool _freezeAnimatorAfterDeathAnimation = false;
    [SerializeField] private bool _staggerOnDamage = true;

    private const float AlertDwellTime = 0.3f;
    private const float LeashMultiplier = 1.5f;
    private const float HurtRecoveryTime = 0.4f;

    public EnemyState CurrentState { get; private set; }
    public IAttackModule CurrentAttack { get; private set; }
    public EnemyData Data => _data;
    public bool IsAlive => _healthComponent.IsAlive;
    public bool IsGrappled { get; private set; }
    public Transform PlayerTransform { get; private set; }

    private NavMeshAgent _agent;
    private HealthComponent _healthComponent;
    private IAttackModule[] _attackModules;
    private Animator _animator;
    private EnemySpriteBillboard _billboard;
    private EnemyHitFlash _hitFlash;
    private EnemyModelHitFlash _modelHitFlash;
    private Collider[] _deathCollisionColliders;
    private bool[] _deathCollisionInitialEnabled;

    private static readonly int AnimParamWalk = Animator.StringToHash("Walk");
    private const float ModelOneShotTransitionWait = 0.5f;

    private float _alertTimer;
    private float _hurtTimer;
    private float _distanceToPlayer;
    private float _attackReselectTimer;
    private float _attacksDisabledUntil;
    private float _chaseInsteadOfAttackUntil;
    private float _nextChaseInsteadOfAttackDecisionAt;
    private Vector3 _externalVelocity;
    private Vector3 _lastStuckCheckPosition;
    private float _stuckTimer;
    private bool _isShowingWalkAnimation;
    private int _lineOfSightMask;

    private void Awake()
    {
        EnsureEnemyIdentification();
        EnsureRootHitboxes();

        _agent = GetComponent<NavMeshAgent>();
        _healthComponent = GetComponent<HealthComponent>();
        _attackModules = GetComponents<IAttackModule>();
        _animator = GetComponentInChildren<Animator>();
        _billboard = GetComponentInChildren<EnemySpriteBillboard>();
        _hitFlash = GetComponentInChildren<EnemyHitFlash>();
        _modelHitFlash = GetComponentInChildren<EnemyModelHitFlash>();
        _lineOfSightMask = GetLineOfSightMask();

        if (_data == null)
            Debug.LogWarning($"[EnemyBase] EnemyData is not assigned on {gameObject.name}. Assign in the Inspector.");

        if (_attackModules == null)
        {
            Debug.LogError(
                $"[EnemyBase] No IAttackModule found on {gameObject.name}. ");
        }
    }

    private void OnEnable()
    {
        if (_agent == null || _healthComponent == null)
            return;

        ResetForSpawn();
    }

    private void Start()
    {
        RefreshAttackModules();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
            Debug.LogWarning("[EnemyBase] No GameObject with tag 'Player' found in scene.");
        else
            PlayerTransform = playerObj.transform;

        _healthComponent.OnDied += OnDeath;
        _healthComponent.OnDamaged += OnHurt;

        ConfigureAgentFromData();

        _billboard?.Initialize(_data);
        CacheDeathCollisionColliders();
        SetDeathCollisionEnabled(true);
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
        RecoverIfStuck();
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
        StopAgent();
        _hurtTimer = duration;
        _isShowingWalkAnimation = false;
        // _billboard?.SetSpriteAnimation(_data?.HurtSprites, loop: false);
        SetAnimation(_data?.HurtAnimTrigger, _data?.HurtSprites, loop: false);
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

    /// <summary>
    /// Drives the Walk bool on the model animator, or swaps the sprite animation on the billboard.
    /// Clearing Walk returns the model animator to its default Idle state.
    /// </summary>
    private void SetWalkAnimation(bool walking)
    {
        if (_useModelAnimation)
            _animator?.SetBool(AnimParamWalk, walking);
        else
            _billboard?.SetSpriteAnimation(walking ? _data?.WalkSprites : _data?.IdleSprites);
    }

    /// <summary>Fires a one-shot trigger on the model animator, or swaps the sprite animation on the billboard.</summary>
    public void SetAnimation(string animTrigger, Sprite[] frames, bool loop = true)
    {
        if (_useModelAnimation)
            TriggerModelAnimation(animTrigger);
        else
            _billboard?.SetSpriteAnimation(frames, loop);
    }

    public void SetAnimationOneShot(string animTrigger, Sprite[] frames, Action onComplete)
    {
        if (_useModelAnimation)
        {
            InterruptAttackTriggersForOneShot(animTrigger);
            TriggerModelAnimation(animTrigger);
            bool freezeAtEnd = _freezeAnimatorAfterDeathAnimation &&
                               !string.IsNullOrWhiteSpace(animTrigger) &&
                               animTrigger == _data?.DeathAnimTrigger;
            StartCoroutine(OnCompleteModelAnimationOneShot(onComplete, freezeAtEnd));
        }
        else
            _billboard?.SetSpriteAnimationOneShot(frames, onComplete);
    }

    public void PlayAttackAnimationOneShot()
    {
        if (_useModelAnimation)
        {
            if (_animator == null || CurrentAttack == null)
                return;

            TriggerModelAnimation(CurrentAttack.AttackAnimTrigger);
            StartCoroutine(OnCompleteModelAnimationOneShot(RestorePostAttackAnimation, freezeAtEnd: false));
        }
        else
        {
            if (_billboard == null || _data == null)
                return;

            _billboard.SetSpriteAnimationOneShot(_data.AttackSprites, RestorePostAttackAnimation);
        }
    }

    public void DisableAttacksFor(float duration)
    {
        _attacksDisabledUntil = Mathf.Max(_attacksDisabledUntil, Time.time + Mathf.Max(0f, duration));

        if (CurrentState == EnemyState.Attack)
            SetState(PlayerTransform != null ? EnemyState.Chase : EnemyState.Idle);
    }

    private IEnumerator OnCompleteModelAnimationOneShot(Action onComplete, bool freezeAtEnd)
    {
        if (_animator != null)
        {
            yield return new WaitForEndOfFrame();

            float transitionWait = 0f;
            while (_animator.IsInTransition(0) && transitionWait < ModelOneShotTransitionWait)
            {
                transitionWait += Time.deltaTime;
                yield return null;
            }

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (freezeAtEnd)
            {
                int stateHash = stateInfo.fullPathHash;
                float elapsed = 0f;
                float maxWait = Mathf.Max(0.15f, stateInfo.length * 1.25f);

                while (_animator != null && elapsed < maxWait)
                {
                    if (_animator.IsInTransition(0))
                        break;

                    stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.fullPathHash != stateHash || stateInfo.normalizedTime >= 0.98f)
                        break;

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (_animator != null)
                    _animator.speed = 0f;
            }
            else
            {
                float duration = Mathf.Max(0.15f, stateInfo.length);
                yield return new WaitForSeconds(duration);
            }
        }

        onComplete?.Invoke();
    }

    private void TriggerModelAnimation(string animTrigger)
    {
        if (_animator == null || string.IsNullOrWhiteSpace(animTrigger))
            return;

        _animator.SetTrigger(animTrigger);
    }

    private void InterruptAttackTriggersForOneShot(string animTrigger)
    {
        if (_animator == null)
            return;

        _animator.SetBool(AnimParamWalk, false);

        if (_attackModules != null)
        {
            for (int i = 0; i < _attackModules.Length; i++)
            {
                string attackTrigger = _attackModules[i]?.AttackAnimTrigger;
                if (!string.IsNullOrWhiteSpace(attackTrigger) && attackTrigger != animTrigger)
                    _animator.ResetTrigger(attackTrigger);
            }
        }

        if (!string.IsNullOrWhiteSpace(_data?.HurtAnimTrigger) && _data.HurtAnimTrigger != animTrigger)
            _animator.ResetTrigger(_data.HurtAnimTrigger);
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
            StopAgent();

            if (IsCurrentAttackExecuting())
            {
                CurrentAttack?.Tick();
                return;
            }

            if (!canAttackPlayer || ShouldChaseInsteadOfAttack())
            {
                SetState(shouldPursuePlayer ? EnemyState.Chase : EnemyState.Idle);
                return;
            }

            CurrentAttack?.Tick();
            TryReselectAttack();

            return;
        }

        if (PlayerTransform != null && _data != null)
        {
            if (canAttackPlayer)
            {
                if (ShouldChaseInsteadOfAttack())
                {
                    if (CurrentState != EnemyState.Chase)
                        SetState(EnemyState.Chase);
                    else
                        SetChaseDestination(PlayerTransform.position);

                    return;
                }

                SetState(EnemyState.Attack);
                return;
            }

            if (canDetectPlayer || (CurrentState == EnemyState.Chase && shouldPursuePlayer))
            {
                if (CurrentState != EnemyState.Chase)
                    SetState(EnemyState.Alert);
                else
                    SetChaseDestination(PlayerTransform.position);

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
        if (PlayerTransform == null || _data == null)
            return false;

        if (IsCurrentAttackExecuting())
            return true;

        if (Time.time < _attacksDisabledUntil)
            return false;

        if (!IsAttackUsable(CurrentAttack))
        {
            IAttackModule[] inRangeAttacks = _attackModules.Where(IsAttackUsable).ToArray();
            CurrentAttack = inRangeAttacks.Length > 0 ? inRangeAttacks[Random.Range(0, inRangeAttacks.Length)] : null;

            return false;
        }

        if (_data.AggroDetectionMode == EnemyAggroDetectionMode.LineOfSight)
            return canDetectPlayer;

        return shouldPursuePlayer;
    }

    private bool IsAttackUsable(IAttackModule module)
    {
        return module != null &&
               _distanceToPlayer > module.MinAttackRange &&
               _distanceToPlayer <= module.MaxAttackRange &&
               (!(module is IConditionalAttackModule conditional) || conditional.CanStartAttack);
    }

    private bool IsCurrentAttackExecuting()
    {
        return CurrentAttack is IAttackExecutionStatus executionStatus && executionStatus.IsExecuting;
    }

    private bool ShouldChaseInsteadOfAttack()
    {
        if (PlayerTransform == null || _data == null)
            return false;

        if (Time.time < _chaseInsteadOfAttackUntil)
            return true;

        if (_chaseInsteadOfAttackChance <= 0f || _chaseInsteadOfAttackDuration <= 0f)
            return false;

        if (Time.time < _nextChaseInsteadOfAttackDecisionAt)
            return false;

        _nextChaseInsteadOfAttackDecisionAt = Time.time + Mathf.Max(0.1f, _chaseInsteadOfAttackDecisionInterval);
        if (Random.value > _chaseInsteadOfAttackChance)
            return false;

        _chaseInsteadOfAttackUntil = Time.time + Mathf.Max(0.1f, _chaseInsteadOfAttackDuration);
        return true;
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
                StopAgent(resetPath: true);
                _isShowingWalkAnimation = false;
                SetWalkAnimation(false);
                break;

            case EnemyState.Alert:
                StopAgent();
                _alertTimer = AlertDwellTime;
                _isShowingWalkAnimation = false;
                SetWalkAnimation(false);
                AudioManager.Instance?.PlaySfx(_data != null ? _data.GetAggroClip() : null, _data != null ? _data.AggroVolume : 1f);
                break;

            case EnemyState.Chase:
                if (PlayerTransform != null)
                    SetChaseDestination(PlayerTransform.position);
                break;

            case EnemyState.Attack:
                StopAgent();
                CurrentAttack.OnAttackEnter();
                _attackReselectTimer = Mathf.Max(0.1f, _attackReselectInterval);
                _isShowingWalkAnimation = false;
                if (CurrentAttack is IManualAttackAnimationModule)
                    SetWalkAnimation(false);
                else
                    // _billboard?.SetSpriteAnimation(_data?.AttackSprites);
                    SetAnimation(CurrentAttack?.AttackAnimTrigger, _data?.AttackSprites);
                break;

            case EnemyState.Hurt:
                StopAgent();
                _hurtTimer = HurtRecoveryTime;
                _isShowingWalkAnimation = false;
                // _billboard?.SetSpriteAnimation(_data?.HurtSprites, loop: false);
                SetAnimation(_data?.HurtAnimTrigger, _data?.HurtSprites, loop: false);
                AudioManager.Instance?.PlaySfx(_data != null ? _data.GetHurtClip() : null, _data != null ? _data.HurtVolume : 1f);
                break;

            case EnemyState.Dead:
                StopAgent();
                _agent.enabled = false;
                SetDeathCollisionEnabled(false);
                _isShowingWalkAnimation = false;
                // _billboard?.SetSpriteAnimationOneShot(_data?.DeathSprites, OnDeathAnimationComplete);
                SetAnimationOneShot(_data?.DeathAnimTrigger, _data?.DeathSprites, OnDeathAnimationComplete);
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

        if (_hitFlash != null)
        {
            _hitFlash.Flash();
        }
        else
        {
            if (_modelHitFlash == null)
                _modelHitFlash = GetComponentInChildren<EnemyModelHitFlash>();

            _modelHitFlash?.Flash();
        }

        EventBus<EnemyDamagedEvent>.Raise(new EnemyDamagedEvent
        {
            Enemy = gameObject,
            Info = info,
            CurrentHealth = _healthComponent.CurrentHealth
        });

        if (_staggerOnDamage && CurrentState != EnemyState.Hurt)
            SetState(EnemyState.Hurt);
    }

    private void OnDeathAnimationComplete()
    {
        EventBus<EnemyDeathAnimationCompletedEvent>.Raise(new EnemyDeathAnimationCompletedEvent
        {
            Enemy = gameObject,
            EnemyId = _data != null ? _data.EnemyId : string.Empty
        });

        TryDropLoot();

        if (_freezeAnimatorAfterDeathAnimation && _animator != null)
            _animator.speed = 0f;

        if (_deactivateAfterDeathAnimation)
            gameObject.SetActive(false);
    }

    private void ResetForSpawn()
    {
        IsGrappled = false;
        _alertTimer = 0f;
        _hurtTimer = 0f;
        _stuckTimer = 0f;
        _isShowingWalkAnimation = false;
        _externalVelocity = Vector3.zero;
        _lastStuckCheckPosition = transform.position;
        if (_animator != null)
            _animator.speed = 1f;

        RemoveGrappledStates();
        _healthComponent.ResetHealth();
        ConfigureAgentFromData();

        TryPlaceAgentOnNavMesh(NavMeshSpawnSampleDistance);
        StopAgent(resetPath: true);
        SetDeathCollisionEnabled(true);

        CurrentState = EnemyState.Idle;
        CurrentAttack = _attackModules[Random.Range(0, _attackModules.Length)];
        _attackReselectTimer = Mathf.Max(0.1f, _attackReselectInterval);
        _attacksDisabledUntil = 0f;
        _chaseInsteadOfAttackUntil = 0f;
        _nextChaseInsteadOfAttackDecisionAt = 0f;
        SetWalkAnimation(false);
    }

    private void RefreshAttackModules()
    {
        _attackModules = GetComponents<IAttackModule>();
        if (_attackModules == null || _attackModules.Length == 0)
        {
            CurrentAttack = null;
            return;
        }

        bool currentStillAvailable = false;
        for (int i = 0; i < _attackModules.Length; i++)
        {
            if (_attackModules[i] == CurrentAttack)
            {
                currentStillAvailable = true;
                break;
            }
        }

        if (!currentStillAvailable)
            CurrentAttack = _attackModules[Random.Range(0, _attackModules.Length)];
    }

    private void TryReselectAttack()
    {
        if (_attackModules == null || _attackModules.Length <= 1)
            return;

        if (IsCurrentAttackExecuting())
            return;

        _attackReselectTimer -= Time.deltaTime;
        if (_attackReselectTimer > 0f)
            return;

        _attackReselectTimer = Mathf.Max(0.1f, _attackReselectInterval);

        IAttackModule[] alternatives = _attackModules
            .Where(module =>
                module != null &&
                module != CurrentAttack &&
                IsAttackUsable(module))
            .ToArray();

        if (alternatives.Length == 0)
            return;

        CurrentAttack = alternatives[Random.Range(0, alternatives.Length)];
        CurrentAttack.OnAttackEnter();
    }

    private void RestorePostAttackAnimation()
    {
        if (CurrentState != EnemyState.Attack || !IsAlive)
            return;

        _isShowingWalkAnimation = false;
        SetWalkAnimation(false);
    }

    private void ConfigureAgentFromData()
    {
        if (_agent == null || _data == null)
            return;

        _agent.speed = _data.MoveSpeed;
        _agent.stoppingDistance = _data.StoppingDistance;
        _agent.radius = _data.AgentRadius;
        _agent.height = _data.AgentHeight;
    }

    private void RemoveGrappledStates()
    {
        GrappledState[] grappledStates = GetComponents<GrappledState>();
        for (int i = 0; i < grappledStates.Length; i++)
        {
            if (grappledStates[i] != null)
                Destroy(grappledStates[i]);
        }
    }

    private void StopAgent(bool resetPath = false)
    {
        if (_agent == null || !_agent.enabled)
            return;

        if (!_agent.isOnNavMesh)
            return;

        _agent.isStopped = true;

        if (resetPath && _agent.isOnNavMesh)
            _agent.ResetPath();
    }

    private bool TrySetAgentDestination(Vector3 destination)
    {
        if (_agent == null)
            return false;

        if ((!_agent.enabled || !_agent.isOnNavMesh) && !TryPlaceAgentOnNavMesh(NavMeshSpawnSampleDistance))
            return false;

        _agent.isStopped = false;
        return _agent.SetDestination(destination);
    }

    private void SetChaseDestination(Vector3 destination)
    {
        if (TrySetAgentDestination(destination))
        {
            if (!_isShowingWalkAnimation)
            {
                _isShowingWalkAnimation = true;
                SetWalkAnimation(true);
            }
        }
        else
        {
            if (_isShowingWalkAnimation)
            {
                _isShowingWalkAnimation = false;
                SetWalkAnimation(false);
            }
        }
    }

    private bool TryPlaceAgentOnNavMesh(float sampleDistance)
    {
        if (_agent == null)
            return false;

        if (_agent.enabled && _agent.isOnNavMesh)
            return true;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            return false;

        if (_agent.enabled)
            _agent.enabled = false;

        transform.position = hit.position;
        _agent.enabled = true;

        return _agent.enabled && _agent.isOnNavMesh;
    }

    private void RecoverIfStuck()
    {
        if (CurrentState != EnemyState.Chase || PlayerTransform == null || _agent == null || !_agent.enabled)
        {
            _stuckTimer = 0f;
            _lastStuckCheckPosition = transform.position;
            return;
        }

        if (!_agent.isOnNavMesh)
        {
            TryPlaceAgentOnNavMesh(NavMeshSpawnSampleDistance);
            _stuckTimer = 0f;
            _lastStuckCheckPosition = transform.position;
            return;
        }

        if (_agent.pathPending || _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
        {
            _stuckTimer = 0f;
            _lastStuckCheckPosition = transform.position;
            return;
        }

        float movedSqr = (transform.position - _lastStuckCheckPosition).sqrMagnitude;
        if (movedSqr > StuckMoveThresholdSqr || _agent.velocity.sqrMagnitude > 0.01f)
        {
            _stuckTimer = 0f;
            _lastStuckCheckPosition = transform.position;
            return;
        }

        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < StuckRecoveryDelay)
            return;

        TryPlaceAgentOnNavMesh(NavMeshSpawnSampleDistance);
        TrySetAgentDestination(PlayerTransform.position);
        _stuckTimer = 0f;
        _lastStuckCheckPosition = transform.position;
    }

    private void ApplyExternalVelocity()
    {
        if (_externalVelocity.sqrMagnitude <= 0.0001f)
        {
            _externalVelocity = Vector3.zero;
            return;
        }

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.Move(_externalVelocity * Time.deltaTime);

        float decay = _knockbackDecay > 0f ? _knockbackDecay : 18f;
        _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, decay * Time.deltaTime);
    }

    private void CacheDeathCollisionColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        int collisionColliderCount = 0;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && !collider.isTrigger)
                collisionColliderCount++;
        }

        _deathCollisionColliders = new Collider[collisionColliderCount];
        _deathCollisionInitialEnabled = new bool[collisionColliderCount];

        int index = 0;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            _deathCollisionColliders[index] = collider;
            _deathCollisionInitialEnabled[index] = collider.enabled;
            index++;
        }
    }

    private void SetDeathCollisionEnabled(bool enabled)
    {
        if (_deathCollisionColliders == null)
            CacheDeathCollisionColliders();

        for (int i = 0; i < _deathCollisionColliders.Length; i++)
        {
            Collider collider = _deathCollisionColliders[i];
            if (collider == null)
                continue;

            collider.enabled = enabled && _deathCollisionInitialEnabled[i];
        }
    }

    private void TryDropLoot()
    {
        if (_data == null || _data.PossibleDrops == null || _data.PossibleDrops.Length == 0)
            return;

        float dropChance = _data.DropChance;
        if (RunUpgradeManager.Current != null)
            dropChance += RunUpgradeManager.Current.GetPickupDropChanceBonus();

        if (Random.value > Mathf.Clamp01(dropChance))
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
        float hitboxRadius = _data != null && _data.AgentRadius > 0f
            ? Mathf.Max(MinimumHitboxRadius, _data.AgentRadius * HitboxRadiusMultiplier)
            : FallbackHitboxRadius;
        float hitboxHeight = _data != null && _data.AgentHeight > 0f
            ? Mathf.Max(MinimumHitboxHeight, _data.AgentHeight * HitboxHeightMultiplier)
            : FallbackHitboxHeight;
        Vector3 hitboxCenter = new(0f, hitboxHeight * 0.5f, 0f);

        for (int i = 0; i < rootCapsules.Length; i++)
        {
            CapsuleCollider capsule = rootCapsules[i];
            if (capsule != null && i > 0)
                capsule.enabled = false;
        }

        EnsureCapsuleCollider(rootCapsules, 0, hitboxRadius, hitboxHeight, hitboxCenter);
        EnsureRootBoxHitbox(hitboxRadius, hitboxHeight);
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

    private void EnsureRootBoxHitbox(float hitboxRadius, float hitboxHeight)
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
            boxCollider = gameObject.AddComponent<BoxCollider>();

        boxCollider.isTrigger = false;
        boxCollider.enabled = true;
        boxCollider.center = new Vector3(0f, hitboxHeight * 0.5f, 0f);
        boxCollider.size = new Vector3(hitboxRadius * 2f, hitboxHeight, hitboxRadius * 2f);
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
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, CurrentAttack.MaxAttackRange);
    }
#endif
}
