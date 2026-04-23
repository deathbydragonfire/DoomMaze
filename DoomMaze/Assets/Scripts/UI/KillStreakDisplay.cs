using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Persistent kill-streak counter that stays on screen until the player takes damage.
/// Supports either named streak labels or a simple multiplier counter.
/// Creates fallback UI labels at runtime when the scene object is missing them.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class KillStreakDisplay : MonoBehaviour
{
    public enum StreakDisplayMode
    {
        NamedLabels,
        MultiplierCounter
    }

    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private TextMeshProUGUI _countLabel;
    [SerializeField] private TMP_FontAsset   _numberFont;
    [SerializeField] private TMP_FontAsset   _namedFont;
    [SerializeField] private StreakDisplayMode _displayMode = StreakDisplayMode.MultiplierCounter;
    [SerializeField] private string          _counterHeader = "KILL STREAK:";
    [SerializeField] private string[]        _namedStreakLabels =
    {
        "DOUBLE KILL",
        "TRIPLE KILL",
        "QUAD KILL",
        "PENTA KILL",
        "RAMPAGE",
        "MASSACRE",
        "GODLIKE",
        "APOCALYPTIC"
    };
    [SerializeField] private float           _punchScale                 = 1.08f;
    [SerializeField] private float           _scaleDuration              = 0.12f;
    [SerializeField] private Vector2         _defaultSize                = new Vector2(420f, 140f);
    [SerializeField] private float           _counterWordFontSize        = 28f;
    [SerializeField] private float           _counterNumberFontSize      = 58f;
    [SerializeField] private float           _namedValueFontSize         = 44f;
    [SerializeField] private Color           _killFlashColor             = new Color(1f, 0.22f, 0.22f, 1f);
    [SerializeField] private float           _nameChangePunchScale       = 1.16f;
    [SerializeField] private float           _nameChangeDuration         = 0.22f;
    [SerializeField] private float           _nameChangeShakeDistance    = 12f;
    [SerializeField] private float           _nameChangeShakeCycles      = 3.5f;

    private CanvasGroup _canvasGroup;
    private Coroutine   _animRoutine;
    private Vector2     _baseAnchoredPosition;

    private const string WordLabelName  = "WordLabel";
    private const string CountLabelName = "CountLabel";
#if UNITY_EDITOR
    private const string NamedFontAssetPath = "Assets/Fonts/Unutterable_Font_1_07/TrueType (.ttf)/Unutterable-Regular SDF 1.asset";
#endif

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        RectTransform rect = transform as RectTransform;
        if (rect != null)
            _baseAnchoredPosition = rect.anchoredPosition;

        EnsureCanvasRenderer(gameObject);
        EnsureDisplayBounds();
        EnsureLabels();
        EnsureGraphicsAreSafe();
        ApplyModeLayout();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_namedFont == null)
            _namedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NamedFontAssetPath);
    }
#endif

    private void OnEnable()
    {
        EventBus<KillStreakEvent>.Subscribe(OnKillStreak);
    }

    private void OnDisable()
    {
        EventBus<KillStreakEvent>.Unsubscribe(OnKillStreak);

        if (_animRoutine != null)
        {
            StopCoroutine(_animRoutine);
            _animRoutine = null;
        }

        ResetVisualState();
    }

    /// <summary>Displays the persistent streak counter for the given kill count.</summary>
    public void ShowStreak(int count)
    {
        if (count < 2 || _label == null || _countLabel == null)
            return;

        ApplyModeLayout();
        bool shouldPlayNameChangeAnimation = false;

        if (_displayMode == StreakDisplayMode.MultiplierCounter)
        {
            _label.text = _counterHeader;
            _countLabel.text = $"{count}x";
            _countLabel.gameObject.SetActive(true);
        }
        else
        {
            string nextNamedLabel = GetNamedStreakLabel(count);
            shouldPlayNameChangeAnimation = !string.Equals(_countLabel.text, nextNamedLabel);
            _label.text = _counterHeader;
            _countLabel.text = nextNamedLabel;
            _countLabel.gameObject.SetActive(true);
        }

        _canvasGroup.alpha = 1f;

        if (_animRoutine != null)
            StopCoroutine(_animRoutine);

        _animRoutine = StartCoroutine(shouldPlayNameChangeAnimation ? NameChangeRoutine() : PunchRoutine());
    }

    private void OnKillStreak(KillStreakEvent e)
    {
        if (e.StreakCount >= 2)
        {
            ShowStreak(e.StreakCount);
            return;
        }

        if (_animRoutine != null)
        {
            StopCoroutine(_animRoutine);
            _animRoutine = null;
        }

        _countLabel.gameObject.SetActive(true);
        ResetVisualState();
    }

    private IEnumerator PunchRoutine()
    {
        transform.localScale = Vector3.one * _punchScale;

        float elapsed = 0f;
        while (elapsed < _scaleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _scaleDuration);
            transform.localScale = Vector3.one * Mathf.Lerp(_punchScale, 1f, t);
            ApplyFlashColors(t);
            yield return null;
        }

        transform.localScale = Vector3.one;
        ApplyFlashColors(1f);
        _animRoutine = null;
    }

    private IEnumerator NameChangeRoutine()
    {
        RectTransform rect = transform as RectTransform;
        Vector2 basePosition = rect != null ? _baseAnchoredPosition : Vector2.zero;
        transform.localScale = Vector3.one * _nameChangePunchScale;

        float elapsed = 0f;
        while (elapsed < _nameChangeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _nameChangeDuration);
            float damping = 1f - t;
            float shake = Mathf.Sin(t * Mathf.PI * 2f * _nameChangeShakeCycles) * _nameChangeShakeDistance * damping;

            transform.localScale = Vector3.one * Mathf.Lerp(_nameChangePunchScale, 1f, t);
            if (rect != null)
                rect.anchoredPosition = basePosition + new Vector2(shake, 0f);
            ApplyFlashColors(t);

            yield return null;
        }

        if (rect != null)
            rect.anchoredPosition = basePosition;

        transform.localScale = Vector3.one;
        ApplyFlashColors(1f);
        _animRoutine = null;
    }

    private void EnsureDisplayBounds()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return;

        if (rect.sizeDelta.x < _defaultSize.x || rect.sizeDelta.y < _defaultSize.y)
            rect.sizeDelta = _defaultSize;
    }

    private void EnsureLabels()
    {
        if (_label == null)
            _label = FindChildLabel(WordLabelName);

        if (_countLabel == null)
            _countLabel = FindChildLabel(CountLabelName);

        if (_label == null)
            _label = CreateLabel(WordLabelName);

        if (_countLabel == null)
            _countLabel = CreateLabel(CountLabelName);
    }

    private TextMeshProUGUI FindChildLabel(string objectName)
    {
        Transform child = transform.Find(objectName);
        if (child != null)
            return child.GetComponent<TextMeshProUGUI>();

        return null;
    }

    private TextMeshProUGUI CreateLabel(string objectName)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(transform, false);

        TextMeshProUGUI createdLabel = labelObject.GetComponent<TextMeshProUGUI>();
        createdLabel.text = string.Empty;
        createdLabel.raycastTarget = false;

        Outline outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(3f, -3f);

        return createdLabel;
    }

    private void ApplyModeLayout()
    {
        if (_displayMode == StreakDisplayMode.MultiplierCounter)
        {
            ConfigureCounterWordLabel(_label);
            ConfigureCounterValueLabel(_countLabel);
            return;
        }

        ConfigureCounterWordLabel(_label);
        ConfigureNamedValueLabel(_countLabel);
    }

    private void ConfigureCounterWordLabel(TextMeshProUGUI label)
    {
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (label.font == null)
            label.font = TMP_Settings.defaultFontAsset;

        label.fontSize = _counterWordFontSize;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = new Color(1f, 0.86f, 0.24f, 1f);
        label.raycastTarget = false;
    }

    private void ConfigureCounterValueLabel(TextMeshProUGUI label)
    {
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.6f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        label.font = _numberFont != null ? _numberFont : TMP_Settings.defaultFontAsset;
        label.fontSize = _counterNumberFontSize;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = new Color(1f, 0.95f, 0.66f, 1f);
        label.raycastTarget = false;
    }

    private void ConfigureNamedValueLabel(TextMeshProUGUI label)
    {
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.6f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        label.font = _namedFont != null ? _namedFont : TMP_Settings.defaultFontAsset;

        label.fontSize = _namedValueFontSize;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = new Color(1f, 0.86f, 0.24f, 1f);
        label.raycastTarget = false;
    }

    private string GetNamedStreakLabel(int count)
    {
        if (count <= 1)
            return "KILL STREAK";

        if (_namedStreakLabels != null && _namedStreakLabels.Length > 0)
        {
            int index = Mathf.Clamp(count - 2, 0, _namedStreakLabels.Length - 1);
            string label = _namedStreakLabels[index];
            if (!string.IsNullOrWhiteSpace(label))
                return label;
        }

        return $"{count}x STREAK";
    }

    private void EnsureGraphicsAreSafe()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            EnsureCanvasRenderer(graphic.gameObject);
            graphic.raycastTarget = false;
        }
    }

    private void ApplyFlashColors(float t)
    {
        if (_label != null)
            _label.color = Color.Lerp(_killFlashColor, GetWordBaseColor(), Mathf.Clamp01(t));

        if (_countLabel != null)
            _countLabel.color = Color.Lerp(_killFlashColor, GetValueBaseColor(), Mathf.Clamp01(t));
    }

    private Color GetWordBaseColor()
    {
        return new Color(1f, 0.86f, 0.24f, 1f);
    }

    private Color GetValueBaseColor()
    {
        if (_displayMode == StreakDisplayMode.MultiplierCounter)
            return new Color(1f, 0.95f, 0.66f, 1f);

        return new Color(1f, 0.86f, 0.24f, 1f);
    }

    private void ResetVisualState()
    {
        transform.localScale = Vector3.one;

        RectTransform rect = transform as RectTransform;
        if (rect != null)
            rect.anchoredPosition = _baseAnchoredPosition;

        ApplyFlashColors(1f);
        _canvasGroup.alpha = 0f;
    }

    private static void EnsureCanvasRenderer(GameObject target)
    {
        if (target != null && target.GetComponent<CanvasRenderer>() == null)
            target.AddComponent<CanvasRenderer>();
    }
}
