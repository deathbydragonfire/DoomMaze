using UnityEngine;

/// <summary>
/// Gives pickups a short physics-driven launch when spawned from an enemy death,
/// then settles them back into their normal trigger-only pickup state.
/// </summary>
public class PickupDropMotion : MonoBehaviour
{
    [SerializeField] private float _spawnHeightOffset    = 0.35f;
    [SerializeField] private float _horizontalImpulseMin = 1.6f;
    [SerializeField] private float _horizontalImpulseMax = 2.8f;
    [SerializeField] private float _verticalImpulse      = 2.75f;
    [SerializeField] private float _spinImpulse          = 5f;
    [SerializeField] private float _collectDelay         = 0.2f;
    [SerializeField] private float _settleDelay          = 0.18f;
    [SerializeField] private float _settleDuration       = 0.2f;
    [SerializeField] private float _settleVelocity       = 0.35f;
    [SerializeField] private float _settleAngularSpeed   = 2f;

    private static PhysicsMaterial s_bounceMaterial;

    private Rigidbody _rigidbody;
    private Collider  _physicsCollider;

    private bool  _isConfigured;
    private bool  _isDropping;
    private float _collectibleAt;
    private float _dropStartedAt;
    private float _settleTimer;

    public bool IsDropping   => _isDropping;
    public bool CanBeCollected => !_isDropping && Time.time >= _collectibleAt;

    private void Awake()
    {
        EnsureConfigured();
    }

    private void OnDisable()
    {
        if (_isConfigured)
            ResetPhysicsState();
    }

    private void Update()
    {
        if (!_isDropping || _rigidbody == null)
            return;

        if (Time.time < _dropStartedAt + _settleDelay)
            return;

        bool isSlowEnough =
            _rigidbody.linearVelocity.sqrMagnitude <= _settleVelocity * _settleVelocity &&
            _rigidbody.angularVelocity.sqrMagnitude <= _settleAngularSpeed * _settleAngularSpeed;

        if (!isSlowEnough)
        {
            _settleTimer = 0f;
            return;
        }

        _settleTimer += Time.deltaTime;
        if (_settleTimer >= _settleDuration)
            Settle();
    }

    public void DropFrom(Vector3 worldOrigin)
    {
        EnsureConfigured();

        transform.position = worldOrigin + Vector3.up * _spawnHeightOffset;

        _dropStartedAt = Time.time;
        _collectibleAt = Time.time + _collectDelay;
        _settleTimer   = 0f;
        _isDropping    = true;

        if (_physicsCollider != null)
        {
            _physicsCollider.enabled  = true;
            _physicsCollider.material = GetBounceMaterial();
        }

        _rigidbody.isKinematic = false;
        _rigidbody.useGravity  = true;
        _rigidbody.linearVelocity        = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;

        Vector3 outward = Random.insideUnitSphere;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f)
            outward = Random.onUnitSphere;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f)
            outward = Vector3.forward;
        outward.Normalize();

        float horizontalImpulse = Random.Range(_horizontalImpulseMin, _horizontalImpulseMax);
        Vector3 launchImpulse = outward * horizontalImpulse + Vector3.up * _verticalImpulse;

        _rigidbody.AddForce(launchImpulse, ForceMode.Impulse);
        _rigidbody.AddTorque(Random.onUnitSphere * _spinImpulse, ForceMode.Impulse);
    }

    private void EnsureConfigured()
    {
        if (_isConfigured)
            return;

        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();

        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].isTrigger)
            {
                _physicsCollider = colliders[i];
                break;
            }
        }

        if (_physicsCollider == null)
            _physicsCollider = gameObject.AddComponent<BoxCollider>();

        _rigidbody.mass                  = 0.5f;
        _rigidbody.linearDamping                  = 0.5f;
        _rigidbody.angularDamping           = 2f;
        _rigidbody.interpolation         = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ResetPhysicsState();
        _collectibleAt = 0f;
        _isConfigured  = true;
    }

    private void Settle()
    {
        _isDropping = false;
        ResetPhysicsState();
    }

    private void ResetPhysicsState()
    {
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity        = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.useGravity      = false;
            _rigidbody.isKinematic     = true;
        }

        if (_physicsCollider != null)
        {
            _physicsCollider.enabled  = false;
            _physicsCollider.material = null;
        }
    }

    private static PhysicsMaterial GetBounceMaterial()
    {
        if (s_bounceMaterial != null)
            return s_bounceMaterial;

        s_bounceMaterial = new PhysicsMaterial("PickupBounce")
        {
            bounciness            = 0.45f,
            dynamicFriction       = 0.35f,
            staticFriction        = 0.35f,
            bounceCombine         = PhysicsMaterialCombine.Maximum,
            frictionCombine       = PhysicsMaterialCombine.Minimum
        };

        return s_bounceMaterial;
    }
}
