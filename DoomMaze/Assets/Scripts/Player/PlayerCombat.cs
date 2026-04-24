using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridge between player input and the active weapon.
/// Subscribes to Fire, AltFire, and Melee input actions and delegates to <see cref="IWeapon"/>.
/// The Melee action always fires the quick-melee weapon regardless of the active slot.
/// Supports both semi-auto (performed callback) and full-auto (held poll in Update).
/// Also owns the player's kill-charged laser super.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    private const int DefaultKillsRequiredForSuper = 5;
    private const float DefaultSuperDamage = 200f;
    private const float DefaultSuperRange = 120f;
    private const float DefaultSuperRadius = 0.75f;
    private const float DefaultSuperChargeUpDuration = 0.45f;
    private const float DefaultBeamDuration = 0.2f;
    private const float DefaultBeamWidth = 0.18f;
    private const float DefaultBeamGlowWidth = 0.52f;
    private const float DefaultBeamBodyDiameter = 0.34f;
    private const float DefaultBeamBodyCoreDiameter = 0.16f;
    private const float DefaultBeamVisualForwardOffset = 0.6f;
    private const float DefaultBeamViewFadeDistance = 0.22f;
    private const float DefaultBeamViewMinAlpha = 0.2f;
    private const float DefaultChargeSpinSpeed = 720f;
    private const float DefaultBeamRotationSpeed = 1440f;
    private const float DefaultBeamSpiralRadius = 0.26f;
    private const float DefaultBeamParticleOrbitRadius = 0.24f;
    private const float DefaultBeamParticleRate = 160f;
    private const float DefaultBeamParticleLifetime = 0.18f;
    private const float DefaultBeamParticleSize = 0.09f;
    private const float DefaultBeamParticleSpin = 2160f;
    private const int SpiralPointCount = 28;
    private const int RingPointCount = 48;

    private static Material s_superLineMaterial;
    private static Material s_superSurfaceMaterial;
    private static Material s_superParticleMaterial;

    /// <summary>Currently active weapon slot index (0-based).</summary>
    public int ActiveWeaponSlot { get; private set; }
    public IWeapon ActiveWeapon => _activeWeapon;
    public float SuperChargeNormalized => Mathf.Clamp01((float)_superKillCharge / GetKillsRequiredForSuper());
    public bool IsSuperReady => _superKillCharge >= GetKillsRequiredForSuper();
    public int SuperKillCharge => _superKillCharge;
    public int SuperKillsRequired => GetKillsRequiredForSuper();
    public bool IsCombatEnabled => _isCombatEnabled;

    [Header("Super")]
    [SerializeField] private int _killsRequiredForSuper = DefaultKillsRequiredForSuper;
    [SerializeField] private float _superDamage = DefaultSuperDamage;
    [SerializeField] private float _superRange = DefaultSuperRange;
    [SerializeField] private float _superRadius = DefaultSuperRadius;
    [SerializeField] private LayerMask _superHitMask = ~0;
    [SerializeField] private float _superChargeUpDuration = DefaultSuperChargeUpDuration;
    [SerializeField] private float _superBeamDuration = DefaultBeamDuration;
    [SerializeField] private float _superBeamWidth = DefaultBeamWidth;
    [SerializeField] private float _superBeamGlowWidth = DefaultBeamGlowWidth;
    [SerializeField] private float _superBeamBodyDiameter = DefaultBeamBodyDiameter;
    [SerializeField] private float _superBeamBodyCoreDiameter = DefaultBeamBodyCoreDiameter;
    [SerializeField] private float _superBeamVisualForwardOffset = DefaultBeamVisualForwardOffset;
    [SerializeField] private float _superBeamViewFadeDistance = DefaultBeamViewFadeDistance;
    [Range(0f, 1f)] [SerializeField] private float _superBeamViewMinAlpha = DefaultBeamViewMinAlpha;
    [SerializeField] private bool _enableSuperBeamSpiralEffects = true;
    [SerializeField] private float _superChargeSpinSpeed = DefaultChargeSpinSpeed;
    [SerializeField] private float _superBeamRotationSpeed = DefaultBeamRotationSpeed;
    [SerializeField] private float _superBeamSpiralRadius = DefaultBeamSpiralRadius;
    [SerializeField] private float _superBeamParticleOrbitRadius = DefaultBeamParticleOrbitRadius;
    [SerializeField] private float _superBeamParticleRate = DefaultBeamParticleRate;
    [SerializeField] private float _superBeamParticleLifetime = DefaultBeamParticleLifetime;
    [SerializeField] private float _superBeamParticleSize = DefaultBeamParticleSize;
    [SerializeField] private float _superBeamParticleSpinSpeed = DefaultBeamParticleSpin;
    [SerializeField] private Vector3 _superChargeLocalOffset = new Vector3(0.38f, -0.22f, 1.15f);
    [SerializeField] private Gradient _superBeamGradient;
    [Header("Super Audio")]
    [SerializeField] private AudioClip[] _superChargeSounds;
    [Range(0f, 1f)] [SerializeField] private float _superChargeSoundVolume = 1f;
    [SerializeField] private AudioClip[] _superFireSounds;
    [Range(0f, 1f)] [SerializeField] private float _superFireSoundVolume = 1f;
    [ColorUsage(true, true)] [SerializeField] private Color _superChargeColor = new Color(3.6f, 1.4f, 0.35f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color _superGlowColor = new Color(4.5f, 0.55f, 0.12f, 1f);
    [SerializeField] private float _superChargeLightIntensity = 5f;
    [SerializeField] private float _superChargeLightRange = 8f;
    [SerializeField] private float _superBeamLightIntensity = 6.5f;
    [SerializeField] private float _superBeamLightRange = 10f;

    private IWeapon _activeWeapon;
    private MeleeWeapon _quickMeleeWeapon;
    private bool _isFiring;
    private bool _inputBound;
    private bool _isUsingSuper;
    private bool _isCombatEnabled = true;
    private int _superKillCharge;
    private Coroutine _superRoutine;

    private readonly RaycastHit[] _superObstacleHits = new RaycastHit[16];
    private readonly Collider[] _superOverlapHits = new Collider[32];
    private readonly HealthComponent[] _superHealthHits = new HealthComponent[16];

    private sealed class SuperChargeFx
    {
        public GameObject RootObject;
        public Transform Root;
        public Transform RingA;
        public Transform RingB;
        public Transform RingC;
        public Transform Core;
        public LineRenderer RingALine;
        public LineRenderer RingBLine;
        public LineRenderer RingCLine;
        public Renderer CoreRenderer;
        public Light GlowLight;
    }

    private sealed class SuperBeamFx
    {
        public GameObject RootObject;
        public Transform Root;
        public Transform BeamBody;
        public Transform BeamCore;
        public Renderer BeamBodyRenderer;
        public Renderer BeamCoreRenderer;
        public LineRenderer GlowLine;
        public LineRenderer CoreLine;
        public LineRenderer SpiralA;
        public LineRenderer SpiralB;
        public Transform ParticleOrbitA;
        public Transform ParticleOrbitB;
        public ParticleSystem OrbitParticlesA;
        public ParticleSystem OrbitParticlesB;
        public Light GlowLight;
        public float Length;
    }

    private void Start()
    {
        EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
        TryBindInput();
        RaiseSuperMeterChanged();
    }

    private void OnDestroy()
    {
        EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
        UnbindInput();

        if (_superRoutine != null)
            StopCoroutine(_superRoutine);
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Playing)
            TryBindInput();
    }

    private void TryBindInput()
    {
        if (_inputBound || InputManager.Instance == null)
            return;

        var player = InputManager.Instance.Controls.Player;
        player.Fire.performed += OnFirePerformed;
        player.Fire.canceled += OnFireCanceled;
        player.AltFire.performed += OnAltFirePerformed;
        player.Melee.performed += OnMeleePerformed;
        player.UseSuper.performed += OnUseSuperPerformed;

        _inputBound = true;
    }

    private void UnbindInput()
    {
        if (!_inputBound || InputManager.Instance == null)
            return;

        var player = InputManager.Instance.Controls.Player;
        player.Fire.performed -= OnFirePerformed;
        player.Fire.canceled -= OnFireCanceled;
        player.AltFire.performed -= OnAltFirePerformed;
        player.Melee.performed -= OnMeleePerformed;
        player.UseSuper.performed -= OnUseSuperPerformed;

        _inputBound = false;
    }

    private void Update()
    {
        if (!_isCombatEnabled || _isUsingSuper)
            return;

        if (_isFiring && _activeWeapon != null && _activeWeapon.Data?.FireMode == FireMode.Auto)
            _activeWeapon.Fire();
    }

    /// <summary>Sets the active weapon slot and raises <see cref="WeaponEquippedEvent"/>.</summary>
    public void SetActiveWeapon(int slot)
    {
        ActiveWeaponSlot = slot;
        EventBus<WeaponEquippedEvent>.Raise(new WeaponEquippedEvent { SlotIndex = slot });
    }

    /// <summary>
    /// Sets both the active slot index and the <see cref="IWeapon"/> reference.
    /// Called by <see cref="WeaponSwitcher"/> on every slot change.
    /// </summary>
    public void SetActiveWeapon(int slot, IWeapon weapon)
    {
        ActiveWeaponSlot = slot;
        _activeWeapon = weapon;
        EventBus<WeaponEquippedEvent>.Raise(new WeaponEquippedEvent { SlotIndex = slot });
    }

    /// <summary>
    /// Registers the always-available quick-melee weapon triggered by the Melee keybind (F).
    /// Called by <see cref="WeaponSwitcher"/> during initialisation.
    /// </summary>
    public void SetQuickMeleeWeapon(MeleeWeapon meleeWeapon)
    {
        _quickMeleeWeapon = meleeWeapon;
    }

    public void SetCombatEnabled(bool isEnabled)
    {
        if (_isCombatEnabled == isEnabled)
            return;

        _isCombatEnabled = isEnabled;

        if (!isEnabled)
            CancelCombatInput();
    }

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        if (!_isCombatEnabled || _isUsingSuper)
            return;

        _isFiring = true;

        if (_activeWeapon?.Data?.FireMode == FireMode.Semi)
            _activeWeapon.Fire();
    }

    private void OnFireCanceled(InputAction.CallbackContext context)
    {
        _isFiring = false;
        _activeWeapon?.StopFiring();
    }

    private void OnAltFirePerformed(InputAction.CallbackContext context)
    {
        if (!_isCombatEnabled || _isUsingSuper)
            return;

        _activeWeapon?.AltFire();
    }

    private void OnMeleePerformed(InputAction.CallbackContext context)
    {
        if (!_isCombatEnabled || _isUsingSuper)
            return;

        _quickMeleeWeapon?.Fire();
    }

    private void OnUseSuperPerformed(InputAction.CallbackContext context)
    {
        if (!_isCombatEnabled)
            return;

        TryUseSuper();
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (_superKillCharge >= GetKillsRequiredForSuper())
            return;

        _superKillCharge = Mathf.Min(_superKillCharge + 1, GetKillsRequiredForSuper());
        RaiseSuperMeterChanged();
    }

    public void FillSuperMeter()
    {
        _superKillCharge = GetKillsRequiredForSuper();
        RaiseSuperMeterChanged();
    }

    private void TryUseSuper()
    {
        if (!_isCombatEnabled || !IsSuperReady || _isUsingSuper)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        _isUsingSuper = true;
        _isFiring = false;
        _activeWeapon?.StopFiring();

        _superKillCharge = 0;
        RaiseSuperMeterChanged();

        if (_superRoutine != null)
            StopCoroutine(_superRoutine);

        _superRoutine = StartCoroutine(UseSuperRoutine(cam));
    }

    private void CancelCombatInput()
    {
        _isFiring = false;
        _activeWeapon?.StopFiring();
    }

    private IEnumerator UseSuperRoutine(Camera cam)
    {
        Transform camTransform = cam != null ? cam.transform : null;
        SuperChargeFx chargeFx = camTransform != null ? CreateChargeFx(camTransform) : null;
        float duration = GetSuperChargeUpDuration();
        float elapsed = 0f;

        AudioManager.Instance?.PlaySfx(_superChargeSounds, _superChargeSoundVolume);
        UpdateChargeFx(chargeFx, 0f, 0f);

        while (elapsed < duration)
        {
            if (camTransform == null)
            {
                CleanupChargeFx(chargeFx);
                _isUsingSuper = false;
                _superRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = duration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / duration);
            UpdateChargeFx(chargeFx, t, elapsed);
            yield return null;
        }

        CleanupChargeFx(chargeFx);

        if (camTransform != null)
        {
            Vector3 origin = camTransform.position;
            Vector3 direction = camTransform.forward;
            Vector3 endPoint = ResolveSuperEndPoint(origin, direction, GetSuperRange());
            FireSuperLaser(origin, endPoint);
        }

        _isUsingSuper = false;
        _superRoutine = null;
    }

    private Vector3 ResolveSuperEndPoint(Vector3 origin, Vector3 direction, float range)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            _superObstacleHits,
            range,
            _superHitMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = range;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _superObstacleHits[i];
            if (hit.collider == null || IsPlayerCollider(hit.collider))
                continue;

            HealthComponent health = hit.collider.GetComponentInParent<HealthComponent>();
            if (health != null && !health.IsPlayer)
                continue;

            if (hit.distance < nearestDistance)
                nearestDistance = hit.distance;
        }

        return origin + direction * nearestDistance;
    }

    private void FireSuperLaser(Vector3 origin, Vector3 endPoint)
    {
        AudioManager.Instance?.PlaySfx(_superFireSounds, _superFireSoundVolume);

        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            origin,
            endPoint,
            GetSuperRadius(),
            _superOverlapHits,
            _superHitMask,
            QueryTriggerInteraction.Ignore);

        int uniqueHealthCount = 0;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = _superOverlapHits[i];
            if (overlap == null || IsPlayerCollider(overlap))
                continue;

            HealthComponent health = overlap.GetComponentInParent<HealthComponent>();
            if (health == null || !health.IsAlive || health.IsPlayer || Contains(_superHealthHits, uniqueHealthCount, health))
                continue;

            if (uniqueHealthCount < _superHealthHits.Length)
                _superHealthHits[uniqueHealthCount++] = health;
        }

        for (int i = 0; i < uniqueHealthCount; i++)
        {
            HealthComponent health = _superHealthHits[i];
            _superHealthHits[i] = null;

            if (health == null || !health.IsAlive)
                continue;

            health.TakeDamage(new DamageInfo
            {
                Amount = GetSuperDamage(),
                Type = DamageType.Energy,
                Source = gameObject
            });
        }

        StartCoroutine(BeamFxRoutine(origin, endPoint));

        EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
        {
            Magnitude = 0.42f,
            Duration = 0.22f
        });

        EventBus<SuperFiredEvent>.Raise(new SuperFiredEvent
        {
            Origin = origin,
            EndPoint = endPoint
        });
    }

    private IEnumerator BeamFxRoutine(Vector3 origin, Vector3 endPoint)
    {
        SuperBeamFx beamFx = CreateBeamFx(origin, endPoint);
        float duration = GetSuperBeamDuration();
        float elapsed = 0f;

        UpdateBeamFx(beamFx, 0f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / duration);
            UpdateBeamFx(beamFx, t, elapsed);
            yield return null;
        }

        CleanupBeamFx(beamFx);
    }

    private SuperChargeFx CreateChargeFx(Transform camTransform)
    {
        var chargeFx = new SuperChargeFx();
        chargeFx.RootObject = new GameObject("SuperChargeFx");
        chargeFx.Root = chargeFx.RootObject.transform;
        chargeFx.Root.SetParent(camTransform, false);
        chargeFx.Root.localPosition = _superChargeLocalOffset;
        chargeFx.Root.localRotation = Quaternion.identity;

        chargeFx.RingALine = CreateLoopLineRenderer(chargeFx.Root, "ChargeRingA", 0.028f);
        chargeFx.RingA = chargeFx.RingALine.transform;
        BuildCircle(chargeFx.RingALine, 0.24f, RingPointCount);

        chargeFx.RingBLine = CreateLoopLineRenderer(chargeFx.Root, "ChargeRingB", 0.022f);
        chargeFx.RingB = chargeFx.RingBLine.transform;
        BuildCircle(chargeFx.RingBLine, 0.18f, RingPointCount);

        chargeFx.RingCLine = CreateLoopLineRenderer(chargeFx.Root, "ChargeRingC", 0.018f);
        chargeFx.RingC = chargeFx.RingCLine.transform;
        BuildCircle(chargeFx.RingCLine, 0.31f, RingPointCount);

        GameObject coreObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreObject.name = "ChargeCore";
        Destroy(coreObject.GetComponent<Collider>());
        coreObject.transform.SetParent(chargeFx.Root, false);
        chargeFx.Core = coreObject.transform;
        chargeFx.Core.localScale = Vector3.one * 0.08f;
        chargeFx.CoreRenderer = coreObject.GetComponent<Renderer>();
        chargeFx.CoreRenderer.sharedMaterial = CreateTintedMaterial(_superChargeColor);

        GameObject lightObject = new GameObject("ChargeLight");
        lightObject.transform.SetParent(chargeFx.Root, false);
        chargeFx.GlowLight = lightObject.AddComponent<Light>();
        chargeFx.GlowLight.type = LightType.Point;
        chargeFx.GlowLight.shadows = LightShadows.None;
        chargeFx.GlowLight.color = _superGlowColor;
        chargeFx.GlowLight.range = 0f;
        chargeFx.GlowLight.intensity = 0f;

        return chargeFx;
    }

    private void UpdateChargeFx(SuperChargeFx chargeFx, float normalized, float elapsed)
    {
        if (chargeFx == null || chargeFx.Root == null)
            return;

        float pulse = 1f + Mathf.Sin(elapsed * 20f) * 0.08f;
        float scaleLerp = Mathf.Lerp(0.45f, 1.25f, normalized);
        float coreScale = Mathf.Lerp(0.05f, 0.16f, normalized) * pulse;
        float spinSpeed = GetSuperChargeSpinSpeed();
        float alpha = Mathf.Lerp(0.35f, 1f, normalized);

        chargeFx.Root.localPosition = _superChargeLocalOffset;
        chargeFx.Core.localScale = Vector3.one * coreScale;
        chargeFx.RingA.localScale = Vector3.one * scaleLerp;
        chargeFx.RingB.localScale = Vector3.one * (scaleLerp * 0.85f);
        chargeFx.RingC.localScale = Vector3.one * (scaleLerp * 1.15f);

        chargeFx.RingA.localRotation = Quaternion.Euler(0f, 0f, elapsed * spinSpeed);
        chargeFx.RingB.localRotation = Quaternion.Euler(elapsed * spinSpeed * 0.75f, elapsed * spinSpeed * 1.15f, elapsed * spinSpeed * 0.4f);
        chargeFx.RingC.localRotation = Quaternion.Euler(90f + elapsed * spinSpeed * 1.1f, elapsed * spinSpeed * 1.75f, elapsed * spinSpeed * 0.65f);

        Color hotColor = WithAlpha(_superChargeColor, alpha);
        Color glowColor = WithAlpha(_superGlowColor, alpha * 0.75f);
        SetLineColor(chargeFx.RingALine, hotColor);
        SetLineColor(chargeFx.RingBLine, glowColor);
        SetLineColor(chargeFx.RingCLine, hotColor);

        chargeFx.RingALine.widthMultiplier = Mathf.Lerp(0.014f, 0.04f, normalized);
        chargeFx.RingBLine.widthMultiplier = Mathf.Lerp(0.012f, 0.03f, normalized);
        chargeFx.RingCLine.widthMultiplier = Mathf.Lerp(0.01f, 0.025f, normalized);

        if (chargeFx.CoreRenderer != null)
            SetRendererColor(chargeFx.CoreRenderer, Color.Lerp(_superChargeColor, _superGlowColor, normalized * 0.4f));

        if (chargeFx.GlowLight != null)
        {
            chargeFx.GlowLight.range = Mathf.Lerp(1.5f, _superChargeLightRange, normalized);
            chargeFx.GlowLight.intensity = Mathf.Lerp(0.3f, _superChargeLightIntensity, normalized) * pulse;
        }
    }

    private void CleanupChargeFx(SuperChargeFx chargeFx)
    {
        if (chargeFx != null && chargeFx.RootObject != null)
            Destroy(chargeFx.RootObject);
    }

    private SuperBeamFx CreateBeamFx(Vector3 origin, Vector3 endPoint)
    {
        Vector3 beamVector = endPoint - origin;
        float beamLength = Mathf.Max(0.01f, beamVector.magnitude);
        Vector3 beamDirection = beamVector.sqrMagnitude > 0.0001f
            ? beamVector.normalized
            : Vector3.forward;
        float visualOffset = Mathf.Clamp(GetSuperBeamVisualForwardOffset(), 0f, Mathf.Max(0f, beamLength - 0.01f));
        Vector3 visualOrigin = origin + beamDirection * visualOffset;
        float visualLength = Mathf.Max(0.01f, beamLength - visualOffset);
        Quaternion beamRotation = beamVector.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(beamDirection)
            : Quaternion.identity;

        var beamFx = new SuperBeamFx();
        beamFx.RootObject = new GameObject("SuperLaserBeamFx");
        beamFx.Root = beamFx.RootObject.transform;
        beamFx.Root.SetPositionAndRotation(visualOrigin, beamRotation);
        beamFx.Length = visualLength;

        beamFx.BeamBodyRenderer = CreateBeamCylinder(beamFx.Root, "BeamBody", visualLength, GetSuperBeamBodyDiameter());
        beamFx.BeamBody = beamFx.BeamBodyRenderer != null ? beamFx.BeamBodyRenderer.transform : null;
        beamFx.BeamCoreRenderer = CreateBeamCylinder(beamFx.Root, "BeamCoreBody", visualLength, GetSuperBeamBodyCoreDiameter());
        beamFx.BeamCore = beamFx.BeamCoreRenderer != null ? beamFx.BeamCoreRenderer.transform : null;
        beamFx.GlowLine = CreateBeamLineRenderer(beamFx.Root, "GlowBeam", GetSuperBeamGlowWidth());
        beamFx.CoreLine = CreateBeamLineRenderer(beamFx.Root, "CoreBeam", GetSuperBeamWidth());

        if (_enableSuperBeamSpiralEffects)
        {
            beamFx.SpiralA = CreateBeamLineRenderer(beamFx.Root, "SpiralA", 0.06f, SpiralPointCount);
            beamFx.SpiralB = CreateBeamLineRenderer(beamFx.Root, "SpiralB", 0.05f, SpiralPointCount);
            beamFx.ParticleOrbitA = CreateParticleOrbitTransform(beamFx.Root, "BeamParticleOrbitA");
            beamFx.ParticleOrbitB = CreateParticleOrbitTransform(beamFx.Root, "BeamParticleOrbitB");
            beamFx.OrbitParticlesA = CreateBeamOrbitParticles(beamFx.ParticleOrbitA, "BeamParticlesA", visualLength, GetSuperBeamParticleOrbitRadius());
            beamFx.OrbitParticlesB = CreateBeamOrbitParticles(beamFx.ParticleOrbitB, "BeamParticlesB", visualLength, GetSuperBeamParticleOrbitRadius() * 0.72f);
        }

        SetStraightLine(beamFx.GlowLine, visualLength);
        SetStraightLine(beamFx.CoreLine, visualLength);

        GameObject lightObject = new GameObject("BeamLight");
        lightObject.transform.SetParent(beamFx.Root, false);
        beamFx.GlowLight = lightObject.AddComponent<Light>();
        beamFx.GlowLight.type = LightType.Point;
        beamFx.GlowLight.shadows = LightShadows.None;
        beamFx.GlowLight.color = _superGlowColor;
        beamFx.GlowLight.range = _superBeamLightRange;
        beamFx.GlowLight.intensity = _superBeamLightIntensity;

        return beamFx;
    }

    private void UpdateBeamFx(SuperBeamFx beamFx, float normalized, float elapsed)
    {
        if (beamFx == null || beamFx.Root == null)
            return;

        float fade = 1f - normalized;
        float pulse = 1f + Mathf.Sin(elapsed * 40f) * 0.08f;
        float bodyDiameter = Mathf.Lerp(GetSuperBeamBodyDiameter() * 1.25f, GetSuperBeamBodyDiameter() * 0.72f, normalized) * fade * pulse;
        float coreDiameter = Mathf.Lerp(GetSuperBeamBodyCoreDiameter() * 1.35f, GetSuperBeamBodyCoreDiameter() * 0.78f, normalized) * pulse;
        float glowWidth = Mathf.Lerp(GetSuperBeamGlowWidth() * 1.2f, GetSuperBeamGlowWidth() * 0.55f, normalized) * fade * pulse;
        float coreWidth = Mathf.Lerp(GetSuperBeamWidth() * 1.8f, GetSuperBeamWidth() * 0.75f, normalized) * pulse;
        float spiralRadius = Mathf.Lerp(GetSuperBeamSpiralRadius(), 0.04f, normalized);
        float spiralPhase = elapsed * GetSuperBeamRotationSpeed();
        float particlePhase = elapsed * GetSuperBeamParticleSpinSpeed();
        float beamViewAlpha = GetBeamViewAlpha(beamFx, Mathf.Max(bodyDiameter, glowWidth));
        float lineViewAlpha = Mathf.Lerp(0.55f, 1f, beamViewAlpha);

        UpdateBeamCylinder(beamFx.BeamBody, bodyDiameter);
        UpdateBeamCylinder(beamFx.BeamCore, coreDiameter);
        beamFx.GlowLine.widthMultiplier = Mathf.Max(0.01f, glowWidth);
        beamFx.CoreLine.widthMultiplier = Mathf.Max(0.01f, coreWidth);
        if (beamFx.SpiralA != null)
            beamFx.SpiralA.widthMultiplier = Mathf.Lerp(0.08f, 0.02f, normalized);
        if (beamFx.SpiralB != null)
            beamFx.SpiralB.widthMultiplier = Mathf.Lerp(0.06f, 0.016f, normalized);

        Color bodyColor = WithAlpha(
            Color.Lerp(_superChargeColor, _superGlowColor, 0.25f + (1f - normalized) * 0.2f),
            Mathf.Lerp(GetSuperBeamViewMinAlpha(), 0.88f, beamViewAlpha) * fade);
        Color coreBodyColor = WithAlpha(
            Color.Lerp(_superGlowColor, Color.white * 1.25f, 0.45f),
            Mathf.Lerp(GetSuperBeamViewMinAlpha() * 0.85f, 0.74f, beamViewAlpha));
        Color beamStart = WithAlpha(GetBeamColor(0f), fade * 0.95f * lineViewAlpha);
        Color beamEnd = WithAlpha(GetBeamColor(1f), fade * 0.8f * lineViewAlpha);
        Color glowStart = WithAlpha(_superGlowColor, fade * 0.42f * lineViewAlpha);
        Color glowEnd = WithAlpha(_superChargeColor, fade * 0.18f * lineViewAlpha);
        Color spiralColor = WithAlpha(Color.Lerp(_superChargeColor, _superGlowColor, 0.5f), fade * 0.7f * lineViewAlpha);

        SetRendererColor(beamFx.BeamBodyRenderer, bodyColor);
        SetRendererColor(beamFx.BeamCoreRenderer, coreBodyColor);
        SetLineGradient(beamFx.CoreLine, beamStart, beamEnd);
        SetLineGradient(beamFx.GlowLine, glowStart, glowEnd);

        if (_enableSuperBeamSpiralEffects)
        {
            SetLineColor(beamFx.SpiralA, spiralColor);
            SetLineColor(beamFx.SpiralB, spiralColor);

            UpdateSpiralLine(beamFx.SpiralA, beamFx.Length, spiralRadius, spiralPhase, 0f);
            UpdateSpiralLine(beamFx.SpiralB, beamFx.Length, spiralRadius * 0.72f, -spiralPhase * 1.15f, 180f);
            UpdateBeamOrbitParticles(
                beamFx.OrbitParticlesA,
                WithAlpha(_superGlowColor, fade * 0.7f),
                GetSuperBeamParticleRate() * Mathf.Lerp(1f, 0.3f, normalized));
            UpdateBeamOrbitParticles(
                beamFx.OrbitParticlesB,
                WithAlpha(_superChargeColor, fade * 0.65f),
                GetSuperBeamParticleRate() * Mathf.Lerp(0.92f, 0.24f, normalized));

            if (beamFx.ParticleOrbitA != null)
                beamFx.ParticleOrbitA.localRotation = Quaternion.Euler(0f, 0f, particlePhase);

            if (beamFx.ParticleOrbitB != null)
                beamFx.ParticleOrbitB.localRotation = Quaternion.Euler(0f, 0f, 180f - particlePhase * 1.25f);
        }

        if (beamFx.GlowLight != null)
        {
            beamFx.GlowLight.range = Mathf.Lerp(_superBeamLightRange, _superBeamLightRange * 0.5f, normalized);
            beamFx.GlowLight.intensity = Mathf.Lerp(_superBeamLightIntensity, 0f, normalized) * pulse;
        }
    }

    private void CleanupBeamFx(SuperBeamFx beamFx)
    {
        if (beamFx != null && beamFx.RootObject != null)
            Destroy(beamFx.RootObject);
    }

    private LineRenderer CreateLoopLineRenderer(Transform parent, string objectName, float width)
    {
        LineRenderer lineRenderer = CreateBaseLineRenderer(parent, objectName, width, RingPointCount);
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.alignment = LineAlignment.TransformZ;
        return lineRenderer;
    }

    private LineRenderer CreateBeamLineRenderer(Transform parent, string objectName, float width, int pointCount = 2)
    {
        LineRenderer lineRenderer = CreateBaseLineRenderer(parent, objectName, width, pointCount);
        lineRenderer.loop = false;
        lineRenderer.useWorldSpace = false;
        lineRenderer.alignment = LineAlignment.View;
        return lineRenderer;
    }

    private LineRenderer CreateBaseLineRenderer(Transform parent, string objectName, float width, int pointCount)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = pointCount;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.numCapVertices = 10;
        lineRenderer.numCornerVertices = 8;
        lineRenderer.widthMultiplier = width;
        lineRenderer.sharedMaterial = GetSuperLineMaterial();
        return lineRenderer;
    }

    private Renderer CreateBeamCylinder(Transform parent, string objectName, float beamLength, float diameter)
    {
        GameObject cylinderObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinderObject.name = objectName;
        Destroy(cylinderObject.GetComponent<Collider>());
        cylinderObject.transform.SetParent(parent, false);
        cylinderObject.transform.localPosition = new Vector3(0f, 0f, beamLength * 0.5f);
        cylinderObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cylinderObject.transform.localScale = new Vector3(
            Mathf.Max(0.01f, diameter),
            Mathf.Max(0.01f, beamLength * 0.5f),
            Mathf.Max(0.01f, diameter));

        Renderer renderer = cylinderObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateTintedMaterial(_superGlowColor);

        return renderer;
    }

    private Transform CreateParticleOrbitTransform(Transform parent, string objectName)
    {
        GameObject orbitObject = new GameObject(objectName);
        orbitObject.transform.SetParent(parent, false);
        orbitObject.transform.localPosition = Vector3.zero;
        orbitObject.transform.localRotation = Quaternion.identity;
        return orbitObject.transform;
    }

    private ParticleSystem CreateBeamOrbitParticles(Transform parent, string objectName, float beamLength, float orbitRadius)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = Vector3.zero;
        particleObject.transform.localRotation = Quaternion.identity;

        ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();

        var main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = Mathf.Max(0.01f, GetSuperBeamDuration());
        main.startLifetime = Mathf.Max(0.01f, GetSuperBeamParticleLifetime());
        main.startSpeed = 0f;
        main.startSize = Mathf.Max(0.01f, GetSuperBeamParticleSize());
        main.startColor = new ParticleSystem.MinMaxGradient(_superGlowColor);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 256;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = GetSuperBeamParticleRate();

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.04f, 0.04f, beamLength);
        shape.position = new Vector3(orbitRadius, 0f, beamLength * 0.5f);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.7f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0.2f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rotationOverLifetime = particleSystem.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(Mathf.Deg2Rad * 160f);

        var noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 1.4f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;

        if (renderer != null)
        {
            renderer.sharedMaterial = GetSuperParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.alignment = ParticleSystemRenderSpace.View;
        }

        particleSystem.Play();
        return particleSystem;
    }

    private static void BuildCircle(LineRenderer lineRenderer, float radius, int pointCount)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float angle = (float)i / pointCount * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private static void SetStraightLine(LineRenderer lineRenderer, float length)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, Vector3.zero);
        lineRenderer.SetPosition(1, new Vector3(0f, 0f, length));
    }

    private static void UpdateSpiralLine(LineRenderer lineRenderer, float length, float radius, float phaseDegrees, float phaseOffsetDegrees)
    {
        if (lineRenderer == null)
            return;

        int pointCount = lineRenderer.positionCount;
        float turns = 9f;

        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount <= 1 ? 0f : (float)i / (pointCount - 1);
            float z = length * t;
            float angle = phaseDegrees + phaseOffsetDegrees + t * turns * 360f;
            float radians = angle * Mathf.Deg2Rad;
            float taperedRadius = radius * (0.35f + (1f - t) * 0.65f);
            float x = Mathf.Cos(radians) * taperedRadius;
            float y = Mathf.Sin(radians) * taperedRadius;
            lineRenderer.SetPosition(i, new Vector3(x, y, z));
        }
    }

    private static void UpdateBeamCylinder(Transform cylinder, float diameter)
    {
        if (cylinder == null)
            return;

        Vector3 localScale = cylinder.localScale;
        localScale.x = Mathf.Max(0.01f, diameter);
        localScale.z = Mathf.Max(0.01f, diameter);
        cylinder.localScale = localScale;
    }

    private float GetBeamViewAlpha(SuperBeamFx beamFx, float beamDiameter)
    {
        if (beamFx == null || beamFx.Root == null)
            return 1f;

        Camera cam = Camera.main;
        if (cam == null)
            return 1f;

        Vector3 start = beamFx.Root.position;
        Vector3 end = start + beamFx.Root.forward * beamFx.Length;
        float distanceToBeam = DistancePointToSegment(cam.transform.position, start, end);
        float fadeRadius = Mathf.Max(0.01f, beamDiameter * 0.5f + GetSuperBeamViewFadeDistance());
        return Mathf.Clamp01(distanceToBeam / fadeRadius);
    }

    private void RaiseSuperMeterChanged()
    {
        EventBus<SuperMeterChangedEvent>.Raise(new SuperMeterChangedEvent
        {
            ChargeNormalized = SuperChargeNormalized,
            CurrentCharges = _superKillCharge,
            ChargesRequired = GetKillsRequiredForSuper(),
            IsReady = IsSuperReady
        });
    }

    private bool IsPlayerCollider(Collider collider)
    {
        return collider != null && collider.transform.root == transform.root;
    }

    private int GetKillsRequiredForSuper()
    {
        return Mathf.Max(1, _killsRequiredForSuper);
    }

    private float GetSuperDamage()
    {
        return _superDamage > 0f ? _superDamage : DefaultSuperDamage;
    }

    private float GetSuperRange()
    {
        return _superRange > 0f ? _superRange : DefaultSuperRange;
    }

    private float GetSuperRadius()
    {
        return _superRadius > 0f ? _superRadius : DefaultSuperRadius;
    }

    private float GetSuperChargeUpDuration()
    {
        return Mathf.Max(0f, _superChargeUpDuration);
    }

    private float GetSuperBeamDuration()
    {
        return _superBeamDuration > 0f ? _superBeamDuration : DefaultBeamDuration;
    }

    private float GetSuperBeamWidth()
    {
        return _superBeamWidth > 0f ? _superBeamWidth : DefaultBeamWidth;
    }

    private float GetSuperBeamGlowWidth()
    {
        return _superBeamGlowWidth > 0f ? _superBeamGlowWidth : DefaultBeamGlowWidth;
    }

    private float GetSuperBeamBodyDiameter()
    {
        return _superBeamBodyDiameter > 0f ? _superBeamBodyDiameter : DefaultBeamBodyDiameter;
    }

    private float GetSuperBeamBodyCoreDiameter()
    {
        return _superBeamBodyCoreDiameter > 0f ? _superBeamBodyCoreDiameter : DefaultBeamBodyCoreDiameter;
    }

    private float GetSuperBeamVisualForwardOffset()
    {
        return Mathf.Max(0f, _superBeamVisualForwardOffset);
    }

    private float GetSuperBeamViewFadeDistance()
    {
        return Mathf.Max(0f, _superBeamViewFadeDistance);
    }

    private float GetSuperBeamViewMinAlpha()
    {
        return Mathf.Clamp01(_superBeamViewMinAlpha);
    }

    private float GetSuperChargeSpinSpeed()
    {
        return _superChargeSpinSpeed != 0f ? _superChargeSpinSpeed : DefaultChargeSpinSpeed;
    }

    private float GetSuperBeamRotationSpeed()
    {
        return _superBeamRotationSpeed != 0f ? _superBeamRotationSpeed : DefaultBeamRotationSpeed;
    }

    private float GetSuperBeamSpiralRadius()
    {
        return _superBeamSpiralRadius > 0f ? _superBeamSpiralRadius : DefaultBeamSpiralRadius;
    }

    private float GetSuperBeamParticleOrbitRadius()
    {
        return _superBeamParticleOrbitRadius > 0f ? _superBeamParticleOrbitRadius : DefaultBeamParticleOrbitRadius;
    }

    private float GetSuperBeamParticleRate()
    {
        return _superBeamParticleRate > 0f ? _superBeamParticleRate : DefaultBeamParticleRate;
    }

    private float GetSuperBeamParticleLifetime()
    {
        return _superBeamParticleLifetime > 0f ? _superBeamParticleLifetime : DefaultBeamParticleLifetime;
    }

    private float GetSuperBeamParticleSize()
    {
        return _superBeamParticleSize > 0f ? _superBeamParticleSize : DefaultBeamParticleSize;
    }

    private float GetSuperBeamParticleSpinSpeed()
    {
        return _superBeamParticleSpinSpeed != 0f ? _superBeamParticleSpinSpeed : DefaultBeamParticleSpin;
    }

    private Color GetBeamColor(float normalized)
    {
        if (_superBeamGradient != null && _superBeamGradient.colorKeys != null && _superBeamGradient.colorKeys.Length > 0)
            return _superBeamGradient.Evaluate(Mathf.Clamp01(normalized));

        return Color.Lerp(_superChargeColor, _superGlowColor, Mathf.Clamp01(normalized));
    }

    private static Material GetSuperLineMaterial()
    {
        if (s_superLineMaterial != null)
            return s_superLineMaterial;

        Shader shader = FindSuperFxShader();
        if (shader == null)
            return null;

        s_superLineMaterial = new Material(shader);
        return s_superLineMaterial;
    }

    private static Material GetSuperSurfaceMaterial()
    {
        if (s_superSurfaceMaterial != null)
            return s_superSurfaceMaterial;

        Shader shader = FindSuperFxShader();
        if (shader == null)
            return null;

        s_superSurfaceMaterial = new Material(shader);
        ConfigureTransparentMaterial(s_superSurfaceMaterial);
        return s_superSurfaceMaterial;
    }

    private static Material GetSuperParticleMaterial()
    {
        if (s_superParticleMaterial != null)
            return s_superParticleMaterial;

        Shader shader = FindSuperParticleShader();
        if (shader == null)
            return null;

        s_superParticleMaterial = new Material(shader);

        if (s_superParticleMaterial.HasProperty("_MainTex"))
            s_superParticleMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);

        if (s_superParticleMaterial.HasProperty("_BaseMap"))
            s_superParticleMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);

        return s_superParticleMaterial;
    }

    private static Material CreateTintedMaterial(Color color)
    {
        Material source = GetSuperSurfaceMaterial();
        if (source == null)
            return null;

        Material material = new Material(source);
        ConfigureTransparentMaterial(material);
        ApplyMaterialColor(material, color);
        return material;
    }

    private static Shader FindSuperFxShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
            return shader;

        shader = Shader.Find("Unlit/Color");
        if (shader != null)
            return shader;

        return Shader.Find("Sprites/Default");
    }

    private static Shader FindSuperParticleShader()
    {
        Shader shader = Shader.Find("Particles/Additive");
        if (shader != null)
            return shader;

        shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader != null)
            return shader;

        shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
            return shader;

        return FindSuperFxShader();
    }

    private static void SetLineColor(LineRenderer lineRenderer, Color color)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    private static void SetLineGradient(LineRenderer lineRenderer, Color startColor, Color endColor)
    {
        if (lineRenderer == null)
            return;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(endColor.a, 1f)
            });

        lineRenderer.colorGradient = gradient;
    }

    private static void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        Material material = renderer.material;
        ApplyMaterialColor(material, color);
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void UpdateBeamOrbitParticles(ParticleSystem particleSystem, Color color, float targetRate)
    {
        if (particleSystem == null)
            return;

        var main = particleSystem.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color);

        var emission = particleSystem.emission;
        emission.rateOverTime = Mathf.Max(0f, targetRate);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static bool Contains(HealthComponent[] items, int count, HealthComponent candidate)
    {
        for (int i = 0; i < count; i++)
        {
            if (items[i] == candidate)
                return true;
        }

        return false;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLengthSq = segment.sqrMagnitude;
        if (segmentLengthSq <= 0.0001f)
            return Vector3.Distance(point, start);

        float t = Vector3.Dot(point - start, segment) / segmentLengthSq;
        t = Mathf.Clamp01(t);
        Vector3 closestPoint = start + segment * t;
        return Vector3.Distance(point, closestPoint);
    }
}
