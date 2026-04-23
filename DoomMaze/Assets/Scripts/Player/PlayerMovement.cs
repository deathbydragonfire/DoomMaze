using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>High-level state of the player's movement each frame.</summary>
public enum MovementState { Idle, Walk, Sprint, Crouch, Jump, Fall, Landing }

/// <summary>
/// Core FPS controller. Reads from <see cref="InputManager"/>, drives
/// <see cref="CharacterController"/>, owns <see cref="MovementState"/>,
/// and exposes <see cref="CurrentSpeedRatio"/> for bob components.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private PlayerStats _stats;
    [SerializeField] private Transform   _cameraTransform;
    [Header("Audio")]
    [SerializeField] private AudioClip[] _jumpSounds;
    [SerializeField] private AudioClip[] _landingSounds;
    [SerializeField] private AudioClip[] _dashSounds;
    [Range(0f, 1f)] [SerializeField] private float _jumpSoundVolume    = 1f;
    [Range(0f, 1f)] [SerializeField] private float _landingSoundVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _dashSoundVolume    = 1f;

    public MovementState CurrentState      { get; private set; }
    public bool          IsGrounded        { get; private set; }
    public bool          IsDashing         => _isDashing;
    public float         CurrentSpeedRatio { get; private set; }
    public Vector3       Velocity          { get; private set; }

    // ── Cached components ─────────────────────────────────────────────────────

    private CharacterController _characterController;

    // ── Input cache (pre-allocated, no per-frame allocs) ─────────────────────

    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool    _isSprinting;
    private bool    _jumpRequested;
    private bool    _queuedAirJump;

    // ── State ─────────────────────────────────────────────────────────────────

    private float _verticalVelocity;
    private float _currentPitch;
    private bool  _canMove = false;
    private bool  _wasGrounded;
    private bool  _isNoclip;
    private bool  _wasSprinting;
    private bool  _wasAirborne;
    private float _peakFallSpeed;
    private int   _remainingAirJumps;
    private float _airJumpRedirectTimer;
    private bool    _isDashing;
    private float   _dashTimer;
    private float   _dashCooldownTimer;
    private Vector3 _dashDirection;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (_stats == null)
            Debug.LogError("[PlayerMovement] _stats (PlayerStats) is not assigned. Assign in the Inspector.");

        _remainingAirJumps = GetMaxJumpCount() - 1;
    }

    private bool _inputBound;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

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
        {
            InputManager.Instance.Controls.Player.Jump.performed -= OnJumpPerformed;
        }
    }

    private void TryBindInput()
    {
        if (_inputBound || InputManager.Instance == null) return;
        InputManager.Instance.Controls.Player.Jump.performed += OnJumpPerformed;
        _inputBound = true;
    }

    private void Update()
    {
        if (!_canMove) return;

        if (_isNoclip)
        {
            GatherInput();
            ApplyNoclipMovement();
            ApplyLook();
            return;
        }

        GatherInput();
        UpdateDash();
        ApplyGravity();
        ApplyMovement();
        ApplyLook();
        UpdateState();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void GatherInput()
    {
        if (InputManager.Instance == null) return;
        _moveInput   = InputManager.Instance.Controls.Player.Move.ReadValue<Vector2>();
        _lookInput   = InputManager.Instance.Controls.Player.Look.ReadValue<Vector2>();
        _isSprinting = InputManager.Instance.Controls.Player.Sprint.IsPressed();
        if (InputManager.Instance.Controls.Player.Crouch.WasPressedThisFrame())
            TryStartDash();
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (!_canMove || _isNoclip || _stats == null) return;

        if (IsGrounded)
        {
            _jumpRequested = true;
            _queuedAirJump = false;
            return;
        }

        if (_remainingAirJumps <= 0) return;

        _remainingAirJumps--;
        _jumpRequested         = true;
        _queuedAirJump         = true;
        _airJumpRedirectTimer  = GetAirJumpRedirectDuration();
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

    // ── Movement ──────────────────────────────────────────────────────────────

    private void ApplyNoclipMovement()
    {
        Vector3 moveDir = _cameraTransform.forward * _moveInput.y
                        + _cameraTransform.right   * _moveInput.x;
        transform.Translate(moveDir * (_stats.WalkSpeed * 2f) * Time.deltaTime, Space.World);
    }

    private void ApplyMovement()
    {
        float speed = _stats.WalkSpeed;

        if (_isSprinting)
            speed *= _stats.SprintMultiplier;

        Vector3 localMove = new Vector3(_moveInput.x, 0f, _moveInput.y) * speed;

        if (_isDashing)
        {
            localMove = transform.InverseTransformDirection(_dashDirection * _stats.DashSpeed);
        }
        else if (!IsGrounded)
            localMove *= GetCurrentAirControlFactor();

        Vector3 worldMove = transform.TransformDirection(localMove);
        worldMove.y = _verticalVelocity;

        _characterController.Move(worldMove * Time.deltaTime);

        IsGrounded = _characterController.isGrounded;
        Velocity   = _characterController.velocity;

        if (IsGrounded)
        {
            _remainingAirJumps   = GetMaxJumpCount() - 1;
            _airJumpRedirectTimer = 0f;
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

        float horizontalSpeed = new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
        CurrentSpeedRatio = Mathf.Clamp01(horizontalSpeed / (_stats.WalkSpeed * _stats.SprintMultiplier));
    }

    private void ApplyGravity()
    {
        if (_isDashing)
        {
            _jumpRequested = false;
            _queuedAirJump = false;
        }

        if (IsGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f; // keep pressed against ground

        if (_jumpRequested && (IsGrounded || _queuedAirJump))
        {
            // v = sqrt(2 * g * h)
            _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * _stats.GravityScale * _stats.JumpHeight);
            PlayJumpSound();
            _jumpRequested    = false;
            _queuedAirJump    = false;
            return;
        }

        if (!IsGrounded)
        {
            _verticalVelocity += Physics.gravity.y * _stats.GravityScale * Time.deltaTime;
        }

        _jumpRequested = false;
        _queuedAirJump = false;
    }

    private void TryStartDash()
    {
        if (!_canMove || _isNoclip || _stats == null) return;
        if (_isDashing || _dashCooldownTimer > 0f) return;
        if (!TryGetDashDirection(out _dashDirection)) return;

        _isDashing = true;
        _dashTimer = _stats.DashDuration;
        _dashCooldownTimer = _stats.DashCooldown;
        PlayDashSound();

        EventBus<PlayerDashedEvent>.Raise(new PlayerDashedEvent
        {
            Direction = _dashDirection,
            Duration  = _stats.DashDuration,
            Speed     = _stats.DashSpeed
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

        Vector3 horizontalVelocity = new Vector3(Velocity.x, 0f, Velocity.z);
        if (horizontalVelocity.sqrMagnitude > 0.01f)
        {
            dashDirection = horizontalVelocity.normalized;
            return true;
        }

        dashDirection = Vector3.zero;
        return false;
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
        if (_stats == null) return 2;
        return Mathf.Max(1, _stats.MaxJumpCount == 0 ? 2 : _stats.MaxJumpCount);
    }

    private float GetAirJumpRedirectControlFactor()
    {
        if (_stats == null) return 1f;
        return _stats.AirJumpRedirectControlFactor <= 0f ? 1f : _stats.AirJumpRedirectControlFactor;
    }

    private float GetAirJumpRedirectDuration()
    {
        if (_stats == null) return 0.2f;
        return _stats.AirJumpRedirectDuration <= 0f ? 0.2f : _stats.AirJumpRedirectDuration;
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

    // ── Look ──────────────────────────────────────────────────────────────────

    private void ApplyLook()
    {
        float sensitivity = _stats.BaseSensitivity
                          * SaveManager.Instance.CurrentSettings.MouseSensitivity;

        // Horizontal rotation on player root
        transform.Rotate(Vector3.up, _lookInput.x * sensitivity);

        // Vertical pitch — accumulated and clamped; never read back from eulerAngles
        _currentPitch -= _lookInput.y * sensitivity;
        _currentPitch  = Mathf.Clamp(_currentPitch, -_stats.MaxLookAngle, _stats.MaxLookAngle);
        _cameraTransform.localEulerAngles = new Vector3(_currentPitch, 0f, 0f);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private void UpdateState()
    {
        bool isMoving    = _moveInput.sqrMagnitude > 0.01f;
        bool isSprinting = _isSprinting && isMoving;

        if (!IsGrounded)
        {
            CurrentState = _verticalVelocity > 0f ? MovementState.Jump : MovementState.Fall;
        }
        else if (_isDashing)
        {
            CurrentState = MovementState.Sprint;
        }
        else if (_wasGrounded == false)
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

    // ── EventBus handlers ─────────────────────────────────────────────────────

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
            _isDashing = false;
            _dashTimer = 0f;
            _dashCooldownTimer = 0f;
        }
    }
}
