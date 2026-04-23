using UnityEngine;

/// <summary>
/// Concrete pickup that grants a weapon to the player's inventory and raises
/// <see cref="WeaponPickedUpEvent"/> so <see cref="WeaponSwitcher"/> can register the slot.
/// </summary>
public class WeaponPickup : PickupBase
{
    [SerializeField] private WeaponData _weaponData;

    protected override bool ExecutePickup(PlayerInventory inventory)
    {
        if (_weaponData == null)
        {
            Debug.LogWarning($"[WeaponPickup] WeaponData is not assigned on {gameObject.name}.");
            return false;
        }

        if (inventory.HasItem(_weaponData.WeaponId))
            return false;

        inventory.AddItem(_weaponData.WeaponId);

        EventBus<WeaponPickedUpEvent>.Raise(new WeaponPickedUpEvent { WeaponData = _weaponData });
        return true;
    }
}
