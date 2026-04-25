using UnityEngine;

public enum UpgradeEffectType
{
    PistolDamage,
    MachineGunDamage,
    ReloadSpeed,
    FlamethrowerUse,
    FlamethrowerCooldown,
    RocketExplosionRadius,
    SpecialCharge,
    ExtraJump,
    ExtraWallJump,
    MovementSpeed,
    MeleeDamage,
    PickupDropRate
}

/// <summary>
/// ScriptableObject describing one rankable run upgrade.
/// Asset naming convention: UpgradeData_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Upgrades/Upgrade Data", fileName = "UpgradeData_New")]
public class UpgradeData : ScriptableObject
{
    [Header("Identity")]
    public string UpgradeId;
    public string DisplayName;
    [TextArea] public string Description;

    [Header("Effect")]
    public UpgradeEffectType EffectType;
    public string TargetWeaponId;
    [Min(1)] public int MaxRank = 1;
    public float PerRankValue;
    public float MinMultiplier;

    public void Configure(
        string upgradeId,
        string displayName,
        UpgradeEffectType effectType,
        int maxRank,
        float perRankValue,
        string targetWeaponId = "",
        float minMultiplier = 0f,
        string description = "")
    {
        UpgradeId = upgradeId;
        DisplayName = displayName;
        EffectType = effectType;
        MaxRank = Mathf.Max(1, maxRank);
        PerRankValue = perRankValue;
        TargetWeaponId = targetWeaponId;
        MinMultiplier = minMultiplier;
        Description = description;
    }
}
