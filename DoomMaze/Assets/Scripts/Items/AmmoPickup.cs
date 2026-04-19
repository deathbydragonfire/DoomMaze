using UnityEngine;

/// <summary>
/// Concrete pickup that adds ammo of a specific type to <see cref="PlayerInventory"/>.
/// Skips collection when the player is already at <see cref="AmmoPickupData.MaxCarryCount"/>.
/// </summary>
public class AmmoPickup : PickupBase
{
    [SerializeField] private AmmoPickupData _data;

    protected override void ExecutePickup(PlayerInventory inventory)
    {
        if (_data == null)
        {
            Debug.LogWarning($"[AmmoPickup] AmmoPickupData is not assigned on {gameObject.name}.");
            return;
        }

        if (inventory.GetAmmo(_data.AmmoTypeId) >= _data.MaxCarryCount)
            return;

        inventory.AddAmmo(_data.AmmoTypeId, _data.Amount);
    }
}
