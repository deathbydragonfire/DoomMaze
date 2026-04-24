using UnityEngine;

/// <summary>
/// Runtime trigger that asks its owning upgrade room to start/stop room music.
/// </summary>
public class UpgradeRoomMusicTrigger : MonoBehaviour
{
    private UpgradeRoomController _controller;

    public void Configure(UpgradeRoomController controller)
    {
        _controller = controller;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _controller?.HandlePlayerEnteredMusicZone();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _controller?.HandlePlayerExitedMusicZone();
    }
}
