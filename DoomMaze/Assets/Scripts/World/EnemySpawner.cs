using UnityEngine;

/// <summary>
/// Spawns enemies via <see cref="ObjectPool{T}"/> either on player trigger enter or on a
/// repeating timer. Subscribe target: <see cref="EnemyDiedEvent"/> to return enemies to the pool.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private int        _poolSize        = 4;
    [SerializeField] private bool       _spawnOnTrigger  = true;
    [SerializeField] private float      _spawnInterval   = 0f;
    [SerializeField] private int        _maxSpawnCount   = -1;

    private ObjectPool<EnemyBase> _pool;
    private int                   _spawnedCount;
    private float                 _intervalTimer;

    private void Awake()
    {
        if (_enemyPrefab == null)
        {
            Debug.LogWarning($"[EnemySpawner] _enemyPrefab is not assigned on {gameObject.name}.");
            return;
        }

        EnemyBase prefabBase = _enemyPrefab.GetComponent<EnemyBase>();
        if (prefabBase == null)
        {
            Debug.LogWarning($"[EnemySpawner] _enemyPrefab '{_enemyPrefab.name}' has no EnemyBase component.");
            return;
        }

        _pool = new ObjectPool<EnemyBase>(prefabBase, _poolSize, transform);
    }

    private void OnEnable()
    {
        EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
    }

    private void OnDisable()
    {
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
    }

    private void Update()
    {
        if (_spawnOnTrigger || _pool == null) return;
        if (_spawnInterval <= 0f) return;

        _intervalTimer -= Time.deltaTime;
        if (_intervalTimer <= 0f)
        {
            _intervalTimer = _spawnInterval;
            Spawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_spawnOnTrigger) return;
        if (!other.CompareTag("Player")) return;

        Spawn();
    }

    /// <summary>Retrieves an enemy from the pool and places it at the spawner's position.</summary>
    public void Spawn()
    {
        if (_pool == null) return;
        if (_maxSpawnCount >= 0 && _spawnedCount >= _maxSpawnCount) return;

        _pool.Get(transform.position, transform.rotation);
        _spawnedCount++;
    }

    private void OnEnemyDied(EnemyDiedEvent evt)
    {
        if (evt.Enemy == null) return;

        EnemyBase enemy = evt.Enemy.GetComponent<EnemyBase>();
        if (enemy != null)
            _pool.Return(enemy);
    }
}
