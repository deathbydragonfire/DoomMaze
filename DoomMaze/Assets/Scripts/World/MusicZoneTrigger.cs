using UnityEngine;

/// <summary>
/// Raises <see cref="MusicZoneChangedEvent"/> for a randomly selected configured track.
/// Can auto-play on scene start and optionally respond to trigger entry later.
/// </summary>
public class MusicZoneTrigger : MonoBehaviour
{
    [SerializeField] private bool _playRandomTrackOnStart = true;
    [SerializeField] private bool _triggerZoneEnabled = false;
    [SerializeField] private string _trackId;
    [SerializeField] private string[] _playlistTrackIds;

    private string _lastSelectedTrackId;
    private bool _hasPlayedStartupTrack;

    private void OnEnable()
    {
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        TryPlayStartupTrack();
    }

    private void OnDisable()
    {
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_triggerZoneEnabled) return;
        if (!other.CompareTag("Player")) return;

        RaiseSelectedTrack();
    }

    private void RaiseSelectedTrack()
    {
        string selectedTrackId = SelectTrackId();
        if (string.IsNullOrWhiteSpace(selectedTrackId))
        {
            Debug.LogWarning("[MusicZoneTrigger] No track ID configured on this music zone.", this);
            return;
        }

        EventBus<MusicZoneChangedEvent>.Raise(new MusicZoneChangedEvent
        {
            TrackId = selectedTrackId
        });
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Playing)
            TryPlayStartupTrack();
    }

    private void TryPlayStartupTrack()
    {
        if (!_playRandomTrackOnStart || _hasPlayedStartupTrack)
            return;

        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            return;

        _hasPlayedStartupTrack = true;
        RaiseSelectedTrack();
    }

    private string SelectTrackId()
    {
        int validTrackCount = CountValidPlaylistTracks();
        if (validTrackCount == 0)
            return _trackId;

        if (validTrackCount == 1)
        {
            string onlyTrackId = GetValidPlaylistTrackAt(0);
            _lastSelectedTrackId = onlyTrackId;
            return onlyTrackId;
        }

        int selectionIndex = Random.Range(0, validTrackCount);
        string selectedTrackId = GetValidPlaylistTrackAt(selectionIndex);

        if (selectedTrackId == _lastSelectedTrackId)
        {
            selectionIndex = (selectionIndex + 1) % validTrackCount;
            selectedTrackId = GetValidPlaylistTrackAt(selectionIndex);
        }

        _lastSelectedTrackId = selectedTrackId;
        return selectedTrackId;
    }

    private int CountValidPlaylistTracks()
    {
        if (_playlistTrackIds == null || _playlistTrackIds.Length == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < _playlistTrackIds.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(_playlistTrackIds[i]))
                count++;
        }

        return count;
    }

    private string GetValidPlaylistTrackAt(int validIndex)
    {
        if (_playlistTrackIds == null)
            return null;

        int currentValidIndex = 0;
        for (int i = 0; i < _playlistTrackIds.Length; i++)
        {
            string trackId = _playlistTrackIds[i];
            if (string.IsNullOrWhiteSpace(trackId))
                continue;

            if (currentValidIndex == validIndex)
                return trackId;

            currentValidIndex++;
        }

        return null;
    }
}
