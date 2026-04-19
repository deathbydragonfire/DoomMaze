using UnityEngine;

/// <summary>
/// Grants the player their starting ammo and equips their default weapon slot
/// once the game reaches <see cref="GameState.Playing"/>.
/// Attach to the Player GameObject alongside <see cref="PlayerInventory"/>
/// and <see cref="WeaponSwitcher"/>.
/// </summary>
public class PlayerLoadout : MonoBehaviour
{
    [SerializeField] private int _startingSlot    = 1;
    [SerializeField] private string _startAmmoType = "pistol_rounds";
    [SerializeField] private int _startAmmoAmount  = 120;

    private PlayerInventory _inventory;
    private WeaponSwitcher  _weaponSwitcher;
    private bool            _applied;

    private void Awake()
    {
        _inventory      = GetComponent<PlayerInventory>();
        _weaponSwitcher = GetComponent<WeaponSwitcher>();
    }

    private void Start()
    {
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
    }

    private void OnDestroy()
    {
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Playing)
            ApplyLoadout();
    }

    private void ApplyLoadout()
    {
        if (_applied) return;
        _applied = true;

        _inventory?.AddAmmo(_startAmmoType, _startAmmoAmount);
        _weaponSwitcher?.SwitchToSlot(_startingSlot);
    }
}
