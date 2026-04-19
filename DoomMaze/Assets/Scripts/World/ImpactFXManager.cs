using UnityEngine;

/// <summary>
/// Scene singleton that owns the <see cref="ObjectPool{T}"/> for <see cref="ImpactFX"/>
/// and exposes a static <see cref="Spawn"/> method so weapons can request impact FX
/// without coupling to the scene hierarchy.
/// </summary>
public class ImpactFXManager : MonoBehaviour
{
    public static ImpactFXManager Instance { get; private set; }

    [SerializeField] private ImpactFX _impactFXPrefab;
    [SerializeField] private int      _poolInitialSize = 16;
    [SerializeField] private Sprite[] _defaultFrames;
    [SerializeField] private float    _defaultFrameRate = 12f;

    private ObjectPool<ImpactFX> _pool;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_impactFXPrefab == null)
        {
            Debug.LogWarning("[ImpactFXManager] ImpactFX prefab is not assigned. Impact effects will not spawn.");
            return;
        }

        GameObject poolRoot = new GameObject("[ImpactFXPool]");
        poolRoot.transform.SetParent(transform);

        _pool = new ObjectPool<ImpactFX>(_impactFXPrefab, _poolInitialSize, poolRoot.transform);

        // Inject owning pool into all pre-warmed instances.
        foreach (Transform child in poolRoot.transform)
        {
            ImpactFX fx = child.GetComponent<ImpactFX>();
            fx?.Initialize(_pool);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns an impact effect using the default sprite frames and frame rate.
    /// </summary>
    /// <param name="position">World-space hit point.</param>
    /// <param name="normal">Surface normal at the hit point — used to orient the sprite.</param>
    public void Spawn(Vector3 position, Vector3 normal)
    {
        Spawn(position, normal, _defaultFrames, _defaultFrameRate);
    }

    /// <summary>
    /// Spawns an impact effect using custom sprite frames and frame rate.
    /// </summary>
    public void Spawn(Vector3 position, Vector3 normal, Sprite[] frames, float frameRate)
    {
        if (_pool == null)
        {
            Debug.LogWarning("[ImpactFXManager] Pool is not initialized. Assign the ImpactFX prefab in the Inspector.");
            return;
        }

        Quaternion rotation = normal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        ImpactFX fx = _pool.Get(position, rotation);
        fx.Initialize(_pool);
        fx.Play(frames, frameRate);
    }
}
