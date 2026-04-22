using UnityEngine;

/// <summary>
/// Central coordinator for all hype feedback systems. Subscribes to combat events
/// and orchestrates kill-streak tracking, post-process pulses, camera reactions,
/// and HUD displays. Wire all Inspector references from the scene.
/// </summary>
public class HypeFeedbackOrchestrator : MonoBehaviour
{
    [Header("Sub-systems")]
    [SerializeField] public HypeVolumeController VolumeController;
    [SerializeField] public KillStreakDisplay     StreakDisplay;
    [SerializeField] public KillFeedDisplay       KillFeedDisplay;

    [Header("Streak Settings")]
    [SerializeField] private float _streakWindowSeconds = 4f;

    [Header("Shake — per tier (index 0 = single kill)")]
    [SerializeField] private float[] _streakShakeMagnitudes = { 0.08f, 0.14f, 0.20f, 0.28f };
    [SerializeField] private float[] _streakShakeDurations  = { 0.10f, 0.15f, 0.18f, 0.22f };

    [Header("Punch — per tier")]
    [SerializeField] private float[] _streakPunchAngles     = { 1.5f, 2.5f, 3.5f, 5.0f };
    [SerializeField] private float   _punchDuration         = 0.08f;

    [Header("Hit Feedback")]
    [SerializeField] private float _hitShakeMagnitude = 0.04f;
    [SerializeField] private float _hitShakeDuration  = 0.06f;

    [Header("Land Feedback")]
    [SerializeField] private float _landSpeedThreshold = 4f;
    [SerializeField] private float _landShakeScale     = 0.015f;

    private int   _streakCount;
    private float _streakTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
        EventBus<EnemyDamagedEvent>.Subscribe(OnEnemyDamaged);
        EventBus<WeaponFiredEvent>.Subscribe(OnWeaponFired);
        EventBus<PlayerLandedEvent>.Subscribe(OnPlayerLanded);
    }

    private void OnDisable()
    {
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
        EventBus<EnemyDamagedEvent>.Unsubscribe(OnEnemyDamaged);
        EventBus<WeaponFiredEvent>.Unsubscribe(OnWeaponFired);
        EventBus<PlayerLandedEvent>.Unsubscribe(OnPlayerLanded);
    }

    private void Update()
    {
        if (_streakCount <= 0) return;

        _streakTimer -= Time.deltaTime;
        if (_streakTimer <= 0f)
            ResetStreak();
    }

    // ── EventBus Handlers ─────────────────────────────────────────────────────

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        _streakCount = Mathf.Max(0, _streakCount) + 1;
        _streakTimer = _streakWindowSeconds;

        Vector3 position = e.Enemy != null ? e.Enemy.transform.position : Vector3.zero;
        bool    isStreak = _streakCount >= 2;

        EventBus<KillConfirmedEvent>.Raise(new KillConfirmedEvent
        {
            WorldPosition = position,
            IsStreakKill  = isStreak
        });

        if (isStreak)
        {
            EventBus<KillStreakEvent>.Raise(new KillStreakEvent { StreakCount = _streakCount });
            StreakDisplay?.ShowStreak(_streakCount);
        }

        int tier = Mathf.Clamp(_streakCount - 1, 0, _streakShakeMagnitudes.Length - 1);

        EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
        {
            Magnitude = _streakShakeMagnitudes[tier],
            Duration  = _streakShakeDurations[tier]
        });

        EventBus<CameraPunchEvent>.Raise(new CameraPunchEvent
        {
            EulerAngles = new Vector3(-_streakPunchAngles[tier], 0f, 0f),
            Duration    = _punchDuration
        });

        VolumeController?.PulseKill(Mathf.Clamp01(0.5f + tier * 0.2f));
    }

    private void OnEnemyDamaged(EnemyDamagedEvent e)
    {
        EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
        {
            Magnitude = _hitShakeMagnitude,
            Duration  = _hitShakeDuration
        });

        VolumeController?.PulseContrast();
    }

    private void OnWeaponFired(WeaponFiredEvent e)
    {
        VolumeController?.PulseVignette();
    }

    private void OnPlayerLanded(PlayerLandedEvent e)
    {
        if (e.FallSpeed < _landSpeedThreshold) return;

        float magnitude = e.FallSpeed * _landShakeScale;
        EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
        {
            Magnitude = magnitude,
            Duration  = 0.15f
        });

        VolumeController?.PulseContrast(0.3f);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void ResetStreak()
    {
        _streakCount = 0;
        _streakTimer = 0f;
        EventBus<KillStreakEvent>.Raise(new KillStreakEvent { StreakCount = 0 });
    }
}
