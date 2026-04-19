using UnityEngine;

/// <summary>
/// Concrete pickup that adds a key item ID to <see cref="PlayerInventory"/>.
/// <see cref="Door"/> checks for this ID before allowing interaction.
/// </summary>
public class KeyPickup : PickupBase
{
    [SerializeField] private string _keyId;

    protected override void ExecutePickup(PlayerInventory inventory)
    {
        if (string.IsNullOrEmpty(_keyId))
        {
            Debug.LogWarning($"[KeyPickup] _keyId is not set on {gameObject.name}.");
            return;
        }

        inventory.AddItem(_keyId);
    }
}
