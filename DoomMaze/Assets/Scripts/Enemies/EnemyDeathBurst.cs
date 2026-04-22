using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns a sprite billboard ring and a pooled particle burst at the enemy's
/// death position. Wire via Inspector on each enemy prefab; call from
/// <see cref="EnemyBase.OnDeath"/>.
/// </summary>
public class EnemyDeathBurst : MonoBehaviour
{
    [Header("Sprite Flash")]
    [SerializeField] private Sprite[] _deathSpriteFrames;
    [SerializeField] private float    _deathFrameRate = 12f;

    [Header("Particle Burst")]
    [SerializeField] private ParticleSystem _deathParticlePrefab;
    [SerializeField] private int            _poolSize = 4;

    [Header("Camera Shake")]
    [SerializeField] private float _deathShakeMagnitude = 0.12f;
    [SerializeField] private float _deathShakeDuration  = 0.14f;

    private ObjectPool<ParticleSystem> _particlePool;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_deathParticlePrefab == null)
        {
            Debug.LogWarning("[EnemyDeathBurst] _deathParticlePrefab is not assigned — particle bursts will not spawn.");
            return;
        }

        GameObject poolRoot = new GameObject("[DeathBurstPool]");
        poolRoot.transform.SetParent(transform);

        _particlePool = new ObjectPool<ParticleSystem>(_deathParticlePrefab, _poolSize, poolRoot.transform);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Triggers the full death burst effect at the given world position.</summary>
    public void Burst(Vector3 position)
    {
        if (_deathSpriteFrames != null && _deathSpriteFrames.Length > 0)
            ImpactFXManager.Instance?.Spawn(position, Vector3.up, _deathSpriteFrames, _deathFrameRate);

        if (_particlePool != null)
            StartCoroutine(SpawnParticleRoutine(position));

        EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
        {
            Magnitude = _deathShakeMagnitude,
            Duration  = _deathShakeDuration
        });
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private IEnumerator SpawnParticleRoutine(Vector3 position)
    {
        ParticleSystem ps = _particlePool.Get(position, Quaternion.identity);
        ps.Play();

        yield return new WaitForSeconds(ps.main.duration + ps.main.startLifetime.constantMax);

        _particlePool.Return(ps);
    }
}
