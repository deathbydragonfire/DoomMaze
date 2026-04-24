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
    private Coroutine   _temporaryMusicCoroutine;
    private AudioSource _temporaryMusicSource;
    private AudioSource _temporaryResumeSource;
    private float       _temporaryResumeVolume = 1f;
    private bool        _temporaryResumeWasPlaying;

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
        ConfigureMusicOutputGroups();
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
        ConfigureMusicOutputGroups();
        StopTemporaryClipImmediate();

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

    /// <summary>Temporarily replaces normal music with a direct clip, preserving the current source for resume.</summary>
    public void PlayTemporaryClip(AudioClip clip, float targetVolume, float fadeOutDuration, float fadeInDuration)
    {
        if (clip == null)
            return;

        ConfigureMusicOutputGroups();
        targetVolume = Mathf.Clamp01(targetVolume);

        if (_temporaryMusicSource != null && _temporaryMusicSource.clip == clip)
        {
            _temporaryMusicSource.volume = Mathf.Max(_temporaryMusicSource.volume, targetVolume);
            return;
        }

        if (_temporaryMusicSource != null || _temporaryResumeSource != null)
            StopTemporaryClipImmediate();

        if (_temporaryMusicCoroutine != null)
        {
            StopCoroutine(_temporaryMusicCoroutine);
            _temporaryMusicCoroutine = null;
        }

        _temporaryMusicCoroutine = StartCoroutine(PlayTemporaryClipRoutine(
            clip,
            targetVolume,
            Mathf.Max(0.01f, fadeOutDuration),
            Mathf.Max(0.01f, fadeInDuration)));
    }

    /// <summary>Temporarily fades normal music to silence, preserving it for resume.</summary>
    public void PlayTemporarySilence(float fadeOutDuration)
    {
        ConfigureMusicOutputGroups();

        if (_temporaryResumeSource != null && _temporaryMusicSource == null)
            return;

        if (_temporaryMusicSource != null || _temporaryResumeSource != null)
            StopTemporaryClipImmediate();

        if (_temporaryMusicCoroutine != null)
        {
            StopCoroutine(_temporaryMusicCoroutine);
            _temporaryMusicCoroutine = null;
        }

        _temporaryMusicCoroutine = StartCoroutine(PlayTemporarySilenceRoutine(Mathf.Max(0.01f, fadeOutDuration)));
    }

    /// <summary>Stops a temporary direct clip and fades the previous normal music back in.</summary>
    public void StopTemporaryClip(float fadeOutDuration)
    {
        if (_temporaryMusicSource == null && _temporaryResumeSource == null)
            return;

        if (_temporaryMusicCoroutine != null)
        {
            StopCoroutine(_temporaryMusicCoroutine);
            _temporaryMusicCoroutine = null;
        }

        _temporaryMusicCoroutine = StartCoroutine(StopTemporaryClipRoutine(Mathf.Max(0.01f, fadeOutDuration)));
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

        if (_temporaryMusicCoroutine != null)
        {
            StopCoroutine(_temporaryMusicCoroutine);
            _temporaryMusicCoroutine = null;
        }

        _activeSource.Stop();
        _inactiveSource.Stop();
        _activeSource.volume = 1f;
        _inactiveSource.volume = 0f;
        _pausedGameplaySource = null;
        _pauseMusicSource = null;
        _pausedGameplayResumeVolume = 1f;
        _temporaryMusicSource = null;
        _temporaryResumeSource = null;
        _temporaryResumeVolume = 1f;
        _temporaryResumeWasPlaying = false;
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

    private IEnumerator PlayTemporarySilenceRoutine(float fadeOutDuration)
    {
        StabilizeCurrentMusicState();

        _temporaryMusicSource = null;
        _temporaryResumeSource = GetDominantPlayingSource();
        _temporaryResumeWasPlaying = _temporaryResumeSource != null
            && _temporaryResumeSource.isPlaying
            && _temporaryResumeSource.clip != null;
        _temporaryResumeVolume = _temporaryResumeSource != null
            ? Mathf.Max(_temporaryResumeSource.volume, 0.0001f)
            : 1f;

        if (_temporaryResumeWasPlaying)
        {
            float startingVolume = _temporaryResumeSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                _temporaryResumeSource.volume = Mathf.Lerp(startingVolume, 0f, t);
                yield return null;
            }

            _temporaryResumeSource.volume = 0f;
            _temporaryResumeSource.Pause();
        }

        _temporaryMusicCoroutine = null;
    }

    private IEnumerator PlayTemporaryClipRoutine(AudioClip clip, float targetVolume, float fadeOutDuration, float fadeInDuration)
    {
        StabilizeCurrentMusicState();

        _temporaryResumeSource = GetDominantPlayingSource();
        _temporaryResumeWasPlaying = _temporaryResumeSource != null
            && _temporaryResumeSource.isPlaying
            && _temporaryResumeSource.clip != null;
        _temporaryResumeVolume = _temporaryResumeSource != null
            ? Mathf.Max(_temporaryResumeSource.volume, 0.0001f)
            : 1f;

        _temporaryMusicSource = GetAlternateSource(_temporaryResumeSource);
        if (_temporaryMusicSource == null)
        {
            _temporaryMusicCoroutine = null;
            yield break;
        }

        _temporaryMusicSource.Stop();
        _temporaryMusicSource.clip = clip;
        _temporaryMusicSource.loop = true;
        _temporaryMusicSource.volume = 0f;
        _temporaryMusicSource.Play();

        if (_temporaryResumeWasPlaying)
        {
            float startingVolume = _temporaryResumeSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                _temporaryResumeSource.volume = Mathf.Lerp(startingVolume, 0f, t);
                yield return null;
            }

            _temporaryResumeSource.volume = 0f;
            _temporaryResumeSource.Pause();
        }

        float fadeInElapsed = 0f;
        while (fadeInElapsed < fadeInDuration)
        {
            fadeInElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeInElapsed / fadeInDuration);
            _temporaryMusicSource.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        _temporaryMusicSource.volume = targetVolume;
        _activeSource = _temporaryMusicSource;
        _inactiveSource = GetAlternateSource(_activeSource);
        _temporaryMusicCoroutine = null;
    }

    private IEnumerator StopTemporaryClipRoutine(float fadeOutDuration)
    {
        AudioSource temporarySource = _temporaryMusicSource;
        AudioSource resumeSource = _temporaryResumeSource;
        float resumeTargetVolume = Mathf.Max(_temporaryResumeVolume, 0.0001f);
        bool shouldResume = _temporaryResumeWasPlaying && resumeSource != null && resumeSource.clip != null;

        if (shouldResume)
        {
            resumeSource.UnPause();
            resumeSource.volume = 0f;
        }

        float temporaryStartVolume = temporarySource != null ? temporarySource.volume : 0f;
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);

            if (temporarySource != null)
                temporarySource.volume = Mathf.Lerp(temporaryStartVolume, 0f, t);

            if (shouldResume)
                resumeSource.volume = Mathf.Lerp(0f, resumeTargetVolume, t);

            yield return null;
        }

        if (temporarySource != null)
        {
            temporarySource.volume = 0f;
            temporarySource.Stop();
            temporarySource.clip = null;
        }

        if (shouldResume)
        {
            resumeSource.volume = resumeTargetVolume;
            _activeSource = resumeSource;
            _inactiveSource = GetAlternateSource(resumeSource);
        }

        _temporaryMusicSource = null;
        _temporaryResumeSource = null;
        _temporaryResumeVolume = 1f;
        _temporaryResumeWasPlaying = false;
        _temporaryMusicCoroutine = null;
    }

    private void StopTemporaryClipImmediate()
    {
        if (_temporaryMusicCoroutine != null)
        {
            StopCoroutine(_temporaryMusicCoroutine);
            _temporaryMusicCoroutine = null;
        }

        if (_temporaryMusicSource != null)
        {
            _temporaryMusicSource.volume = 0f;
            _temporaryMusicSource.Stop();
            _temporaryMusicSource.clip = null;
        }

        if (_temporaryResumeWasPlaying && _temporaryResumeSource != null && _temporaryResumeSource.clip != null)
        {
            _temporaryResumeSource.UnPause();
            _temporaryResumeSource.volume = Mathf.Max(_temporaryResumeVolume, 0.0001f);
            _activeSource = _temporaryResumeSource;
            _inactiveSource = GetAlternateSource(_temporaryResumeSource);
        }

        _temporaryMusicSource = null;
        _temporaryResumeSource = null;
        _temporaryResumeVolume = 1f;
        _temporaryResumeWasPlaying = false;
    }

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

    private void ConfigureMusicOutputGroups()
    {
        AudioManager.Instance?.ConfigureMusicSource(_musicSourceA);
        AudioManager.Instance?.ConfigureMusicSource(_musicSourceB);
    }
}
