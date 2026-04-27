using UnityEngine;

/// <summary>
/// Simple enemy-generated trigger area that either appears instantly or expands,
/// damages the player on entering trigger area, and renders itself as a glowing shape.
/// </summary>
public class EnemyAreaOfEffect : MonoBehaviour
{
    private const float DEFAULT_EXPANSION_SPEED = 16f;
    private const float DEFAULT_MAX_DISTANCE = 18f;
    private const float DEFAULT_LIFETIME = 4f;
    private const int RING_SEGMENTS = 96;
    private const float RING_OUTER_RADIUS = 0.5f;
    private const float RING_INNER_RADIUS = 0.38f;
    private const float RING_HEIGHT = 0.85f;
    private const float RING_CLEARANCE_HEIGHT = 0.72f;
    private const float RING_ALPHA = 0.82f;
    private const float PLAYER_TARGET_REFRESH_INTERVAL = 0.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Material _sharedMaterial;
    private static Mesh _sharedRingMesh;

    private GameObject _visual;
    private Renderer _visualRenderer;
    private Light _lightComponent;
    private GameObject _owner;
    private Collider[] _ownerColliders;
    private Transform _playerTransform;
    private HealthComponent _playerHealth;
    private CharacterController _playerCharacterController;
    private Vector3 _direction;
    private float _damage;
    private DamageType _damageType;
    private float _expansionSpeed = DEFAULT_EXPANSION_SPEED;
    private float _maxDistance = DEFAULT_MAX_DISTANCE;
    private float _distanceTravelled;
    private float _remainingLifetime = DEFAULT_LIFETIME;
    private float _targetRefreshTimer;
    private bool _isGenerated;
    private bool _hasHitPlayer;
    private bool _bossVisualsEnabled;
    private Color _bossRingColor = new(1f, 0.55f, 0.08f, RING_ALPHA);
    private Color _bossEmissionColor = new(4f, 2.2f, 0.35f, RING_ALPHA);
    private Color _bossPulseColor = new(1f, 0.35f, 0.04f, 0.5f);
    private float _bossVisualIntensity = 1f;
    private float _bossImpactShake;
    private bool _impactFeedbackPlayed;

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
        _remainingLifetime = Mathf.Max(DEFAULT_LIFETIME, (_maxDistance / _expansionSpeed) + 0.1f);
        _distanceTravelled = 0f;
        _targetRefreshTimer = 0f;
        _hasHitPlayer = false;
        _isGenerated = true;

        transform.position = origin;
        CachePlayerTarget();
        EnsureAreaOfEffectTrigger();
    }

    public void ConfigureBossVisuals(Color ringColor, Color emissionColor, Color pulseColor, float intensity, float impactShake)
    {
        _bossVisualsEnabled = true;
        _bossRingColor = ringColor;
        _bossEmissionColor = emissionColor;
        _bossPulseColor = pulseColor;
        _bossVisualIntensity = Mathf.Max(0.1f, intensity);
        _bossImpactShake = Mathf.Max(0f, impactShake);

        EnsureAreaOfEffectTrigger();

        if (_visualRenderer != null)
            _visualRenderer.material = CreateMaterial("BossEnemyAreaOfEffect_Ring_Runtime", _bossRingColor, _bossEmissionColor);

        if (_lightComponent == null)
            _lightComponent = gameObject.AddComponent<Light>();

        _lightComponent.type = LightType.Point;
        _lightComponent.color = ringColor;
        _lightComponent.intensity = 3.5f * _bossVisualIntensity;
        _lightComponent.range = Mathf.Max(4f, _maxDistance * 0.35f);
        _lightComponent.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (!_isGenerated)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        float stepDistance = _expansionSpeed * deltaTime;

        float previousRadius = _distanceTravelled;

        if (_distanceTravelled < _maxDistance)
        {
            _distanceTravelled = Mathf.Min(_maxDistance, _distanceTravelled + stepDistance);
            float visualScale = _distanceTravelled / RING_OUTER_RADIUS;
            transform.localScale = new Vector3(visualScale, 1f, visualScale);
            UpdateBossVisualPulse();
            TryResolvePlayerAtRing(previousRadius, _distanceTravelled);
        }
        else
        {
            DestroyWithFeedback(shake: false);
            return;
        }

        _remainingLifetime -= deltaTime;

        if (_remainingLifetime <= 0f)
            DestroyWithFeedback(shake: false);
    }

    public void HandleEnterAreaOfEffect(Collider other)
    {
        if (!ShouldIgnoreHit(other))
            ResolveImpact(other);
    }

    private void TryResolvePlayerAtRing(float previousOuterRadius, float currentOuterRadius)
    {
        if (_hasHitPlayer)
            return;

        _targetRefreshTimer -= Time.deltaTime;
        if (_playerTransform == null || _playerHealth == null || _targetRefreshTimer <= 0f)
            CachePlayerTarget();

        if (_playerTransform == null || _playerHealth == null || !_playerHealth.IsAlive)
            return;

        Vector3 toPlayer = _playerTransform.position - transform.position;
        toPlayer.y = 0f;

        float playerDistance = toPlayer.magnitude;
        float previousInnerRadius = previousOuterRadius * (RING_INNER_RADIUS / RING_OUTER_RADIUS);

        if (playerDistance > currentOuterRadius || playerDistance < previousInnerRadius)
            return;

        if (IsPlayerAboveRing())
            return;

        _hasHitPlayer = true;
        _playerHealth.TakeDamage(new DamageInfo
        {
            Amount = _damage,
            Type = _damageType,
            Source = _owner
        });

        DestroyWithFeedback(shake: true);
    }

    private void UpdateBossVisualPulse()
    {
        if (!_bossVisualsEnabled)
            return;

        float progress = _maxDistance > 0f ? Mathf.Clamp01(_distanceTravelled / _maxDistance) : 1f;
        float pulse = 0.65f + Mathf.Sin(Time.time * 18f) * 0.18f + progress * 0.3f;

        if (_visualRenderer != null && _visualRenderer.material != null)
        {
            Color ringColor = _bossRingColor;
            ringColor.a = Mathf.Clamp01(_bossRingColor.a * pulse);
            if (_visualRenderer.material.HasProperty(BaseColorId))
                _visualRenderer.material.SetColor(BaseColorId, ringColor);
            if (_visualRenderer.material.HasProperty(ColorId))
                _visualRenderer.material.SetColor(ColorId, ringColor);
        }

        if (_lightComponent != null)
        {
            _lightComponent.intensity = Mathf.Lerp(2.5f, 6.5f, progress) * _bossVisualIntensity;
            _lightComponent.range = Mathf.Max(4f, _distanceTravelled * 0.65f);
        }
    }

    private void CachePlayerTarget()
    {
        _targetRefreshTimer = PLAYER_TARGET_REFRESH_INTERVAL;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }

        if (_playerTransform != null && _playerHealth == null)
            _playerHealth = _playerTransform.GetComponentInParent<HealthComponent>();

        if (_playerTransform != null && _playerCharacterController == null)
            _playerCharacterController = _playerTransform.GetComponentInParent<CharacterController>();
    }

    private bool IsPlayerAboveRing()
    {
        float ringClearHeight = transform.position.y + RING_CLEARANCE_HEIGHT;

        if (_playerCharacterController != null)
        {
            Vector3 center = _playerCharacterController.transform.TransformPoint(_playerCharacterController.center);
            float bottom = center.y - (_playerCharacterController.height * 0.5f) + _playerCharacterController.skinWidth;
            return bottom > ringClearHeight;
        }

        return _playerTransform != null && _playerTransform.position.y > ringClearHeight;
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

        DestroyWithFeedback(shake: true);
    }

    private void DestroyWithFeedback(bool shake)
    {
        SpawnImpactFeedback(shake);
        Destroy(gameObject);
    }

    private void SpawnImpactFeedback(bool shake)
    {
        if (!_bossVisualsEnabled || _impactFeedbackPlayed)
            return;

        _impactFeedbackPlayed = true;
        float radius = Mathf.Max(2f, _distanceTravelled);
        BossAttackVfx.SpawnImpactPulse(transform.position, radius, _bossPulseColor, 0.24f, shake ? _bossImpactShake : 0f);
    }

    private void EnsureAreaOfEffectTrigger()
    {
        if (transform.Find("Visual") == null)
        {
            _visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
            _visual.name = "Visual";
            _visual.transform.SetParent(transform, false);

            MeshFilter meshFilter = _visual.GetComponent<MeshFilter>();
            if (meshFilter != null)
                meshFilter.sharedMesh = GetSharedRingMesh();

            MeshRenderer meshRenderer = _visual.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sharedMaterial = GetSharedMaterial();
            _visualRenderer = meshRenderer;

            transform.localScale = new Vector3(0f, 1f, 0f);
        }
        else if (_visual == null || _visualRenderer == null)
        {
            Transform visualTransform = transform.Find("Visual");
            _visual = visualTransform != null ? visualTransform.gameObject : null;
            _visualRenderer = _visual != null ? _visual.GetComponent<Renderer>() : null;
        }

    }

    private static Mesh GetSharedRingMesh()
    {
        if (_sharedRingMesh != null)
            return _sharedRingMesh;

        Vector3[] vertices = new Vector3[RING_SEGMENTS * 4];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[RING_SEGMENTS * 24];

        for (int i = 0; i < RING_SEGMENTS; i++)
        {
            float angle = (float)i / RING_SEGMENTS * Mathf.PI * 2f;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            int baseIndex = i * 4;

            vertices[baseIndex] = new Vector3(cos * RING_OUTER_RADIUS, 0f, sin * RING_OUTER_RADIUS);
            vertices[baseIndex + 1] = new Vector3(cos * RING_INNER_RADIUS, 0f, sin * RING_INNER_RADIUS);
            vertices[baseIndex + 2] = new Vector3(cos * RING_OUTER_RADIUS, RING_HEIGHT, sin * RING_OUTER_RADIUS);
            vertices[baseIndex + 3] = new Vector3(cos * RING_INNER_RADIUS, RING_HEIGHT, sin * RING_INNER_RADIUS);

            uvs[baseIndex] = new Vector2(1f, 0f);
            uvs[baseIndex + 1] = new Vector2(0f, 0f);
            uvs[baseIndex + 2] = new Vector2(1f, 1f);
            uvs[baseIndex + 3] = new Vector2(0f, 1f);
        }

        int triangleIndex = 0;
        for (int i = 0; i < RING_SEGMENTS; i++)
        {
            int next = (i + 1) % RING_SEGMENTS;
            int a = i * 4;
            int b = next * 4;

            int outerBottomA = a;
            int innerBottomA = a + 1;
            int outerTopA = a + 2;
            int innerTopA = a + 3;

            int outerBottomB = b;
            int innerBottomB = b + 1;
            int outerTopB = b + 2;
            int innerTopB = b + 3;

            triangles[triangleIndex++] = innerTopA;
            triangles[triangleIndex++] = outerTopA;
            triangles[triangleIndex++] = outerTopB;
            triangles[triangleIndex++] = innerTopA;
            triangles[triangleIndex++] = outerTopB;
            triangles[triangleIndex++] = innerTopB;

            triangles[triangleIndex++] = innerBottomA;
            triangles[triangleIndex++] = outerBottomB;
            triangles[triangleIndex++] = outerBottomA;
            triangles[triangleIndex++] = innerBottomA;
            triangles[triangleIndex++] = innerBottomB;
            triangles[triangleIndex++] = outerBottomB;

            triangles[triangleIndex++] = outerBottomA;
            triangles[triangleIndex++] = outerBottomB;
            triangles[triangleIndex++] = outerTopB;
            triangles[triangleIndex++] = outerBottomA;
            triangles[triangleIndex++] = outerTopB;
            triangles[triangleIndex++] = outerTopA;

            triangles[triangleIndex++] = innerBottomA;
            triangles[triangleIndex++] = innerTopB;
            triangles[triangleIndex++] = innerBottomB;
            triangles[triangleIndex++] = innerBottomA;
            triangles[triangleIndex++] = innerTopA;
            triangles[triangleIndex++] = innerTopB;
        }

        _sharedRingMesh = new Mesh
        {
            name = "EnemyAreaOfEffect_Ring"
        };
        _sharedRingMesh.vertices = vertices;
        _sharedRingMesh.uv = uvs;
        _sharedRingMesh.triangles = triangles;
        _sharedRingMesh.RecalculateNormals();
        _sharedRingMesh.RecalculateBounds();
        _sharedRingMesh.hideFlags = HideFlags.HideAndDontSave;

        return _sharedRingMesh;
    }

    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            return null;

        Color baseColor = new Color(1f, 1f, 1f, RING_ALPHA);
        Color emissionColor = new Color(1.2f, 1.2f, 1.2f, RING_ALPHA);
        _sharedMaterial = CreateMaterial("EnemyAreaOfEffect_Ring_Runtime", baseColor, emissionColor);
        _sharedMaterial.hideFlags = HideFlags.HideAndDontSave;

        return _sharedMaterial;
    }

    private static Material CreateMaterial(string materialName, Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
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

        ConfigureTransparentMaterial(material);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);

        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_EMISSION");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
