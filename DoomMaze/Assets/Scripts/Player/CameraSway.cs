using UnityEngine;

/// <summary>
/// Adds rotational sway to the camera's local rotation based on mouse delta,
/// giving a subtle feel of weight. Attach to the camera GameObject.
/// </summary>
public class CameraSway : MonoBehaviour
{
    [SerializeField] private float _swayAmount    = 2f;
    [SerializeField] private float _swaySmoothing = 6f;
    [SerializeField] private float _maxSway       = 5f;

    private Quaternion _targetSway = Quaternion.identity;

    private void LateUpdate()
    {
        if (InputManager.Instance == null) return;

        Vector2 lookDelta = InputManager.Instance.Controls.Player.Look.ReadValue<Vector2>();

        float swayX = Mathf.Clamp(-lookDelta.y * _swayAmount, -_maxSway, _maxSway);
        float swayZ = Mathf.Clamp(-lookDelta.x * _swayAmount, -_maxSway, _maxSway);

        _targetSway = Quaternion.Euler(swayX, 0f, swayZ);
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            _targetSway,
            Time.deltaTime * _swaySmoothing
        );
    }
}
