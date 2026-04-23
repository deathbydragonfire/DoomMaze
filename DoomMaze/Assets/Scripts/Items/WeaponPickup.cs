using UnityEngine;

/// <summary>
/// Concrete pickup that grants a weapon to the player's inventory and raises
/// <see cref="WeaponPickedUpEvent"/> so <see cref="WeaponSwitcher"/> can register the slot.
/// </summary>
public class WeaponPickup : PickupBase
{
    private static readonly Color WeaponFeedColor = new Color(1f, 0.52f, 0.18f, 1f);

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

        string displayName = !string.IsNullOrWhiteSpace(_weaponData.DisplayName)
            ? _weaponData.DisplayName.ToUpperInvariant()
            : "WEAPON";

        EventBus<WeaponPickedUpEvent>.Raise(new WeaponPickedUpEvent { WeaponData = _weaponData });
        EventBus<PickupFeedMessageEvent>.Raise(new PickupFeedMessageEvent
        {
            Message = $"PICKED UP {displayName}",
            Tint    = WeaponFeedColor
        });

        return true;
    }
}
