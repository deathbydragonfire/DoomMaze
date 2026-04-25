using System;
using UnityEngine;

/// <summary>
/// Rotates the enemy sprite child to always face the main camera (Y-axis billboard).
/// Also drives the sprite flipbook by advancing frames from the active <see cref="Sprite"/> array.
/// Does not own state - <see cref="EnemyBase"/> calls <see cref="SetAnimation"/> or
/// <see cref="SetAnimationOneShot"/> to switch the active sequence.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemySpriteBillboard : MonoBehaviour
{
    private static Sprite _placeholderSprite;
    private const float MinimumHitboxWidth = 0.6f;
    private const float MinimumHitboxHeight = 0.9f;
    private const float MinimumHitboxDepth = 0.6f;

    [SerializeField] private bool _enableSpriteDamageCollider;

    private SpriteRenderer _spriteRenderer;
    private Camera _mainCamera;
    private EnemyData _data;
    private BoxCollider _damageCollider;

    private Sprite[] _activeFrames;
    private int _currentFrame;
    private float _frameTimer;
    private bool _loop = true;
    private bool _completed;
    private Action _onComplete;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _mainCamera = Camera.main;
        _damageCollider = GetComponent<BoxCollider>();

        if (_damageCollider == null)
            _damageCollider = gameObject.AddComponent<BoxCollider>();

        _damageCollider.isTrigger = false;
        _damageCollider.enabled = _enableSpriteDamageCollider;
    }

    /// <summary>Injects the shared <see cref="EnemyData"/> reference from <see cref="EnemyBase"/>.</summary>
    public void Initialize(EnemyData data)
    {
        _data = data;

        if (_spriteRenderer.sprite == null && !HasConfiguredSprites(data))
            _spriteRenderer.sprite = GetPlaceholderSprite();

        SyncDamageCollider();
    }

    private void LateUpdate()
    {
        BillboardToCamera();
        AdvanceFrame();
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

    private void AdvanceFrame()
    {
        if (_activeFrames == null || _activeFrames.Length == 0)
            return;

        if (_data == null || _data.FrameRate <= 0f)
            return;

        if (_completed && !_loop)
            return;

        _frameTimer += Time.deltaTime;

        float frameDuration = 1f / _data.FrameRate;
        if (_frameTimer < frameDuration)
            return;

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
        SyncDamageCollider();
    }

    /// <summary>Switches to a looping sprite sequence.</summary>
    public void SetAnimation(Sprite[] frames, bool loop = true)
    {
        if (frames == null || frames.Length == 0)
            return;

        _activeFrames = frames;
        _currentFrame = 0;
        _frameTimer = 0f;
        _loop = loop;
        _completed = false;
        _onComplete = null;

        _spriteRenderer.sprite = _activeFrames[0];
        SyncDamageCollider();
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
        _frameTimer = 0f;
        _loop = false;
        _completed = false;
        _onComplete = onComplete;

        _spriteRenderer.sprite = _activeFrames[0];
        SyncDamageCollider();
    }

    private static bool HasConfiguredSprites(EnemyData data)
    {
        return HasFrames(data != null ? data.IdleSprites : null)
            || HasFrames(data != null ? data.WalkSprites : null)
            || HasFrames(data != null ? data.AttackSprites : null)
            || HasFrames(data != null ? data.HurtSprites : null)
            || HasFrames(data != null ? data.DeathSprites : null);
    }

    private static bool HasFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    private static Sprite GetPlaceholderSprite()
    {
        if (_placeholderSprite != null)
            return _placeholderSprite;

        const int width = 32;
        const int height = 48;
        const float pixelsPerUnit = 80f;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
        {
            name = "EnemyPlaceholderSprite",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32 fillColor = new Color32(77, 219, 255, 255);
        Color32 borderColor = new Color32(18, 54, 64, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                texture.SetPixel(x, y, isBorder ? borderColor : fillColor);
            }
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        _placeholderSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );
        _placeholderSprite.name = "EnemyPlaceholderSprite";

        return _placeholderSprite;
    }

    private void SyncDamageCollider()
    {
        if (_damageCollider == null || _spriteRenderer == null || _spriteRenderer.sprite == null)
            return;

        if (!_enableSpriteDamageCollider)
        {
            _damageCollider.enabled = false;
            return;
        }

        Bounds spriteBounds = _spriteRenderer.sprite.bounds;
        Vector3 size = spriteBounds.size;

        _damageCollider.enabled = true;
        _damageCollider.center = spriteBounds.center;
        _damageCollider.size = new Vector3(
            Mathf.Max(size.x, MinimumHitboxWidth),
            Mathf.Max(size.y, MinimumHitboxHeight),
            Mathf.Max(size.x, MinimumHitboxDepth));
    }
}
