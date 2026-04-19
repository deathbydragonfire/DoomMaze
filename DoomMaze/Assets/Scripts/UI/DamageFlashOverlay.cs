using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen red <see cref="Image"/> overlay that spikes to <see cref="_peakAlpha"/>
/// when the player takes damage and fades out over <see cref="_fadeDuration"/> seconds.
/// Uses <c>Time.unscaledDeltaTime</c> so it resolves correctly during slow-motion or
/// while paused via <see cref="PauseManager"/>.
/// Subscribes to <see cref="PlayerDamagedEvent"/> via the EventBus.
/// </summary>
[RequireComponent(typeof(Image))]
public class DamageFlashOverlay : MonoBehaviour
{
    [SerializeField] private float _peakAlpha    = 0.45f;
    [SerializeField] private float _fadeDuration = 0.4f;

    private Image _image;
    private float _currentAlpha;

    // Pre-computed fade rate to avoid per-frame division.
    private float _fadeRate;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _image    = GetComponent<Image>();
        _fadeRate = _peakAlpha / _fadeDuration;

        SetAlpha(0f);

        EventBus<PlayerDamagedEvent>.Subscribe(OnPlayerDamaged);
    }

    private void OnDestroy()
    {
        EventBus<PlayerDamagedEvent>.Unsubscribe(OnPlayerDamaged);
    }

    private void Update()
    {
        if (_currentAlpha <= 0f) return;

        _currentAlpha = Mathf.Max(0f, _currentAlpha - _fadeRate * Time.unscaledDeltaTime);
        SetAlpha(_currentAlpha);
    }

    // ── EventBus Handler ──────────────────────────────────────────────────────

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        // Highest alpha wins — re-trigger always fully spikes the flash.
        _currentAlpha = _peakAlpha;
        SetAlpha(_currentAlpha);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SetAlpha(float alpha)
    {
        Color c = _image.color;
        c.a         = alpha;
        _image.color = c;
    }
}
