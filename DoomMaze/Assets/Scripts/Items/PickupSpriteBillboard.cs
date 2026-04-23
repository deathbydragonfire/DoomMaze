using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a pickup sprite face the camera and assigns a simple generated fallback sprite
/// when no authored sprite has been assigned on the <see cref="SpriteRenderer"/>.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PickupSpriteBillboard : MonoBehaviour
{
    [SerializeField] private PickupSpriteShape _fallbackShape = PickupSpriteShape.Ammo;
    [SerializeField] private Color             _tint          = Color.white;
    [SerializeField] private float             _bobAmplitude  = 0.08f;
    [SerializeField] private float             _bobSpeed      = 3f;

    private static readonly Dictionary<PickupSpriteShape, Sprite> SpriteCache = new Dictionary<PickupSpriteShape, Sprite>(2);

    private SpriteRenderer _spriteRenderer;
    private Camera         _mainCamera;
    private PickupDropMotion _dropMotion;
    private PickupIdleMotion _idleMotion;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _mainCamera     = Camera.main;
        _dropMotion     = GetComponent<PickupDropMotion>();
        _idleMotion     = GetComponent<PickupIdleMotion>();

        EnsureSprite();
        _spriteRenderer.color = _tint;
    }

    private void LateUpdate()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_dropMotion == null)
            _dropMotion = GetComponent<PickupDropMotion>();

        if (_idleMotion == null)
            _idleMotion = GetComponent<PickupIdleMotion>();

        BillboardToCamera();
    }

    private void EnsureSprite()
    {
        if (_spriteRenderer == null || _spriteRenderer.sprite != null)
            return;

        if (!SpriteCache.TryGetValue(_fallbackShape, out Sprite sprite) || sprite == null)
        {
            sprite = CreateFallbackSprite(_fallbackShape);
            SpriteCache[_fallbackShape] = sprite;
        }

        _spriteRenderer.sprite = sprite;
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

    private static Sprite CreateFallbackSprite(PickupSpriteShape shape)
    {
        const int size = 32;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode   = TextureWrapMode.Clamp;
        texture.name       = $"GeneratedPickup_{shape}";

        Color32[] pixels = new Color32[size * size];
        Color32   solid  = new Color32(255, 255, 255, 255);

        switch (shape)
        {
            case PickupSpriteShape.Health:
                DrawFilledRect(pixels, size, 12, 4, 20, 28, solid);
                DrawFilledRect(pixels, size, 4, 12, 28, 20, solid);
                break;

            default:
                DrawFilledRect(pixels, size, 6, 8, 26, 24, solid);
                DrawFilledRect(pixels, size, 10, 11, 22, 21, new Color32(0, 0, 0, 0));
                DrawFilledRect(pixels, size, 13, 24, 19, 27, solid);
                break;
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
    }

    private static void DrawFilledRect(Color32[] pixels, int textureSize, int minX, int minY, int maxX, int maxY, Color32 color)
    {
        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                pixels[(y * textureSize) + x] = color;
            }
        }
    }
}

public enum PickupSpriteShape
{
    Ammo,
    Health
}
