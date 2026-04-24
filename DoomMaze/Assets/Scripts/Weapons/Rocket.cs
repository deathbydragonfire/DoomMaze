using System.Collections;
using UnityEngine;

/// <summary>
/// Poolable projectile for the rocket launcher.
/// Moves with sphere-cast collision, detonates on impact or range timeout, applies splash damage
/// and knockback, optionally spawns an explosion FX prefab, then returns itself to its pool.
/// </summary>
public class Rocket : MonoBehaviour
{
    private const float DefaultSpeed = 25f;
    private const float DefaultCollisionRadius = 0.35f;
    private const float DefaultExplosionRadius = 3f;
    private const float DefaultKnockbackPower = 18f;
    private const float DefaultExplosionFxLifetime = 2f;
    private const float DefaultAudioMaxDistance = 30f;

    [Header("Flight")]
    [SerializeField] private float _speed = DefaultSpeed;
    [SerializeField] private float _launchSpeed = 1f;
    [SerializeField] private float _ignitionDelay = 1f;
    [SerializeField] private float _ignitionRampTime = 0.08f;
    [SerializeField] private float _collisionRadius = DefaultCollisionRadius;

    [Header("Explosion")]
    [SerializeField] private float _explosionRadius = DefaultExplosionRadius;
    [SerializeField] private float _knockbackPower = DefaultKnockbackPower;
    [SerializeField] private float _innerKillRadius = 1.25f;
    [Range(0f, 1f)] [SerializeField] private float _edgeDamageMultiplier = 0f;
    [SerializeField] private GameObject _explosionFxPrefab;
    [SerializeField] private float _explosionFxLifetime = DefaultExplosionFxLifetime;
    [ColorUsage(true, true)] [SerializeField] private Color _explosionGlowColor = new Color(3f, 1.45f, 0.35f, 1f);
    [SerializeField] private float _explosionGlowSize = 3.5f;
    [SerializeField] private float _explosionGlowDuration = 0.18f;
    [ColorUsage(true, true)] [SerializeField] private Color _explosionLightColor = new Color(1f, 0.72f, 0.38f, 1f);
    [SerializeField] private float _explosionLightIntensity = 8f;
    [SerializeField] private float _explosionLightRange = 5f;
    [SerializeField] private float _explosionLightDuration = 0.12f;

    [Header("Visuals")]
    [SerializeField] private Renderer[] _flightRenderers;

    [Header("Audio")]
    [SerializeField] private AudioClip[] _flightSounds;
    [Range(0f, 1f)] [SerializeField] private float _flightSoundVolume = 1f;
    [SerializeField] private AudioClip[] _explosionSounds;
    [Range(0f, 1f)] [SerializeField] private float _explosionSoundVolume = 1f;

    private readonly Collider[] _overlapBuffer = new Collider[32];
    private readonly RaycastHit[] _castBuffer = new RaycastHit[8];
    private readonly HealthComponent[] _healthBuffer = new HealthComponent[16];
    private readonly EnemyBase[] _enemyBuffer = new EnemyBase[16];

    private ObjectPool<Rocket> _pool;
    private SphereCollider _sphereCollider;
    private AudioSource _audioSource;
    private Transform _ownerRoot;
    private Vector3 _direction;
    private float _damage;
    private float _maxDistance;
    private LayerMask _hitMask;
    private Coroutine _moveCoroutine;
    private bool _detonated;
    private float _flightTime;
    private float _explosionRadiusMultiplier = 1f;

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        _audioSource = GetComponent<AudioSource>();

        CacheFlightRenderersIfNeeded();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = DefaultAudioMaxDistance;
    }

    /// <summary>
    /// Registers the owning pool so the rocket can return itself on impact or timeout.
    /// Must be called once after the pool creates this instance.
    /// </summary>
    public void Init(ObjectPool<Rocket> pool)
    {
        _pool = pool;
    }

    /// <summary>
    /// Activates the rocket and begins its flight toward <paramref name="direction"/>.
    /// </summary>
    public void Launch(
        GameObject owner,
        Vector3 direction,
        float damage,
        float maxDistance,
        LayerMask hitMask,
        float explosionRadiusMultiplier = 1f)
    {
        _ownerRoot = owner != null ? owner.transform.root : null;
        _direction = direction.normalized;
        _damage = damage;
        _maxDistance = Mathf.Max(0.1f, maxDistance);
        _hitMask = hitMask;
        _explosionRadiusMultiplier = Mathf.Max(0.01f, explosionRadiusMultiplier);
        _detonated = false;
        _flightTime = 0f;

        SetFlightVisualsVisible(true);
        SetFlightColliderEnabled(true);
        PlayFlightAudio();

        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        _moveCoroutine = StartCoroutine(MoveCoroutine());
    }

    private IEnumerator MoveCoroutine()
    {
        float travelled = 0f;

        while (travelled < _maxDistance)
        {
            float step = GetCurrentSpeed() * Time.deltaTime;
            Vector3 currentPosition = transform.position;
            Vector3 nextPosition = currentPosition + _direction * step;

            if (TryGetImpact(currentPosition, step, out RaycastHit hit))
            {
                transform.position = hit.point - _direction * Mathf.Min(0.05f, GetCollisionRadius() * 0.5f);
                Detonate(hit.point, hit.normal, GetHealthComponent(hit.collider));
                yield break;
            }

            if (TryGetOverlapImpact(nextPosition, out Vector3 overlapPoint, out Vector3 overlapNormal, out HealthComponent overlapHealth))
            {
                transform.position = overlapPoint;
                Detonate(overlapPoint, overlapNormal, overlapHealth);
                yield break;
            }

            transform.position = nextPosition;
            travelled += step;
            _flightTime += Time.deltaTime;

            yield return null;
        }

        Detonate(transform.position, -_direction);
    }

    private bool TryGetImpact(Vector3 origin, float distance, out RaycastHit impactHit)
    {
        impactHit = default;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            GetCollisionRadius(),
            _direction,
            _castBuffer,
            distance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _castBuffer[i];
            if (hit.collider == null || IsOwnerCollider(hit.collider) || IsSelfCollider(hit.collider))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                impactHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private bool TryGetOverlapImpact(
        Vector3 position,
        out Vector3 impactPoint,
        out Vector3 impactNormal,
        out HealthComponent impactHealth)
    {
        impactPoint = position;
        impactNormal = -_direction;
        impactHealth = null;

        int overlapCount = Physics.OverlapSphereNonAlloc(
            position,
            GetCollisionRadius(),
            _overlapBuffer,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = _overlapBuffer[i];
            if (overlap == null || IsOwnerCollider(overlap) || IsSelfCollider(overlap))
                continue;

            Vector3 point = overlap.ClosestPoint(position);
            if ((point - position).sqrMagnitude <= 0.0001f)
                point = position;

            Vector3 normal = position - point;
            if (normal.sqrMagnitude <= 0.0001f)
                normal = -_direction;
            else
                normal.Normalize();

            impactPoint = point;
            impactNormal = normal;
            impactHealth = GetHealthComponent(overlap);
            return true;
        }

        return false;
    }

    private void Detonate(Vector3 explosionPosition, Vector3 explosionNormal, HealthComponent directHitHealth = null)
    {
        if (_detonated)
            return;

        _detonated = true;
        _moveCoroutine = null;
        transform.position = explosionPosition;

        SetFlightVisualsVisible(false);
        SetFlightColliderEnabled(false);
        StopFlightAudio();
        SpawnExplosionFx(explosionPosition, explosionNormal);
        ApplyExplosion(explosionPosition, directHitHealth);

        float explosionSoundDuration = PlayExplosionAudio();
        if (explosionSoundDuration > 0f)
        {
            StartCoroutine(ReturnAfterDelay(explosionSoundDuration));
            return;
        }

        ReturnToPool();
    }

    private void ApplyExplosion(Vector3 explosionPosition, HealthComponent directHitHealth)
    {
        int overlapCount = Physics.OverlapSphereNonAlloc(
            explosionPosition,
            GetExplosionRadius(),
            _overlapBuffer,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        int uniqueHealthCount = 0;
        int uniqueEnemyCount = 0;
        PlayerMovement playerMovement = null;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = _overlapBuffer[i];
            if (overlap == null)
                continue;

            HealthComponent health = overlap.GetComponentInParent<HealthComponent>();
            if (health != null && !Contains(_healthBuffer, uniqueHealthCount, health) && uniqueHealthCount < _healthBuffer.Length)
                _healthBuffer[uniqueHealthCount++] = health;

            EnemyBase enemy = overlap.GetComponentInParent<EnemyBase>();
            if (enemy != null && !Contains(_enemyBuffer, uniqueEnemyCount, enemy) && uniqueEnemyCount < _enemyBuffer.Length)
                _enemyBuffer[uniqueEnemyCount++] = enemy;

            if (playerMovement == null)
                playerMovement = overlap.GetComponentInParent<PlayerMovement>();
        }

        for (int i = 0; i < uniqueHealthCount; i++)
        {
            HealthComponent health = _healthBuffer[i];
            _healthBuffer[i] = null;

            if (health == null || !health.IsAlive || health.IsPlayer)
                continue;

            float damageMultiplier = health == directHitHealth
                ? 1f
                : GetExplosionDamageMultiplier(explosionPosition, GetTargetPoint(health.transform));
            if (damageMultiplier <= 0f)
                continue;

            health.TakeDamage(new DamageInfo
            {
                Amount = _damage * damageMultiplier,
                Type = DamageType.Explosive,
                Source = _ownerRoot != null ? _ownerRoot.gameObject : gameObject
            });
        }

        if (playerMovement != null)
        {
            Vector3 impulse = GetExplosionImpulse(explosionPosition, GetTargetPoint(playerMovement.transform));
            if (impulse.sqrMagnitude > 0.0001f)
                playerMovement.ApplyExplosionKnockback(impulse);
        }

        for (int i = 0; i < uniqueEnemyCount; i++)
        {
            EnemyBase enemy = _enemyBuffer[i];
            _enemyBuffer[i] = null;

            if (enemy == null || !enemy.IsAlive)
                continue;

            Vector3 impulse = GetExplosionImpulse(explosionPosition, GetTargetPoint(enemy.transform));
            if (impulse.sqrMagnitude > 0.0001f)
                enemy.ApplyExplosionKnockback(impulse);
        }
    }

    private Vector3 GetExplosionImpulse(Vector3 explosionPosition, Vector3 targetPoint)
    {
        float falloff = GetExplosionFalloff(explosionPosition, targetPoint);
        if (falloff <= 0f)
            return Vector3.zero;

        Vector3 direction = targetPoint - explosionPosition;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.up;
        else
            direction.Normalize();

        return direction * GetKnockbackPower() * falloff;
    }

    private float GetExplosionFalloff(Vector3 explosionPosition, Vector3 targetPoint)
    {
        float distance = Vector3.Distance(explosionPosition, targetPoint);
        return 1f - Mathf.Clamp01(distance / GetExplosionRadius());
    }

    private float GetExplosionDamageMultiplier(Vector3 explosionPosition, Vector3 targetPoint)
    {
        float radius = GetExplosionRadius();
        if (radius <= 0f)
            return 0f;

        float distance = Vector3.Distance(explosionPosition, targetPoint);
        if (distance >= radius)
            return 0f;

        float innerKillRadius = Mathf.Clamp(_innerKillRadius, 0f, radius);
        if (distance <= innerKillRadius)
            return 1f;

        float outerBand = radius - innerKillRadius;
        if (outerBand <= 0.0001f)
            return 1f;

        float t = (distance - innerKillRadius) / outerBand;
        return Mathf.Lerp(1f, Mathf.Clamp01(_edgeDamageMultiplier), t);
    }

    private static Vector3 GetTargetPoint(Transform target)
    {
        return target.position + Vector3.up * 0.9f;
    }

    private static HealthComponent GetHealthComponent(Collider collider)
    {
        return collider != null ? collider.GetComponentInParent<HealthComponent>() : null;
    }

    private bool IsOwnerCollider(Collider collider)
    {
        return _ownerRoot != null && collider.transform.root == _ownerRoot;
    }

    private bool IsSelfCollider(Collider collider)
    {
        return collider != null && collider.transform.root == transform;
    }

    private void SpawnExplosionFx(Vector3 explosionPosition, Vector3 explosionNormal)
    {
        if (_explosionFxPrefab == null)
            return;

        Quaternion rotation = explosionNormal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(explosionNormal)
            : Quaternion.identity;

        GameObject fxInstance = Instantiate(_explosionFxPrefab, explosionPosition, rotation);
        ExplosionFxEnhancer glowEnhancer = fxInstance.GetComponent<ExplosionFxEnhancer>();
        if (glowEnhancer == null)
            glowEnhancer = fxInstance.AddComponent<ExplosionFxEnhancer>();

        glowEnhancer.Play(
            _explosionGlowColor,
            _explosionGlowSize,
            _explosionGlowDuration,
            _explosionLightColor,
            _explosionLightIntensity,
            _explosionLightRange,
            _explosionLightDuration);

        float lifetime = _explosionFxLifetime > 0f ? _explosionFxLifetime : DefaultExplosionFxLifetime;
        Destroy(fxInstance, lifetime);
    }

    private void PlayFlightAudio()
    {
        if (_audioSource == null || _flightSounds == null || _flightSounds.Length == 0)
            return;

        _audioSource.clip = _flightSounds[Random.Range(0, _flightSounds.Length)];
        _audioSource.volume = Mathf.Clamp01(_flightSoundVolume);
        _audioSource.loop = true;
        _audioSource.Play();
    }

    private void StopFlightAudio()
    {
        if (_audioSource == null)
            return;

        _audioSource.Stop();
        _audioSource.clip = null;
        _audioSource.loop = false;
    }

    private float PlayExplosionAudio()
    {
        if (_audioSource == null || _explosionSounds == null || _explosionSounds.Length == 0)
            return 0f;

        AudioClip clip = _explosionSounds[Random.Range(0, _explosionSounds.Length)];
        if (clip == null)
            return 0f;

        _audioSource.clip = clip;
        _audioSource.volume = Mathf.Clamp01(_explosionSoundVolume);
        _audioSource.loop = false;
        _audioSource.Play();
        return clip.length;
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }

    private void SetFlightColliderEnabled(bool isEnabled)
    {
        if (_sphereCollider != null)
            _sphereCollider.enabled = isEnabled;
    }

    private void SetFlightVisualsVisible(bool isVisible)
    {
        CacheFlightRenderersIfNeeded();

        if (_flightRenderers == null)
            return;

        for (int i = 0; i < _flightRenderers.Length; i++)
        {
            if (_flightRenderers[i] != null)
                _flightRenderers[i].enabled = isVisible;
        }
    }

    private float GetSpeed()
    {
        return _speed > 0f ? _speed : DefaultSpeed;
    }

    private float GetCurrentSpeed()
    {
        float topSpeed = GetSpeed();
        float launchSpeed = Mathf.Clamp(_launchSpeed, 0f, topSpeed);
        float ignitionDelay = Mathf.Max(0f, _ignitionDelay);
        float ignitionRampTime = Mathf.Max(0.001f, _ignitionRampTime);

        if (_flightTime <= ignitionDelay)
            return launchSpeed;

        float t = Mathf.Clamp01((_flightTime - ignitionDelay) / ignitionRampTime);
        return Mathf.Lerp(launchSpeed, topSpeed, t);
    }

    private float GetCollisionRadius()
    {
        if (_collisionRadius > 0f)
            return _collisionRadius;

        return _sphereCollider != null && _sphereCollider.radius > 0f
            ? _sphereCollider.radius
            : DefaultCollisionRadius;
    }

    private float GetExplosionRadius()
    {
        float baseRadius = _explosionRadius > 0f ? _explosionRadius : DefaultExplosionRadius;
        return baseRadius * _explosionRadiusMultiplier;
    }

    private float GetKnockbackPower()
    {
        return _knockbackPower > 0f ? _knockbackPower : DefaultKnockbackPower;
    }

    private static bool Contains<T>(T[] items, int count, T candidate) where T : class
    {
        for (int i = 0; i < count; i++)
        {
            if (items[i] == candidate)
                return true;
        }

        return false;
    }

    private void ReturnToPool()
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        _moveCoroutine = null;
        _detonated = false;
        _ownerRoot = null;
        _flightTime = 0f;
        _explosionRadiusMultiplier = 1f;
        StopFlightAudio();
        _pool?.Return(this);
    }

    private void CacheFlightRenderersIfNeeded()
    {
        if (HasAssignedFlightRenderers())
            return;

        _flightRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private bool HasAssignedFlightRenderers()
    {
        if (_flightRenderers == null || _flightRenderers.Length == 0)
            return false;

        for (int i = 0; i < _flightRenderers.Length; i++)
        {
            if (_flightRenderers[i] != null)
                return true;
        }

        return false;
    }
}
