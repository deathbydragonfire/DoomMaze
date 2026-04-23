using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Static crosshair that expands on <see cref="WeaponFiredEvent"/> and contracts
/// back to its base size via a frame-smooth coroutine.
/// When the active weapon is a <see cref="FlamethrowerWeapon"/>, this widget also
/// renders a small vertical heat bar beside the crosshair.
/// </summary>
public class CrosshairWidget : MonoBehaviour
{
    [SerializeField] private RectTransform _crosshairRect;
    [SerializeField] private float         _baseSize         = 24f;
    [SerializeField] private float         _expandAmount     = 12f;
    [SerializeField] private float         _contractDuration = 0.15f;

    [Header("Flamethrower Heat Bar")]
    [SerializeField] private Vector2 _heatBarOffset = new Vector2(28f, 0f);
    [SerializeField] private Vector2 _heatBarSize = new Vector2(10f, 54f);
    [SerializeField] private float _heatBarPadding = 2f;
    [SerializeField] private Color _heatBarBackground = new Color(0.08f, 0.04f, 0.02f, 0.75f);
    [SerializeField] private Color _heatBarFillCold = new Color(1f, 0.84f, 0.2f, 0.92f);
    [SerializeField] private Color _heatBarFillHot = new Color(1f, 0.18f, 0.05f, 1f);
    [SerializeField] private Color _heatBarOverheated = new Color(1f, 0.05f, 0.05f, 1f);

    private Coroutine _contractRoutine;
    private RectTransform _heatBarRoot;
    private RectTransform _heatBarFillRect;
    private RawImage _heatBarBackgroundImage;
    private RawImage _heatBarFillImage;
    private PlayerCombat _playerCombat;

    private void Awake()
    {
        if (_crosshairRect == null)
            Debug.LogError("[CrosshairWidget] _crosshairRect is not assigned.");

        EnsureHeatBar();
        SetHeatBarVisible(false);
    }

    private void Update()
    {
        ResolvePlayerCombat();
        UpdateFlamethrowerHeatBar();
    }

    private void OnEnable()
    {
        EventBus<WeaponFiredEvent>.Subscribe(OnWeaponFiredEvent);
    }

    private void OnDisable()
    {
        EventBus<WeaponFiredEvent>.Unsubscribe(OnWeaponFiredEvent);
    }

    private void OnWeaponFiredEvent(WeaponFiredEvent e)
    {
        OnWeaponFired();
    }

    /// <summary>Expands the crosshair and starts the contraction coroutine.</summary>
    public void OnWeaponFired()
    {
        if (_crosshairRect == null) return;

        float expandedSize = _baseSize + _expandAmount;
        _crosshairRect.sizeDelta = new Vector2(expandedSize, expandedSize);

        if (_contractRoutine != null)
            StopCoroutine(_contractRoutine);

        _contractRoutine = StartCoroutine(ContractRoutine());
    }

    private IEnumerator ContractRoutine()
    {
        Vector2 baseSize     = new Vector2(_baseSize, _baseSize);
        Vector2 expandedSize = new Vector2(_baseSize + _expandAmount, _baseSize + _expandAmount);
        float   elapsed      = 0f;

        while (elapsed < _contractDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _contractDuration);
            _crosshairRect.sizeDelta = Vector2.Lerp(expandedSize, baseSize, t);
            yield return null;
        }

        _crosshairRect.sizeDelta = baseSize;
        _contractRoutine = null;
    }

    private void ResolvePlayerCombat()
    {
        if (_playerCombat != null)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            _playerCombat = player.GetComponent<PlayerCombat>();
    }

    private void UpdateFlamethrowerHeatBar()
    {
        if (_heatBarRoot == null || _heatBarFillRect == null || _heatBarFillImage == null)
            return;

        FlamethrowerWeapon flamethrower = _playerCombat?.ActiveWeapon as FlamethrowerWeapon;
        if (flamethrower == null)
        {
            SetHeatBarVisible(false);
            return;
        }

        SetHeatBarVisible(true);

        float normalizedHeat = flamethrower.HeatNormalized;
        float availableHeight = Mathf.Max(0f, _heatBarSize.y - (_heatBarPadding * 2f));

        _heatBarFillRect.sizeDelta = new Vector2(
            Mathf.Max(0f, _heatBarSize.x - (_heatBarPadding * 2f)),
            availableHeight * normalizedHeat);

        _heatBarFillImage.color = flamethrower.IsOverheated
            ? _heatBarOverheated
            : Color.Lerp(_heatBarFillCold, _heatBarFillHot, normalizedHeat);
    }

    private void EnsureHeatBar()
    {
        if (_crosshairRect == null || _heatBarRoot != null)
            return;

        GameObject rootObject = new GameObject("FlamethrowerHeatBar", typeof(RectTransform));
        rootObject.layer = gameObject.layer;
        _heatBarRoot = rootObject.GetComponent<RectTransform>();
        _heatBarRoot.SetParent(_crosshairRect.parent, false);
        _heatBarRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _heatBarRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _heatBarRoot.pivot = new Vector2(0.5f, 0.5f);
        _heatBarRoot.anchoredPosition = _heatBarOffset;
        _heatBarRoot.sizeDelta = _heatBarSize;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
        backgroundObject.layer = gameObject.layer;
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.SetParent(_heatBarRoot, false);
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        _heatBarBackgroundImage = backgroundObject.GetComponent<RawImage>();
        _heatBarBackgroundImage.texture = Texture2D.whiteTexture;
        _heatBarBackgroundImage.color = _heatBarBackground;
        _heatBarBackgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(RawImage));
        fillObject.layer = gameObject.layer;
        _heatBarFillRect = fillObject.GetComponent<RectTransform>();
        _heatBarFillRect.SetParent(_heatBarRoot, false);
        _heatBarFillRect.anchorMin = new Vector2(0.5f, 0f);
        _heatBarFillRect.anchorMax = new Vector2(0.5f, 0f);
        _heatBarFillRect.pivot = new Vector2(0.5f, 0f);
        _heatBarFillRect.anchoredPosition = new Vector2(0f, _heatBarPadding);
        _heatBarFillRect.sizeDelta = new Vector2(
            Mathf.Max(0f, _heatBarSize.x - (_heatBarPadding * 2f)),
            0f);

        _heatBarFillImage = fillObject.GetComponent<RawImage>();
        _heatBarFillImage.texture = Texture2D.whiteTexture;
        _heatBarFillImage.color = _heatBarFillCold;
        _heatBarFillImage.raycastTarget = false;
    }

    private void SetHeatBarVisible(bool visible)
    {
        if (_heatBarRoot != null && _heatBarRoot.gameObject.activeSelf != visible)
            _heatBarRoot.gameObject.SetActive(visible);
    }
}
