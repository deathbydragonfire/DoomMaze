using UnityEngine;

/// <summary>
/// Stores armor points and applies damage mitigation. Always lives alongside
/// <see cref="HealthComponent"/> on the same GameObject. <see cref="HealthComponent"/>
/// calls <see cref="MitigateDamage"/> before subtracting health.
/// </summary>
public class ArmorComponent : MonoBehaviour
{
    [SerializeField] private int _maxArmor = 100;

    public int CurrentArmor { get; private set; }
    public int MaxArmor     { get; private set; }

    private void Awake()
    {
        MaxArmor     = _maxArmor;
        CurrentArmor = 0;
    }

    // ── Absorption rates per damage type ─────────────────────────────────────

    private const float PHYSICAL_ABSORPTION  = 0.66f;
    private const float EXPLOSIVE_ABSORPTION = 0.33f;
    private const float FIRE_ABSORPTION      = 0.50f;
    private const float ENERGY_ABSORPTION    = 0.50f;

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes armor with the given maximum. Called by the player setup code
    /// or <see cref="PlayerEquipment"/> after equipping armor.
    /// </summary>
    public void Initialize(int maxArmor)
    {
        MaxArmor    = maxArmor;
        CurrentArmor = maxArmor;
    }

    /// <summary>
    /// Absorbs part of <paramref name="info"/>.Amount based on damage type and
    /// available armor. Subtracts absorbed amount from <see cref="CurrentArmor"/>,
    /// raises <see cref="ArmorChangedEvent"/>, and returns a new <see cref="DamageInfo"/>
    /// with the reduced amount.
    /// </summary>
    public DamageInfo MitigateDamage(DamageInfo info)
    {
        if (CurrentArmor <= 0)
            return info;

        float absorptionRate = info.Type switch
        {
            DamageType.Physical  => PHYSICAL_ABSORPTION,
            DamageType.Explosive => EXPLOSIVE_ABSORPTION,
            DamageType.Fire      => FIRE_ABSORPTION,
            DamageType.Energy    => ENERGY_ABSORPTION,
            _                    => 0f
        };

        float absorbed = info.Amount * absorptionRate;
        absorbed = Mathf.Min(absorbed, CurrentArmor); // cap by available armor

        CurrentArmor -= Mathf.RoundToInt(absorbed);
        CurrentArmor  = Mathf.Max(0, CurrentArmor);

        EventBus<ArmorChangedEvent>.Raise(new ArmorChangedEvent { CurrentArmor = CurrentArmor });

        return new DamageInfo
        {
            Amount = info.Amount - absorbed,
            Type   = info.Type,
            Source = info.Source
        };
    }

    /// <summary>Adds armor points, clamped to <see cref="MaxArmor"/>.</summary>
    public void AddArmor(int amount)
    {
        CurrentArmor = Mathf.Clamp(CurrentArmor + amount, 0, MaxArmor);
        EventBus<ArmorChangedEvent>.Raise(new ArmorChangedEvent { CurrentArmor = CurrentArmor });
    }
}
