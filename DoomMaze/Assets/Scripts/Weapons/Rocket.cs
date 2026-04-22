using System.Collections;
using UnityEngine;

/// <summary>
/// Poolable projectile for the rocket launcher.
/// Moves forward each frame, applies AOE <see cref="DamageType.Explosion"/> damage on first
/// collision, then returns itself to its <see cref="ObjectPool{T}"/>.
/// </summary>
public class Rocket : MonoBehaviour
{
    [SerializeField] private float _speed       = 25f;
    [SerializeField] private float _splashRadius = 3f;

    private readonly Collider[] _overlapBuffer = new Collider[8];

    private ObjectPool<Rocket> _pool;
    private Vector3            _direction;
    private float              _damage;
    private float              _maxDistance;
    private LayerMask          _hitMask;
    private Coroutine          _moveCoroutine;

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
    public void Launch(Vector3 direction, float damage, float maxDistance, LayerMask hitMask)
    {
        _direction   = direction.normalized;
        _damage      = damage;
        _maxDistance = maxDistance;
        _hitMask     = hitMask;

        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        _moveCoroutine = StartCoroutine(MoveCoroutine());
    }

    private IEnumerator MoveCoroutine()
    {
        float travelled = 0f;

        while (travelled < _maxDistance)
        {
            float step = _speed * Time.deltaTime;
            transform.position += _direction * step;
            travelled          += step;

            int hits = Physics.OverlapSphereNonAlloc(transform.position, _splashRadius, _overlapBuffer, _hitMask);

            if (hits > 0)
            {
                for (int i = 0; i < hits; i++)
                {
                    _overlapBuffer[i].GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo
                    {
                        Amount = _damage,
                        Type   = DamageType.Explosive,
                        Source = gameObject
                    });
                }

                ReturnToPool();
                yield break;
            }

            yield return null;
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        _moveCoroutine = null;
        _pool?.Return(this);
    }
}
