/// <summary>
/// Contract for all weapon types. <see cref="PlayerCombat"/> depends on this interface,
/// never on a concrete weapon class.
/// </summary>
public interface IWeapon
{
    /// <summary>The data asset that drives this weapon's stats and configuration.</summary>
    WeaponData Data { get; }

    /// <summary>Ammo currently loaded in the weapon's magazine.</summary>
    int CurrentAmmo { get; }

    /// <summary>Returns true when the weapon can be safely switched away from.</summary>
    bool CanBeSwitchedAway { get; }

    /// <summary>Returns true if the weapon is allowed to fire right now (rate, ammo, state).</summary>
    bool CanFire();

    /// <summary>Executes a primary fire action.</summary>
    void Fire();

    /// <summary>Stops continuous primary-fire effects when the trigger is released or interrupted.</summary>
    void StopFiring();

    /// <summary>Executes an alternate fire action. No-op by default.</summary>
    void AltFire();

    /// <summary>Reloads the weapon from carried ammo.</summary>
    void Reload();

    /// <summary>Called when this weapon becomes the active weapon.</summary>
    void OnEquip();

    /// <summary>Called when this weapon is deactivated or swapped away.</summary>
    void OnUnequip();
}
