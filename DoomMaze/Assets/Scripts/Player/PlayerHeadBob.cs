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
    [Header("Footsteps")]
    [SerializeField] private AudioClip[]    _walkFootstepSounds;
    [SerializeField] private AudioClip[]    _sprintFootstepSounds;
    [SerializeField] private float          _footstepPhaseOffset = 1.5707964f;
    [SerializeField] private float          _walkFootstepPhaseInterval = 4.712389f;
    [SerializeField] private float          _sprintFootstepPhaseInterval = 3.926991f;
    [Range(0f, 1f)] [SerializeField] private float _walkFootstepVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float _sprintFootstepVolume = 0.9f;

    private float   _bobTimer;
    private Vector3 _bobOffset;
    private float   _nextFootstepBobTime = 1.5707964f;

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

            TryPlayFootstep();
        }
        else
        {
            _bobTimer = 0f;
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * _returnSpeed);
            _nextFootstepBobTime = _footstepPhaseOffset;
        }

        _cameraTransform.localPosition = Vector3.Lerp(
            _cameraTransform.localPosition,
            _bobOffset,
            Time.deltaTime * _returnSpeed
        );
    }

    private void TryPlayFootstep()
    {
        if (!_playerMovement.IsGrounded || _playerMovement.IsDashing)
            return;

        while (_bobTimer >= _nextFootstepBobTime)
        {
            bool isSprinting = _playerMovement.CurrentState == MovementState.Sprint;
            AudioClip[] clips = isSprinting
                ? _sprintFootstepSounds
                : _walkFootstepSounds;
            float volume = isSprinting ? _sprintFootstepVolume : _walkFootstepVolume;
            float interval = Mathf.Max(0.01f, isSprinting ? _sprintFootstepPhaseInterval : _walkFootstepPhaseInterval);

            AudioManager.Instance?.PlaySfx(clips, volume);
            _nextFootstepBobTime += interval;
        }
    }
}
