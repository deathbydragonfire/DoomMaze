using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Implements <see cref="IInteractable"/>. Fires a <see cref="UnityEvent"/> on activation.
/// Supports one-shot (single-use) or repeatable modes via <see cref="_oneShot"/>.
/// </summary>
public class Switch : MonoBehaviour, IInteractable
{
    [SerializeField] private UnityEvent _onActivated;
    [SerializeField] private bool       _oneShot = true;

    public bool CanInteract { get; private set; } = true;

    /// <summary>
    /// Invokes <see cref="_onActivated"/>, raises <see cref="SwitchActivatedEvent"/>,
    /// and disables further interaction if <see cref="_oneShot"/> is true.
    /// </summary>
    public void Interact(GameObject interactor)
    {
        _onActivated?.Invoke();

        EventBus<SwitchActivatedEvent>.Raise(new SwitchActivatedEvent());

        if (_oneShot)
            CanInteract = false;
    }
}
