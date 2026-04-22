using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Animates a streak label ("DOUBLE KILL", "TRIPLE KILL", etc.) with a punch-scale
/// tween and fade-out. Subscribe to <see cref="KillStreakEvent"/> on the EventBus,
/// or call <see cref="ShowStreak"/> directly from <see cref="HypeFeedbackOrchestrator"/>.
/// Requires a <see cref="CanvasGroup"/> and a <see cref="TextMeshProUGUI"/> on this GameObject.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class KillStreakDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private float           _punchScale     = 1.4f;
    [SerializeField] private float           _scaleDuration  = 0.12f;
    [SerializeField] private float           _displayDuration = 1.8f;
    [SerializeField] private float           _fadeDuration   = 0.3f;

    private CanvasGroup _canvasGroup;
    private Coroutine   _animRoutine;

    private static readonly string[] STREAK_LABELS =
    {
        "",
        "",
        "DOUBLE KILL",
        "TRIPLE KILL",
        "QUAD KILL"
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        if (_label == null)
            _label = GetComponentInChildren<TextMeshProUGUI>();

        if (_label == null)
            Debug.LogError("[KillStreakDisplay] TextMeshProUGUI is not assigned.");
    }

    private void OnEnable()
    {
        EventBus<KillStreakEvent>.Subscribe(OnKillStreak);
    }

    private void OnDisable()
    {
        EventBus<KillStreakEvent>.Unsubscribe(OnKillStreak);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Displays the streak label for the given kill count.</summary>
    public void ShowStreak(int count)
    {
        if (count < 2) return;
        if (_label == null) return;

        _label.text = count < STREAK_LABELS.Length
            ? STREAK_LABELS[count]
            : $"KILLSTREAK x{count}";

        if (_animRoutine != null)
            StopCoroutine(_animRoutine);

        _animRoutine = StartCoroutine(AnimateRoutine());
    }

    // ── EventBus Handler ──────────────────────────────────────────────────────

    private void OnKillStreak(KillStreakEvent e)
    {
        if (e.StreakCount >= 2)
            ShowStreak(e.StreakCount);
        else
        {
            if (_animRoutine != null)
                StopCoroutine(_animRoutine);
            _canvasGroup.alpha = 0f;
        }
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    private IEnumerator AnimateRoutine()
    {
        _canvasGroup.alpha = 1f;
        transform.localScale = Vector3.one * _punchScale;

        float elapsed = 0f;
        while (elapsed < _scaleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _scaleDuration);
            transform.localScale = Vector3.one * Mathf.Lerp(_punchScale, 1f, t);
            yield return null;
        }
        transform.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(_displayDuration);

        elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        _animRoutine = null;
    }
}
