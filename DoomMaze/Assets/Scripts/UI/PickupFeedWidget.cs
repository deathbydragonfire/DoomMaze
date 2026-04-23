using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Stacked pickup notification feed. Each entry fades out and is destroyed
/// after its display duration. Listens to <see cref="PickupFeedMessageEvent"/> on the EventBus.
/// </summary>
public class PickupFeedWidget : MonoBehaviour
{
    [SerializeField] private GameObject _entryPrefab;
    [SerializeField] private float      _displayDuration = 2.5f;
    [SerializeField] private float      _fadeDuration    = 0.5f;
    [SerializeField] private int        _maxEntries      = 4;
    [SerializeField] private float      _entrySpacing    = 34f;
    [SerializeField] private float      _spawnDuration   = 0.18f;
    [SerializeField] private float      _settleDuration  = 0.12f;
    [SerializeField] private float      _stackMoveDuration = 0.18f;
    [SerializeField] private float      _slideDistance   = 42f;
    [SerializeField] private float      _spawnRise       = 10f;
    [SerializeField] private float      _fadeRise        = 18f;
    [SerializeField] private Color      _defaultTint     = new Color(1f, 0.93f, 0.72f, 1f);

    private readonly List<EntryInstance> _activeEntries = new List<EntryInstance>();

    private sealed class EntryInstance
    {
        public GameObject      GameObject;
        public RectTransform   RectTransform;
        public CanvasGroup     CanvasGroup;
        public TextMeshProUGUI Label;
        public Coroutine       LifetimeRoutine;
        public Coroutine       MoveRoutine;
        public Vector2         TargetPosition;
    }

    private void Awake()
    {
        if (_entryPrefab == null)
            Debug.LogError("[PickupFeedWidget] _entryPrefab is not assigned.");
    }

    private void OnEnable()
    {
        EventBus<PickupFeedMessageEvent>.Subscribe(OnPickupFeedMessage);
    }

    private void OnDisable()
    {
        EventBus<PickupFeedMessageEvent>.Unsubscribe(OnPickupFeedMessage);
    }

    private void OnPickupFeedMessage(PickupFeedMessageEvent e)
    {
        ShowMessage(e.Message, e.Tint.a > 0f ? e.Tint : _defaultTint);
    }

    /// <summary>Displays a pickup notification message in the feed.</summary>
    public void ShowMessage(string text)
    {
        ShowMessage(text, _defaultTint);
    }

    public void ShowMessage(string text, Color tint)
    {
        if (_entryPrefab == null) return;

        if (_activeEntries.Count >= _maxEntries)
        {
            RemoveEntryImmediate(_activeEntries[_activeEntries.Count - 1]);
        }

        GameObject entryObject = Instantiate(_entryPrefab, transform);
        RectTransform rect = entryObject.GetComponent<RectTransform>();
        CanvasGroup group = entryObject.GetComponent<CanvasGroup>();
        TextMeshProUGUI label = entryObject.GetComponentInChildren<TextMeshProUGUI>();

        if (label != null)
        {
            label.text = text;
            label.color = tint;
        }

        if (group != null)
            group.alpha = 0f;

        if (rect != null)
        {
            rect.localScale = Vector3.one * 0.8f;
            rect.anchoredPosition = new Vector2(-_slideDistance, -_spawnRise);
        }

        EntryInstance entry = new EntryInstance
        {
            GameObject = entryObject,
            RectTransform = rect,
            CanvasGroup = group,
            Label = label,
            TargetPosition = Vector2.zero
        };

        _activeEntries.Insert(0, entry);
        RefreshTargets(true, entry);
        entry.LifetimeRoutine = StartCoroutine(EntryLifetimeRoutine(entry));
    }

    private void RefreshTargets(bool animate, EntryInstance entryToSkip = null)
    {
        for (int i = 0; i < _activeEntries.Count; i++)
        {
            EntryInstance entry = _activeEntries[i];
            if (entry == null || entry.GameObject == null)
                continue;

            entry.TargetPosition = new Vector2(0f, i * _entrySpacing);

            if (entry.MoveRoutine != null)
                StopCoroutine(entry.MoveRoutine);

            if (entry.RectTransform == null)
                continue;

            if (ReferenceEquals(entry, entryToSkip))
                continue;

            if (animate)
                entry.MoveRoutine = StartCoroutine(AnimateToTarget(entry, entry.TargetPosition, _stackMoveDuration));
            else
                entry.RectTransform.anchoredPosition = entry.TargetPosition;
        }
    }

    private IEnumerator AnimateToTarget(EntryInstance entry, Vector2 targetPosition, float duration)
    {
        if (entry == null || entry.RectTransform == null)
            yield break;

        Vector2 startPosition = entry.RectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (entry.GameObject == null || entry.RectTransform == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
            entry.RectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, t);
            yield return null;
        }

        if (entry.RectTransform != null)
            entry.RectTransform.anchoredPosition = targetPosition;

        entry.MoveRoutine = null;
    }

    private IEnumerator EntryLifetimeRoutine(EntryInstance entry)
    {
        if (entry == null)
            yield break;

        yield return AnimateSpawn(entry);
        yield return new WaitForSecondsRealtime(_displayDuration);
        yield return AnimateFadeOut(entry);

        if (entry.GameObject != null)
        {
            _activeEntries.Remove(entry);
            Destroy(entry.GameObject);
            RefreshTargets(true);
        }

        entry.LifetimeRoutine = null;
    }

    private IEnumerator AnimateSpawn(EntryInstance entry)
    {
        if (entry.GameObject == null)
            yield break;

        Vector2 targetPosition = entry.TargetPosition;
        Vector2 startPosition = new Vector2(-_slideDistance, targetPosition.y - _spawnRise);
        float elapsed = 0f;

        if (entry.RectTransform != null)
        {
            entry.RectTransform.anchoredPosition = startPosition;
            entry.RectTransform.localScale = Vector3.one * 0.8f;
        }

        while (elapsed < _spawnDuration)
        {
            if (entry.GameObject == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / _spawnDuration));

            if (entry.CanvasGroup != null)
                entry.CanvasGroup.alpha = Mathf.Clamp01(elapsed / _spawnDuration);

            if (entry.RectTransform != null)
            {
                entry.RectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, t);
                float scale = Mathf.LerpUnclamped(0.8f, 1.08f, t);
                entry.RectTransform.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        elapsed = 0f;
        Vector3 settleStartScale = entry.RectTransform != null ? entry.RectTransform.localScale : Vector3.one * 1.08f;
        while (elapsed < _settleDuration)
        {
            if (entry.GameObject == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / _settleDuration));

            if (entry.CanvasGroup != null)
                entry.CanvasGroup.alpha = 1f;

            if (entry.RectTransform != null)
            {
                entry.RectTransform.anchoredPosition = targetPosition;
                entry.RectTransform.localScale = Vector3.LerpUnclamped(settleStartScale, Vector3.one, t);
            }

            yield return null;
        }

        if (entry.CanvasGroup != null)
            entry.CanvasGroup.alpha = 1f;

        if (entry.RectTransform != null)
        {
            entry.RectTransform.anchoredPosition = targetPosition;
            entry.RectTransform.localScale = Vector3.one;
        }
    }

    private IEnumerator AnimateFadeOut(EntryInstance entry)
    {
        if (entry == null || entry.GameObject == null)
            yield break;

        if (entry.MoveRoutine != null)
        {
            StopCoroutine(entry.MoveRoutine);
            entry.MoveRoutine = null;
        }

        Vector2 startPosition = entry.RectTransform != null ? entry.RectTransform.anchoredPosition : Vector2.zero;
        Vector2 endPosition = startPosition + new Vector2(0f, _fadeRise);
        Vector3 startScale = entry.RectTransform != null ? entry.RectTransform.localScale : Vector3.one;
        Vector3 endScale = startScale * 0.92f;
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            if (entry.GameObject == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / _fadeDuration));

            if (entry.CanvasGroup != null)
                entry.CanvasGroup.alpha = 1f - t;

            if (entry.RectTransform != null)
            {
                entry.RectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, t);
                entry.RectTransform.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            }

            yield return null;
        }
    }

    private void RemoveEntryImmediate(EntryInstance entry)
    {
        if (entry == null)
            return;

        if (entry.MoveRoutine != null)
            StopCoroutine(entry.MoveRoutine);

        if (entry.LifetimeRoutine != null)
            StopCoroutine(entry.LifetimeRoutine);

        _activeEntries.Remove(entry);

        if (entry.GameObject != null)
            Destroy(entry.GameObject);

        RefreshTargets(true);
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - t;
        return 1f - (inv * inv * inv);
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float inv = t - 1f;
        return 1f + ((overshoot + 1f) * inv * inv * inv) + (overshoot * inv * inv);
    }
}
