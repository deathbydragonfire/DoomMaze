using UnityEngine;

/// <summary>
/// Smoothly tweens the camera's FOV between base and sprint values on
/// <see cref="PlayerSprintChangedEvent"/>, and applies a brief -2° snap on
/// <see cref="WeaponFiredEvent"/> for recoil feel.
/// Subscribes via the EventBus in <c>OnEnable</c> / <c>OnDisable</c>.
/// Attach to the same GameObject as the <see cref="Camera"/> component.
/// </summary>
[RequireComponent(typeof(Camera))]
public class FovKick : MonoBehaviour
{
    [SerializeField] private float _baseFov        = 70f;
    [SerializeField] private float _sprintFov      = 80f;
    [SerializeField] private float _fireFovSnap    = -2f;
    [SerializeField] private float _fireSnapDecay  = 0.05f;
    [SerializeField] private float _lerpSpeed      = 8f;

    private Camera _camera;
    private float  _targetFov;
    private float  _fireSnapCurrent;
    private float  _fireSnapVelocity;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _camera    = GetComponent<Camera>();
        _targetFov = _baseFov;
    }

    private void OnEnable()
    {
        EventBus<PlayerSprintChangedEvent>.Subscribe(OnSprintChanged);
        EventBus<WeaponFiredEvent>.Subscribe(OnWeaponFired);
    }

    private void OnDisable()
    {
        EventBus<PlayerSprintChangedEvent>.Unsubscribe(OnSprintChanged);
        EventBus<WeaponFiredEvent>.Unsubscribe(OnWeaponFired);
    }

    private void Update()
    {
        _fireSnapCurrent = Mathf.SmoothDamp(_fireSnapCurrent, 0f, ref _fireSnapVelocity, _fireSnapDecay);

        float desired = _targetFov + _fireSnapCurrent;
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, desired, _lerpSpeed * Time.deltaTime);
    }

    // ── EventBus Handlers ─────────────────────────────────────────────────────

    private void OnSprintChanged(PlayerSprintChangedEvent e)
    {
        _targetFov = e.IsSprinting ? _sprintFov : _baseFov;
    }

    private void OnWeaponFired(WeaponFiredEvent e)
    {
        _fireSnapCurrent += _fireFovSnap;
    }
}
