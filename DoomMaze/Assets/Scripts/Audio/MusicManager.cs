using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent singleton that owns music playback and crossfade between tracks.
/// Subscribes to <see cref="MusicZoneChangedEvent"/> and <see cref="GameStateChangedEvent"/>
/// via the EventBus.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] private MusicDatabase _musicDatabase;
    [SerializeField] private AudioSource   _musicSourceA;
    [SerializeField] private AudioSource   _musicSourceB;
    [SerializeField] private float         _crossfadeDuration = 1f;

    private AudioSource _activeSource;
    private AudioSource _inactiveSource;
    private Coroutine   _crossfadeCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_musicDatabase == null)
            Debug.LogError("[MusicManager] _musicDatabase is not assigned. Assign in the Inspector.");
        if (_musicSourceA == null)
            Debug.LogError("[MusicManager] _musicSourceA is not assigned. Assign in the Inspector.");
        if (_musicSourceB == null)
            Debug.LogError("[MusicManager] _musicSourceB is not assigned. Assign in the Inspector.");

        _activeSource   = _musicSourceA;
        _inactiveSource = _musicSourceB;
    }

    private void Start()
    {
        EventBus<MusicZoneChangedEvent>.Subscribe(OnMusicZoneChanged);
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
    }

    private void OnDestroy()
    {
        EventBus<MusicZoneChangedEvent>.Unsubscribe(OnMusicZoneChanged);
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Crossfades to the clip matching trackId. No-ops if already playing.</summary>
    public void PlayTrack(string trackId)
    {
        if (_musicDatabase == null) return;

        AudioClip clip = _musicDatabase.GetClip(trackId);
        if (clip == null) return;

        if (_activeSource.isPlaying && _activeSource.clip == clip) return;

        _inactiveSource.clip   = clip;
        _inactiveSource.volume = 0f;
        _inactiveSource.Play();

        if (_crossfadeCoroutine != null)
            StopCoroutine(_crossfadeCoroutine);

        _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine());
    }

    /// <summary>Stops all music immediately.</summary>
    public void Stop()
    {
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = null;
        }

        _activeSource.Stop();
        _inactiveSource.Stop();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private IEnumerator CrossfadeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < _crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _crossfadeDuration);

            _activeSource.volume   = 1f - t;
            _inactiveSource.volume = t;

            yield return null;
        }

        _activeSource.volume   = 0f;
        _inactiveSource.volume = 1f;
        _activeSource.Stop();

        (_activeSource, _inactiveSource) = (_inactiveSource, _activeSource);
        _crossfadeCoroutine = null;
    }

    // ── EventBus handlers ─────────────────────────────────────────────────────

    private void OnMusicZoneChanged(MusicZoneChangedEvent e)
    {
        PlayTrack(e.TrackId);
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Dead || e.NewState == GameState.MainMenu)
            Stop();
    }
}
