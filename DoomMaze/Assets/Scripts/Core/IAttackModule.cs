/// <summary>
/// Contract for all attack behaviours composed onto <see cref="EnemyBase"/>.
/// </summary>
public interface IAttackModule
{
    /// <summary>Called by <see cref="EnemyBase"/> when transitioning into the Attack state.</summary>
    void OnAttackEnter();

    /// <summary>Called by <see cref="EnemyBase"/> each Update tick while in the Attack state.</summary>
    void Tick();

    float MinAttackRange { get; }         // Minimum distance at which enemy can attack

    float MaxAttackRange { get; }         // Maximum distance at which enemy can attack

    float AttackDamage { get; }

    float AttackRate { get; }          // Attacks per second

    DamageType AttackDamageType { get; }

    //TODO: Move all attack fields from data onto this script

    string AttackAnimTrigger { get; }
}

/// <summary>Optional attack contract for modules that can be selected only when their own cooldown is ready.</summary>
public interface IConditionalAttackModule
{
    bool CanStartAttack { get; }
}

/// <summary>Optional attack contract for modules with a delayed windup that should not be interrupted by reselection.</summary>
public interface IAttackExecutionStatus
{
    bool IsExecuting { get; }
}
