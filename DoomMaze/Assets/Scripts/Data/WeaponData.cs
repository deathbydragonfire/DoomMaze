using UnityEngine;

/// <summary>
/// ScriptableObject holding all stats and references for a single weapon.
/// Pure data — no logic. Asset naming convention: WeaponData_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Weapon Data", fileName = "WeaponData_New")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string WeaponId;
    public string DisplayName;

    [Header("Fire Behaviour")]
    public FireMode FireMode;
    public float    Damage;
    public float    FireRate;        // Shots per second
    public int      MagazineSize;
    public int      PelletsPerShot;  // 1 for single-shot; >1 for shotgun spread
    public float    SpreadAngle;     // Cone half-angle in degrees
    public float    Range;           // Hitscan max distance

    [Header("Ammo")]
    public string AmmoTypeId;        // Must match AmmoTypeData.AmmoId; empty = infinite
    public float  ReloadTime = 1.5f; // Seconds to complete a reload

    [Header("Alt Fire")]
    public bool HasAltFire;

    [Header("Audio")]
    public AudioClip FireSound;
    public AudioClip EmptyClickSound;
    public AudioClip ReloadSound;

    [Header("Viewmodel")]
    public Sprite[] ViewmodelSprites; // [0] = idle frame
}

/// <summary>Defines whether the weapon fires once per press or continuously while held.</summary>
public enum FireMode { Semi, Auto }
