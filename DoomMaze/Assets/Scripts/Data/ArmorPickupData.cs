using UnityEngine;

/// <summary>
/// ScriptableObject data for an armor pickup. Asset naming: ArmorPickupData_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Pickups/Armor Pickup", fileName = "ArmorPickupData_New")]
public class ArmorPickupData : ScriptableObject
{
    public string DisplayName;
    public int    ArmorAmount;
}
