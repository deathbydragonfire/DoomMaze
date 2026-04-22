using System.Collections;
using UnityEngine;

/// <summary>
/// Owns all procedural camera offsets for the MainCamera.
/// Two independent effects applied each <see cref="LateUpdate"/>:
/// - <see cref="CameraShakeEvent"/> — Perlin-noise positional shake with falloff
/// - <see cref="CameraPunchEvent"/> — directed euler rotation impulse that springs back
/// Both can run simultaneously without conflicting.
/// </summary>
public class CameraShaker : MonoBehaviour
{
    private Vector3 _shakeOffset;
    private Vector3 _punchEuler;

    private Coroutine _shakeCoroutine;
    private Coroutine _punchCoroutine;

    private void OnEnable()
    {
        EventBus<CameraShakeEvent>.Subscribe(OnCameraShake);
        EventBus<CameraPunchEvent>.Subscribe(OnCameraPunch);
    }

    private void OnDisable()
    {
        EventBus<CameraShakeEvent>.Unsubscribe(OnCameraShake);
        EventBus<CameraPunchEvent>.Unsubscribe(OnCameraPunch);
    }

    private void LateUpdate()
    {
        transform.localPosition = _shakeOffset;
        transform.localRotation = Quaternion.Euler(_punchEuler);
    }

    // ── Shake ─────────────────────────────────────────────────────────────────

    private void OnCameraShake(CameraShakeEvent e)
    {
        if (_shakeCoroutine != null)
            StopCoroutine(_shakeCoroutine);

        _shakeCoroutine = StartCoroutine(Shake(e.Magnitude, e.Duration));
    }

    private IEnumerator Shake(float magnitude, float duration)
    {
        float elapsed = 0f;
        float seedX   = Random.value * 100f;
        float seedY   = Random.value * 100f;

        while (elapsed < duration)
        {
            float falloff = 1f - elapsed / duration;
            float noiseX  = (Mathf.PerlinNoise(seedX + elapsed * 20f, 0f) - 0.5f) * 2f;
            float noiseY  = (Mathf.PerlinNoise(0f, seedY + elapsed * 20f) - 0.5f) * 2f;

            _shakeOffset = new Vector3(noiseX, noiseY, 0f) * (magnitude * falloff);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _shakeOffset    = Vector3.zero;
        _shakeCoroutine = null;
    }

    // ── Punch ─────────────────────────────────────────────────────────────────

    private void OnCameraPunch(CameraPunchEvent e)
    {
        if (_punchCoroutine != null)
            StopCoroutine(_punchCoroutine);

        _punchCoroutine = StartCoroutine(Punch(e.EulerAngles, e.Duration));
    }

    private IEnumerator Punch(Vector3 targetEuler, float duration)
    {
        float inDuration  = duration * 0.25f;
        float outDuration = duration * 0.75f;

        float elapsed = 0f;
        while (elapsed < inDuration)
        {
            _punchEuler = Vector3.Lerp(Vector3.zero, targetEuler, elapsed / inDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < outDuration)
        {
            float t = elapsed / outDuration;
            _punchEuler = Vector3.Lerp(targetEuler, Vector3.zero, t * t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _punchEuler     = Vector3.zero;
        _punchCoroutine = null;
    }
}
