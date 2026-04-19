using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry for all <see cref="WeaponData"/> assets.
/// Looked up by <see cref="WeaponData.WeaponId"/>.
/// Asset naming convention: WeaponDatabase.
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Weapon Database", fileName = "WeaponDatabase")]
public class WeaponDatabase : ScriptableObject
{
    [SerializeField] private List<WeaponData> _weapons = new List<WeaponData>();

    /// <summary>Returns <see cref="WeaponData"/> matching <paramref name="weaponId"/>; null if not found.</summary>
    public WeaponData GetById(string weaponId)
    {
        for (int i = 0; i < _weapons.Count; i++)
        {
            if (_weapons[i] != null && _weapons[i].WeaponId == weaponId)
                return _weapons[i];
        }
        return null;
    }
}
