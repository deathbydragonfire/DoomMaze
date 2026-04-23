using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls a local URP <see cref="Volume"/> component at runtime to produce brief
/// post-process pulses without touching the global default volume profile.
/// Always accesses <c>volume.profile</c> (the instance) so changes are not shared.
/// </summary>
[RequireComponent(typeof(Volume))]
public class HypeVolumeController : MonoBehaviour
{
    [SerializeField] private float _killRiseTime  = 0.06f;
    [SerializeField] private float _killFallTime  = 0.18f;
    [SerializeField] private float _hitRiseTime   = 0.04f;
    [SerializeField] private float _hitFallTime   = 0.12f;
    [SerializeField] private float _vigRiseTime   = 0.03f;
    [SerializeField] private float _vigFallTime   = 0.08f;
    [SerializeField] private float _dashRiseTime  = 0.02f;
    [SerializeField] private float _dashFallTime  = 0.16f;

    private Volume              _volume;
    private VolumeProfile       _profile;

    private ChromaticAberration _chromaticAberration;
    private LensDistortion      _lensDistortion;
    private ColorAdjustments    _colorAdjustments;
    private Vignette            _vignette;

    private Coroutine _killRoutine;
    private Coroutine _contrastRoutine;
    private Coroutine _vignetteRoutine;
    private Coroutine _dashRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _volume  = GetComponent<Volume>();
        _profile = _volume.profile;

        if (!_profile.TryGet(out _chromaticAberration))
            _chromaticAberration = _profile.Add<ChromaticAberration>(true);

        if (!_profile.TryGet(out _lensDistortion))
            _lensDistortion = _profile.Add<LensDistortion>(true);

        if (!_profile.TryGet(out _colorAdjustments))
            _colorAdjustments = _profile.Add<ColorAdjustments>(true);

        if (!_profile.TryGet(out _vignette))
            _vignette = _profile.Add<Vignette>(true);

        ResetAllEffects();
    }

    private void OnDestroy()
    {
        ResetAllEffects();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Brief chromatic aberration + lens distortion spike on kill.</summary>
    public void PulseKill(float intensity = 1f)
    {
        if (_killRoutine != null)
            StopCoroutine(_killRoutine);
        _killRoutine = StartCoroutine(PulseKillRoutine(intensity));
    }

    /// <summary>Subtle contrast + saturation dip on any hit dealt.</summary>
    public void PulseContrast(float intensity = 0.4f)
    {
        if (_contrastRoutine != null)
            StopCoroutine(_contrastRoutine);
        _contrastRoutine = StartCoroutine(PulseContrastRoutine(intensity));
    }

    /// <summary>Subtle vignette flicker on weapon fire.</summary>
    public void PulseVignette(float intensity = 0.15f)
    {
        if (_vignetteRoutine != null)
            StopCoroutine(_vignetteRoutine);
        _vignetteRoutine = StartCoroutine(PulseVignetteRoutine(intensity));
    }

    /// <summary>Dash burst with edge smear and tunnel-vision punch.</summary>
    public void PulseDash(float intensity = 1f)
    {
        if (_dashRoutine != null)
            StopCoroutine(_dashRoutine);
        _dashRoutine = StartCoroutine(PulseDashRoutine(intensity));
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator PulseKillRoutine(float intensity)
    {
        _chromaticAberration.active                = true;
        _chromaticAberration.intensity.overrideState = true;
        _lensDistortion.active                     = true;
        _lensDistortion.intensity.overrideState    = true;

        float caTarget   = Mathf.Clamp01(intensity);
        float ldTarget   = Mathf.Clamp(intensity * -0.25f, -1f, 0f);

        yield return LerpFloats(
            v => {
                _chromaticAberration.intensity.value = v * caTarget;
                _lensDistortion.intensity.value      = v * ldTarget;
            },
            0f, 1f, _killRiseTime);

        yield return LerpFloats(
            v => {
                _chromaticAberration.intensity.value = v * caTarget;
                _lensDistortion.intensity.value      = v * ldTarget;
            },
            1f, 0f, _killFallTime);

        _chromaticAberration.intensity.value = 0f;
        _lensDistortion.intensity.value      = 0f;
        _killRoutine = null;
    }

    private IEnumerator PulseContrastRoutine(float intensity)
    {
        _colorAdjustments.active                 = true;
        _colorAdjustments.contrast.overrideState = true;

        float target = intensity * 30f;

        yield return LerpFloats(v => _colorAdjustments.contrast.value = v * target, 0f, 1f, _hitRiseTime);
        yield return LerpFloats(v => _colorAdjustments.contrast.value = v * target, 1f, 0f, _hitFallTime);

        _colorAdjustments.contrast.value = 0f;
        _contrastRoutine = null;
    }

    private IEnumerator PulseVignetteRoutine(float intensity)
    {
        _vignette.active                   = true;
        _vignette.intensity.overrideState  = true;

        float target = Mathf.Clamp01(intensity);

        yield return LerpFloats(v => _vignette.intensity.value = v * target, 0f, 1f, _vigRiseTime);
        yield return LerpFloats(v => _vignette.intensity.value = v * target, 1f, 0f, _vigFallTime);

        _vignette.intensity.value = 0f;
        _vignetteRoutine = null;
    }

    private IEnumerator PulseDashRoutine(float intensity)
    {
        _chromaticAberration.active                 = true;
        _chromaticAberration.intensity.overrideState = true;
        _lensDistortion.active                      = true;
        _lensDistortion.intensity.overrideState     = true;
        _vignette.active                            = true;
        _vignette.intensity.overrideState           = true;

        float caTarget = Mathf.Clamp01(0.35f * intensity);
        float ldTarget = Mathf.Clamp(-0.45f * intensity, -1f, 0f);
        float vigTarget = Mathf.Clamp01(0.22f * intensity);

        yield return LerpFloats(
            v =>
            {
                _chromaticAberration.intensity.value = v * caTarget;
                _lensDistortion.intensity.value = v * ldTarget;
                _vignette.intensity.value = v * vigTarget;
            },
            0f, 1f, _dashRiseTime);

        yield return LerpFloats(
            v =>
            {
                _chromaticAberration.intensity.value = v * caTarget;
                _lensDistortion.intensity.value = v * ldTarget;
                _vignette.intensity.value = v * vigTarget;
            },
            1f, 0f, _dashFallTime);

        _chromaticAberration.intensity.value = 0f;
        _lensDistortion.intensity.value = 0f;
        _vignette.intensity.value = 0f;
        _dashRoutine = null;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static IEnumerator LerpFloats(System.Action<float> setter, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            setter(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        setter(to);
    }

    private void ResetAllEffects()
    {
        if (_chromaticAberration != null) _chromaticAberration.intensity.value = 0f;
        if (_lensDistortion      != null) _lensDistortion.intensity.value      = 0f;
        if (_colorAdjustments    != null) _colorAdjustments.contrast.value     = 0f;
        if (_vignette            != null) _vignette.intensity.value            = 0f;
    }
}
