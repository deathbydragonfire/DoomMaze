using UnityEngine;

/// <summary>
/// ScriptableObject holding one complete punch sprite sequence.
/// Pure data — no logic. Asset naming convention: PunchSet_[Name].
/// </summary>
[CreateAssetMenu(menuName = "DoomMaze/Punch Animation Set", fileName = "PunchSet_New")]
public class PunchAnimationSet : ScriptableObject
{
    [Header("Frames")]
    public Sprite[] Frames;

    [Header("Playback")]
    public float FramesPerSecond = 12f;

    [Header("Position Override")]
    public bool    OverridePosition;
    public Vector2 PositionOffset;

    [Header("Camera Punch")]
    public bool    UseCameraPunch;
    public Vector3 CameraPunchEuler;
    public float   CameraPunchDuration = 0.2f;
}
