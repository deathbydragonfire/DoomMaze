using UnityEngine;

/// <summary>
/// Pooled, self-returning impact effect that plays a sprite flipbook at a hit point,
/// billboards toward the camera, and automatically returns itself to its
/// <see cref="ObjectPool{T}"/> when the animation finishes.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ImpactFX : MonoBehaviour
{
    private SpriteRenderer       _spriteRenderer;
    private Camera               _mainCamera;
    private ObjectPool<ImpactFX> _owningPool;

    private Sprite[] _frames;
    private float    _frameDuration;
    private int      _currentFrame;
    private float    _frameTimer;
    private bool     _playing;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _mainCamera     = Camera.main;
    }

    private void Update()
    {
        if (!_playing) return;
        AdvanceFrame();
    }

    private void LateUpdate()
    {
        BillboardToCamera();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Injects the owning pool reference so this instance can self-return.</summary>
    public void Initialize(ObjectPool<ImpactFX> owningPool)
    {
        _owningPool = owningPool;
    }

    /// <summary>
    /// Begins flipbook playback. Resets frame state to the beginning.
    /// Automatically returns to pool after the last frame.
    /// </summary>
    /// <param name="frames">Sprite sequence to play.</param>
    /// <param name="frameRate">Frames per second.</param>
    public void Play(Sprite[] frames, float frameRate)
    {
        if (frames == null || frames.Length == 0)
        {
            ReturnToPool();
            return;
        }

        _frames        = frames;
        _frameDuration = frameRate > 0f ? 1f / frameRate : 0.083f;
        _currentFrame  = 0;
        _frameTimer    = 0f;
        _playing       = true;

        _spriteRenderer.sprite = _frames[0];

        // Refresh camera reference in case Camera.main changed.
        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void AdvanceFrame()
    {
        _frameTimer += Time.deltaTime;

        if (_frameTimer < _frameDuration) return;

        _frameTimer -= _frameDuration;
        _currentFrame++;

        if (_currentFrame >= _frames.Length)
        {
            _playing = false;
            ReturnToPool();
            return;
        }

        _spriteRenderer.sprite = _frames[_currentFrame];
    }

    private void BillboardToCamera()
    {
        if (_mainCamera == null) return;

        Vector3 direction = _mainCamera.transform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            transform.forward = direction;
    }

    private void ReturnToPool()
    {
        _owningPool?.Return(this);
    }
}
