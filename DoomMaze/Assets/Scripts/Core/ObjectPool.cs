using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic, type-safe object pool for any <see cref="Component"/>-bearing GameObject.
/// Pre-warms a fixed number of instances at construction time; grows on demand.
/// All frequently-spawned objects (impact FX, projectiles, sound emitters, pickup
/// notifications) must use this class — never call Instantiate/Destroy at runtime.
/// </summary>
/// <typeparam name="T">The <see cref="Component"/> type managed by this pool.</typeparam>
public class ObjectPool<T> where T : Component
{
    private readonly T         _prefab;
    private readonly Transform _parent;
    private readonly Stack<T>  _available = new Stack<T>();
    private readonly HashSet<T> _availableSet = new HashSet<T>();
    private readonly List<T>   _allInstances = new List<T>();

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the pool and pre-warms it with <paramref name="initialSize"/> inactive instances.
    /// </summary>
    /// <param name="prefab">The prefab to instantiate. Must not be null.</param>
    /// <param name="initialSize">Number of instances to create immediately.</param>
    /// <param name="parent">Optional parent Transform for scene hygiene.</param>
    public ObjectPool(T prefab, int initialSize, Transform parent = null)
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPool] Prefab must not be null.");
            return;
        }

        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < initialSize; i++)
            CreateInstance(addToAvailable: true);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves an instance from the pool at the given position and rotation.
    /// Grows the pool by one if no instances are currently available.
    /// </summary>
    public T Get(Vector3 position, Quaternion rotation)
    {
        T instance = null;

        while (_available.Count > 0 && instance == null)
        {
            T candidate = _available.Pop();
            _availableSet.Remove(candidate);

            if (candidate != null && !candidate.gameObject.activeSelf)
                instance = candidate;
        }

        if (instance == null)
            instance = CreateInstance(addToAvailable: false);

        Transform t = instance.transform;
        t.position = position;
        t.rotation = rotation;
        instance.gameObject.SetActive(true);

        return instance;
    }

    /// <summary>
    /// Returns an instance to the pool, deactivating it.
    /// Logs a warning if the instance does not belong to this pool.
    /// </summary>
    public void Return(T instance)
    {
        if (instance == null) return;

        if (!_allInstances.Contains(instance))
        {
            Debug.LogWarning($"[ObjectPool] Attempted to return an instance that does not belong to this pool: {instance.name}");
            return;
        }

        if (_availableSet.Contains(instance))
            return;

        instance.gameObject.SetActive(false);
        _available.Push(instance);
        _availableSet.Add(instance);
    }

    /// <summary>Returns all currently active instances to the pool.</summary>
    public void ReturnAll()
    {
        foreach (T instance in _allInstances)
        {
            if (instance != null && instance.gameObject.activeSelf)
                Return(instance);
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private T CreateInstance(bool addToAvailable)
    {
        T instance = Object.Instantiate(_prefab, _parent);
        instance.gameObject.SetActive(false);
        _allInstances.Add(instance);

        if (addToAvailable)
        {
            _available.Push(instance);
            _availableSet.Add(instance);
        }

        return instance;
    }
}
