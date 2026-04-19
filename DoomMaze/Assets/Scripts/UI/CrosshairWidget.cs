using System.Collections;
using UnityEngine;

/// <summary>
/// Static crosshair that expands on <see cref="WeaponFiredEvent"/> and contracts
/// back to its base size via a frame-smooth coroutine.
/// </summary>
public class CrosshairWidget : MonoBehaviour
{
    [SerializeField] private RectTransform _crosshairRect;
    [SerializeField] private float         _baseSize         = 24f;
    [SerializeField] private float         _expandAmount     = 12f;
    [SerializeField] private float         _contractDuration = 0.15f;

    private Coroutine _contractRoutine;

    private void Awake()
    {
        if (_crosshairRect == null)
            Debug.LogError("[CrosshairWidget] _crosshairRect is not assigned.");
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
}
