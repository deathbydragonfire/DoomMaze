using UnityEngine;

/// <summary>All recognized categories of damage in the game.</summary>
public enum DamageType { Physical, Explosive, Fire, Energy }

/// <summary>
/// Shared value type passed through the <see cref="IDamageable"/> pipeline.
/// Must be defined before <see cref="HealthComponent"/> or <see cref="IDamageable"/>.
/// </summary>
[System.Serializable]
public struct DamageInfo
{
    public float      Amount;
    public DamageType Type;
    public GameObject Source;
    public bool       IgnoreInvulnerability;
    public bool       IgnoreArmor;
}
