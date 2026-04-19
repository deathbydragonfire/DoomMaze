using UnityEngine;

/// <summary>
/// Contract for all world interactables. Any collider whose owning GameObject implements
/// this interface is eligible to be activated by <see cref="InteractHandler"/>.
/// </summary>
public interface IInteractable
{
    /// <summary>Attempt to interact with this object. Called by the player's <see cref="InteractHandler"/>.</summary>
    void Interact(GameObject interactor);

    /// <summary>
    /// Guards against calling <see cref="Interact"/> when the object is in a locked or
    /// transitioning state (e.g. door already open, key not held, switch already used).
    /// </summary>
    bool CanInteract { get; }
}
