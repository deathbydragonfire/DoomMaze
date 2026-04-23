using UnityEngine;

/// <summary>
/// Concrete pickup that adds a key item ID to <see cref="PlayerInventory"/>.
/// <see cref="Door"/> checks for this ID before allowing interaction.
/// </summary>
public class KeyPickup : PickupBase
{
    private static readonly Color KeyFeedColor = new Color(1f, 0.9f, 0.24f, 1f);

    [SerializeField] private string _keyId;

    protected override bool ExecutePickup(PlayerInventory inventory)
    {
        if (string.IsNullOrEmpty(_keyId))
        {
            Debug.LogWarning($"[KeyPickup] _keyId is not set on {gameObject.name}.");
            return false;
        }

        if (inventory.HasItem(_keyId))
            return false;

        inventory.AddItem(_keyId);
        EventBus<PickupFeedMessageEvent>.Raise(new PickupFeedMessageEvent
        {
            Message = $"PICKED UP {FormatKeyLabel(_keyId)}",
            Tint    = KeyFeedColor
        });

        return true;
    }

    private static string FormatKeyLabel(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            return "KEY";

        string label = keyId.Replace('_', ' ').Trim();
        return label.ToUpperInvariant();
    }
}
