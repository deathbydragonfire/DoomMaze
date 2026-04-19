using System;
using UnityEngine;

/// <summary>
/// Rotates the enemy sprite child to always face the main camera (Y-axis billboard).
/// Also drives the sprite flipbook by advancing frames from the active <see cref="Sprite"/> array.
/// Does not own state — <see cref="EnemyBase"/> calls <see cref="SetAnimation"/> or
/// <see cref="SetAnimationOneShot"/> to switch the active sequence.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemySpriteBillboard : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private Camera         _mainCamera;
    private EnemyData      _data;

    private Sprite[] _activeFrames;
    private int      _currentFrame;
    private float    _frameTimer;
    private bool     _loop       = true;
    private bool     _completed  = false;
    private Action   _onComplete;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _mainCamera     = Camera.main;
    }

    /// <summary>Injects the shared <see cref="EnemyData"/> reference from <see cref="EnemyBase"/>.</summary>
    public void Initialize(EnemyData data)
    {
        _data = data;
    }

    private void LateUpdate()
    {
        BillboardToCamera();
        AdvanceFrame();
    }

    // ── Billboard ─────────────────────────────────────────────────────────────

    private void BillboardToCamera()
    {
        if (_mainCamera == null) return;

        Vector3 directionToCamera = _mainCamera.transform.position - transform.position;
        directionToCamera.y = 0f;

        if (directionToCamera.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
    }

    // ── Flipbook ──────────────────────────────────────────────────────────────

    private void AdvanceFrame()
    {
        if (_activeFrames == null || _activeFrames.Length == 0) return;
        if (_data == null || _data.FrameRate <= 0f) return;
        if (_completed && !_loop) return;

        _frameTimer += Time.deltaTime;

        float frameDuration = 1f / _data.FrameRate;
        if (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            _currentFrame++;

            if (_currentFrame >= _activeFrames.Length)
            {
                if (_loop)
                {
                    _currentFrame = 0;
                }
                else
                {
                    _currentFrame = _activeFrames.Length - 1;
                    if (!_completed)
                    {
                        _completed = true;
                        _onComplete?.Invoke();
                    }
                    return;
                }
            }

            _spriteRenderer.sprite = _activeFrames[_currentFrame];
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Switches to a looping sprite sequence.</summary>
    public void SetAnimation(Sprite[] frames, bool loop = true)
    {
        if (frames == null || frames.Length == 0) return;

        _activeFrames = frames;
        _currentFrame = 0;
        _frameTimer   = 0f;
        _loop         = loop;
        _completed    = false;
        _onComplete   = null;

        _spriteRenderer.sprite = _activeFrames[0];
    }

    /// <summary>Plays a one-shot sprite sequence, then invokes <paramref name="onComplete"/>.</summary>
    public void SetAnimationOneShot(Sprite[] frames, Action onComplete)
    {
        if (frames == null || frames.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        _activeFrames = frames;
        _currentFrame = 0;
        _frameTimer   = 0f;
        _loop         = false;
        _completed    = false;
        _onComplete   = onComplete;

        _spriteRenderer.sprite = _activeFrames[0];
    }
}
