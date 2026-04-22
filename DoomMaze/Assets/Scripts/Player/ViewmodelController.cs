using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages the viewmodel child camera (depth, clear flags, culling mask)
/// and drives weapon bob from <see cref="PlayerMovement"/>.
/// Registers itself into the base camera's URP overlay stack at Awake.
/// Attach to the viewmodel camera's parent or the camera itself.
/// </summary>
public class ViewmodelController : MonoBehaviour
{
    [SerializeField] private Camera         _viewmodelCamera;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Transform      _viewmodelRoot;
    [SerializeField] private float          _bobAmplitude = 0.02f;
    [SerializeField] private float          _bobFrequency = 2f;
    [SerializeField] private float          _returnSpeed  = 8f;

    private float   _bobTimer;
    private Vector3 _bobOffset;

    private void Awake()
    {
        if (_viewmodelCamera == null)
        {
            Debug.LogError("[ViewmodelController] _viewmodelCamera is not assigned.");
            return;
        }

        _viewmodelCamera.cullingMask = LayerMask.GetMask("Viewmodel");

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            _viewmodelCamera.depth = mainCam.depth + 1;

            UniversalAdditionalCameraData baseCamData =
                mainCam.GetComponent<UniversalAdditionalCameraData>();

            if (baseCamData != null)
            {
                List<Camera> stack = baseCamData.cameraStack;
                if (!stack.Contains(_viewmodelCamera))
                    stack.Add(_viewmodelCamera);
            }
            else
            {
                Debug.LogError("[ViewmodelController] MainCamera has no UniversalAdditionalCameraData.");
            }
        }
    }

    private void LateUpdate()
    {
        if (_playerMovement == null || _viewmodelRoot == null) return;

        bool isBobbing = _playerMovement.CurrentState == MovementState.Walk
                      || _playerMovement.CurrentState == MovementState.Sprint;

        if (isBobbing)
        {
            _bobTimer += Time.deltaTime * _bobFrequency;

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

        _viewmodelRoot.localPosition = Vector3.Lerp(
            _viewmodelRoot.localPosition,
            _bobOffset,
            Time.deltaTime * _returnSpeed
        );
    }
}
