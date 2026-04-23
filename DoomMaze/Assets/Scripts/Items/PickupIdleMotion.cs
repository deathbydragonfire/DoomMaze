using UnityEngine;

/// <summary>
/// Adds a gentle bob and color pulse to pickups while they are idle in the world.
/// </summary>
public class PickupIdleMotion : MonoBehaviour
{
    [SerializeField] private float _bobAmplitude = 0.08f;
    [SerializeField] private float _bobSpeed     = 2.5f;
    [SerializeField] private Color _glowTint     = new Color(1.1f, 1.04f, 0.82f, 1f);
    [SerializeField] [Range(0f, 1f)] private float _glowStrength = 0.22f;
    [SerializeField] private float _glowSpeed = 1.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");

    private PickupDropMotion _dropMotion;
    private Vector3          _basePosition;
    private bool             _wasDropping;

    private SpriteRenderer[]     _spriteRenderers;
    private Color[]              _spriteBaseColors;
    private Renderer[]           _otherRenderers;
    private MaterialPropertyBlock[] _propertyBlocks;
    private Color[]              _otherBaseColors;
    private int[]                _otherColorPropertyIds;

    private void Awake()
    {
        _dropMotion   = GetComponent<PickupDropMotion>();
        _basePosition = transform.position;
        CacheRenderers();
        ApplyGlow(0f);
    }

    private void OnEnable()
    {
        _basePosition = transform.position;
    }

    private void OnDisable()
    {
        ApplyGlow(0f);
    }

    private void Update()
    {
        if (_dropMotion == null)
            _dropMotion = GetComponent<PickupDropMotion>();

        bool isDropping = _dropMotion != null && _dropMotion.IsDropping;

        if (isDropping)
        {
            _basePosition = transform.position;
            _wasDropping  = true;
            ApplyGlow(0f);
            return;
        }

        if (_wasDropping)
        {
            _basePosition = transform.position;
            _wasDropping  = false;
        }

        Vector3 position = _basePosition;
        position.y += Mathf.Sin(Time.time * _bobSpeed) * _bobAmplitude;
        transform.position = position;

        float pulse = (Mathf.Sin(Time.time * _glowSpeed) + 1f) * 0.5f;
        ApplyGlow(pulse);
    }

    private void CacheRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        int spriteCount = 0;
        int otherCount  = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is SpriteRenderer)
                spriteCount++;
            else
                otherCount++;
        }

        _spriteRenderers  = new SpriteRenderer[spriteCount];
        _spriteBaseColors = new Color[spriteCount];
        _otherRenderers   = new Renderer[otherCount];
        _propertyBlocks   = new MaterialPropertyBlock[otherCount];
        _otherBaseColors  = new Color[otherCount];
        _otherColorPropertyIds = new int[otherCount];

        int spriteIndex = 0;
        int otherIndex  = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is SpriteRenderer spriteRenderer)
            {
                _spriteRenderers[spriteIndex]  = spriteRenderer;
                _spriteBaseColors[spriteIndex] = spriteRenderer.color;
                spriteIndex++;
                continue;
            }

            _otherRenderers[otherIndex] = renderers[i];
            _propertyBlocks[otherIndex] = new MaterialPropertyBlock();
            _otherColorPropertyIds[otherIndex] = GetColorPropertyId(renderers[i]);
            _otherBaseColors[otherIndex]       = GetRendererBaseColor(renderers[i], _otherColorPropertyIds[otherIndex]);
            otherIndex++;
        }
    }

    private void ApplyGlow(float pulse)
    {
        float glowAmount = _glowStrength * pulse;

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color baseColor = _spriteBaseColors[i];
            spriteRenderer.color = Color.Lerp(baseColor, Multiply(baseColor, _glowTint), glowAmount);
        }

        for (int i = 0; i < _otherRenderers.Length; i++)
        {
            Renderer renderer = _otherRenderers[i];
            int propertyId    = _otherColorPropertyIds[i];

            if (renderer == null || propertyId < 0)
                continue;

            Color color = Color.Lerp(_otherBaseColors[i], Multiply(_otherBaseColors[i], _glowTint), glowAmount);
            MaterialPropertyBlock block = _propertyBlocks[i];
            renderer.GetPropertyBlock(block);
            block.SetColor(propertyId, color);
            renderer.SetPropertyBlock(block);
        }
    }

    private static int GetColorPropertyId(Renderer renderer)
    {
        Material material = renderer.sharedMaterial;
        if (material == null)
            return -1;

        if (material.HasProperty(BaseColorId))
            return BaseColorId;

        if (material.HasProperty(ColorId))
            return ColorId;

        return -1;
    }

    private static Color GetRendererBaseColor(Renderer renderer, int propertyId)
    {
        Material material = renderer.sharedMaterial;
        if (material == null || propertyId < 0)
            return Color.white;

        return material.GetColor(propertyId);
    }

    private static Color Multiply(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);
    }
}
