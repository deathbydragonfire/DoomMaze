using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only owner for upgrades collected during the current run.
/// It is intentionally not saved to SaveData; New Game resets it via RunResetEvent.
/// </summary>
public class RunUpgradeManager : MonoBehaviour
{
    public static RunUpgradeManager Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            RunUpgradeManager existing = FindFirstObjectByType<RunUpgradeManager>();
            if (existing != null)
            {
                _instance = existing;
                return _instance;
            }

            GameObject managerObject = new GameObject("RunUpgradeManager");
            _instance = managerObject.AddComponent<RunUpgradeManager>();
            return _instance;
        }
    }

    public static RunUpgradeManager Current => _instance;

    private static RunUpgradeManager _instance;

    private readonly Dictionary<string, int> _ranksByUpgradeId = new Dictionary<string, int>(16);
    private readonly Dictionary<string, UpgradeData> _knownUpgradesById = new Dictionary<string, UpgradeData>(16);

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EventBus<RunResetEvent>.Subscribe(OnRunReset);
    }

    private void OnDisable()
    {
        EventBus<RunResetEvent>.Unsubscribe(OnRunReset);
    }

    public int GetRank(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return 0;

        return _ranksByUpgradeId.TryGetValue(upgradeId, out int rank) ? rank : 0;
    }

    public bool CanApply(UpgradeData upgrade)
    {
        if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.UpgradeId))
            return false;

        return GetRank(upgrade.UpgradeId) < Mathf.Max(1, upgrade.MaxRank);
    }

    public bool ApplyUpgrade(UpgradeData upgrade, out int newRank)
    {
        newRank = 0;

        if (!CanApply(upgrade))
            return false;

        string id = upgrade.UpgradeId;
        newRank = GetRank(id) + 1;
        _ranksByUpgradeId[id] = newRank;
        _knownUpgradesById[id] = upgrade;

        EventBus<UpgradeCollectedEvent>.Raise(new UpgradeCollectedEvent
        {
            UpgradeId = id,
            DisplayName = upgrade.DisplayName,
            Rank = newRank,
            MaxRank = Mathf.Max(1, upgrade.MaxRank)
        });

        return true;
    }

    public float GetWeaponDamageMultiplier(WeaponData weaponData)
    {
        if (weaponData == null)
            return 1f;

        string weaponId = weaponData.WeaponId;

        if (weaponId == "pistol")
            return 1f + GetAccumulatedValue(UpgradeEffectType.PistolDamage, weaponId, UpgradeDatabase.PistolDamageId, 0.2f);

        if (weaponId == "machine_gun")
            return 1f + GetAccumulatedValue(UpgradeEffectType.MachineGunDamage, weaponId, UpgradeDatabase.MachineGunDamageId, 0.15f);

        if (weaponId == "fists")
            return 1f + GetAccumulatedValue(UpgradeEffectType.MeleeDamage, weaponId, UpgradeDatabase.MeleeDamageId, 0.25f);

        return 1f;
    }

    public float GetReloadTimeMultiplier(WeaponData weaponData)
    {
        float reduction = GetAccumulatedValue(UpgradeEffectType.ReloadSpeed, null, UpgradeDatabase.ReloadSpeedId, 0.15f);
        float minMultiplier = GetMinMultiplier(UpgradeDatabase.ReloadSpeedId, 0.55f);
        return Mathf.Max(minMultiplier, 1f - reduction);
    }

    public float GetFlamethrowerHeatGainMultiplier()
    {
        float reduction = GetAccumulatedValue(UpgradeEffectType.FlamethrowerUse, "flamethrower", UpgradeDatabase.FlamethrowerUseId, 0.15f);
        float minMultiplier = GetMinMultiplier(UpgradeDatabase.FlamethrowerUseId, 0.55f);
        return Mathf.Max(minMultiplier, 1f - reduction);
    }

    public float GetFlamethrowerCooldownMultiplier()
    {
        return 1f + GetAccumulatedValue(UpgradeEffectType.FlamethrowerCooldown, "flamethrower", UpgradeDatabase.FlamethrowerCooldownId, 0.25f);
    }

    public float GetRocketExplosionRadiusMultiplier()
    {
        return 1f + GetAccumulatedValue(UpgradeEffectType.RocketExplosionRadius, "rocket_launcher", UpgradeDatabase.RocketExplosionRadiusId, 0.2f);
    }

    public int GetSuperKillRequirementReduction()
    {
        return Mathf.RoundToInt(GetAccumulatedValue(UpgradeEffectType.SpecialCharge, null, UpgradeDatabase.SpecialChargeId, 1f));
    }

    public int GetExtraJumpCount()
    {
        return Mathf.RoundToInt(GetAccumulatedValue(UpgradeEffectType.ExtraJump, null, UpgradeDatabase.ExtraJumpId, 1f));
    }

    public int GetExtraWallJumpCount()
    {
        return Mathf.RoundToInt(GetAccumulatedValue(UpgradeEffectType.ExtraWallJump, null, UpgradeDatabase.ExtraWallJumpId, 1f));
    }

    public float GetMovementSpeedMultiplier()
    {
        return 1f + GetAccumulatedValue(UpgradeEffectType.MovementSpeed, null, UpgradeDatabase.MovementSpeedId, 0.1f);
    }

    public float GetPickupDropChanceBonus()
    {
        return GetAccumulatedValue(UpgradeEffectType.PickupDropRate, null, UpgradeDatabase.PickupDropRateId, 0.1f);
    }

    private float GetAccumulatedValue(UpgradeEffectType effectType, string targetWeaponId, string defaultUpgradeId, float defaultPerRankValue)
    {
        float total = 0f;
        bool countedDefaultId = false;

        foreach (KeyValuePair<string, UpgradeData> pair in _knownUpgradesById)
        {
            UpgradeData upgrade = pair.Value;
            if (upgrade == null || upgrade.EffectType != effectType)
                continue;

            if (!MatchesTarget(upgrade, targetWeaponId))
                continue;

            int rank = GetRank(pair.Key);
            total += rank * upgrade.PerRankValue;

            if (pair.Key == defaultUpgradeId)
                countedDefaultId = true;
        }

        if (!countedDefaultId)
            total += GetRank(defaultUpgradeId) * defaultPerRankValue;

        return total;
    }

    private float GetMinMultiplier(string upgradeId, float fallback)
    {
        return _knownUpgradesById.TryGetValue(upgradeId, out UpgradeData upgrade) && upgrade != null && upgrade.MinMultiplier > 0f
            ? upgrade.MinMultiplier
            : fallback;
    }

    private static bool MatchesTarget(UpgradeData upgrade, string targetWeaponId)
    {
        if (string.IsNullOrWhiteSpace(targetWeaponId))
            return true;

        return string.IsNullOrWhiteSpace(upgrade.TargetWeaponId) || upgrade.TargetWeaponId == targetWeaponId;
    }

    private void OnRunReset(RunResetEvent e)
    {
        _ranksByUpgradeId.Clear();
        _knownUpgradesById.Clear();
    }
}
