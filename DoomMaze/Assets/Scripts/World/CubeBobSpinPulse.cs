using UnityEngine;

/// <summary>
/// Adds idle bobbing, spinning, and emission pulsing to a cube or other prop.
/// Attach this to the cube and tune the serialized fields in the Inspector.
/// </summary>
public class CubeBobSpinPulse : MonoBehaviour
{
    [Header("Bob")]
    [SerializeField] private float _bobStrength = 0.25f;
    [SerializeField] private float _bobSpeed = 1.5f;

    [Header("Spin")]
    [SerializeField] private Vector3 _spinAxis = Vector3.up;
    [SerializeField] private float _spinStrength = 90f;
    [SerializeField] private float _spinSpeed = 1f;

    [Header("Emission")]
    [SerializeField] private Renderer _targetRenderer;
    [ColorUsage(true, true)] [SerializeField] private Color _emissionColor = new Color(0.3f, 1.8f, 1.8f, 1f);
    [SerializeField] private float _emissionMinStrength = 0.4f;
    [SerializeField] private float _emissionMaxStrength = 2f;
    [SerializeField] private float _emissionPulseSpeed = 2f;
    [SerializeField] private bool _randomFlicker;
    [SerializeField] private float _flickerChangeSpeed = 18f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _propertyBlock;
    private Material[] _materials;
    private Vector3 _baseLocalPosition;
    private float _flickerValue;
    private float _flickerTarget;

    private void Awake()
    {
        if (_targetRenderer == null)
            _targetRenderer = GetComponent<Renderer>();

        _propertyBlock = new MaterialPropertyBlock();
        _baseLocalPosition = transform.localPosition;
        _flickerValue = Random.value;
        _flickerTarget = Random.value;

        CacheMaterials();
        ApplyEmission(0f);
    }

    private void OnEnable()
    {
        _baseLocalPosition = transform.localPosition;
    }

    private void OnDisable()
    {
        ApplyEmission(0f);
    }

    private void Update()
    {
        UpdateBob();
        UpdateSpin();
        UpdateEmission();
    }

    private void CacheMaterials()
    {
        if (_targetRenderer == null)
        {
            _materials = new Material[0];
            return;
        }

        _materials = _targetRenderer.materials;

        for (int i = 0; i < _materials.Length; i++)
        {
            Material material = _materials[i];
            if (material == null || !material.HasProperty(EmissionColorId))
                continue;

            material.EnableKeyword("_EMISSION");
        }
    }

    private void UpdateBob()
    {
        Vector3 position = _baseLocalPosition;
        position.y += Mathf.Sin(Time.time * _bobSpeed) * _bobStrength;
        transform.localPosition = position;
    }

    private void UpdateSpin()
    {
        Vector3 axis = _spinAxis.sqrMagnitude > 0.0001f ? _spinAxis.normalized : Vector3.up;
        float degrees = _spinStrength * _spinSpeed * Time.deltaTime;
        transform.Rotate(axis, degrees, Space.Self);
    }

    private void UpdateEmission()
    {
        float pulse = _randomFlicker ? GetFlickerPulse() : (Mathf.Sin(Time.time * _emissionPulseSpeed) + 1f) * 0.5f;
        ApplyEmission(pulse);
    }

    private float GetFlickerPulse()
    {
        if (Mathf.Abs(_flickerTarget - _flickerValue) < 0.03f)
            _flickerTarget = Random.value;

        _flickerValue = Mathf.MoveTowards(
            _flickerValue,
            _flickerTarget,
            Mathf.Max(0.01f, _flickerChangeSpeed) * Time.deltaTime
        );

        return _flickerValue;
    }

    private void ApplyEmission(float pulse)
    {
        if (_targetRenderer == null)
            return;

        float strength = Mathf.Lerp(_emissionMinStrength, _emissionMaxStrength, Mathf.Clamp01(pulse));
        Color color = _emissionColor * Mathf.Max(0f, strength);

        _targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(EmissionColorId, color);
        _targetRenderer.SetPropertyBlock(_propertyBlock);
    }
}
