using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies fire damage when flamethrower particles collide with a target.
/// Damage is rate-limited per health component so a single spray cannot stack
/// dozens of hits in the same instant.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class FlamethrowerParticleDamage : MonoBehaviour
{
    private readonly List<ParticleCollisionEvent> _collisionEvents = new List<ParticleCollisionEvent>();
    private readonly Dictionary<int, float> _nextDamageTimeByTarget = new Dictionary<int, float>();

    private ParticleSystem _particleSystem;
    private float _damagePerTick;
    private float _damageInterval = 0.1f;
    private GameObject _damageSource;
    private LayerMask _damageMask;

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
    }

    /// <summary>Updates the active damage profile driven by the owning flamethrower weapon.</summary>
    public void Configure(float damagePerTick, float damageInterval, GameObject damageSource, LayerMask damageMask)
    {
        _damagePerTick = damagePerTick;
        _damageInterval = Mathf.Max(0.01f, damageInterval);
        _damageSource = damageSource;
        _damageMask = damageMask;
    }

    private void OnParticleCollision(GameObject other)
    {
        if (_particleSystem == null || _damagePerTick <= 0f) return;
        if ((_damageMask.value & (1 << other.layer)) == 0) return;

        int collisionCount = _particleSystem.GetCollisionEvents(other, _collisionEvents);
        if (collisionCount <= 0) return;

        HealthComponent health = other.GetComponentInParent<HealthComponent>();
        if (health == null || !health.IsAlive) return;

        int targetId = health.GetInstanceID();
        if (_nextDamageTimeByTarget.TryGetValue(targetId, out float nextDamageTime) && Time.time < nextDamageTime)
            return;

        health.TakeDamage(new DamageInfo
        {
            Amount = _damagePerTick,
            Type = DamageType.Fire,
            Source = _damageSource
        });

        _nextDamageTimeByTarget[targetId] = Time.time + _damageInterval;
    }
}
