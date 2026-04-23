using UnityEngine;

/// <summary>
/// Concrete pickup that heals the player by <see cref="HealthPickupData.HealAmount"/>.
/// Skips collection if the player is already at full health.
/// </summary>
public class HealthPickup : PickupBase
{
    private static readonly Color HealthFeedColor = new Color(0.45f, 1f, 0.62f, 1f);

    [SerializeField] private HealthPickupData _data;

    protected override bool ExecutePickup(PlayerInventory inventory)
    {
        if (_data == null)
        {
            Debug.LogWarning($"[HealthPickup] HealthPickupData is not assigned on {gameObject.name}.");
            return false;
        }

        HealthComponent health = inventory.GetComponent<HealthComponent>();

        if (health == null)
        {
            Debug.LogWarning($"[HealthPickup] No HealthComponent found on player {inventory.gameObject.name}.");
            return false;
        }

        if (health.CurrentHealth >= health.MaxHealth)
            return false;

        int restoredAmount = health.Heal(_data.HealAmount);
        if (restoredAmount <= 0)
            return false;

        EventBus<PickupFeedMessageEvent>.Raise(new PickupFeedMessageEvent
        {
            Message = $"+{restoredAmount} HEALTH",
            Tint    = HealthFeedColor
        });

        return true;
    }
}
