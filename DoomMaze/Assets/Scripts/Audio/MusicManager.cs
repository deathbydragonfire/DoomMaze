using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent singleton that owns music playback and crossfade between tracks.
/// Subscribes to <see cref="MusicZoneChangedEvent"/> and <see cref="GameStateChangedEvent"/>
/// via the EventBus, and fades music for pause transitions using unscaled time.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] private MusicDatabase _musicDatabase;
    [SerializeField] private AudioSource   _musicSourceA;
    [SerializeField] private AudioSource   _musicSourceB;
    [SerializeField] private float         _crossfadeDuration = 1f;
    [SerializeField] private float         _pauseFadeDuration = 0.35f;

    private AudioSource _activeSource;
    private AudioSource _inactiveSource;
    private Coroutine   _crossfadeCoroutine;
    private Coroutine   _pauseTransitionCoroutine;
    private AudioSource _pausedGameplaySource;
    private AudioSource _pauseMusicSource;
    private float       _pausedGameplayResumeVolume = 1f;
    private string      _pauseTrackId;

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

    private void OnEnable()
    {
        EventBus<MusicZoneChangedEvent>.Subscribe(OnMusicZoneChanged);
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        EventBus<PauseChangedEvent>.Subscribe(OnPauseChanged);
    }

    private void OnDisable()
    {
        EventBus<MusicZoneChangedEvent>.Unsubscribe(OnMusicZoneChanged);
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        EventBus<PauseChangedEvent>.Unsubscribe(OnPauseChanged);
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

    /// <summary>Sets the optional track to play while the game is paused.</summary>
    public void SetPauseTrack(string trackId)
    {
        _pauseTrackId = trackId;
    }

    /// <summary>Clears the pause-music override track.</summary>
    public void ClearPauseTrack()
    {
        _pauseTrackId = null;
    }

    /// <summary>Stops all music immediately.</summary>
    public void Stop()
    {
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = null;
        }

        if (_pauseTransitionCoroutine != null)
        {
            StopCoroutine(_pauseTransitionCoroutine);
            _pauseTransitionCoroutine = null;
        }

        _activeSource.Stop();
        _inactiveSource.Stop();
        _activeSource.volume = 1f;
        _inactiveSource.volume = 0f;
        _pausedGameplaySource = null;
        _pauseMusicSource = null;
        _pausedGameplayResumeVolume = 1f;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private IEnumerator CrossfadeRoutine()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _crossfadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

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

    private void OnPauseChanged(PauseChangedEvent e)
    {
        if (_pauseTransitionCoroutine != null)
        {
            StopCoroutine(_pauseTransitionCoroutine);
            _pauseTransitionCoroutine = null;
        }

        _pauseTransitionCoroutine = StartCoroutine(e.IsPaused
            ? PauseMusicRoutine()
            : ResumeMusicRoutine());
    }

    private IEnumerator PauseMusicRoutine()
    {
        StabilizeCurrentMusicState();

        _pausedGameplaySource = GetDominantPlayingSource();
        _pauseMusicSource = GetAlternateSource(_pausedGameplaySource);
        _pausedGameplayResumeVolume = _pausedGameplaySource != null
            ? Mathf.Max(_pausedGameplaySource.volume, 0.0001f)
            : 1f;

        if (_pauseMusicSource != null)
        {
            _pauseMusicSource.Stop();
            _pauseMusicSource.clip = null;
            _pauseMusicSource.volume = 0f;
        }

        float duration = GetPauseFadeDuration();
        if (_pausedGameplaySource != null && _pausedGameplaySource.isPlaying)
        {
            float startingVolume = _pausedGameplaySource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _pausedGameplaySource.volume = Mathf.Lerp(startingVolume, 0f, t);
                yield return null;
            }

            _pausedGameplaySource.volume = 0f;
            _pausedGameplaySource.Pause();
        }

        AudioClip pauseClip = GetPauseClip();
        if (pauseClip != null && _pauseMusicSource != null)
        {
            _pauseMusicSource.clip = pauseClip;
            _pauseMusicSource.volume = 0f;
            _pauseMusicSource.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _pauseMusicSource.volume = t;
                yield return null;
            }

            _pauseMusicSource.volume = 1f;
        }

        _pauseTransitionCoroutine = null;
    }

    private IEnumerator ResumeMusicRoutine()
    {
        AudioSource gameplaySource = _pausedGameplaySource;
        AudioSource pauseSource = _pauseMusicSource;
        float gameplayTargetVolume = Mathf.Max(_pausedGameplayResumeVolume, 0.0001f);
        float duration = GetPauseFadeDuration();

        if (gameplaySource != null && gameplaySource.clip != null)
        {
            gameplaySource.UnPause();
            gameplaySource.volume = 0f;
        }

        float pauseStartingVolume = pauseSource != null ? pauseSource.volume : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (gameplaySource != null && gameplaySource.clip != null)
                gameplaySource.volume = Mathf.Lerp(0f, gameplayTargetVolume, t);

            if (pauseSource != null && pauseSource.isPlaying)
                pauseSource.volume = Mathf.Lerp(pauseStartingVolume, 0f, t);

            yield return null;
        }

        if (gameplaySource != null && gameplaySource.clip != null)
            gameplaySource.volume = gameplayTargetVolume;

        if (pauseSource != null)
        {
            pauseSource.volume = 0f;
            pauseSource.Stop();
            pauseSource.clip = null;
        }

        if (gameplaySource != null)
        {
            _activeSource = gameplaySource;
            _inactiveSource = GetAlternateSource(gameplaySource);
        }

        _pausedGameplaySource = null;
        _pauseMusicSource = null;
        _pausedGameplayResumeVolume = 1f;
        _pauseTransitionCoroutine = null;
    }

    private void StabilizeCurrentMusicState()
    {
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = null;
        }

        AudioSource dominantSource = GetDominantPlayingSource();
        AudioSource secondarySource = GetAlternateSource(dominantSource);

        if (dominantSource != null)
        {
            _activeSource = dominantSource;
            _inactiveSource = secondarySource;
        }

        if (secondarySource != null)
        {
            secondarySource.Stop();
            secondarySource.volume = 0f;
            if (secondarySource != _pauseMusicSource)
                secondarySource.clip = null;
        }
    }

    private AudioSource GetDominantPlayingSource()
    {
        bool sourceAPlaying = _musicSourceA != null && _musicSourceA.isPlaying && _musicSourceA.clip != null;
        bool sourceBPlaying = _musicSourceB != null && _musicSourceB.isPlaying && _musicSourceB.clip != null;

        if (sourceAPlaying && sourceBPlaying)
            return _musicSourceA.volume >= _musicSourceB.volume ? _musicSourceA : _musicSourceB;

        if (sourceAPlaying)
            return _musicSourceA;

        if (sourceBPlaying)
            return _musicSourceB;

        if (_activeSource != null && _activeSource.clip != null)
            return _activeSource;

        if (_musicSourceA != null && _musicSourceA.clip != null)
            return _musicSourceA;

        if (_musicSourceB != null && _musicSourceB.clip != null)
            return _musicSourceB;

        return _activeSource;
    }

    private AudioSource GetAlternateSource(AudioSource source)
    {
        if (source == null)
            return _musicSourceA != null ? _musicSourceA : _musicSourceB;

        if (source == _musicSourceA)
            return _musicSourceB;

        if (source == _musicSourceB)
            return _musicSourceA;

        return _inactiveSource;
    }

    private AudioClip GetPauseClip()
    {
        if (_musicDatabase == null || string.IsNullOrWhiteSpace(_pauseTrackId))
            return null;

        return _musicDatabase.GetClip(_pauseTrackId);
    }

    private float GetPauseFadeDuration()
    {
        return Mathf.Max(0.01f, _pauseFadeDuration);
    }
}
