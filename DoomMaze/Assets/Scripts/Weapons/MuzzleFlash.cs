using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns a billboard sprite flash at the weapon's muzzle point on each shot,
/// and optionally pops a point light for one frame. Call <see cref="Flash"/>
/// from <see cref="HitscanWeapon.ExecuteFire"/>.
/// Wire <see cref="_muzzlePoint"/> to a child Transform at the barrel tip.
/// </summary>
public class MuzzleFlash : MonoBehaviour
{
    [Header("Flash Sprites")]
    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private Sprite[]  _flashFrames;
    [SerializeField] private float     _flashFrameRate = 24f;

    [Header("Optional Point Light")]
    [SerializeField] private Light _muzzleLight;
    [SerializeField] private float _lightIntensity = 8f;
    [SerializeField] private float _lightRange     = 2f;

    private Coroutine _lightRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_muzzlePoint == null)
            Debug.LogWarning("[MuzzleFlash] _muzzlePoint is not assigned.");

        if (_muzzleLight != null)
        {
            _muzzleLight.intensity = 0f;
            _muzzleLight.range     = _lightRange;
            _muzzleLight.enabled   = false;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Triggers the muzzle flash sprite and optional light pop.</summary>
    public void Flash()
    {
        if (_muzzlePoint == null) return;

        if (_flashFrames != null && _flashFrames.Length > 0)
            ImpactFXManager.Instance?.Spawn(_muzzlePoint.position, _muzzlePoint.forward, _flashFrames, _flashFrameRate);

        if (_muzzleLight != null)
        {
            if (_lightRoutine != null)
                StopCoroutine(_lightRoutine);
            _lightRoutine = StartCoroutine(LightPopRoutine());
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private IEnumerator LightPopRoutine()
    {
        _muzzleLight.enabled   = true;
        _muzzleLight.intensity = _lightIntensity;
        yield return null;
        _muzzleLight.intensity = 0f;
        _muzzleLight.enabled   = false;
        _lightRoutine = null;
    }
}
