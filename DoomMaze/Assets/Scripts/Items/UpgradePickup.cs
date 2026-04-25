using UnityEngine;

/// <summary>
/// Pickup choice used in upgrade rooms. Applying one upgrade asks its room to
/// disable the remaining choices.
/// </summary>
public class UpgradePickup : PickupBase
{
    private static readonly Color UpgradeFeedColor = new Color(1f, 0.48f, 0.18f, 1f);

    [SerializeField] private UpgradeData _data;

    private UpgradeRoomController _roomController;

    public UpgradeData Data => _data;

    public void Configure(UpgradeData data, UpgradeRoomController roomController)
    {
        _data = data;
        _roomController = roomController;
        gameObject.name = data != null ? $"UpgradePickup_{data.UpgradeId}" : "UpgradePickup";
    }

    protected override bool ExecutePickup(PlayerInventory inventory)
    {
        if (_data == null)
        {
            Debug.LogWarning($"[UpgradePickup] UpgradeData is not assigned on {gameObject.name}.");
            return false;
        }

        RunUpgradeManager manager = RunUpgradeManager.Instance;
        if (!manager.ApplyUpgrade(_data, out int rank))
            return false;

        string displayName = !string.IsNullOrWhiteSpace(_data.DisplayName)
            ? _data.DisplayName.ToUpperInvariant()
            : _data.UpgradeId.ToUpperInvariant();

        EventBus<PickupFeedMessageEvent>.Raise(new PickupFeedMessageEvent
        {
            Message = $"{displayName} RANK {rank}/{Mathf.Max(1, _data.MaxRank)}",
            Tint = UpgradeFeedColor
        });

        _roomController?.DisableOtherChoices(this);
        return true;
    }
}
