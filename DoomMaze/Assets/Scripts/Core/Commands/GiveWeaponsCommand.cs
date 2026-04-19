#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Debug command that grants all weapon slots and fills every ammo type to
/// its maximum carry capacity.
/// Usage: <c>give weapons</c>
/// </summary>
public class GiveWeaponsCommand : IDebugCommand
{
    public string Id          => "give";
    public string Description => "give weapons — grants all weapons and max ammo.";

    private readonly WeaponDatabase _weaponDatabase;
    private readonly AmmoTypeData[] _ammoTypes;

    private WeaponSwitcher  _weaponSwitcher;
    private PlayerInventory _playerInventory;

    public GiveWeaponsCommand(WeaponDatabase weaponDatabase, AmmoTypeData[] ammoTypes)
    {
        _weaponDatabase = weaponDatabase;
        _ammoTypes      = ammoTypes;
    }

    public void Execute(string[] args, DebugConsole console)
    {
        if (args.Length < 1 || args[0].ToLowerInvariant() != "weapons")
        {
            console.Print("Usage: give weapons");
            return;
        }

        if (_weaponSwitcher == null || _playerInventory == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null)
            {
                console.Print("[GiveWeaponsCommand] Player not found in scene.");
                return;
            }
            _weaponSwitcher  = playerObj.GetComponent<WeaponSwitcher>();
            _playerInventory = playerObj.GetComponent<PlayerInventory>();

            if (_weaponSwitcher == null || _playerInventory == null)
            {
                console.Print("[GiveWeaponsCommand] WeaponSwitcher or PlayerInventory not found on Player.");
                return;
            }
        }

        _weaponSwitcher.UnlockAllWeapons();

        if (_ammoTypes != null)
        {
            for (int i = 0; i < _ammoTypes.Length; i++)
            {
                AmmoTypeData ammoType = _ammoTypes[i];
                if (ammoType != null)
                    _playerInventory.AddAmmo(ammoType.AmmoId, ammoType.MaxCarryCount);
            }
        }

        console.Print("All weapons and ammo granted.");
    }
}
#endif
