using UnityEngine;

/// <summary>
/// <see cref="WeaponBase"/> subclass for the flamethrower.
/// Drives a configurable particle system from a muzzle point and applies
/// <see cref="DamageType.Fire"/> through particle collision hits.
/// Also owns the flamethrower overheat state and forces a full cooldown
/// before the weapon can fire again after overheating.
/// </summary>
public class FlamethrowerWeapon : WeaponBase
{
    private const float MIN_COOLDOWN_RATE = 0.01f;

    [SerializeField] private LayerMask _hitMask;
    [SerializeField] private Transform _flameOrigin;
    [SerializeField] private ParticleSystem _flameParticles;

    [Header("Flame Visuals")]
    [SerializeField] private float _particleLifetime = 0.35f;
    [SerializeField] private float _particleSpeed = 16f;
    [SerializeField] private float _particleSize = 0.08f;
    [SerializeField] private float _sprayAngle = 18f;
    [SerializeField] private float _emissionRate = 140f;

    [Header("Overheat")]
    [SerializeField] private float _maxHeat = 100f;
    [SerializeField] private float _heatGainPerSecond = 28f;
    [SerializeField] private float _heatCooldownPerSecond = 38f;
    [SerializeField] private AudioClip[] _overheatSounds;

    public float HeatNormalized => _maxHeat > 0f ? Mathf.Clamp01(_currentHeat / _maxHeat) : 0f;
    public bool IsOverheated => _isOverheated;

    private FlamethrowerParticleDamage _particleDamage;
    private bool _triggerHeld;
    private bool _isOverheated;
    private float _currentHeat;
    private float _lastHeatTimestamp;

    protected override void Awake()
    {
        base.Awake();
        _lastHeatTimestamp = Time.time;

        if (_data == null)
            Debug.LogWarning("[FlamethrowerWeapon] No WeaponData assigned.", this);

        if (_flameParticles == null)
        {
            Debug.LogWarning("[FlamethrowerWeapon] No flame particle system assigned.", this);
            return;
        }

        _particleDamage = _flameParticles.GetComponent<FlamethrowerParticleDamage>();
        if (_particleDamage == null)
            _particleDamage = _flameParticles.gameObject.AddComponent<FlamethrowerParticleDamage>();

        ConfigureParticles();
        SyncFlameTransform();
    }

    /// <inheritdoc/>
    public override bool CanFire()
    {
        return !_isOverheated && base.CanFire();
    }

    /// <inheritdoc/>
    public override void Fire()
    {
        ApplyPassiveCooldown();

        if (_isOverheated)
        {
            _triggerHeld = false;
            return;
        }

        _triggerHeld = true;
        base.Fire();
    }

    /// <inheritdoc/>
    protected override void ExecuteFire()
    {
        SyncFlameTransform();

        if (_particleDamage != null && _data != null)
            _particleDamage.Configure(_data.Damage, 1f / Mathf.Max(_data.FireRate, 0.01f), gameObject, _hitMask);

        if (_flameParticles != null && !_flameParticles.isPlaying)
            _flameParticles.Play(true);

        if (_data != null)
            EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
            {
                Magnitude = _data.ShakeMagnitude,
                Duration  = _data.ShakeDuration
            });
    }

    /// <inheritdoc/>
    public override void StopFiring()
    {
        _triggerHeld = false;
        base.StopFiring();

        if (_flameParticles != null)
            _flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    /// <inheritdoc/>
    public override void OnEquip()
    {
        ApplyPassiveCooldown();
        base.OnEquip();
    }

    /// <inheritdoc/>
    public override void OnUnequip()
    {
        _triggerHeld = false;
        _lastHeatTimestamp = Time.time;
        base.OnUnequip();
    }

    private void Update()
    {
        TickHeat(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (_flameParticles != null && _flameParticles.isPlaying)
            SyncFlameTransform();
    }

    private void ConfigureParticles()
    {
        ParticleSystem.MainModule main = _flameParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = _particleLifetime;
        main.startSpeed = _particleSpeed;
        main.startSize = _particleSize;
        main.startColor = new ParticleSystem.MinMaxGradient(
            BuildStartColorGradientA(),
            BuildStartColorGradientB());

        ParticleSystem.EmissionModule emission = _flameParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = _emissionRate;

        ParticleSystem.ShapeModule shape = _flameParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = _sprayAngle;
        shape.radius = 0.05f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _flameParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            BuildLifetimeColorGradientA(),
            BuildLifetimeColorGradientB());

        ParticleSystemRenderer renderer = _flameParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Material flameMaterial = renderer.sharedMaterial;
            if (flameMaterial != null)
            {
                if (flameMaterial.HasProperty("_BaseColor"))
                    flameMaterial.SetColor("_BaseColor", Color.white);

                if (flameMaterial.HasProperty("_Color"))
                    flameMaterial.SetColor("_Color", Color.white);
            }
        }

        ParticleSystem.CollisionModule collision = _flameParticles.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.collidesWith = _hitMask;
        collision.sendCollisionMessages = true;
        collision.lifetimeLoss = 1f;
        collision.dampen = 0f;
        collision.bounce = 0f;
        collision.quality = ParticleSystemCollisionQuality.High;
    }

    private static Gradient BuildStartColorGradientA()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.98f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.72f, 0.16f), 0.45f),
                new GradientColorKey(new Color(0.95f, 0.22f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.08f),
                new GradientAlphaKey(0.82f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Gradient BuildStartColorGradientB()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.48f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.9f, 0.08f, 0.04f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.06f),
                new GradientAlphaKey(0.76f, 0.68f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Gradient BuildLifetimeColorGradientA()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.97f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.56f, 0.1f), 0.42f),
                new GradientColorKey(new Color(0.9f, 0.12f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(0.75f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Gradient BuildLifetimeColorGradientB()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.88f, 0.28f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.08f), 0.5f),
                new GradientColorKey(new Color(0.86f, 0.04f, 0.04f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.92f, 0.08f),
                new GradientAlphaKey(0.68f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private void SyncFlameTransform()
    {
        if (_flameParticles == null) return;

        Transform origin = _flameOrigin != null ? _flameOrigin : transform;
        _flameParticles.transform.SetPositionAndRotation(origin.position, Quaternion.LookRotation(GetFlameDirection(origin)));
    }

    private Vector3 GetFlameDirection(Transform origin)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return origin.forward;

        Vector3 aimPoint = cam.transform.position + cam.transform.forward * (_data != null ? _data.Range : 12f);
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, _data != null ? _data.Range : 12f, _hitMask))
            aimPoint = hit.point;

        Vector3 direction = aimPoint - origin.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : origin.forward;
    }

    private void TickHeat(float deltaTime)
    {
        if (!isActiveAndEnabled)
            return;

        if (_triggerHeld && !_isOverheated)
        {
            _currentHeat += _heatGainPerSecond * deltaTime;
            if (_currentHeat >= _maxHeat)
            {
                _currentHeat = _maxHeat;
                TriggerOverheat();
            }
        }
        else if (_currentHeat > 0f)
        {
            _currentHeat = Mathf.Max(0f, _currentHeat - Mathf.Max(MIN_COOLDOWN_RATE, _heatCooldownPerSecond) * deltaTime);
            if (_isOverheated && _currentHeat <= 0f)
                _isOverheated = false;
        }

        _lastHeatTimestamp = Time.time;
    }

    private void ApplyPassiveCooldown()
    {
        if (_lastHeatTimestamp <= 0f)
        {
            _lastHeatTimestamp = Time.time;
            return;
        }

        float elapsed = Mathf.Max(0f, Time.time - _lastHeatTimestamp);
        if (elapsed <= 0f)
            return;

        if (!_triggerHeld)
        {
            _currentHeat = Mathf.Max(0f, _currentHeat - Mathf.Max(MIN_COOLDOWN_RATE, _heatCooldownPerSecond) * elapsed);
            if (_isOverheated && _currentHeat <= 0f)
                _isOverheated = false;
        }

        _lastHeatTimestamp = Time.time;
    }

    private void TriggerOverheat()
    {
        if (_isOverheated)
            return;

        _isOverheated = true;
        StopFiring();

        if (_overheatSounds != null && _overheatSounds.Length > 0)
            AudioManager.Instance?.PlaySfx(_overheatSounds);
        else if (_data != null)
            AudioManager.Instance?.PlaySfx(_data.EmptyClickSounds);
    }
}
