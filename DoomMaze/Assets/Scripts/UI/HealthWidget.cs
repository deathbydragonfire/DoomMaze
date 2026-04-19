using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays current and max health. Pulses the label colour during low-health state
/// using unscaled time so the warning persists through the pause menu.
/// </summary>
public class HealthWidget : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _healthLabel;
    [SerializeField] private Slider          _healthBar;
    [SerializeField] private Color           _lowHealthColor  = Color.red;
    [SerializeField] private Color           _normalColor     = Color.white;
    [SerializeField] private float           _pulseDuration   = 0.6f;

    private Coroutine _pulseCoroutine;
    private int       _cachedMax = 1;

    private void Awake()
    {
        if (_healthLabel == null)
            Debug.LogError("[HealthWidget] _healthLabel is not assigned.");
    }

    /// <summary>Updates the health label and optional slider.</summary>
    public void SetHealth(int current, int max)
    {
        _cachedMax = Mathf.Max(max, 1);

        if (_healthLabel != null)
            _healthLabel.text = current.ToString();

        if (_healthBar != null)
            _healthBar.value = (float)current / _cachedMax;
    }

    /// <summary>Activates or deactivates the low-health colour pulse.</summary>
    public void SetLowHealthWarning(bool isLow)
    {
        if (isLow)
        {
            if (_pulseCoroutine == null)
                _pulseCoroutine = StartCoroutine(PulseRoutine());
        }
        else
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            if (_healthLabel != null)
                _healthLabel.color = _normalColor;
        }
    }

    private IEnumerator PulseRoutine()
    {
        while (true)
        {
            float elapsed = 0f;
            while (elapsed < _pulseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.PingPong(elapsed, _pulseDuration * 0.5f) / (_pulseDuration * 0.5f);
                if (_healthLabel != null)
                    _healthLabel.color = Color.Lerp(_normalColor, _lowHealthColor, t);
                yield return null;
            }
        }
    }
}
