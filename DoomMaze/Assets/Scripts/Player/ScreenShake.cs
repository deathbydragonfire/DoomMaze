using UnityEngine;

/// <summary>
/// Applies an additive positional shake to <see cref="_cameraRoot"/> in response to
/// <see cref="PlayerDamagedEvent"/> and <see cref="WeaponFiredEvent"/> via the EventBus.
/// Strongest-wins policy: a new shake only overrides if its magnitude exceeds the
/// remaining magnitude of the current shake.
/// Targets <c>CameraRoot</c> rather than <c>Camera</c> directly so that
/// <see cref="PlayerHeadBob"/> and <see cref="CameraSway"/> offsets compose cleanly.
/// </summary>
public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    [SerializeField] private Transform _cameraRoot;

    // Shake parameters for player taking damage.
    private const float DAMAGE_SHAKE_DURATION  = 0.25f;
    private const float DAMAGE_SHAKE_MAGNITUDE = 0.08f;

    // Weapon recoil shake only fires when SpreadAngle exceeds this threshold.
    private const float RECOIL_SPREAD_THRESHOLD = 0f;
    private const float RECOIL_SHAKE_DURATION   = 0.08f;
    private const float RECOIL_SHAKE_MAGNITUDE  = 0.03f;

    private Vector3 _originalLocalPos;
    private float   _remaining;
    private float   _shakeDuration;
    private float   _magnitude;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (_cameraRoot != null)
            _originalLocalPos = _cameraRoot.localPosition;

        EventBus<PlayerDamagedEvent>.Subscribe(OnPlayerDamaged);
        EventBus<WeaponFiredEvent>.Subscribe(OnWeaponFired);
    }

    private void OnDestroy()
    {
        EventBus<PlayerDamagedEvent>.Unsubscribe(OnPlayerDamaged);
        EventBus<WeaponFiredEvent>.Unsubscribe(OnWeaponFired);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (_cameraRoot == null || _remaining <= 0f) return;

        _remaining -= Time.unscaledDeltaTime;

        if (_remaining > 0f)
        {
            float intensity = _remaining / _shakeDuration;
            _cameraRoot.localPosition = _originalLocalPos + Random.insideUnitSphere * (_magnitude * intensity);
        }
        else
        {
            _cameraRoot.localPosition = _originalLocalPos;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a shake of the given <paramref name="duration"/> and <paramref name="magnitude"/>.
    /// Only overrides the active shake if the incoming magnitude is greater.
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        if (_cameraRoot == null)
        {
            Debug.LogWarning("[ScreenShake] CameraRoot is not assigned.");
            return;
        }

        // Strongest-wins: only override if the new shake is more intense.
        float remainingMagnitude = _remaining > 0f ? _magnitude * (_remaining / _shakeDuration) : 0f;
        if (magnitude <= remainingMagnitude) return;

        _shakeDuration = duration;
        _remaining     = duration;
        _magnitude     = magnitude;
    }

    // ── EventBus Handlers ─────────────────────────────────────────────────────

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        Shake(DAMAGE_SHAKE_DURATION, DAMAGE_SHAKE_MAGNITUDE);
    }

    private void OnWeaponFired(WeaponFiredEvent e)
    {
        if (e.Data == null) return;
        if (e.Data.SpreadAngle > RECOIL_SPREAD_THRESHOLD)
            Shake(RECOIL_SHAKE_DURATION, RECOIL_SHAKE_MAGNITUDE);
    }
}
