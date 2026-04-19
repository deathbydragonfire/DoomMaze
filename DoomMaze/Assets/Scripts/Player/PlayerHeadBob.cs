using UnityEngine;

/// <summary>
/// Drives sinusoidal camera offset based on <see cref="PlayerMovement.CurrentState"/>.
/// Attach this to the same GameObject as the camera or reference the camera transform.
/// </summary>
public class PlayerHeadBob : MonoBehaviour
{
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Transform      _cameraTransform;
    [SerializeField] private float          _bobFrequency = 2f;
    [SerializeField] private float          _bobAmplitude = 0.05f;
    [SerializeField] private float          _returnSpeed  = 8f;

    private float   _bobTimer;
    private Vector3 _bobOffset;

    private void LateUpdate()
    {
        if (_playerMovement == null || _cameraTransform == null) return;

        bool isBobbing = _playerMovement.CurrentState == MovementState.Walk
                      || _playerMovement.CurrentState == MovementState.Sprint;

        if (isBobbing)
        {
            float speedScale = _playerMovement.CurrentState == MovementState.Sprint
                ? _playerMovement.CurrentSpeedRatio
                : 1f;

            _bobTimer += Time.deltaTime * _bobFrequency * speedScale;

            _bobOffset = new Vector3(
                Mathf.Sin(_bobTimer * 0.5f) * _bobAmplitude,
                Mathf.Sin(_bobTimer)        * _bobAmplitude,
                0f
            );
        }
        else
        {
            _bobTimer = 0f;
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * _returnSpeed);
        }

        _cameraTransform.localPosition = Vector3.Lerp(
            _cameraTransform.localPosition,
            _bobOffset,
            Time.deltaTime * _returnSpeed
        );
    }
}
