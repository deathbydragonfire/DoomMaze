using UnityEngine;

/// <summary>
/// Concrete pickup that heals the player by <see cref="HealthPickupData.HealAmount"/>.
/// Skips collection if the player is already at full health.
/// </summary>
public class HealthPickup : PickupBase
{
    [SerializeField] private HealthPickupData _data;

    protected override void ExecutePickup(PlayerInventory inventory)
    {
        if (_data == null)
        {
            Debug.LogWarning($"[HealthPickup] HealthPickupData is not assigned on {gameObject.name}.");
            return;
        }

        HealthComponent health = inventory.GetComponent<HealthComponent>();

        if (health == null)
        {
            Debug.LogWarning($"[HealthPickup] No HealthComponent found on player {inventory.gameObject.name}.");
            return;
        }

        if (health.CurrentHealth >= health.MaxHealth)
            return;

        health.Heal(_data.HealAmount);
    }
}
