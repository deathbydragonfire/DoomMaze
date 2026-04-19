using UnityEngine;

/// <summary>
/// Implements <see cref="IInteractable"/> and auto-trigger on <c>OnTriggerEnter</c>.
/// <see cref="_requireInteract"/> controls which path is active. Both paths converge
/// on <see cref="TriggerExit"/> which raises <see cref="LevelExitTriggeredEvent"/>
/// and calls <see cref="SceneFlowManager.LoadNextScene"/>.
/// </summary>
public class LevelExit : MonoBehaviour, IInteractable
{
    [SerializeField] private bool _requireInteract = false;

    public bool CanInteract => true;

    /// <summary>Called by <see cref="InteractHandler"/> when <see cref="_requireInteract"/> is true.</summary>
    public void Interact(GameObject interactor)
    {
        if (_requireInteract)
            TriggerExit();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_requireInteract) return;

        if (other.CompareTag("Player"))
            TriggerExit();
    }

    private void TriggerExit()
    {
        EventBus<LevelExitTriggeredEvent>.Raise(new LevelExitTriggeredEvent());
        SceneFlowManager.Instance?.LoadNextScene();
    }
}
