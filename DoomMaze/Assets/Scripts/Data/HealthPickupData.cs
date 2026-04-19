using UnityEngine;

/// <summary>
/// ScriptableObject data for a health pickup. Asset naming: HealthPickupData_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Pickups/Health Pickup", fileName = "HealthPickupData_New")]
public class HealthPickupData : ScriptableObject
{
    public string DisplayName;
    public int    HealAmount;
}
