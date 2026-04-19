using UnityEngine;

/// <summary>
/// Manages armor equipment slots (Helmet, Chest, Legs).
/// Applies armor values to <see cref="ArmorComponent"/> on the same GameObject.
/// Slot items use string IDs for now; will become ScriptableObject refs in Phase 6.
/// </summary>
[RequireComponent(typeof(ArmorComponent))]
public class PlayerEquipment : MonoBehaviour
{
    private const string SLOT_HELMET = "Helmet";
    private const string SLOT_CHEST  = "Chest";
    private const string SLOT_LEGS   = "Legs";

    public string EquippedHelmet { get; private set; }
    public string EquippedChest  { get; private set; }
    public string EquippedLegs   { get; private set; }

    private ArmorComponent _armorComponent;

    private void Awake()
    {
        _armorComponent = GetComponent<ArmorComponent>();
    }

    /// <summary>
    /// Equips an armor item into the specified slot and applies
    /// <paramref name="armorValue"/> to <see cref="ArmorComponent"/>.
    /// </summary>
    /// <param name="slot">One of "Helmet", "Chest", or "Legs".</param>
    /// <param name="armorId">String identifier of the armor item.</param>
    /// <param name="armorValue">Armor points to add.</param>
    public void EquipArmor(string slot, string armorId, int armorValue)
    {
        switch (slot)
        {
            case SLOT_HELMET: EquippedHelmet = armorId; break;
            case SLOT_CHEST:  EquippedChest  = armorId; break;
            case SLOT_LEGS:   EquippedLegs   = armorId; break;
            default:
                Debug.LogWarning($"[PlayerEquipment] Unknown slot '{slot}'. Use Helmet, Chest, or Legs.");
                return;
        }

        _armorComponent.AddArmor(armorValue);
        EventBus<ArmorChangedEvent>.Raise(new ArmorChangedEvent { CurrentArmor = _armorComponent.CurrentArmor });
    }

    /// <summary>Unequips the armor from the specified slot.</summary>
    /// <param name="slot">One of "Helmet", "Chest", or "Legs".</param>
    public void UnequipArmor(string slot)
    {
        switch (slot)
        {
            case SLOT_HELMET: EquippedHelmet = null; break;
            case SLOT_CHEST:  EquippedChest  = null; break;
            case SLOT_LEGS:   EquippedLegs   = null; break;
            default:
                Debug.LogWarning($"[PlayerEquipment] Unknown slot '{slot}'. Use Helmet, Chest, or Legs.");
                return;
        }

        EventBus<ArmorChangedEvent>.Raise(new ArmorChangedEvent { CurrentArmor = _armorComponent.CurrentArmor });
    }
}
