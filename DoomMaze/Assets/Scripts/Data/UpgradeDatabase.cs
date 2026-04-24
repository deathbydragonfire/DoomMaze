using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry of run upgrades available to upgrade rooms.
/// If no asset entries are assigned, the v1 upgrade set is used as a runtime fallback.
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Upgrades/Upgrade Database", fileName = "UpgradeDatabase")]
public class UpgradeDatabase : ScriptableObject
{
    public const string PistolDamageId = "upgrade_pistol_damage";
    public const string MachineGunDamageId = "upgrade_machine_gun_damage";
    public const string ReloadSpeedId = "upgrade_reload_speed";
    public const string FlamethrowerUseId = "upgrade_flamethrower_use";
    public const string FlamethrowerCooldownId = "upgrade_flamethrower_cooldown";
    public const string RocketExplosionRadiusId = "upgrade_rocket_explosion_radius";
    public const string SpecialChargeId = "upgrade_special_charge";
    public const string ExtraJumpId = "upgrade_extra_jump";
    public const string ExtraWallJumpId = "upgrade_extra_wall_jump";
    public const string MovementSpeedId = "upgrade_movement_speed";
    public const string MeleeDamageId = "upgrade_melee_damage";
    public const string PickupDropRateId = "upgrade_pickup_drop_rate";

    [SerializeField] private List<UpgradeData> _upgrades = new List<UpgradeData>();

    private static List<UpgradeData> s_defaultUpgrades;

    public IReadOnlyList<UpgradeData> Upgrades => HasAssignedUpgrades() ? _upgrades : GetDefaultUpgrades();

    public List<UpgradeData> GetRandomChoices(int count, RunUpgradeManager manager)
    {
        return GetRandomChoices(Upgrades, count, manager);
    }

    public static List<UpgradeData> GetDefaultRandomChoices(int count, RunUpgradeManager manager)
    {
        return GetRandomChoices(GetDefaultUpgrades(), count, manager);
    }

    public static IReadOnlyList<UpgradeData> GetDefaultUpgrades()
    {
        if (s_defaultUpgrades != null)
            return s_defaultUpgrades;

        s_defaultUpgrades = new List<UpgradeData>
        {
            CreateRuntimeUpgrade(PistolDamageId, "Pistol Damage", UpgradeEffectType.PistolDamage, 5, 0.2f, "pistol"),
            CreateRuntimeUpgrade(MachineGunDamageId, "Machine Gun Damage", UpgradeEffectType.MachineGunDamage, 5, 0.15f, "machine_gun"),
            CreateRuntimeUpgrade(ReloadSpeedId, "Faster Reload", UpgradeEffectType.ReloadSpeed, 3, 0.15f, minMultiplier: 0.55f),
            CreateRuntimeUpgrade(FlamethrowerUseId, "Longer Flamethrower Use", UpgradeEffectType.FlamethrowerUse, 3, 0.15f, "flamethrower", 0.55f),
            CreateRuntimeUpgrade(FlamethrowerCooldownId, "Faster Flamethrower Cooldown", UpgradeEffectType.FlamethrowerCooldown, 3, 0.25f, "flamethrower"),
            CreateRuntimeUpgrade(RocketExplosionRadiusId, "Rocket Explosion Radius", UpgradeEffectType.RocketExplosionRadius, 3, 0.2f, "rocket_launcher"),
            CreateRuntimeUpgrade(SpecialChargeId, "Faster Special Charge", UpgradeEffectType.SpecialCharge, 3, 1f),
            CreateRuntimeUpgrade(ExtraJumpId, "Extra Jump", UpgradeEffectType.ExtraJump, 2, 1f),
            CreateRuntimeUpgrade(ExtraWallJumpId, "Extra Wall Jump", UpgradeEffectType.ExtraWallJump, 2, 1f),
            CreateRuntimeUpgrade(MovementSpeedId, "Increased Speed", UpgradeEffectType.MovementSpeed, 3, 0.1f),
            CreateRuntimeUpgrade(MeleeDamageId, "Melee Damage", UpgradeEffectType.MeleeDamage, 4, 0.25f, "fists"),
            CreateRuntimeUpgrade(PickupDropRateId, "Pickup Drop Rate", UpgradeEffectType.PickupDropRate, 3, 0.1f),
        };

        return s_defaultUpgrades;
    }

    private static List<UpgradeData> GetRandomChoices(IReadOnlyList<UpgradeData> source, int count, RunUpgradeManager manager)
    {
        var eligible = new List<UpgradeData>();

        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                UpgradeData upgrade = source[i];
                if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.UpgradeId))
                    continue;

                if (manager == null || manager.CanApply(upgrade))
                    eligible.Add(upgrade);
            }
        }

        for (int i = eligible.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (eligible[i], eligible[swapIndex]) = (eligible[swapIndex], eligible[i]);
        }

        if (eligible.Count > count)
            eligible.RemoveRange(count, eligible.Count - count);

        return eligible;
    }

    private bool HasAssignedUpgrades()
    {
        if (_upgrades == null || _upgrades.Count == 0)
            return false;

        for (int i = 0; i < _upgrades.Count; i++)
        {
            if (_upgrades[i] != null)
                return true;
        }

        return false;
    }

    private static UpgradeData CreateRuntimeUpgrade(
        string id,
        string displayName,
        UpgradeEffectType effectType,
        int maxRank,
        float perRankValue,
        string targetWeaponId = "",
        float minMultiplier = 0f)
    {
        UpgradeData upgrade = CreateInstance<UpgradeData>();
        upgrade.name = id;
        upgrade.Configure(id, displayName, effectType, maxRank, perRankValue, targetWeaponId, minMultiplier);
        upgrade.hideFlags = HideFlags.HideAndDontSave;
        return upgrade;
    }
}
