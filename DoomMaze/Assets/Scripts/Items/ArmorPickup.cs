using UnityEngine;

/// <summary>
/// Concrete pickup that adds armor via <see cref="ArmorComponent.AddArmor"/>.
/// </summary>
public class ArmorPickup : PickupBase
{
    private static readonly Color ArmorFeedColor = new Color(0.35f, 0.82f, 1f, 1f);

    [SerializeField] private ArmorPickupData _data;

    protected override bool ExecutePickup(PlayerInventory inventory)
    {
        if (_data == null)
        {
            Debug.LogWarning($"[ArmorPickup] ArmorPickupData is not assigned on {gameObject.name}.");
            return false;
        }

        ArmorComponent armor = inventory.GetComponent<ArmorComponent>();

        if (armor == null)
        {
            Debug.LogWarning($"[ArmorPickup] No ArmorComponent found on player {inventory.gameObject.name}.");
            return false;
        }

        if (armor.CurrentArmor >= armor.MaxArmor)
            return false;

        int previousArmor = armor.CurrentArmor;
        armor.AddArmor(_data.ArmorAmount);

        int addedArmor = armor.CurrentArmor - previousArmor;
        if (addedArmor <= 0)
            return false;

        string displayName = _data != null && !string.IsNullOrWhiteSpace(_data.DisplayName)
            ? _data.DisplayName.ToUpperInvariant()
            : "ARMOR";

        EventBus<PickupFeedMessageEvent>.Raise(new PickupFeedMessageEvent
        {
            Message = $"+{addedArmor} {displayName}",
            Tint    = ArmorFeedColor
        });

        return true;
    }
}
