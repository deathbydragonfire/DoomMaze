using System.Collections;
using UnityEngine;

/// <summary>
/// Concrete <see cref="WeaponBase"/> for raycast (hitscan) weapons such as the Pistol and Shotgun.
/// Fires one or more rays from camera centre, applies <see cref="DamageInfo"/> to any
/// <see cref="IDamageable"/> hit. Supports pellet spread for shotgun-style weapons.
/// Spawns a small travelling tracer object along the bullet path for visual feedback.
/// Assign <see cref="_bulletSprite"/> to swap from the cube placeholder to a sprite later.
/// </summary>
public class HitscanWeapon : WeaponBase
{
    private const int MaxHitCount = 16;

    [SerializeField] private LayerMask _hitMask;

    [Header("Tracer")]
    [SerializeField] private Sprite _bulletSprite;
    [SerializeField] private float  _tracerSpeed    = 80f;
    [SerializeField] private float  _tracerScale    = 0.06f;
    [SerializeField] private Color  _tracerColor    = new Color(1f, 0.95f, 0.55f, 1f);

    [Header("Muzzle Flash")]
    [SerializeField] private MuzzleFlash _muzzleFlash;

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[MaxHitCount];

    /// <inheritdoc/>
    protected override void ExecuteFire()
    {
        _muzzleFlash?.Flash();

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 origin  = cam.transform.position;
        Vector3 forward = cam.transform.forward;
        float   spread  = Mathf.Tan(_data.SpreadAngle * Mathf.Deg2Rad);

        for (int i = 0; i < _data.PelletsPerShot; i++)
        {
            Vector3 direction = forward;

            if (_data.SpreadAngle > 0f)
            {
                Vector2 s = Random.insideUnitCircle * spread;
                direction = (forward + cam.transform.right * s.x + cam.transform.up * s.y).normalized;
            }

            Vector3 endPoint;
            int hits = Physics.RaycastNonAlloc(origin, direction, _hitBuffer, _data.Range, _hitMask, QueryTriggerInteraction.Ignore);

            if (TryGetClosestHit(hits, out RaycastHit hit))
            {
                endPoint = hit.point;

                ApplyDirectDamage(hit.collider);

                ImpactFXManager.Instance?.Spawn(hit.point, hit.normal);
            }
            else
            {
                endPoint = origin + direction * _data.Range;
            }

            StartCoroutine(SpawnTracer(origin, endPoint));
        }

        if (_data != null)
            EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent
            {
                Magnitude = _data.ShakeMagnitude,
                Duration  = _data.ShakeDuration
            });
    }

    private IEnumerator SpawnTracer(Vector3 start, Vector3 end)
    {
        GameObject tracer;

        if (_bulletSprite != null)
        {
            tracer = new GameObject("BulletTracer");
            SpriteRenderer sr = tracer.AddComponent<SpriteRenderer>();
            sr.sprite        = _bulletSprite;
            sr.color         = _tracerColor;
            sr.material      = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            tracer.transform.localScale = Vector3.one * _tracerScale;
        }
        else
        {
            tracer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tracer.name = "BulletTracer";

            Destroy(tracer.GetComponent<Collider>());

            Renderer r = tracer.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", _tracerColor);
            r.material = mat;

            tracer.transform.localScale = new Vector3(_tracerScale * 0.4f, _tracerScale * 0.4f, _tracerScale);
        }

        tracer.transform.position = start;

        Vector3 direction     = (end - start).normalized;
        float   totalDistance = Vector3.Distance(start, end);
        float   travelled     = 0f;

        tracer.transform.rotation = Quaternion.LookRotation(direction);

        while (travelled < totalDistance)
        {
            float step = _tracerSpeed * Time.deltaTime;
            tracer.transform.position += direction * step;
            travelled += step;
            yield return null;
        }

        Destroy(tracer);
    }

    private bool TryGetClosestHit(int hitCount, out RaycastHit closestHit)
    {
        closestHit = default(RaycastHit);

        float closestDistance = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _hitBuffer[i];
            if (hit.collider == null)
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

    private void ApplyDirectDamage(Collider hitCollider)
    {
        if (hitCollider == null)
            return;

        HealthComponent health = hitCollider.GetComponentInParent<HealthComponent>();
        if (health != null && health.IsAlive)
        {
            health.TakeDamage(new DamageInfo
            {
                Amount = _data.Damage,
                Type = DamageType.Physical,
                Source = gameObject
            });
            return;
        }

        hitCollider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo
        {
            Amount = _data.Damage,
            Type = DamageType.Physical,
            Source = gameObject
        });
    }
}
