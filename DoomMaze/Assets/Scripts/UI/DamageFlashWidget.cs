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
    [SerializeField] private float _peakAlpha    = 0.45f;
    [SerializeField] private float _fadeDuration = 0.5f;

    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (_image == null)
            Debug.LogError("[DamageFlashWidget] _image is not assigned.");

        if (_image != null)
            SetAlpha(0f);
    }

    /// <summary>Triggers a full-screen damage flash that fades out over <see cref="_fadeDuration"/>.</summary>
    public void Flash()
    {
        if (_image == null) return;

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        SetAlpha(_peakAlpha);
        _fadeRoutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        float alpha = _peakAlpha;

        while (alpha > 0f)
        {
            alpha -= Time.unscaledDeltaTime / _fadeDuration;
            SetAlpha(Mathf.Max(alpha, 0f));
            yield return null;
        }

        _fadeRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        Color c = _image.color;
        c.a = alpha;
        _image.color = c;
    }
}
