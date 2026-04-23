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
    [SerializeField] private float _dashFovBoost   = 9f;
    [SerializeField] private float _dashSpeedReference = 28f;
    [SerializeField] private float _fireFovSnap    = -2f;
    [SerializeField] private float _dashSnapDecay  = 0.12f;
    [SerializeField] private float _fireSnapDecay  = 0.05f;
    [SerializeField] private float _lerpSpeed      = 8f;
    [Header("Dash Speed Lines")]
    [SerializeField] private int   _dashLineCount     = 12;
    [SerializeField] private float _dashLineWidth     = 8f;
    [SerializeField] private float _dashLineLength    = 220f;
    [Range(0f, 0.2f)] [SerializeField] private float _dashLineEdgeInsetNormalized = 0.035f;
    [Range(0f, 0.12f)] [SerializeField] private float _dashLineDepthVarianceNormalized = 0.025f;
    [SerializeField] private float _dashLineAlpha     = 0.16f;
    [SerializeField] private float _dashLineJitter    = 8f;

    private Camera _camera;
    private float  _targetFov;
    private float  _fireSnapCurrent;
    private float  _fireSnapVelocity;
    private float  _dashSnapCurrent;
    private float  _dashSnapVelocity;
    private float  _dashLinesCurrent;
    private float  _dashLinesVelocity;
    private Texture2D _speedLineTexture;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _camera    = GetComponent<Camera>();
        _targetFov = _baseFov;
        _speedLineTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        _speedLineTexture.SetPixel(0, 0, Color.white);
        _speedLineTexture.Apply();
    }

    private void OnEnable()
    {
        EventBus<PlayerSprintChangedEvent>.Subscribe(OnSprintChanged);
        EventBus<PlayerDashedEvent>.Subscribe(OnPlayerDashed);
        EventBus<WeaponFiredEvent>.Subscribe(OnWeaponFired);
    }

    private void OnDisable()
    {
        EventBus<PlayerSprintChangedEvent>.Unsubscribe(OnSprintChanged);
        EventBus<PlayerDashedEvent>.Unsubscribe(OnPlayerDashed);
        EventBus<WeaponFiredEvent>.Unsubscribe(OnWeaponFired);
    }

    private void OnDestroy()
    {
        if (_speedLineTexture != null)
            Destroy(_speedLineTexture);
    }

    private void Update()
    {
        _fireSnapCurrent = Mathf.SmoothDamp(_fireSnapCurrent, 0f, ref _fireSnapVelocity, _fireSnapDecay);
        _dashSnapCurrent = Mathf.SmoothDamp(_dashSnapCurrent, 0f, ref _dashSnapVelocity, _dashSnapDecay);
        _dashLinesCurrent = Mathf.SmoothDamp(_dashLinesCurrent, 0f, ref _dashLinesVelocity, _dashSnapDecay);

        float desired = _targetFov + _fireSnapCurrent + _dashSnapCurrent;
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, desired, _lerpSpeed * Time.deltaTime);
    }

    private void OnGUI()
    {
        if (_speedLineTexture == null || _dashLinesCurrent <= 0.01f || Event.current.type != EventType.Repaint)
            return;

        float intensity = Mathf.Clamp01(_dashLinesCurrent);
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float screenRadius = Mathf.Min(Screen.width, Screen.height) * 0.5f;
        float edgeInset = screenRadius * _dashLineEdgeInsetNormalized;
        float depthVariance = screenRadius * _dashLineDepthVarianceNormalized;
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 48f) * 0.08f;

        for (int i = 0; i < _dashLineCount; i++)
        {
            float normalized = i / (float)_dashLineCount;
            float angle = normalized * 360f + Mathf.Sin(Time.unscaledTime * 14f + i * 1.7f) * _dashLineJitter;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float lineWidth = _dashLineWidth * (1f + (i % 3) * 0.2f) * intensity;
            float lineLength = Mathf.Min(_dashLineLength * pulse * (1f - normalized * 0.12f), screenRadius * 0.22f);
            Vector2 edgePoint = GetScreenEdgePoint(screenCenter, direction);
            float inwardOffset = edgeInset + lineLength * 0.5f + depthVariance * (i % 3) * 0.5f;
            Vector2 pivot = edgePoint - direction * inwardOffset;
            Color lineColor = new Color(1f, 1f, 1f, _dashLineAlpha * intensity * (1f - normalized * 0.35f));

            DrawSpeedLine(new Rect(pivot.x - lineWidth * 0.5f, pivot.y - lineLength * 0.5f, lineWidth, lineLength), angle + 90f, lineColor);
        }
    }

    // ── EventBus Handlers ─────────────────────────────────────────────────────

    private void OnSprintChanged(PlayerSprintChangedEvent e)
    {
        _targetFov = e.IsSprinting ? _sprintFov : _baseFov;
    }

    private void OnPlayerDashed(PlayerDashedEvent e)
    {
        float dashScale = _dashSpeedReference > 0f ? Mathf.Clamp01(e.Speed / _dashSpeedReference) : 1f;
        _dashSnapCurrent = Mathf.Max(_dashSnapCurrent, _dashFovBoost * Mathf.Lerp(0.75f, 1.15f, dashScale));
        _dashLinesCurrent = 1f;
    }

    private void OnWeaponFired(WeaponFiredEvent e)
    {
        _fireSnapCurrent += _fireFovSnap;
    }

    private void DrawSpeedLine(Rect rect, float angle, Color color)
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Vector2 pivot = rect.center;

        GUIUtility.RotateAroundPivot(angle, pivot);
        GUI.color = color;
        GUI.DrawTexture(rect, _speedLineTexture);
        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    private static Vector2 GetScreenEdgePoint(Vector2 center, Vector2 direction)
    {
        float halfWidth = Screen.width * 0.5f;
        float halfHeight = Screen.height * 0.5f;
        float scaleX = Mathf.Abs(direction.x) > 0.0001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float scaleY = Mathf.Abs(direction.y) > 0.0001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
        float edgeScale = Mathf.Min(scaleX, scaleY);
        return center + direction * edgeScale;
    }
}
