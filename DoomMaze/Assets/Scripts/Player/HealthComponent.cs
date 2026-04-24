using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable component that owns current HP, death detection, invulnerability frames,
/// and delegates armor mitigation to <see cref="ArmorComponent"/> when present.
/// Implements <see cref="IDamageable"/>. Works for both the player and enemies.
/// </summary>
public class HealthComponent : MonoBehaviour, IDamageable
{
    [SerializeField] private int  _maxHealth = 100;
    [SerializeField] private bool _isPlayer  = false;

    private const float LOW_HEALTH_THRESHOLD = 0.25f;

    public int  CurrentHealth  { get; private set; }
    public int  MaxHealth      => _maxHealth;
    public bool IsAlive        { get; private set; }
    public bool IsPlayer       => _isPlayer;
    public bool IsInvulnerable { get; private set; }

    /// <summary>Raised when this entity dies. Subscribers own any post-death logic (e.g. deactivation).</summary>
    public event Action OnDied;

    /// <summary>Raised after each damage application, before the death check.</summary>
    public event Action<DamageInfo> OnDamaged;

    private ArmorComponent _armorComponent;
    private WaitForSeconds _invulnerabilityWait;
    private bool           _wasLowHealth;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        CurrentHealth        = _maxHealth;
        IsAlive              = true;
        _armorComponent      = GetComponent<ArmorComponent>();
        _invulnerabilityWait = new WaitForSeconds(GetInvulnerabilityTime());
    }

    private float GetInvulnerabilityTime()
    {
        // Attempt to read from PlayerStats if available on the scene;
        // falls back to a sensible constant so HealthComponent works on enemies too.
        return 0.5f;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Apply damage through the unified pipeline.</summary>
    public void TakeDamage(DamageInfo info)
    {
        if (info.Amount <= 0f)
        {
            Debug.LogWarning($"[HealthComponent] TakeDamage called with Amount <= 0 on {gameObject.name}. Skipping.");
            return;
        }

        if (!IsAlive || IsInvulnerable)
            return;

        // Armor mitigation (player only has ArmorComponent by default)
        DamageInfo mitigated = _armorComponent != null
            ? _armorComponent.MitigateDamage(info)
            : info;

        int damage = Mathf.RoundToInt(mitigated.Amount);
        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, _maxHealth);

        OnDamaged?.Invoke(mitigated);

        if (_isPlayer)
        {
            EventBus<PlayerDamagedEvent>.Raise(new PlayerDamagedEvent
            {
                CurrentHealth = CurrentHealth,
                MaxHealth     = _maxHealth,
                Info          = mitigated
            });

            bool isLow = (float)CurrentHealth / _maxHealth <= LOW_HEALTH_THRESHOLD;
            if (isLow != _wasLowHealth)
            {
                _wasLowHealth = isLow;
                EventBus<PlayerLowHealthEvent>.Raise(new PlayerLowHealthEvent { IsLow = isLow });
            }
        }

        if (CurrentHealth == 0)
        {
            Die();
        }
        else if (_isPlayer)
        {
            StartCoroutine(InvulnerabilityRoutine());
        }
    }

    /// <summary>Restore HP, clamped to <see cref="MaxHealth"/>.</summary>
    public int Heal(int amount)
    {
        if (!IsAlive || amount <= 0)
            return 0;

        int previousHealth = CurrentHealth;

        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, _maxHealth);
        int restoredAmount = CurrentHealth - previousHealth;

        if (restoredAmount <= 0)
            return 0;

        if (_isPlayer)
        {
            EventBus<PlayerHealedEvent>.Raise(new PlayerHealedEvent
            {
                CurrentHealth = CurrentHealth,
                MaxHealth     = _maxHealth
            });

            bool isLow = (float)CurrentHealth / _maxHealth <= LOW_HEALTH_THRESHOLD;
            if (isLow != _wasLowHealth)
            {
                _wasLowHealth = isLow;
                EventBus<PlayerLowHealthEvent>.Raise(new PlayerLowHealthEvent { IsLow = isLow });
            }
        }

        return restoredAmount;
    }

    /// <summary>Instantly kills this entity.</summary>
    public void Kill()
    {
        if (!IsAlive) return;
        CurrentHealth = 0;
        Die();
    }

    /// <summary>Resets HP and alive state back to full.</summary>
    public void ResetHealth()
    {
        CurrentHealth    = _maxHealth;
        IsAlive          = true;
        IsInvulnerable   = false;
        _wasLowHealth    = false;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Die()
    {
        IsAlive = false;

        if (_isPlayer)
        {
            EventBus<PlayerDiedEvent>.Raise(new PlayerDiedEvent());
            GameManager.Instance?.SetState(GameState.Dead);
        }
        else
        {
            OnDied?.Invoke(); // EnemyBase subscribes and owns deactivation logic
        }
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        IsInvulnerable = true;
        yield return _invulnerabilityWait;
        IsInvulnerable = false;
    }
}
