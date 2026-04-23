using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>High-level state of the player's movement each frame.</summary>
public enum MovementState { Idle, Walk, Sprint, Crouch, Jump, Fall, Landing, WallRun }

/// <summary>
/// Core FPS controller. Reads from <see cref="InputManager"/>, drives
/// <see cref="CharacterController"/>, owns <see cref="MovementState"/>,
/// and exposes <see cref="CurrentSpeedRatio"/> for bob components.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private const int MaxWallJumpsPerAirborneSequence = 2;

    [SerializeField] private PlayerStats _stats;
    [SerializeField] private Transform _cameraTransform;
    [Header("Audio")]
    [SerializeField] private AudioClip[] _jumpSounds;
    [SerializeField] private AudioClip[] _landingSounds;
    [SerializeField] private AudioClip[] _dashSounds;
    [Range(0f, 1f)] [SerializeField] private float _jumpSoundVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _landingSoundVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _dashSoundVolume = 1f;

    public MovementState CurrentState { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsDashing => _isDashing;
    public bool IsWallRunning => _isWallRunning;
    public float WallRunCameraTiltSign
    {
        get
        {
            if (!_isWallRunning || _wallNormal.sqrMagnitude <= 0.001f)
                return 0f;

            float side = Vector3.Dot(_wallNormal.normalized, transform.right);
            if (Mathf.Abs(side) <= 0.05f)
                return 0f;

            return -Mathf.Sign(side);
        }
    }
    public float CurrentSpeedRatio { get; private set; }
    public Vector3 Velocity { get; private set; }

    private CharacterController _characterController;

    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _isSprinting;
    private bool _jumpRequested;
    private bool _queuedAirJump;

    private float _verticalVelocity;
    private float _currentPitch;
    private bool _canMove;
    private bool _wasGrounded;
    private bool _isNoclip;
    private bool _wasSprinting;
    private bool _wasAirborne;
    private float _peakFallSpeed;
    private int _remainingAirJumps;
    private int _remainingWallJumps;
    private float _airJumpRedirectTimer;
    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;
    private Vector3 _bonusVelocity;
    private bool _isWallRunning;
    private float _wallRunTimer;
    private float _wallDetachTimer;
    private Vector3 _wallNormal;
    private Vector3 _wallRunDirection;

    private bool _inputBound;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (_stats == null)
            Debug.LogError("[PlayerMovement] _stats (PlayerStats) is not assigned. Assign in the Inspector.");

        _remainingAirJumps = GetMaxJumpCount() - 1;
        _remainingWallJumps = GetMaxWallJumpCount();
        IsGrounded = _characterController.isGrounded;
        _wasGrounded = IsGrounded;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EventBus<PauseChangedEvent>.Subscribe(OnPauseChanged);
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        EventBus<NoclipChangedEvent>.Subscribe(OnNoclipChanged);

        TryBindInput();
    }

    private void OnDestroy()
    {
        EventBus<PauseChangedEvent>.Unsubscribe(OnPauseChanged);
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        EventBus<NoclipChangedEvent>.Unsubscribe(OnNoclipChanged);

        if (InputManager.Instance != null && _inputBound)
            InputManager.Instance.Controls.Player.Jump.performed -= OnJumpPerformed;
    }

    private void TryBindInput()
    {
        if (_inputBound || InputManager.Instance == null)
            return;

        InputManager.Instance.Controls.Player.Jump.performed += OnJumpPerformed;
        _inputBound = true;
    }

    private void Update()
    {
        if (!_canMove)
            return;

        if (_isNoclip)
        {
            GatherInput();
            ApplyNoclipMovement();
            ApplyLook();
            return;
        }

        GatherInput();
        UpdateDash();
        UpdateWallRun();
        ApplyGravity();
        ApplyMovement();
        ApplyLook();
        UpdateState();
    }

    public void ApplyExplosionKnockback(Vector3 impulse)
    {
        if (_isNoclip || _stats == null)
            return;

        if (_isDashing)
        {
            _isDashing = false;
            _dashTimer = 0f;
        }

        StopWallRun(true);

        Vector3 horizontalImpulse = Vector3.ProjectOnPlane(impulse, Vector3.up);
        _bonusVelocity += horizontalImpulse;

        if (impulse.y > 0f)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity, impulse.y);
            IsGrounded = false;
            _wasGrounded = false;
        }
        else if (impulse.y < 0f)
        {
            _verticalVelocity += impulse.y;
        }
    }

    private void GatherInput()
    {
        if (InputManager.Instance == null)
            return;

        _moveInput = InputManager.Instance.Controls.Player.Move.ReadValue<Vector2>();
        _lookInput = InputManager.Instance.Controls.Player.Look.ReadValue<Vector2>();
        _isSprinting = InputManager.Instance.Controls.Player.Sprint.IsPressed();

        if (InputManager.Instance.Controls.Player.Crouch.WasPressedThisFrame())
            TryStartDash();
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (!_canMove || _isNoclip || _stats == null)
            return;

        if (_isWallRunning)
        {
            if (_remainingWallJumps <= 0)
                return;

            PerformWallJump();
            return;
        }

        if (IsGrounded)
        {
            _jumpRequested = true;
            _queuedAirJump = false;
            return;
        }

        if (_remainingAirJumps <= 0)
            return;

        _remainingAirJumps--;
        _jumpRequested = true;
        _queuedAirJump = true;
        _airJumpRedirectTimer = GetAirJumpRedirectDuration();
    }

    private void UpdateDash()
    {
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer = Mathf.Max(0f, _dashCooldownTimer - Time.deltaTime);

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
                _isDashing = false;
        }
    }

    private void UpdateWallRun()
    {
        if (_wallDetachTimer > 0f)
            _wallDetachTimer = Mathf.Max(0f, _wallDetachTimer - Time.deltaTime);

        if (_stats == null || _isNoclip || _isDashing || IsGrounded)
        {
            StopWallRun();
            return;
        }

        if (!CanAttemptWallRun())
        {
            StopWallRun();
            return;
        }

        if (!TryGetRunnableWall(out RaycastHit wallHit, out Vector3 runDirection))
        {
            StopWallRun();
            return;
        }

        if (!_isWallRunning)
            _wallRunTimer = 0f;

        _isWallRunning = true;
        _wallRunTimer += Time.deltaTime;
        _wallNormal = wallHit.normal;
        _wallRunDirection = runDirection;
    }

    private bool CanAttemptWallRun()
    {
        if (_wallDetachTimer > 0f)
            return false;

        if (_isWallRunning)
            return true;

        if (_moveInput.y < GetWallRunMinForwardInput())
            return false;

        float horizontalSpeed = GetHorizontalVelocity().magnitude;
        return horizontalSpeed >= GetWallRunMinHorizontalSpeed();
    }

    private bool TryGetRunnableWall(out RaycastHit wallHit, out Vector3 wallRunDirection)
    {
        wallHit = default;
        wallRunDirection = Vector3.zero;

        Vector3 origin = transform.TransformPoint(_characterController.center);
        float castRadius = Mathf.Max(0.05f, _characterController.radius * 0.9f);
        float castDistance = GetWallCheckDistance();

        bool hasLeftWall = Physics.SphereCast(
            origin,
            castRadius,
            -transform.right,
            out RaycastHit leftHit,
            castDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore) && IsWallSurfaceValid(leftHit.normal);

        bool hasRightWall = Physics.SphereCast(
            origin,
            castRadius,
            transform.right,
            out RaycastHit rightHit,
            castDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore) && IsWallSurfaceValid(rightHit.normal);

        if (!hasLeftWall && !hasRightWall)
            return false;

        Vector3 referenceDirection = GetWallRunReferenceDirection();

        if (hasLeftWall && hasRightWall)
            wallHit = leftHit.distance <= rightHit.distance ? leftHit : rightHit;
        else
            wallHit = hasLeftWall ? leftHit : rightHit;

        wallRunDirection = GetWallRunDirection(wallHit.normal, referenceDirection);
        return wallRunDirection.sqrMagnitude > 0.001f;
    }

    private static bool IsWallSurfaceValid(Vector3 wallNormal)
    {
        return Mathf.Abs(wallNormal.y) < 0.2f;
    }

    private Vector3 GetWallRunReferenceDirection()
    {
        Vector3 moveDirection = transform.TransformDirection(new Vector3(_moveInput.x, 0f, _moveInput.y));
        Vector3 horizontalMove = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
        if (horizontalMove.sqrMagnitude > 0.01f)
            return horizontalMove.normalized;

        Vector3 horizontalVelocity = GetHorizontalVelocity();
        if (horizontalVelocity.sqrMagnitude > 0.01f)
            return horizontalVelocity.normalized;

        return Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    private static Vector3 GetWallRunDirection(Vector3 wallNormal, Vector3 referenceDirection)
    {
        Vector3 alongWall = Vector3.Cross(Vector3.up, wallNormal);
        if (alongWall.sqrMagnitude <= 0.001f)
            return Vector3.zero;

        alongWall.Normalize();

        if (referenceDirection.sqrMagnitude <= 0.001f)
            return alongWall;

        if (Vector3.Dot(alongWall, referenceDirection) < 0f)
            alongWall = -alongWall;

        return alongWall;
    }

    private void ApplyNoclipMovement()
    {
        Vector3 moveDir = _cameraTransform.forward * _moveInput.y
                        + _cameraTransform.right * _moveInput.x;
        transform.Translate(moveDir * (_stats.WalkSpeed * 2f) * Time.deltaTime, Space.World);
    }

    private void ApplyMovement()
    {
        Vector3 worldMove = GetHorizontalMoveVelocity();
        worldMove.y = _verticalVelocity;

        if (_isWallRunning)
            worldMove += -_wallNormal * GetWallStickForce();

        _characterController.Move(worldMove * Time.deltaTime);

        IsGrounded = _characterController.isGrounded;
        Velocity = _characterController.velocity;

        if (IsGrounded)
        {
            _remainingAirJumps = GetMaxJumpCount() - 1;
            _remainingWallJumps = GetMaxWallJumpCount();
            _airJumpRedirectTimer = 0f;
            _bonusVelocity = Vector3.zero;
            StopWallRun();
        }

        if (!IsGrounded && _verticalVelocity < 0f)
            _peakFallSpeed = Mathf.Max(_peakFallSpeed, Mathf.Abs(_verticalVelocity));

        if (IsGrounded && _wasAirborne)
        {
            EventBus<PlayerLandedEvent>.Raise(new PlayerLandedEvent { FallSpeed = _peakFallSpeed });
            PlayLandingSound();
            _peakFallSpeed = 0f;
        }

        _wasAirborne = !IsGrounded;
        UpdateBonusVelocity();

        float horizontalSpeed = GetHorizontalVelocity().magnitude;
        CurrentSpeedRatio = Mathf.Clamp01(horizontalSpeed / (_stats.WalkSpeed * _stats.SprintMultiplier));
    }

    private Vector3 GetHorizontalMoveVelocity()
    {
        if (_stats == null)
            return Vector3.zero;

        if (_isDashing)
            return _dashDirection * _stats.DashSpeed;

        if (_isWallRunning)
            return GetWallRunMoveVelocity();

        float speed = _stats.WalkSpeed;
        if (_isSprinting)
            speed *= _stats.SprintMultiplier;

        Vector3 localMove = new Vector3(_moveInput.x, 0f, _moveInput.y) * speed;
        if (!IsGrounded)
            localMove *= GetCurrentAirControlFactor();

        Vector3 worldMove = transform.TransformDirection(localMove);
        if (!IsGrounded)
            worldMove += _bonusVelocity;

        return worldMove;
    }

    private Vector3 GetWallRunMoveVelocity()
    {
        float runBlend = Mathf.Clamp01(1f - (_wallRunTimer / Mathf.Max(0.01f, GetWallRunDuration())));
        float slideSpeed = Mathf.Max(_stats.WalkSpeed * 0.55f, GetWallRunMinHorizontalSpeed() * 0.75f);
        float runSpeed = Mathf.Lerp(slideSpeed, GetWallRunSpeed(), runBlend);
        return _wallRunDirection * runSpeed;
    }

    private void UpdateBonusVelocity()
    {
        if (_bonusVelocity.sqrMagnitude <= 0.0001f)
        {
            _bonusVelocity = Vector3.zero;
            return;
        }

        float decay = GetWallJumpMomentumDecay();
        if (IsGrounded)
            decay *= 3f;

        _bonusVelocity = Vector3.MoveTowards(_bonusVelocity, Vector3.zero, decay * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (_isDashing)
        {
            _jumpRequested = false;
            _queuedAirJump = false;
            return;
        }

        if (_isWallRunning)
        {
            float maxRiseSpeed = GetWallSlideSpeed() * 0.35f;
            _verticalVelocity = Mathf.Min(_verticalVelocity, maxRiseSpeed);
            _verticalVelocity += Physics.gravity.y * _stats.GravityScale * GetWallRunGravityFactor() * Time.deltaTime;

            float slideFloor = _wallRunTimer < GetWallSlideDelay()
                ? -GetWallSlideSpeed() * 0.15f
                : -GetWallSlideSpeed();

            _verticalVelocity = Mathf.Max(_verticalVelocity, slideFloor);
            _jumpRequested = false;
            _queuedAirJump = false;
            return;
        }

        if (IsGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        if (_jumpRequested && (IsGrounded || _queuedAirJump))
        {
            _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * _stats.GravityScale * _stats.JumpHeight);
            PlayJumpSound();
            _jumpRequested = false;
            _queuedAirJump = false;
            return;
        }

        if (!IsGrounded)
            _verticalVelocity += Physics.gravity.y * _stats.GravityScale * Time.deltaTime;

        _jumpRequested = false;
        _queuedAirJump = false;
    }

    private void PerformWallJump()
    {
        _remainingWallJumps--;

        Vector3 wallNormal = _wallNormal.sqrMagnitude > 0.001f
            ? _wallNormal.normalized
            : -transform.forward;

        Vector3 wallRunDirection = _wallRunDirection.sqrMagnitude > 0.001f
            ? _wallRunDirection.normalized
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        StopWallRun(true);

        _verticalVelocity = Mathf.Sqrt(
            2f * Mathf.Abs(Physics.gravity.y) * _stats.GravityScale * _stats.JumpHeight * GetWallJumpHeightMultiplier());

        _bonusVelocity = wallNormal * GetWallJumpAwaySpeed()
                       + wallRunDirection * GetWallJumpForwardSpeed();
        _airJumpRedirectTimer = GetAirJumpRedirectDuration();
        _jumpRequested = false;
        _queuedAirJump = false;

        PlayJumpSound();
    }

    private void StopWallRun(bool applyDetachCooldown = false)
    {
        if (applyDetachCooldown)
            _wallDetachTimer = Mathf.Max(_wallDetachTimer, GetWallReattachCooldown());

        _isWallRunning = false;
        _wallRunTimer = 0f;
        _wallNormal = Vector3.zero;
        _wallRunDirection = Vector3.zero;
    }

    private void TryStartDash()
    {
        if (!_canMove || _isNoclip || _stats == null)
            return;

        if (_isDashing || _dashCooldownTimer > 0f)
            return;

        if (!TryGetDashDirection(out _dashDirection))
            return;

        StopWallRun(true);
        _isDashing = true;
        _dashTimer = _stats.DashDuration;
        _dashCooldownTimer = _stats.DashCooldown;
        PlayDashSound();

        EventBus<PlayerDashedEvent>.Raise(new PlayerDashedEvent
        {
            Direction = _dashDirection,
            Duration = _stats.DashDuration,
            Speed = _stats.DashSpeed
        });
    }

    private bool TryGetDashDirection(out Vector3 dashDirection)
    {
        Vector3 localMove = new Vector3(_moveInput.x, 0f, _moveInput.y);
        if (localMove.sqrMagnitude > 0.01f)
        {
            dashDirection = transform.TransformDirection(localMove.normalized);
            return true;
        }

        Vector3 horizontalVelocity = GetHorizontalVelocity();
        if (horizontalVelocity.sqrMagnitude > 0.01f)
        {
            dashDirection = horizontalVelocity.normalized;
            return true;
        }

        dashDirection = Vector3.zero;
        return false;
    }

    private Vector3 GetHorizontalVelocity()
    {
        return Vector3.ProjectOnPlane(Velocity, Vector3.up);
    }

    private float GetCurrentAirControlFactor()
    {
        float airControlFactor = _stats.AirControlFactor;

        if (_airJumpRedirectTimer <= 0f)
            return airControlFactor;

        _airJumpRedirectTimer = Mathf.Max(0f, _airJumpRedirectTimer - Time.deltaTime);
        return Mathf.Max(airControlFactor, GetAirJumpRedirectControlFactor());
    }

    private int GetMaxJumpCount()
    {
        if (_stats == null)
            return 2;

        return Mathf.Max(1, _stats.MaxJumpCount == 0 ? 2 : _stats.MaxJumpCount);
    }

    private int GetMaxWallJumpCount()
    {
        return MaxWallJumpsPerAirborneSequence;
    }

    private float GetAirJumpRedirectControlFactor()
    {
        if (_stats == null)
            return 1f;

        return _stats.AirJumpRedirectControlFactor <= 0f ? 1f : _stats.AirJumpRedirectControlFactor;
    }

    private float GetAirJumpRedirectDuration()
    {
        if (_stats == null)
            return 0.2f;

        return _stats.AirJumpRedirectDuration <= 0f ? 0.2f : _stats.AirJumpRedirectDuration;
    }

    private float GetWallRunSpeed()
    {
        if (_stats == null)
            return 12f;

        return _stats.WallRunSpeed <= 0f ? Mathf.Max(_stats.WalkSpeed, _stats.WalkSpeed * _stats.SprintMultiplier) : _stats.WallRunSpeed;
    }

    private float GetWallRunDuration()
    {
        if (_stats == null)
            return 0.65f;

        return _stats.WallRunDuration <= 0f ? 0.65f : _stats.WallRunDuration;
    }

    private float GetWallSlideDelay()
    {
        if (_stats == null)
            return 0.18f;

        return _stats.WallSlideDelay <= 0f ? 0.18f : _stats.WallSlideDelay;
    }

    private float GetWallSlideSpeed()
    {
        if (_stats == null)
            return 6f;

        return _stats.WallSlideSpeed <= 0f ? 6f : _stats.WallSlideSpeed;
    }

    private float GetWallRunGravityFactor()
    {
        if (_stats == null)
            return 0.3f;

        return _stats.WallRunGravityFactor <= 0f ? 0.3f : _stats.WallRunGravityFactor;
    }

    private float GetWallStickForce()
    {
        if (_stats == null)
            return 6f;

        return _stats.WallStickForce <= 0f ? 6f : _stats.WallStickForce;
    }

    private float GetWallCheckDistance()
    {
        if (_stats == null)
            return 0.8f;

        return _stats.WallCheckDistance <= 0f ? 0.8f : _stats.WallCheckDistance;
    }

    private float GetWallRunMinForwardInput()
    {
        if (_stats == null)
            return 0.1f;

        return _stats.WallRunMinForwardInput <= 0f ? 0.1f : _stats.WallRunMinForwardInput;
    }

    private float GetWallRunMinHorizontalSpeed()
    {
        if (_stats == null)
            return 6f;

        return _stats.WallRunMinHorizontalSpeed <= 0f ? Mathf.Max(4f, _stats.WalkSpeed * 0.6f) : _stats.WallRunMinHorizontalSpeed;
    }

    private float GetWallJumpAwaySpeed()
    {
        if (_stats == null)
            return 11f;

        return _stats.WallJumpAwaySpeed <= 0f ? 11f : _stats.WallJumpAwaySpeed;
    }

    private float GetWallJumpForwardSpeed()
    {
        if (_stats == null)
            return 7f;

        return _stats.WallJumpForwardSpeed <= 0f ? 7f : _stats.WallJumpForwardSpeed;
    }

    private float GetWallJumpHeightMultiplier()
    {
        if (_stats == null)
            return 1.05f;

        return _stats.WallJumpHeightMultiplier <= 0f ? 1.05f : _stats.WallJumpHeightMultiplier;
    }

    private float GetWallJumpMomentumDecay()
    {
        if (_stats == null)
            return 12f;

        return _stats.WallJumpMomentumDecay <= 0f ? 12f : _stats.WallJumpMomentumDecay;
    }

    private float GetWallReattachCooldown()
    {
        if (_stats == null)
            return 0.2f;

        return _stats.WallReattachCooldown <= 0f ? 0.2f : _stats.WallReattachCooldown;
    }

    private void PlayJumpSound()
    {
        AudioManager.Instance?.PlaySfx(_jumpSounds, _jumpSoundVolume);
    }

    private void PlayLandingSound()
    {
        AudioManager.Instance?.PlaySfx(_landingSounds, _landingSoundVolume);
    }

    private void PlayDashSound()
    {
        AudioManager.Instance?.PlaySfx(_dashSounds, _dashSoundVolume);
    }

    private void ApplyLook()
    {
        float sensitivity = _stats.BaseSensitivity
                          * SaveManager.Instance.CurrentSettings.MouseSensitivity;

        transform.Rotate(Vector3.up, _lookInput.x * sensitivity);

        _currentPitch -= _lookInput.y * sensitivity;
        _currentPitch = Mathf.Clamp(_currentPitch, -_stats.MaxLookAngle, _stats.MaxLookAngle);
        _cameraTransform.localEulerAngles = new Vector3(_currentPitch, 0f, 0f);
    }

    private void UpdateState()
    {
        bool isMoving = _moveInput.sqrMagnitude > 0.01f;
        bool isSprinting = _isSprinting && isMoving && IsGrounded;

        if (_isWallRunning)
        {
            CurrentState = MovementState.WallRun;
        }
        else if (!IsGrounded)
        {
            CurrentState = _verticalVelocity > 0f ? MovementState.Jump : MovementState.Fall;
        }
        else if (_isDashing)
        {
            CurrentState = MovementState.Sprint;
        }
        else if (!_wasGrounded)
        {
            CurrentState = MovementState.Landing;
        }
        else if (isSprinting)
        {
            CurrentState = MovementState.Sprint;
        }
        else if (isMoving)
        {
            CurrentState = MovementState.Walk;
        }
        else
        {
            CurrentState = MovementState.Idle;
        }

        if (isSprinting != _wasSprinting)
        {
            EventBus<PlayerSprintChangedEvent>.Raise(new PlayerSprintChangedEvent { IsSprinting = isSprinting });
            _wasSprinting = isSprinting;
        }

        _wasGrounded = IsGrounded;
    }

    private void OnPauseChanged(PauseChangedEvent e)
    {
        _canMove = !e.IsPaused;
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        _canMove = e.NewState == GameState.Playing;
        TryBindInput();
    }

    private void OnNoclipChanged(NoclipChangedEvent e)
    {
        _isNoclip = e.IsActive;
        _characterController.enabled = !e.IsActive;

        if (e.IsActive)
        {
            _verticalVelocity = 0f;
            _bonusVelocity = Vector3.zero;
            _isDashing = false;
            _dashTimer = 0f;
            _dashCooldownTimer = 0f;
            StopWallRun();
        }
    }
}
