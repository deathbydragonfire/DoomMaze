using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Stacking kill-feed ticker. Pre-allocates a fixed pool of row GameObjects.
/// Each row slides in and fades out after <see cref="_lineDuration"/> seconds.
/// Subscribes to <see cref="KillConfirmedEvent"/> via the EventBus.
/// Rows must have a <see cref="CanvasGroup"/> and a <see cref="TextMeshProUGUI"/> as a child.
/// </summary>
public class KillFeedDisplay : MonoBehaviour
{
    [SerializeField] private GameObject _rowPrefab;
    [SerializeField] private int        _maxRows        = 5;
    [SerializeField] private float      _lineDuration   = 2.5f;
    [SerializeField] private float      _fadeOutDuration = 0.4f;
    [SerializeField] private float      _slideDistance  = 24f;
    [SerializeField] private float      _slideInDuration = 0.12f;

    private readonly Queue<GameObject> _pool   = new Queue<GameObject>();
    private readonly List<GameObject>  _active = new List<GameObject>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_rowPrefab == null)
        {
            Debug.LogError("[KillFeedDisplay] _rowPrefab is not assigned.");
            return;
        }

        for (int i = 0; i < _maxRows; i++)
        {
            GameObject row = Instantiate(_rowPrefab, transform);
            row.SetActive(false);
            _pool.Enqueue(row);
        }
    }

    private void OnEnable()
    {
        EventBus<KillConfirmedEvent>.Subscribe(OnKillConfirmed);
    }

    private void OnDisable()
    {
        EventBus<KillConfirmedEvent>.Unsubscribe(OnKillConfirmed);
    }

    // ── EventBus Handler ──────────────────────────────────────────────────────

    private void OnKillConfirmed(KillConfirmedEvent e)
    {
        string text = e.IsStreakKill ? "★ ELIMINATED" : "ELIMINATED";
        ShowEntry(text);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void ShowEntry(string text)
    {
        if (_pool.Count == 0)
        {
            if (_active.Count == 0) return;
            GameObject oldest = _active[0];
            _active.RemoveAt(0);
            ReturnToPool(oldest);
        }

        GameObject row = _pool.Dequeue();
        _active.Add(row);

        TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = text;

        CanvasGroup group = row.GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 0f;

        row.SetActive(true);

        RectTransform rect = row.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 pos = rect.anchoredPosition;
            pos.x = _slideDistance;
            rect.anchoredPosition = pos;
        }

        StartCoroutine(EntryRoutine(row, rect, group));
    }

    private IEnumerator EntryRoutine(GameObject row, RectTransform rect, CanvasGroup group)
    {
        float elapsed = 0f;
        float startX  = _slideDistance;

        while (elapsed < _slideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _slideInDuration);
            if (group != null) group.alpha = t;
            if (rect  != null)
            {
                Vector2 pos = rect.anchoredPosition;
                pos.x = Mathf.Lerp(startX, 0f, t);
                rect.anchoredPosition = pos;
            }
            yield return null;
        }

        if (group != null) group.alpha = 1f;

        yield return new WaitForSecondsRealtime(_lineDuration);

        elapsed = 0f;
        while (elapsed < _fadeOutDuration)
        {
            if (row == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            if (group != null) group.alpha = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);
            yield return null;
        }

        if (row != null)
        {
            _active.Remove(row);
            ReturnToPool(row);
        }
    }

    private void ReturnToPool(GameObject row)
    {
        if (row == null) return;
        row.SetActive(false);
        _pool.Enqueue(row);
    }
}
