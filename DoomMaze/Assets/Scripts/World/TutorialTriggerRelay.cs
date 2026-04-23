using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class TutorialTriggerRelay : MonoBehaviour
{
    public TutorialManager Manager { get; private set; }
    public TutorialTriggerType TriggerType { get; private set; }
    public int CheckpointIndex { get; private set; } = -1;

    public void Configure(TutorialManager manager, TutorialTriggerType triggerType, int checkpointIndex)
    {
        Manager = manager;
        TriggerType = triggerType;
        CheckpointIndex = checkpointIndex;
    }

    private void OnTriggerEnter(Collider other)
    {
        Manager?.HandleTrigger(this, other);
    }
}

public enum TutorialTriggerType
{
    Checkpoint,
    FailRespawn,
    CombatAndHudUnlock
}
