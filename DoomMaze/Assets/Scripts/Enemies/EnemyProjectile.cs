using UnityEngine;

/// <summary>
/// Simple enemy-fired projectile that travels forward, damages the player on hit,
/// and renders itself as a glowing cube.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    private const float DEFAULT_SPEED = 16f;
    private const float DEFAULT_COLLISION_RADIUS = 0.22f;
    private const float DEFAULT_MAX_DISTANCE = 18f;
    private const float DEFAULT_LIFETIME = 4f;
    private const float VISUAL_SCALE = 0.35f;
    private const float LIGHT_INTENSITY = 3f;
    private const float LIGHT_RANGE = 2.8f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _sharedMaterial;

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

    private GameObject _owner;
    private Collider[] _ownerColliders;
    private Vector3 _direction;
    private float _damage;
    private DamageType _damageType;
    private float _speed = DEFAULT_SPEED;
    private float _collisionRadius = DEFAULT_COLLISION_RADIUS;
    private float _maxDistance = DEFAULT_MAX_DISTANCE;
    private float _distanceTravelled;
    private float _remainingLifetime = DEFAULT_LIFETIME;
    private bool _isLaunched;

    private void Awake()
    {
        EnsureVisuals();
    }

    public void Launch(
        GameObject owner,
        Vector3 origin,
        Vector3 direction,
        float damage,
        DamageType damageType,
        float maxDistance,
        float speed = DEFAULT_SPEED,
        float collisionRadius = DEFAULT_COLLISION_RADIUS)
    {
        _owner = owner;
        _ownerColliders = owner != null ? owner.GetComponentsInChildren<Collider>() : null;
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        _damage = damage;
        _damageType = damageType;
        _speed = speed > 0f ? speed : DEFAULT_SPEED;
        _collisionRadius = Mathf.Max(0.05f, collisionRadius);
        _maxDistance = maxDistance > 0f ? maxDistance : DEFAULT_MAX_DISTANCE;
        _remainingLifetime = DEFAULT_LIFETIME;
        _distanceTravelled = 0f;
        _isLaunched = true;

        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
    }

    private void Update()
    {
        if (!_isLaunched)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        float stepDistance = _speed * deltaTime;
        if (TryGetNextHit(stepDistance, out RaycastHit hit))
        {
            transform.position = hit.point - _direction * Mathf.Min(_collisionRadius, stepDistance * 0.5f);
            ResolveImpact(hit.collider);
            return;
        }

        transform.position += _direction * stepDistance;
        _distanceTravelled += stepDistance;
        _remainingLifetime -= deltaTime;

        if (_distanceTravelled >= _maxDistance || _remainingLifetime <= 0f)
            Destroy(gameObject);
    }

    private bool TryGetNextHit(float stepDistance, out RaycastHit closestHit)
    {
        closestHit = default(RaycastHit);

        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            _collisionRadius,
            _direction,
            _hitBuffer,
            stepDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        if (hitCount <= 0)
            return false;

        float closestDistance = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _hitBuffer[i];
            if (hit.collider == null || ShouldIgnoreHit(hit.collider))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private bool ShouldIgnoreHit(Collider other)
    {
        if (other == null)
            return true;

        if (_ownerColliders != null)
        {
            for (int i = 0; i < _ownerColliders.Length; i++)
            {
                if (_ownerColliders[i] == other)
                    return true;
            }
        }

        EnemyBase enemy = other.GetComponentInParent<EnemyBase>();
        return enemy != null;
    }

    private void ResolveImpact(Collider other)
    {
        if (other == null)
        {
            Destroy(gameObject);
            return;
        }

        HealthComponent health = other.GetComponentInParent<HealthComponent>();
        if (health != null && health.IsPlayer && health.IsAlive)
        {
            health.TakeDamage(new DamageInfo
            {
                Amount = _damage,
                Type = _damageType,
                Source = _owner
            });
        }

        Destroy(gameObject);
    }

    private void EnsureVisuals()
    {
        if (transform.Find("Visual") == null)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * VISUAL_SCALE;

            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            MeshRenderer meshRenderer = visual.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sharedMaterial = GetSharedMaterial();
        }

        Light lightComponent = GetComponent<Light>();
        if (lightComponent == null)
            lightComponent = gameObject.AddComponent<Light>();

        lightComponent.type = LightType.Point;
        lightComponent.color = new Color(0.35f, 0.9f, 1f, 1f);
        lightComponent.intensity = LIGHT_INTENSITY;
        lightComponent.range = LIGHT_RANGE;
        lightComponent.shadows = LightShadows.None;
    }

    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        _sharedMaterial = new Material(shader)
        {
            name = "EnemyProjectile_Runtime"
        };

        Color baseColor = new Color(0.35f, 0.9f, 1f, 1f);
        Color emissionColor = new Color(0.8f, 3.5f, 4.2f, 1f);

        if (_sharedMaterial.HasProperty(BaseColorId))
            _sharedMaterial.SetColor(BaseColorId, baseColor);

        if (_sharedMaterial.HasProperty(ColorId))
            _sharedMaterial.SetColor(ColorId, baseColor);

        if (_sharedMaterial.HasProperty(EmissionColorId))
            _sharedMaterial.SetColor(EmissionColorId, emissionColor);

        _sharedMaterial.EnableKeyword("_EMISSION");
        _sharedMaterial.hideFlags = HideFlags.HideAndDontSave;

        return _sharedMaterial;
    }
}
