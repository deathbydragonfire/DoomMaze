using UnityEngine;

/// <summary>
/// ScriptableObject that defines an ammo category and its maximum carry capacity.
/// Asset naming convention: AmmoType_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Ammo Type", fileName = "AmmoType_New")]
public class AmmoTypeData : ScriptableObject
{
    /// <summary>Unique identifier. Must match <see cref="WeaponData.AmmoTypeId"/>.</summary>
    public string AmmoId;

    public string DisplayName;

    /// <summary>Maximum amount the player can carry of this ammo type.</summary>
    public int MaxCarryCount;
}
