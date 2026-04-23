using UnityEngine;

/// <summary>
/// ScriptableObject holding all numeric configuration for the player.
/// Zero hardcoded values in MonoBehaviours — always reference this asset.
/// Asset naming convention: PlayerStats_Default
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats_Default", menuName = "DoomMaze/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Health & Armor")]
    public int   MaxHealth           = 100;
    public int   MaxArmor            = 200;
    public float InvulnerabilityTime = 0.5f;

    [Header("Movement")]
    public float WalkSpeed             = 5f;
    public float SprintMultiplier      = 1.6f;
    public float CrouchSpeedMultiplier = 0.55f;
    public float JumpHeight            = 1.5f;
    [Min(1)] public int MaxJumpCount   = 2;
    public float GravityScale          = 2f;
    public float AirControlFactor      = 0.4f;
    [Range(0f, 1f)] public float AirJumpRedirectControlFactor = 1f;
    [Min(0f)] public float AirJumpRedirectDuration            = 0.2f;
    [Min(0f)] public float DashSpeed                         = 28f;
    [Min(0f)] public float DashDuration                      = 0.18f;
    [Min(0f)] public float DashCooldown                      = 0.5f;
    [Header("Wall Run")]
    [Min(0f)] public float WallRunSpeed                      = 14f;
    [Min(0f)] public float WallRunDuration                   = 0.65f;
    [Min(0f)] public float WallSlideDelay                    = 0.18f;
    [Min(0f)] public float WallSlideSpeed                    = 6f;
    [Range(0f, 1f)] public float WallRunGravityFactor        = 0.3f;
    [Min(0f)] public float WallStickForce                    = 6f;
    [Min(0f)] public float WallCheckDistance                 = 0.8f;
    [Range(0f, 1f)] public float WallRunMinForwardInput      = 0.1f;
    [Min(0f)] public float WallRunMinHorizontalSpeed         = 6f;
    [Min(0f)] public float WallJumpAwaySpeed                 = 11f;
    [Min(0f)] public float WallJumpForwardSpeed              = 7f;
    [Min(0f)] public float WallJumpHeightMultiplier          = 1.05f;
    [Min(0f)] public float WallJumpMomentumDecay             = 12f;
    [Min(0f)] public float WallReattachCooldown              = 0.2f;
    public float GroundCheckDistance   = 0.12f;

    [Header("Look")]
    public float BaseSensitivity = 1f;
    public float MaxLookAngle    = 85f;
}
