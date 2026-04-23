using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds lightweight hover and selection feedback to menu buttons.
/// Uses unscaled time so the animation still runs while the game is paused.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MenuButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Color _hoverColor = new Color(1f, 0.35f, 0.2f, 1f);
    [SerializeField] private float _hoverScaleMultiplier = 1.08f;
    [SerializeField] private float _transitionSpeed = 16f;

    private Button _button;
    private Graphic _targetGraphic;
    private RectTransform _rectTransform;
    private Vector3 _baseScale;
    private Color _baseColor;
    private bool _isHovered;
    private bool _isSelected;
    private bool _hasCapturedDefaults;

    public static void AttachToButtons(Transform root)
    {
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.GetComponent<MenuButtonHoverEffect>() == null)
                button.gameObject.AddComponent<MenuButtonHoverEffect>();
        }
    }

    private void Awake()
    {
        CacheReferences();
        CaptureDefaults();
        SnapToCurrentState();
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureDefaults();
        SnapToCurrentState();
    }

    private void OnDisable()
    {
        ResetVisuals();
    }

    private void Update()
    {
        if (_rectTransform == null || _targetGraphic == null)
            return;

        bool isHighlighted = _button != null && _button.interactable && (_isHovered || _isSelected);
        float animationStep = 1f - Mathf.Exp(-_transitionSpeed * Time.unscaledDeltaTime);

        Vector3 targetScale = isHighlighted
            ? _baseScale * _hoverScaleMultiplier
            : _baseScale;
        Color targetColor = isHighlighted
            ? _hoverColor
            : _baseColor;

        _rectTransform.localScale = Vector3.Lerp(_rectTransform.localScale, targetScale, animationStep);
        _targetGraphic.color = Color.Lerp(_targetGraphic.color, targetColor, animationStep);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
    }

    private void CacheReferences()
    {
        _button = GetComponent<Button>();
        _rectTransform = transform as RectTransform;

        TMP_Text label = GetComponentInChildren<TMP_Text>(true);
        _targetGraphic = label != null ? label : _button != null ? _button.targetGraphic : GetComponent<Graphic>();
    }

    private void CaptureDefaults()
    {
        if (_hasCapturedDefaults || _rectTransform == null || _targetGraphic == null)
            return;

        _baseScale = _rectTransform.localScale;
        _baseColor = _targetGraphic.color;
        _hasCapturedDefaults = true;
    }

    private void SnapToCurrentState()
    {
        if (_rectTransform == null || _targetGraphic == null)
            return;

        bool isHighlighted = _button != null && _button.interactable && (_isHovered || _isSelected);
        _rectTransform.localScale = isHighlighted ? _baseScale * _hoverScaleMultiplier : _baseScale;
        _targetGraphic.color = isHighlighted ? _hoverColor : _baseColor;
    }

    private void ResetVisuals()
    {
        if (!_hasCapturedDefaults || _rectTransform == null || _targetGraphic == null)
            return;

        _isHovered = false;
        _isSelected = false;
        _rectTransform.localScale = _baseScale;
        _targetGraphic.color = _baseColor;
    }
}
