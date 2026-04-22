using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD component that reacts to grapple events via <see cref="EventBus{T}"/>.
/// Shows a slider fill meter while the player mashes during the grapple window,
/// and hides it when the grapple is resolved or released.
/// </summary>
public class GrappleMashUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Slider     _slider;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        EventBus<GrappleMashProgressEvent>.Subscribe(OnMashProgress);
        EventBus<GrappleHookedEvent>.Subscribe(OnHooked);
        EventBus<GrappleReleasedEvent>.Subscribe(OnReleased);
        EventBus<GrapplePulledEvent>.Subscribe(OnPulled);
    }

    private void OnDisable()
    {
        EventBus<GrappleMashProgressEvent>.Unsubscribe(OnMashProgress);
        EventBus<GrappleHookedEvent>.Unsubscribe(OnHooked);
        EventBus<GrappleReleasedEvent>.Unsubscribe(OnReleased);
        EventBus<GrapplePulledEvent>.Unsubscribe(OnPulled);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnHooked(GrappleHookedEvent e)
    {
        if (_panel != null)  _panel.SetActive(true);
        if (_slider != null) _slider.value = 0f;
    }

    private void OnMashProgress(GrappleMashProgressEvent e)
    {
        if (_slider != null) _slider.value = e.Progress;
    }

    private void OnReleased(GrappleReleasedEvent e)
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnPulled(GrapplePulledEvent e)
    {
        if (_panel != null) _panel.SetActive(false);
    }
}
