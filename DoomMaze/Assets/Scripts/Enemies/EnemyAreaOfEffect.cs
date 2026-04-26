using UnityEngine;
using static Beautify.Universal.Beautify;

/// <summary>
/// Simple enemy-generated trigger area that either appears instantly or expands,
/// damages the player on entering trigger area, and renders itself as a glowing shape.
/// </summary>
public class EnemyAreaOfEffect : MonoBehaviour
{
    private const float DEFAULT_EXPANSION_SPEED = 16f;
    private const float DEFAULT_MAX_DISTANCE = 18f;
    private const float DEFAULT_LIFETIME = 4f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _sharedMaterial;

    private GameObject _visual;
    private AreaOfEffectTrigger _areaOfEffectTrigger;
    private GameObject _owner;
    private Collider[] _ownerColliders;
    private Vector3 _direction;
    private float _damage;
    private DamageType _damageType;
    private float _expansionSpeed = DEFAULT_EXPANSION_SPEED;
    private float _maxDistance = DEFAULT_MAX_DISTANCE;
    private float _distanceTravelled;
    private float _remainingLifetime = DEFAULT_LIFETIME;
    private bool _isGenerated;

    public void Generate(
        GameObject owner,
        Vector3 origin,
        Vector3 direction,
        float damage,
        DamageType damageType,
        float maxDistance,
        float expansionSpeed = DEFAULT_EXPANSION_SPEED)
    {
        _owner = owner;
        _ownerColliders = owner != null ? owner.GetComponentsInChildren<Collider>() : null;
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        _damage = damage;
        _damageType = damageType;
        _maxDistance = maxDistance > 0f ? maxDistance : DEFAULT_MAX_DISTANCE;
        _expansionSpeed = expansionSpeed > 0f ? expansionSpeed : DEFAULT_EXPANSION_SPEED;
        _remainingLifetime = DEFAULT_LIFETIME;
        _distanceTravelled = 0f;
        _isGenerated = true;

        transform.position = origin;
        EnsureAreaOfEffectTrigger();
    }

    private void Update()
    {
        if (!_isGenerated)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        float stepDistance = _expansionSpeed * deltaTime;

        if (_distanceTravelled < _maxDistance)
        {
            transform.localScale += new Vector3(1, 0, 1) * stepDistance;
            _distanceTravelled += stepDistance;
        }

        _remainingLifetime -= deltaTime;

        if (_remainingLifetime <= 0f)
            Destroy(gameObject);
    }

    public void HandleEnterAreaOfEffect(Collider other)
    {
        if (!ShouldIgnoreHit(other))
            ResolveImpact(other);
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

    private void EnsureAreaOfEffectTrigger()
    {
        if (transform.Find("Visual") == null)
        {
            _visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _visual.name = "Visual";
            _visual.transform.SetParent(transform, false);

            transform.localScale = Vector3.zero;

            Collider collider = _visual.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

            MeshRenderer meshRenderer = _visual.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sharedMaterial = GetSharedMaterial();
        }

        _areaOfEffectTrigger = _visual.AddComponent<AreaOfEffectTrigger>();
        _areaOfEffectTrigger.Configure(this);
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