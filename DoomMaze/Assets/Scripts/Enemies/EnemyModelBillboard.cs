using System;
using UnityEngine;

/// <summary>
/// Rotates the enemy model to follow player/camera. Also sets animation state motions.
/// Does not own state - <see cref="EnemyBase"/> calls <see cref="SetAnimation"/> or
/// <see cref="SetAnimationOneShot"/> to switch the active animation.
/// </summary>
public class EnemyModelBillboard : MonoBehaviour
{
    private const float MinimumHitboxWidth = 0.6f;
    private const float MinimumHitboxHeight = 0.9f;
    private const float MinimumHitboxDepth = 0.6f;

    [SerializeField] private bool _enableModelDamageCollider;

    private SkinnedMeshRenderer _skinnedMeshRenderer;
    private Camera _mainCamera;
    private EnemyData _data;
    private BoxCollider _damageCollider;

    private void Awake()
    {
        _skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        _mainCamera = Camera.main;
        _damageCollider = GetComponent<BoxCollider>();

        if (_damageCollider == null)
            _damageCollider = gameObject.AddComponent<BoxCollider>();

        _damageCollider.isTrigger = false;
        _damageCollider.enabled = _enableModelDamageCollider;
    }

    private void Start()
    {
        SyncDamageCollider();
    }

    private void LateUpdate()
    {
        BillboardToCamera();
        SyncDamageCollider();
    }

    private void BillboardToCamera()
    {
        if (_mainCamera == null)
            return;

        Vector3 directionToCamera = _mainCamera.transform.position - transform.position;
        directionToCamera.y = 0f;

        if (directionToCamera.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
    }

    private void SyncDamageCollider()
    {
        if (_damageCollider == null || _skinnedMeshRenderer == null)
            return;

        if (!_enableModelDamageCollider)
        {
            _damageCollider.enabled = false;
            return;
        }

        Bounds skinnedMeshRendererBounds = _skinnedMeshRenderer.bounds;
        Vector3 size = skinnedMeshRendererBounds.size;

        _damageCollider.enabled = true;
        _damageCollider.center = skinnedMeshRendererBounds.center;
        _damageCollider.size = new Vector3(
            Mathf.Max(size.x, MinimumHitboxWidth),
            Mathf.Max(size.y, MinimumHitboxHeight),
            Mathf.Max(size.x, MinimumHitboxDepth));
    }
}