using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent UI vignette <see cref="Image"/> that fades in when the player is at low
/// health and fades back out when health is restored.
/// Alpha is lerped smoothly each frame using <see cref="_transitionSpeed"/> so the
/// transition never pops. Uses <c>Time.unscaledDeltaTime</c> for pause-safety.
/// Subscribes to <see cref="PlayerLowHealthEvent"/> via the EventBus.
/// </summary>
[RequireComponent(typeof(Image))]
public class LowHealthVignette : MonoBehaviour
{
    [SerializeField] private float _targetAlpha     = 0.35f;
    [SerializeField] private float _transitionSpeed = 2f;

    private Image _image;
    private bool  _targetVisible;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _image = GetComponent<Image>();

        SetAlpha(0f);

        EventBus<PlayerLowHealthEvent>.Subscribe(OnPlayerLowHealth);
    }

    private void OnDestroy()
    {
        EventBus<PlayerLowHealthEvent>.Unsubscribe(OnPlayerLowHealth);
    }

    private void Update()
    {
        float goal         = _targetVisible ? _targetAlpha : 0f;
        float currentAlpha = _image.color.a;

        if (Mathf.Approximately(currentAlpha, goal)) return;

        float next = Mathf.MoveTowards(currentAlpha, goal, _transitionSpeed * Time.unscaledDeltaTime);
        SetAlpha(next);
    }

    // ── EventBus Handler ──────────────────────────────────────────────────────

    private void OnPlayerLowHealth(PlayerLowHealthEvent e)
    {
        _targetVisible = e.IsLow;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SetAlpha(float alpha)
    {
        Color c = _image.color;
        c.a          = alpha;
        _image.color  = c;
    }
}
