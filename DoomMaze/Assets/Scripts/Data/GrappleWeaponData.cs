using UnityEngine;

/// <summary>
/// <see cref="WeaponData"/> subclass adding all grapple-specific fields.
/// Pure data — no logic. Asset naming convention: GrappleWeaponData_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Grapple Weapon Data", fileName = "GrappleWeaponData_New")]
public class GrappleWeaponData : WeaponData
{
    [Header("Grapple")]
    public float MashWindowSeconds     = 0.75f;
    public float MashProgressPerPress  = 0.18f;
    public float MashProgressDecayRate = 0f;
    public float PullDurationSeconds   = 0.25f;
    public float GrabPointDistance     = 1.5f;
    public float TetherDuration        = 0.3f;
    public float CooldownSeconds       = 1.5f;
    public float PullStunDuration      = 1.5f;
}
