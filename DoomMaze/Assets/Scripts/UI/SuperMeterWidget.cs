using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placeholder slider widget for a future super/ability meter.
/// Starts hidden. Included to complete the HUD widget set.
/// </summary>
public class SuperMeterWidget : MonoBehaviour
{
    [SerializeField] private Slider     _slider;
    [SerializeField] private GameObject _container;

    private void Awake()
    {
        if (_container != null)
            _container.SetActive(false);
    }

    /// <summary>Sets the meter fill to a normalized 0–1 value and shows the container.</summary>
    public void SetValue(float normalized)
    {
        if (_container != null)
            _container.SetActive(true);

        if (_slider != null)
            _slider.value = Mathf.Clamp01(normalized);
    }
}
