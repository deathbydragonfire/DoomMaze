/// <summary>
/// Contract for any GameObject that can receive damage through the unified pipeline.
/// Both the player and enemies implement this interface.
/// </summary>
public interface IDamageable
{
    /// <summary>Apply damage through the unified pipeline.</summary>
    void TakeDamage(DamageInfo info);

    bool IsAlive { get; }
}
