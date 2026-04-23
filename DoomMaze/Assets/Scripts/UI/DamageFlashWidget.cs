using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen red overlay that flashes on player damage. Uses unscaled time so
/// the fade runs correctly while the game is paused.
/// </summary>
public class DamageFlashWidget : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Color _damageColor      = new Color(1f, 0.08f, 0.08f, 0f);
    [SerializeField] private float _damagePeakAlpha  = 0.45f;
    [SerializeField] private float _damageFadeDuration = 0.5f;
    [SerializeField] private Color _pickupColor      = new Color(1f, 0.86f, 0.28f, 0f);
    [SerializeField] private float _pickupPeakAlpha  = 0.16f;
    [SerializeField] private float _pickupFadeDuration = 0.28f;

    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (_image == null)
            Debug.LogError("[DamageFlashWidget] _image is not assigned.");

        if (_image != null)
            SetColorAndAlpha(_damageColor, 0f);
    }

    /// <summary>Triggers the default damage edge flash.</summary>
    public void Flash()
    {
        Flash(_damageColor, _damagePeakAlpha, _damageFadeDuration);
    }

    public void FlashPickup()
    {
        Flash(_pickupColor, _pickupPeakAlpha, _pickupFadeDuration);
    }

    private void Flash(Color color, float peakAlpha, float fadeDuration)
    {
        if (_image == null) return;

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        SetColorAndAlpha(color, peakAlpha);
        _fadeRoutine = StartCoroutine(FadeRoutine(color, peakAlpha, fadeDuration));
    }

    private IEnumerator FadeRoutine(Color color, float peakAlpha, float fadeDuration)
    {
        float alpha = peakAlpha;

        while (alpha > 0f)
        {
            alpha -= Time.unscaledDeltaTime / Mathf.Max(fadeDuration, 0.0001f);
            SetColorAndAlpha(color, Mathf.Max(alpha, 0f));
            yield return null;
        }

        _fadeRoutine = null;
    }

    private void SetColorAndAlpha(Color color, float alpha)
    {
        color.a = alpha;
        _image.color = color;
    }
}
