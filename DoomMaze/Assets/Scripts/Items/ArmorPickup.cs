using UnityEngine;

/// <summary>
/// Concrete pickup that adds armor via <see cref="ArmorComponent.AddArmor"/>.
/// </summary>
public class ArmorPickup : PickupBase
{
    [SerializeField] private ArmorPickupData _data;

    protected override void ExecutePickup(PlayerInventory inventory)
    {
        if (_data == null)
        {
            Debug.LogWarning($"[ArmorPickup] ArmorPickupData is not assigned on {gameObject.name}.");
            return;
        }

        ArmorComponent armor = inventory.GetComponent<ArmorComponent>();

        if (armor == null)
        {
            Debug.LogWarning($"[ArmorPickup] No ArmorComponent found on player {inventory.gameObject.name}.");
            return;
        }

        armor.AddArmor(_data.ArmorAmount);
    }
}
