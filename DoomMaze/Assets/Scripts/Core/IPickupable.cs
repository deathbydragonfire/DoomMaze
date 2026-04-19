/// <summary>
/// Contract for all pickup types. Kept separate from <see cref="IInteractable"/> because
/// pickups are auto-triggered on <c>OnTriggerEnter</c>, not player-initiated.
/// </summary>
public interface IPickupable
{
    /// <summary>Apply this pickup's effect to the given <paramref name="inventory"/>.</summary>
    void OnPickup(PlayerInventory inventory);
}
