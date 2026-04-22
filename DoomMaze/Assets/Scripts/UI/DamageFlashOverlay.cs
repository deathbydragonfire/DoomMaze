using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen red <see cref="Image"/> overlay that spikes to <see cref="_peakAlpha"/>
/// when the player takes damage and fades out over <see cref="_fadeDuration"/> seconds.
/// Also fires a much subtler flash on a successful melee hit via <see cref="MeleeHitEvent"/>.
/// Uses <c>Time.unscaledDeltaTime</c> so it resolves correctly during slow-motion or
/// while paused via <see cref="PauseManager"/>.
/// Subscribes to <see cref="PlayerDamagedEvent"/> and <see cref="MeleeHitEvent"/> via the EventBus.
/// </summary>
[RequireComponent(typeof(Image))]
public class DamageFlashOverlay : MonoBehaviour
{
    [SerializeField] private float _peakAlpha       = 0.45f;
    [SerializeField] private float _fadeDuration    = 0.4f;
    [SerializeField] private float _meleeHitAlpha   = 0.08f;
    [SerializeField] private float _meleeHitFadeDuration = 0.25f;

    private Image _image;
    private float _currentAlpha;
    private float _fadeRate;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _image    = GetComponent<Image>();
        _fadeRate = _peakAlpha / _fadeDuration;

        SetAlpha(0f);

        EventBus<PlayerDamagedEvent>.Subscribe(OnPlayerDamaged);
        EventBus<MeleeHitEvent>.Subscribe(OnMeleeHit);
    }

    private void OnDestroy()
    {
        EventBus<PlayerDamagedEvent>.Unsubscribe(OnPlayerDamaged);
        EventBus<MeleeHitEvent>.Unsubscribe(OnMeleeHit);
    }

    private void Update()
    {
        if (_currentAlpha <= 0f) return;

        _currentAlpha = Mathf.Max(0f, _currentAlpha - _fadeRate * Time.unscaledDeltaTime);
        SetAlpha(_currentAlpha);
    }

    // ── EventBus Handlers ─────────────────────────────────────────────────────

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        _fadeRate     = _peakAlpha / _fadeDuration;
        _currentAlpha = _peakAlpha;
        SetAlpha(_currentAlpha);
    }

    private void OnMeleeHit(MeleeHitEvent e)
    {
        if (_currentAlpha >= _meleeHitAlpha) return;

        _fadeRate     = _meleeHitAlpha / _meleeHitFadeDuration;
        _currentAlpha = _meleeHitAlpha;
        SetAlpha(_currentAlpha);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SetAlpha(float alpha)
    {
        Color c  = _image.color;
        c.a      = alpha;
        _image.color = c;
    }
}
