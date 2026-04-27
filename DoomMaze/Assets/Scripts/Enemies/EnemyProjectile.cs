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

    private GameObject _visual;
    private Light _lightComponent;
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
    private bool _bossTrailEnabled;
    private bool _bossImpactPulseEnabled;
    private float _bossImpactPulseRadius = 1.2f;
    private float _bossImpactShake;
    private Color _bossImpactPulseColor = new(0.25f, 0.95f, 1f, 0.42f);
    private bool _impactFeedbackPlayed;

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
        _remainingLifetime = Mathf.Max(DEFAULT_LIFETIME, (_maxDistance / _speed) + 0.1f);
        _distanceTravelled = 0f;
        _isLaunched = true;

        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
    }

    public void ConfigureBossVisuals(
        Color projectileColor,
        Color emissionColor,
        float lightIntensity,
        float lightRange,
        bool useTrail,
        bool impactPulse,
        float impactPulseRadius,
        Color impactPulseColor,
        float impactShake)
    {
        _bossTrailEnabled = useTrail;
        _bossImpactPulseEnabled = impactPulse;
        _bossImpactPulseRadius = Mathf.Max(0.1f, impactPulseRadius);
        _bossImpactPulseColor = impactPulseColor;
        _bossImpactShake = Mathf.Max(0f, impactShake);

        EnsureVisuals();

        Renderer renderer = _visual != null ? _visual.GetComponent<Renderer>() : null;
        if (renderer != null)
            renderer.material = CreateMaterial("BossEnemyProjectile_Runtime", projectileColor, emissionColor);

        if (_lightComponent != null)
        {
            _lightComponent.color = projectileColor;
            _lightComponent.intensity = Mathf.Max(0f, lightIntensity);
            _lightComponent.range = Mathf.Max(0.1f, lightRange);
        }

        if (_bossTrailEnabled)
            EnsureTrail(projectileColor, emissionColor);
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
            DestroyWithFeedback();
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

        DestroyWithFeedback();
    }

    private void DestroyWithFeedback()
    {
        SpawnImpactFeedback();
        Destroy(gameObject);
    }

    private void SpawnImpactFeedback()
    {
        if (!_bossImpactPulseEnabled || _impactFeedbackPlayed)
            return;

        _impactFeedbackPlayed = true;
        BossAttackVfx.SpawnImpactPulse(transform.position, _bossImpactPulseRadius, _bossImpactPulseColor, 0.18f, _bossImpactShake);
    }

    private void EnsureVisuals()
    {
        if (transform.Find("Visual") == null)
        {
            _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _visual.name = "Visual";
            _visual.transform.SetParent(transform, false);
            _visual.transform.localScale = Vector3.one * VISUAL_SCALE;

            Collider collider = _visual.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            MeshRenderer meshRenderer = _visual.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sharedMaterial = GetSharedMaterial();
        }
        else if (_visual == null)
        {
            Transform visualTransform = transform.Find("Visual");
            _visual = visualTransform != null ? visualTransform.gameObject : null;
        }

        _lightComponent = GetComponent<Light>();
        if (_lightComponent == null)
            _lightComponent = gameObject.AddComponent<Light>();

        _lightComponent.type = LightType.Point;
        _lightComponent.color = new Color(0.35f, 0.9f, 1f, 1f);
        _lightComponent.intensity = LIGHT_INTENSITY;
        _lightComponent.range = LIGHT_RANGE;
        _lightComponent.shadows = LightShadows.None;
    }

    private void EnsureTrail(Color projectileColor, Color emissionColor)
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();

        trail.time = 0.18f;
        trail.minVertexDistance = 0.05f;
        trail.widthMultiplier = Mathf.Max(0.18f, _collisionRadius * 0.75f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.material = CreateTrailMaterial("BossEnemyProjectileTrail_Runtime", projectileColor, emissionColor);

        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(projectileColor, 0f),
                new GradientColorKey(emissionColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.62f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
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

        Color baseColor = new Color(0.35f, 0.9f, 1f, 1f);
        Color emissionColor = new Color(0.8f, 3.5f, 4.2f, 1f);
        _sharedMaterial = CreateMaterial("EnemyProjectile_Runtime", baseColor, emissionColor);
        _sharedMaterial.hideFlags = HideFlags.HideAndDontSave;

        return _sharedMaterial;
    }

    private static Material CreateMaterial(string materialName, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new(shader)
        {
            name = materialName
        };

        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, baseColor);

        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, baseColor);

        if (material.HasProperty(EmissionColorId))
            material.SetColor(EmissionColorId, emissionColor);

        material.EnableKeyword("_EMISSION");
        return material;
    }

    private static Material CreateTrailMaterial(string materialName, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            return CreateMaterial(materialName, baseColor, emissionColor);

        Material material = new(shader)
        {
            name = materialName
        };

        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, baseColor);

        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, baseColor);

        if (material.HasProperty(EmissionColorId))
            material.SetColor(EmissionColorId, emissionColor);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_EMISSION");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }
}
