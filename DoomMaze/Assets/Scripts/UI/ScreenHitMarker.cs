using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a directional hit-marker (4 line images) that pops to a larger scale
/// and contracts back. White on hit, red on kill. Subscribes to
/// <see cref="EnemyDamagedEvent"/> and <see cref="EnemyDiedEvent"/> via the EventBus.
/// The four marker images (top, bottom, left, right) must be assigned in the Inspector.
/// </summary>
public class ScreenHitMarker : MonoBehaviour
{
    [Header("Marker Images (Top, Bottom, Left, Right)")]
    [SerializeField] private Image[] _markerImages = new Image[4];

    [Header("Hit Settings")]
    [SerializeField] private float _hitPunchScale    = 1.3f;
    [SerializeField] private float _hitContractTime  = 0.08f;
    [SerializeField] private Color _hitColor         = Color.white;

    [Header("Kill Settings")]
    [SerializeField] private float _killPunchScale   = 1.6f;
    [SerializeField] private float _killContractTime = 0.10f;
    [SerializeField] private float _killHoldTime     = 0.05f;
    [SerializeField] private Color _killColor        = Color.red;

    [Header("Fade")]
    [SerializeField] private float _fadeDuration = 0.15f;

    private Coroutine _animRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        EventBus<EnemyDamagedEvent>.Subscribe(OnEnemyDamaged);
        EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
    }

    private void OnDisable()
    {
        EventBus<EnemyDamagedEvent>.Unsubscribe(OnEnemyDamaged);
        EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
    }

    // ── EventBus Handlers ─────────────────────────────────────────────────────

    private void OnEnemyDamaged(EnemyDamagedEvent e)
    {
        PlayMarker(_hitColor, _hitPunchScale, _hitContractTime, 0f);
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        PlayMarker(_killColor, _killPunchScale, _killContractTime, _killHoldTime);
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    private void PlayMarker(Color color, float punchScale, float contractTime, float holdTime)
    {
        if (_animRoutine != null)
            StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(MarkerRoutine(color, punchScale, contractTime, holdTime));
    }

    private IEnumerator MarkerRoutine(Color color, float punchScale, float contractTime, float holdTime)
    {
        SetColor(color);
        SetAlpha(1f);
        SetScale(punchScale);

        float elapsed = 0f;
        while (elapsed < contractTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / contractTime);
            SetScale(Mathf.Lerp(punchScale, 1f, t));
            yield return null;
        }
        SetScale(1f);

        if (holdTime > 0f)
            yield return new WaitForSecondsRealtime(holdTime);

        elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / _fadeDuration));
            yield return null;
        }

        SetAlpha(0f);
        _animRoutine = null;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SetAlpha(float alpha)
    {
        foreach (Image img in _markerImages)
        {
            if (img == null) continue;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    private void SetColor(Color color)
    {
        foreach (Image img in _markerImages)
        {
            if (img == null) continue;
            color.a = img.color.a;
            img.color = color;
        }
    }

    private void SetScale(float scale)
    {
        transform.localScale = Vector3.one * scale;
    }
}
