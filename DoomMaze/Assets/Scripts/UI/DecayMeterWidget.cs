using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// White HUD slider for the player's secondary decay resource.
/// </summary>
public class DecayMeterWidget : MonoBehaviour
{
    private static readonly Vector3 DefaultWorldPosition = new Vector3(53f, 581.7999877929688f, 0f);
    private static readonly Quaternion DefaultWorldRotation = Quaternion.identity;
    private static readonly Vector3 DefaultWorldScale = new Vector3(
        1.7399998903274537f,
        1.7399998903274537f,
        1.7399998903274537f);

    [SerializeField] private Slider _slider;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _fillImage;
    [SerializeField] private Color _backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color _fillColor = Color.white;

    private RectTransform _rectTransform;

    private void Awake()
    {
        EnsureSlider();
        ApplyDefaultWorldPlacement();
        SetValue(1f);
    }

    public void ConfigureRuntimeLayout(RectTransform healthRect)
    {
        _rectTransform = transform as RectTransform;
        if (_rectTransform == null)
            return;

        if (healthRect != null)
        {
            _rectTransform.anchorMin = healthRect.anchorMin;
            _rectTransform.anchorMax = healthRect.anchorMax;
            _rectTransform.pivot = healthRect.pivot;
            _rectTransform.localScale = healthRect.localScale;
            _rectTransform.sizeDelta = new Vector2(Mathf.Max(120f, healthRect.sizeDelta.x), 8f);

            float yOffset = healthRect.sizeDelta.y * Mathf.Max(1f, healthRect.localScale.y) + 12f;
            _rectTransform.anchoredPosition = healthRect.anchoredPosition + new Vector2(0f, yOffset);
        }

        ApplyDefaultWorldPlacement();
    }

    public void SetValue(float normalized)
    {
        EnsureSlider();

        if (_slider != null)
            _slider.SetValueWithoutNotify(Mathf.Clamp01(normalized));
    }

    private void EnsureSlider()
    {
        _rectTransform ??= transform as RectTransform;

        if (_slider == null)
            _slider = GetComponent<Slider>();

        if (_slider == null)
            _slider = gameObject.AddComponent<Slider>();

        RectTransform root = _rectTransform;
        if (root == null)
            return;

        if (_backgroundImage == null)
        {
            _backgroundImage = GetComponent<Image>();
            if (_backgroundImage == null)
                _backgroundImage = gameObject.AddComponent<Image>();
        }

        if (_fillImage == null)
        {
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(transform, false);
            _fillImage = fillObject.GetComponent<Image>();
        }

        RectTransform fillRect = _fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        _backgroundImage.color = _backgroundColor;
        _backgroundImage.raycastTarget = false;
        _fillImage.color = _fillColor;
        _fillImage.raycastTarget = false;

        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.wholeNumbers = false;
        _slider.direction = Slider.Direction.LeftToRight;
        _slider.fillRect = fillRect;
        _slider.handleRect = null;
        _slider.targetGraphic = null;
        _slider.interactable = false;
    }

    private void ApplyDefaultWorldPlacement()
    {
        transform.SetPositionAndRotation(DefaultWorldPosition, DefaultWorldRotation);
        SetWorldScale(transform, DefaultWorldScale);
    }

    private static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
    }
}
