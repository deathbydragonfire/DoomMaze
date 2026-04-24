using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standalone player decay resource. It drains during active gameplay, refills on
/// enemy kills, pauses in upgrade rooms, and damages health while empty.
/// </summary>
public class PlayerDecayComponent : MonoBehaviour
{
    [SerializeField] private float _fullDrainDuration = 90f;
    [SerializeField] [Range(0f, 1f)] private float _killRefillAmount = 0.2f;
    [SerializeField] private float _emptyDamagePerSecond = 5f;

    private readonly HashSet<UpgradeRoomController> _upgradePauseRooms = new HashSet<UpgradeRoomController>();

    private HealthComponent _health;
    private float _decayNormalized = 1f;
    private float _pendingEmptyDamage;

    public float DecayNormalized => _decayNormalized;
    public float GrayscaleAmount => CalculateGrayscaleAmount(_decayNormalized);

    private void Awake()
    {
        _health = GetComponentInParent<HealthComponent>();
    }

    private void OnEnable()
    {
        EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
        EventBus<UpgradeRoomPresenceChangedEvent>.Subscribe(OnUpgradeRoomPresenceChanged);
    }

    private void Start()
    {
        RaiseDecayChanged();
    }

    private void OnDisable()
    {
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
        EventBus<UpgradeRoomPresenceChangedEvent>.Unsubscribe(OnUpgradeRoomPresenceChanged);
    }

    private void Update()
    {
        if (!ShouldDecayRun())
            return;

        float previousDecay = _decayNormalized;

        if (_decayNormalized > 0f)
        {
            float drainMultiplier = Mathf.Max(0f, GameDifficultyManager.CurrentProfile.DecayRateMultiplier);
            float drainRate = drainMultiplier / Mathf.Max(0.01f, _fullDrainDuration);
            _decayNormalized = Mathf.Max(0f, _decayNormalized - drainRate * Time.deltaTime);
        }

        if (_decayNormalized <= 0f)
            ApplyEmptyDamage();

        if (!Mathf.Approximately(previousDecay, _decayNormalized))
            RaiseDecayChanged();
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (_health != null && !_health.IsAlive)
            return;

        SetDecay(_decayNormalized + _killRefillAmount);
    }

    private void OnUpgradeRoomPresenceChanged(UpgradeRoomPresenceChangedEvent e)
    {
        if (e.Room == null)
            return;

        if (e.IsPlayerInside)
            _upgradePauseRooms.Add(e.Room);
        else
            _upgradePauseRooms.Remove(e.Room);
    }

    private bool ShouldDecayRun()
    {
        if (_health == null)
            _health = GetComponentInParent<HealthComponent>();

        if (_health == null || !_health.IsAlive)
            return false;

        if (_upgradePauseRooms.Count > 0)
            return false;

        if (UpgradeRoomController.IsPlayerInAnyUpgradeRoom)
            return false;

        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            return false;

        return GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing;
    }

    private void ApplyEmptyDamage()
    {
        if (_health == null || !_health.IsAlive)
            return;

        _pendingEmptyDamage += Mathf.Max(0f, _emptyDamagePerSecond) * Time.deltaTime;
        int damage = Mathf.FloorToInt(_pendingEmptyDamage);
        if (damage <= 0)
            return;

        _pendingEmptyDamage -= damage;
        _health.TakeDamage(new DamageInfo
        {
            Amount = damage,
            Type = DamageType.Energy,
            Source = gameObject,
            IgnoreInvulnerability = true,
            IgnoreArmor = true
        });
    }

    private void SetDecay(float value)
    {
        float previousDecay = _decayNormalized;
        _decayNormalized = Mathf.Clamp01(value);

        if (_decayNormalized > 0f)
            _pendingEmptyDamage = 0f;

        if (!Mathf.Approximately(previousDecay, _decayNormalized))
            RaiseDecayChanged();
    }

    private void RaiseDecayChanged()
    {
        EventBus<PlayerDecayChangedEvent>.Raise(new PlayerDecayChangedEvent
        {
            DecayNormalized = _decayNormalized,
            GrayscaleAmount = GrayscaleAmount
        });
    }

    private static float CalculateGrayscaleAmount(float decayNormalized)
    {
        return decayNormalized >= 0.5f
            ? 0f
            : Mathf.Clamp01(Mathf.InverseLerp(0.5f, 0f, decayNormalized));
    }
}
