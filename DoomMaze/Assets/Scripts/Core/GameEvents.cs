/// <summary>
/// Centralized definitions for all EventBus payload structs.
/// Add new event types here as phases progress — never scatter them across files.
/// Structs are used to avoid heap allocations on the bus.
/// </summary>

public struct GameStateChangedEvent
{
    public GameState NewState;
    public GameState PreviousState;
}

public struct PauseChangedEvent
{
    public bool IsPaused;
}

public struct SceneLoadRequestEvent
{
    public string SceneName;
    public bool Additive;
}

public struct GameSavedEvent { }

public struct GameLoadedEvent { }

public struct RunResetEvent { }

public struct MazePopulatedEvent
{
    public MazePopulator Populator;
}

// ── Phase 2: Player events ────────────────────────────────────────────────────

public struct PlayerDamagedEvent   { public int CurrentHealth; public int MaxHealth; public DamageInfo Info; }
public struct PlayerDiedEvent      { }
public struct PlayerHealedEvent    { public int CurrentHealth; public int MaxHealth; }
public struct PlayerLowHealthEvent { public bool IsLow; }
public struct PlayerDecayChangedEvent
{
    public float DecayNormalized;
    public float GrayscaleAmount;
}
public struct ArmorChangedEvent    { public int CurrentArmor; }
public struct InventoryChangedEvent{ }
public struct WeaponEquippedEvent  { public int SlotIndex; }

// ── Phase 3: Weapon events ────────────────────────────────────────────────────
public struct WeaponFiredEvent    { public WeaponData Data; }
public struct AmmoChangedEvent    { public string AmmoTypeId; public int CurrentAmmo; public int CarriedAmmo; }
public struct WeaponSwitchedEvent { public int FromSlot; public int ToSlot; public WeaponData NewWeapon; }
public struct SuperMeterChangedEvent
{
    public float ChargeNormalized;
    public int CurrentCharges;
    public int ChargesRequired;
    public bool IsReady;
}
public struct SuperFiredEvent
{
    public UnityEngine.Vector3 Origin;
    public UnityEngine.Vector3 EndPoint;
}

// ── Phase 4: Enemy events ─────────────────────────────────────────────────────
public struct EnemyDamagedEvent { public UnityEngine.GameObject Enemy; public DamageInfo Info; public int CurrentHealth; }
public struct EnemyDiedEvent    { public UnityEngine.GameObject Enemy; public string EnemyId; }
public struct EnemyDeathAnimationCompletedEvent { public UnityEngine.GameObject Enemy; public string EnemyId; }

// ── Phase 6: Pickups & World events ──────────────────────────────────────────
public struct PickupCollectedEvent    { public string PickupId; }
public struct WeaponPickedUpEvent     { public WeaponData WeaponData; }
public struct UpgradeCollectedEvent   { public string UpgradeId; public string DisplayName; public int Rank; public int MaxRank; }
public struct DoorToggledEvent        { public bool IsOpen; }
public struct DoorLockedEvent         { public string RequiredKeyId; }
public struct SwitchActivatedEvent    { }
public struct LevelExitTriggeredEvent { }
public struct MusicZoneChangedEvent   { public string TrackId; }
public struct InteractAttemptedEvent  { public bool HitInteractable; }
public struct UpgradeRoomPresenceChangedEvent
{
    public UpgradeRoomController Room;
    public bool IsPlayerInside;
}

// ── Phase 7: Audio events ─────────────────────────────────────────────────────
public struct SfxRequestEvent { public UnityEngine.AudioClip Clip; }
public struct MusicTrackEvent { public string TrackId; }

// ── Phase 8: UI events ────────────────────────────────────────────────────────
public struct PickupFeedMessageEvent
{
    public string            Message;
    public UnityEngine.Color Tint;
}

// ── Camera events ─────────────────────────────────────────────────────────────
public struct CameraShakeEvent { public float Magnitude; public float Duration; }
public struct CameraPunchEvent  { public UnityEngine.Vector3 EulerAngles; public float Duration; }

// ── Grapple events ────────────────────────────────────────────────────────────
public struct GrappleHookedEvent       { public UnityEngine.GameObject Enemy; }
public struct GrappleMissedEvent       { }
public struct GrapplePulledEvent       { public UnityEngine.GameObject Enemy; }
public struct GrappleMashProgressEvent { public float Progress; }
public struct GrappleReleasedEvent     { }

// ── Hype / Feedback events ────────────────────────────────────────────────────
public struct KillStreakEvent          { public int StreakCount; }
public struct KillConfirmedEvent
{
    public UnityEngine.Vector3 WorldPosition;
    public bool                IsStreakKill;
    public int                 StreakCount;
    public string              EnemyName;
}
public struct PlayerLandedEvent        { public float FallSpeed; }
public struct PlayerDashedEvent        { public UnityEngine.Vector3 Direction; public float Duration; public float Speed; }
public struct PlayerSprintChangedEvent { public bool IsSprinting; }
public struct MeleeHitEvent            { public int HitCount; }
