using UnityEngine;

/// <summary>
/// Concrete pickup that adds ammo to one or more ammo pools on <see cref="PlayerInventory"/>.
/// When <see cref="_ammoTypes"/> is configured, the pickup grants the same amount to every listed ammo type.
/// Otherwise it falls back to the legacy single-type data stored on <see cref="_data"/>.
/// </summary>
public class AmmoPickup : PickupBase
{
    private static readonly Color AmmoFeedColor = new Color(1f, 0.84f, 0.34f, 1f);

    [SerializeField] private AmmoPickupData _data;
    [SerializeField] private AmmoTypeData[] _ammoTypes;

    protected override bool ExecutePickup(PlayerInventory inventory)
    {
        if (_data == null)
        {
            Debug.LogWarning($"[AmmoPickup] AmmoPickupData is not assigned on {gameObject.name}.");
            return false;
        }

        int totalAdded = 0;

        if (_ammoTypes != null && _ammoTypes.Length > 0)
        {
            for (int i = 0; i < _ammoTypes.Length; i++)
            {
                AmmoTypeData ammoType = _ammoTypes[i];
                if (ammoType == null || string.IsNullOrEmpty(ammoType.AmmoId))
                    continue;

                totalAdded += inventory.AddAmmo(ammoType.AmmoId, _data.Amount, ammoType.MaxCarryCount);
            }

            if (totalAdded > 0)
            {
                RaiseFeedMessage(totalAdded);
                return true;
            }

            return false;
        }

        if (string.IsNullOrEmpty(_data.AmmoTypeId))
        {
            Debug.LogWarning($"[AmmoPickup] AmmoTypeId is not set on {gameObject.name}.");
            return false;
        }

        totalAdded = inventory.AddAmmo(_data.AmmoTypeId, _data.Amount, _data.MaxCarryCount);
        if (totalAdded <= 0)
            return false;

        RaiseFeedMessage(totalAdded);
        return true;
    }

    private static void RaiseFeedMessage(int totalAdded)
    {
        EventBus<PickupFeedMessageEvent>.Raise(new PickupFeedMessageEvent
        {
            Message = $"+{totalAdded} AMMO",
            Tint    = AmmoFeedColor
        });
    }
}
