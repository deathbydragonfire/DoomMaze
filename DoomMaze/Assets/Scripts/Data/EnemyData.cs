using UnityEngine;

public enum EnemyAggroDetectionMode
{
    Radius,
    LineOfSight
}

/// <summary>
/// ScriptableObject holding all stats and references for a single enemy type.
/// Pure data - no logic. Asset naming convention: EnemyData_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Enemy Data", fileName = "EnemyData_New")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string EnemyId;
    public string DisplayName;
    public GameObject EnemyPrefab;

    [Header("Stats")]
    public int MaxHealth;
    public float MoveSpeed;
    public EnemyAggroDetectionMode AggroDetectionMode = EnemyAggroDetectionMode.Radius;
    public float AggroRange;          // Max distance used for aggro checks in either mode

    [Header("Navigation")]
    public float StoppingDistance;
    public float AgentRadius;
    public float AgentHeight;

    [Header("Drop Table")]
    public GameObject[] PossibleDrops;    // Prefab references; Phase 6 pickup prefabs slot in here
    [Range(0f, 1f)]
    public float DropChance;

    [Header("Audio")]
    public AudioClip AggroSound;
    public AudioClip[] AggroSoundVariants;
    [Range(0f, 1f)] public float AggroVolume = 1f;
    public AudioClip AttackSound;
    public AudioClip[] AttackSoundVariants;
    [Range(0f, 1f)] public float AttackVolume = 1f;
    public AudioClip HurtSound;
    public AudioClip[] HurtSoundVariants;
    [Range(0f, 1f)] public float HurtVolume = 1f;
    public AudioClip DeathSound;
    public AudioClip[] DeathSoundVariants;
    [Range(0f, 1f)] public float DeathVolume = 1f;
    public AudioClip FootstepSound;
    public AudioClip[] FootstepSoundVariants;
    [Range(0f, 1f)] public float FootstepVolume = 1f;

    [Header("Sprites")]
    public Sprite[] IdleSprites;
    public Sprite[] WalkSprites;
    public Sprite[] AttackSprites;
    public Sprite[] HurtSprites;
    public Sprite[] DeathSprites;
    public float FrameRate;           // Sprite animation FPS

    [Header("Animation Triggers")]
    public string HurtAnimTrigger;
    public string DeathAnimTrigger;

    [Header("Grapple")]
    public bool IsHookImmune;

    public AudioClip GetAggroClip() => GetRandomClip(AggroSound, AggroSoundVariants);
    public AudioClip GetAttackClip() => GetRandomClip(AttackSound, AttackSoundVariants);
    public AudioClip GetHurtClip() => GetRandomClip(HurtSound, HurtSoundVariants);
    public AudioClip GetDeathClip() => GetRandomClip(DeathSound, DeathSoundVariants);
    public AudioClip GetFootstepClip() => GetRandomClip(FootstepSound, FootstepSoundVariants);

    private static AudioClip GetRandomClip(AudioClip primaryClip, AudioClip[] variantClips)
    {
        int clipCount = primaryClip != null ? 1 : 0;

        if (variantClips != null)
        {
            for (int i = 0; i < variantClips.Length; i++)
            {
                if (variantClips[i] != null)
                    clipCount++;
            }
        }

        if (clipCount == 0)
            return null;

        int clipIndex = Random.Range(0, clipCount);

        if (primaryClip != null)
        {
            if (clipIndex == 0)
                return primaryClip;

            clipIndex--;
        }

        if (variantClips != null)
        {
            for (int i = 0; i < variantClips.Length; i++)
            {
                AudioClip variantClip = variantClips[i];
                if (variantClip == null)
                    continue;

                if (clipIndex == 0)
                    return variantClip;

                clipIndex--;
            }
        }

        return primaryClip;
    }
}
