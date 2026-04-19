/// <summary>
/// Plain serializable class holding runtime game progress.
/// Owned and persisted by <see cref="SaveManager"/>.
/// Inventory fields will be added in Phase 2 when PlayerInventory exists.
/// </summary>
[System.Serializable]
public class SaveData
{
    public int   CurrentLevel     = 0;
    public int   CheckpointIndex  = 0;
    public int   CurrentHealth    = 100;
    public int   CurrentArmor     = 0;
    public float CheckpointPositionX;
    public float CheckpointPositionY;
    public float CheckpointPositionZ;
}
