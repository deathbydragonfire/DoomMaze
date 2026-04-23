using UnityEngine;

/// <summary>
/// Adds rotational sway to the camera's local rotation based on mouse delta,
/// giving a subtle feel of weight. Attach to the camera GameObject.
/// </summary>
public class CameraSway : MonoBehaviour
{
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private float _swayAmount    = 2f;
    [SerializeField] private float _swaySmoothing = 6f;
    [SerializeField] private float _maxSway       = 5f;
    [Header("Wall Run")]
    [SerializeField] private float _wallRunTiltAngle = 4f;

    private Quaternion _targetSway = Quaternion.identity;

    private void Awake()
    {
        if (_playerMovement == null)
            _playerMovement = GetComponentInParent<PlayerMovement>();
    }

    private void LateUpdate()
    {
        if (InputManager.Instance == null) return;

        Vector2 lookDelta = InputManager.Instance.Controls.Player.Look.ReadValue<Vector2>();

        float swayX = Mathf.Clamp(-lookDelta.y * _swayAmount, -_maxSway, _maxSway);
        float swayZ = Mathf.Clamp(-lookDelta.x * _swayAmount, -_maxSway, _maxSway);
        float wallTilt = _playerMovement != null && _playerMovement.IsWallRunning
            ? _playerMovement.WallRunCameraTiltSign * _wallRunTiltAngle
            : 0f;

        _targetSway = Quaternion.Euler(swayX, 0f, swayZ + wallTilt);
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            _targetSway,
            Time.deltaTime * _swaySmoothing
        );
    }
}
