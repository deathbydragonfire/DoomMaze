using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD widget that displays the player's super charge as masked text fill.
/// A white base label is always visible, and a red copy fills from left to right as charge increases.
/// </summary>
public class SuperMeterWidget : MonoBehaviour
{
    [SerializeField] private bool _debugBinding = false;
    [SerializeField] private Slider _slider;
    [SerializeField] private GameObject _container;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Text _legacyLabel;
    [SerializeField] private string _labelText = "Super Meter";
    [SerializeField] private Vector2 _textOffset = new Vector2(0f, 10f);
    [SerializeField] private Vector2 _textSize = new Vector2(320f, 40f);
    [SerializeField] private Color _baseTextColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color _fillTextColor = new Color(1f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color _readyFlashColor = new Color(1f, 0.85f, 0.85f, 1f);
    [SerializeField] private float _readyShakeDuration = 0.24f;
    [SerializeField] private float _readyShakeDistance = 10f;
    [SerializeField] private float _readyShakeFrequency = 26f;
    [SerializeField] private float _readyFlashSpeed = 4.5f;
    [SerializeField] private float _readyMotionDistance = 6f;
    [SerializeField] private float _readyMotionSpeed = 2.6f;

    private RectTransform _widgetRect;
    private RectTransform _textRootRect;
    private RectTransform _fillViewportRect;
    private RectTransform _fillContentRect;
    private TextMeshProUGUI _baseLabelRuntime;
    private TextMeshProUGUI _fillLabelRuntime;
    private Vector2 _baseAnchoredPosition;
    private Vector3 _baseScale = Vector3.one;
    private Coroutine _readyAnimationRoutine;
    private bool _wasReady;
    private float _currentNormalized;

    private void Awake()
    {
        _widgetRect = transform as RectTransform;

        if (_container == null)
            _container = gameObject;

        EnsureTextMeter();
        DisableLegacyVisuals();
        ResetReadyVisuals();
        SetValue(0f, 0, 1, false);
    }

    private void LateUpdate()
    {
        RefreshTextRootPlacement();
    }

    private void OnDisable()
    {
        StopReadyAnimation();
        _wasReady = false;
        ResetReadyVisuals();
    }

    /// <summary>Updates the super meter fill and ready-state styling.</summary>
    public void SetValue(float normalized, int currentCharges, int chargesRequired, bool isReady)
    {
        if (_container != null)
            _container.SetActive(true);

        EnsureTextMeter();
        DisableLegacyVisuals();

        _currentNormalized = Mathf.Clamp01(normalized);
        UpdateTextVisuals(_currentNormalized);

        if (isReady && !_wasReady)
            StartReadyAnimation();
        else if (!isReady && _wasReady)
            StopReadyAnimation();

        _wasReady = isReady;
    }

    private void EnsureTextMeter()
    {
        if (_textRootRect != null && _baseLabelRuntime != null && _fillLabelRuntime != null && _fillViewportRect != null && _fillContentRect != null)
            return;

        RectTransform parentRect = _widgetRect != null ? _widgetRect.parent as RectTransform : null;
        if (parentRect == null)
            return;

        if (_textRootRect == null)
        {
            GameObject rootObject = new GameObject("SuperMeterTextRoot", typeof(RectTransform));
            rootObject.transform.SetParent(parentRect, false);
            _textRootRect = rootObject.GetComponent<RectTransform>();
        }

        if (_baseLabelRuntime == null)
            _baseLabelRuntime = CreateRuntimeLabel("BaseLabel", _textRootRect);

        if (_fillViewportRect == null)
        {
            GameObject viewportObject = new GameObject("FillViewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(_textRootRect, false);
            _fillViewportRect = viewportObject.GetComponent<RectTransform>();
        }

        if (_fillContentRect == null)
        {
            GameObject contentObject = new GameObject("FillContent", typeof(RectTransform));
            contentObject.transform.SetParent(_fillViewportRect, false);
            _fillContentRect = contentObject.GetComponent<RectTransform>();
        }

        if (_fillLabelRuntime == null)
            _fillLabelRuntime = CreateRuntimeLabel("FillLabel", _fillContentRect);

        ApplyReferenceLabelStyle(_baseLabelRuntime);
        ApplyReferenceLabelStyle(_fillLabelRuntime);

        _baseLabelRuntime.color = _baseTextColor;
        _fillLabelRuntime.color = _fillTextColor;
        _baseLabelRuntime.text = _labelText;
        _fillLabelRuntime.text = _labelText;
        _baseLabelRuntime.raycastTarget = false;
        _fillLabelRuntime.raycastTarget = false;
        _baseLabelRuntime.maskable = false;
        _fillLabelRuntime.maskable = true;
        _baseLabelRuntime.textWrappingMode = TextWrappingModes.NoWrap;
        _fillLabelRuntime.textWrappingMode = TextWrappingModes.NoWrap;
        _baseLabelRuntime.overflowMode = TextOverflowModes.Overflow;
        _fillLabelRuntime.overflowMode = TextOverflowModes.Overflow;

        StretchToParent(_baseLabelRuntime.rectTransform);
        StretchToParent(_fillViewportRect);
        ConfigureFillContentRect();
        StretchToParent(_fillLabelRuntime.rectTransform);

        RefreshTextRootPlacement();
        _baseAnchoredPosition = _textRootRect.anchoredPosition;
        _baseScale = _textRootRect.localScale;

        if (_debugBinding)
        {
            Debug.Log(
                $"[SuperMeterWidget] Created runtime text meter on '{gameObject.name}' " +
                $"with parent='{_textRootRect.parent?.name}', anchoredPosition={_textRootRect.anchoredPosition}, sizeDelta={_textRootRect.sizeDelta}.",
                this);
        }
    }

    private void DisableLegacyVisuals()
    {
        if (_slider == null)
            _slider = GetComponentInChildren<Slider>(true);

        if (_slider != null)
            _slider.gameObject.SetActive(false);

        if (_label == null)
            _label = GetComponentInChildren<TMP_Text>(true);

        if (_legacyLabel == null)
            _legacyLabel = GetComponentInChildren<Text>(true);

        if (_label != null && _label != _baseLabelRuntime && _label != _fillLabelRuntime)
            _label.enabled = false;

        if (_legacyLabel != null)
            _legacyLabel.enabled = false;
    }

    private void ApplyReferenceLabelStyle(TextMeshProUGUI target)
    {
        if (target == null)
            return;

        TMP_Text reference = ResolveReferenceHudLabel();

        if (reference != null)
        {
            if (reference.font != null)
                target.font = reference.font;

            if (reference.fontSharedMaterial != null)
                target.fontSharedMaterial = reference.fontSharedMaterial;

            target.fontSize = reference.fontSize;
            target.fontStyle = reference.fontStyle;
        }
        else if (TMP_Settings.defaultFontAsset != null)
        {
            target.font = TMP_Settings.defaultFontAsset;
            target.fontSize = 28f;
        }

        target.alignment = TextAlignmentOptions.Center;
    }

    private TMP_Text ResolveReferenceHudLabel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        TMP_Text[] labels = canvas.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text candidate in labels)
        {
            if (candidate == null)
                continue;

            if (candidate == _label || candidate == _baseLabelRuntime || candidate == _fillLabelRuntime)
                continue;

            if (candidate.font != null)
                return candidate;
        }

        return null;
    }

    private static TextMeshProUGUI CreateRuntimeLabel(string objectName, Transform parent)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        return labelObject.GetComponent<TextMeshProUGUI>();
    }

    private void RefreshTextRootPlacement()
    {
        if (_textRootRect == null || _widgetRect == null)
            return;

        Vector2 textSize = GetTextSize();
        _textRootRect.anchorMin = GetWidgetAnchorCenter();
        _textRootRect.anchorMax = _textRootRect.anchorMin;
        _textRootRect.pivot = new Vector2(0.5f, 0f);
        _textRootRect.anchoredPosition = _widgetRect.anchoredPosition + _textOffset;
        _textRootRect.sizeDelta = textSize;
        _textRootRect.localScale = _baseScale;
        _textRootRect.SetAsLastSibling();

        ConfigureFillContentRect();

        if (_readyAnimationRoutine == null)
            _baseAnchoredPosition = _textRootRect.anchoredPosition;

        UpdateFillMask(_currentNormalized);
    }

    private Vector2 GetTextSize()
    {
        if (_widgetRect == null)
            return _textSize;

        float width = _widgetRect.sizeDelta.x > 1f ? _widgetRect.sizeDelta.x : _textSize.x;
        float height = _textSize.y > 1f ? _textSize.y : 40f;
        return new Vector2(width, height);
    }

    private Vector2 GetWidgetAnchorCenter()
    {
        if (_widgetRect == null)
            return new Vector2(0.5f, 1f);

        return (_widgetRect.anchorMin + _widgetRect.anchorMax) * 0.5f;
    }

    private void UpdateTextVisuals(float normalized)
    {
        if (_baseLabelRuntime == null || _fillLabelRuntime == null)
            return;

        _baseLabelRuntime.text = _labelText;
        _fillLabelRuntime.text = _labelText;
        _baseLabelRuntime.color = _baseTextColor;
        _fillLabelRuntime.color = _wasReady ? _readyFlashColor : _fillTextColor;

        UpdateFillMask(normalized);
    }

    private void UpdateFillMask(float normalized)
    {
        if (_fillViewportRect == null || _fillContentRect == null || _textRootRect == null)
            return;

        float width = GetTextSize().x * Mathf.Clamp01(normalized);
        _fillViewportRect.anchorMin = new Vector2(0f, 0f);
        _fillViewportRect.anchorMax = new Vector2(0f, 1f);
        _fillViewportRect.pivot = new Vector2(0f, 0.5f);
        _fillViewportRect.anchoredPosition = Vector2.zero;
        _fillViewportRect.sizeDelta = new Vector2(width, 0f);
    }

    private void ConfigureFillContentRect()
    {
        if (_fillContentRect == null)
            return;

        float width = GetTextSize().x;
        _fillContentRect.anchorMin = new Vector2(0f, 0f);
        _fillContentRect.anchorMax = new Vector2(0f, 1f);
        _fillContentRect.pivot = new Vector2(0f, 0.5f);
        _fillContentRect.anchoredPosition = Vector2.zero;
        _fillContentRect.sizeDelta = new Vector2(width, 0f);
    }

    private void StartReadyAnimation()
    {
        StopReadyAnimation();

        if (_textRootRect == null)
            return;

        _baseAnchoredPosition = _textRootRect.anchoredPosition;
        _baseScale = _textRootRect.localScale;
        _readyAnimationRoutine = StartCoroutine(ReadyAnimationRoutine());
    }

    private void StopReadyAnimation()
    {
        if (_readyAnimationRoutine != null)
        {
            StopCoroutine(_readyAnimationRoutine);
            _readyAnimationRoutine = null;
        }

        ResetReadyVisuals();
    }

    private IEnumerator ReadyAnimationRoutine()
    {
        float shakeDuration = Mathf.Max(0.01f, _readyShakeDuration);
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / shakeDuration);
            float damping = 1f - t;
            float angle = elapsed * _readyShakeFrequency;
            float x = Mathf.Sin(angle) * _readyShakeDistance * damping;
            float y = Mathf.Cos(angle * 0.83f) * (_readyShakeDistance * 0.35f) * damping;
            Color flashColor = Color.Lerp(_readyFlashColor, _fillTextColor, t);

            ApplyTextRootTransform(_baseAnchoredPosition + new Vector2(x, y), _baseScale);
            SetRuntimeTextColors(_baseTextColor, flashColor);
            yield return null;
        }

        while (true)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _readyFlashSpeed * Mathf.PI * 2f);
            Color flashColor = Color.Lerp(_fillTextColor, _readyFlashColor, pulse);
            float motionPhase = Time.unscaledTime * _readyMotionSpeed * Mathf.PI * 2f;
            Vector2 motionOffset = new Vector2(
                Mathf.Sin(motionPhase * 0.7f) * (_readyMotionDistance * 0.35f),
                Mathf.Cos(motionPhase) * _readyMotionDistance);

            ApplyTextRootTransform(_baseAnchoredPosition + motionOffset, _baseScale);
            SetRuntimeTextColors(_baseTextColor, flashColor);
            yield return null;
        }
    }

    private void ResetReadyVisuals()
    {
        ApplyTextRootTransform(_textRootRect != null ? _textRootRect.anchoredPosition : Vector2.zero, _baseScale);
        SetRuntimeTextColors(_baseTextColor, _fillTextColor);
        UpdateFillMask(_currentNormalized);
    }

    private void ApplyTextRootTransform(Vector2 anchoredPosition, Vector3 localScale)
    {
        if (_textRootRect == null)
            return;

        _textRootRect.anchoredPosition = anchoredPosition;
        _textRootRect.localScale = localScale;
    }

    private void SetRuntimeTextColors(Color baseColor, Color fillColor)
    {
        if (_baseLabelRuntime != null)
            _baseLabelRuntime.color = baseColor;

        if (_fillLabelRuntime != null)
            _fillLabelRuntime.color = fillColor;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }
}
