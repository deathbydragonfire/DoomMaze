/// <summary>
/// Contract for all attack behaviours composed onto <see cref="EnemyBase"/>.
/// </summary>
public interface IAttackModule
{
    /// <summary>Called by <see cref="EnemyBase"/> when transitioning into the Attack state.</summary>
    void OnAttackEnter();

    /// <summary>Called by <see cref="EnemyBase"/> each Update tick while in the Attack state.</summary>
    void Tick();

    float AttackRange { get; }         // Distance at which enemy can attack

    float AttackDamage { get; }

    float AttackRate { get; }          // Attacks per second

    DamageType AttackDamageType { get; }

    //TODO: Move all attack fields from data onto this script

    string AttackAnimTrigger { get; }
}