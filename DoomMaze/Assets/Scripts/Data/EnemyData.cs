using UnityEngine;

/// <summary>
/// ScriptableObject holding all stats and references for a single enemy type.
/// Pure data — no logic. Asset naming convention: EnemyData_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Enemy Data", fileName = "EnemyData_New")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string     EnemyId;
    public string     DisplayName;
    public GameObject EnemyPrefab;

    [Header("Stats")]
    public int        MaxHealth;
    public float      MoveSpeed;
    public float      AggroRange;         // Distance at which enemy transitions Idle → Alert/Chase
    public float      AttackRange;        // Distance at which enemy can attack
    public float      AttackDamage;
    public float      AttackRate;         // Attacks per second
    public DamageType AttackDamageType;

    [Header("Navigation")]
    public float StoppingDistance;
    public float AgentRadius;
    public float AgentHeight;

    [Header("Drop Table")]
    public GameObject[] PossibleDrops;   // Prefab references; Phase 6 pickup prefabs slot in here
    [Range(0f, 1f)]
    public float DropChance;

    [Header("Audio")]
    public AudioClip AggroSound;
    public AudioClip AttackSound;
    public AudioClip HurtSound;
    public AudioClip DeathSound;
    public AudioClip FootstepSound;

    [Header("Visuals")]
    public Sprite[] IdleSprites;
    public Sprite[] WalkSprites;
    public Sprite[] AttackSprites;
    public Sprite[] HurtSprites;
    public Sprite[] DeathSprites;
    public float    FrameRate;            // Sprite animation FPS

    [Header("Grapple")]
    public bool IsHookImmune;
}
