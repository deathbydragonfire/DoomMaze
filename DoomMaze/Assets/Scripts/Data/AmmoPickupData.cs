using UnityEngine;

/// <summary>
/// ScriptableObject data for an ammo pickup. Asset naming: AmmoPickupData_[Name].
/// <see cref="AmmoTypeId"/> must match an <see cref="AmmoTypeData.AmmoId"/> in the database.
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Pickups/Ammo Pickup", fileName = "AmmoPickupData_New")]
public class AmmoPickupData : ScriptableObject
{
    public string AmmoTypeId;    // must match AmmoTypeData.AmmoId
    public int    Amount;
    public int    MaxCarryCount; // cap reference — mirrors AmmoTypeData.MaxCarryCount
}
