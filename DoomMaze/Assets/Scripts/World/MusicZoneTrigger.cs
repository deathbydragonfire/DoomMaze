using UnityEngine;

/// <summary>
/// On player enter, raises <see cref="MusicZoneChangedEvent"/> so AudioManager (Phase 7)
/// can crossfade to the configured track.
/// </summary>
public class MusicZoneTrigger : MonoBehaviour
{
    [SerializeField] private string _trackId;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        EventBus<MusicZoneChangedEvent>.Raise(new MusicZoneChangedEvent
        {
            TrackId = _trackId
        });
    }
}
