using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stacking kill-feed ticker. Pre-allocates a fixed pool of row GameObjects.
/// Each row slides in and fades out after <see cref="_lineDuration"/> seconds.
/// Displays entries in the form "Enemy Name: Eliminated".
/// </summary>
public class KillFeedDisplay : MonoBehaviour
{
    [SerializeField] private GameObject _rowPrefab;
    [SerializeField] private int        _maxRows         = 5;
    [SerializeField] private float      _lineDuration    = 2.5f;
    [SerializeField] private float      _fadeOutDuration = 0.4f;
    [SerializeField] private float      _slideDistance   = 24f;
    [SerializeField] private float      _slideInDuration = 0.12f;
    [SerializeField] private float      _rowSpacing      = 30f;

    private readonly Queue<GameObject>                 _pool        = new Queue<GameObject>();
    private readonly List<GameObject>                  _active      = new List<GameObject>();
    private readonly Dictionary<GameObject, Coroutine> _rowRoutines = new Dictionary<GameObject, Coroutine>();
    private readonly Dictionary<GameObject, RowWidgets> _rowWidgets = new Dictionary<GameObject, RowWidgets>();

    private sealed class RowWidgets
    {
        public RectTransform   RectTransform;
        public CanvasGroup     CanvasGroup;
        public TextMeshProUGUI Label;
    }

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
            _rowWidgets[row] = EnsureRowWidgets(row);
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

    private void OnKillConfirmed(KillConfirmedEvent e)
    {
        string enemyName = string.IsNullOrWhiteSpace(e.EnemyName) ? "Enemy" : e.EnemyName;
        ShowEntry($"{enemyName}: Eliminated");
    }

    private void ShowEntry(string text)
    {
        if (_pool.Count == 0)
        {
            if (_active.Count == 0)
                return;

            GameObject oldest = _active[0];
            _active.RemoveAt(0);
            ReturnToPool(oldest);
        }

        GameObject row = _pool.Dequeue();
        _active.Add(row);

        RowWidgets widgets = EnsureRowWidgets(row);
        if (widgets.Label != null)
            widgets.Label.text = text;

        if (widgets.CanvasGroup != null)
        {
            widgets.CanvasGroup.alpha = 0f;
            widgets.CanvasGroup.interactable = false;
            widgets.CanvasGroup.blocksRaycasts = false;
        }

        row.SetActive(true);

        if (widgets.RectTransform != null)
        {
            Vector2 pos = GetTargetPosition(_active.Count - 1);
            pos.x = _slideDistance;
            widgets.RectTransform.anchoredPosition = pos;
        }

        RefreshActiveRowPositions();
        Coroutine routine = StartCoroutine(EntryRoutine(row, widgets.RectTransform, widgets.CanvasGroup));
        _rowRoutines[row] = routine;
    }

    private IEnumerator EntryRoutine(GameObject row, RectTransform rect, CanvasGroup group)
    {
        float elapsed = 0f;
        float startX = _slideDistance;

        while (elapsed < _slideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _slideInDuration);

            if (group != null)
                group.alpha = t;

            if (rect != null)
            {
                Vector2 pos = rect.anchoredPosition;
                pos.x = Mathf.Lerp(startX, 0f, t);
                rect.anchoredPosition = pos;
            }

            yield return null;
        }

        if (group != null)
            group.alpha = 1f;

        yield return new WaitForSecondsRealtime(_lineDuration);

        elapsed = 0f;
        while (elapsed < _fadeOutDuration)
        {
            if (row == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            if (group != null)
                group.alpha = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);

            yield return null;
        }

        if (row != null)
        {
            _active.Remove(row);
            _rowRoutines.Remove(row);
            ReturnToPool(row);
            RefreshActiveRowPositions();
        }
    }

    private void ReturnToPool(GameObject row)
    {
        if (row == null)
            return;

        if (_rowRoutines.TryGetValue(row, out Coroutine routine) && routine != null)
            StopCoroutine(routine);

        _rowRoutines.Remove(row);
        row.SetActive(false);
        _pool.Enqueue(row);
    }

    private void RefreshActiveRowPositions()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            GameObject row = _active[i];
            if (row == null || !_rowWidgets.TryGetValue(row, out RowWidgets widgets) || widgets.RectTransform == null)
                continue;

            Vector2 position = widgets.RectTransform.anchoredPosition;
            position.y = GetTargetPosition(i).y;
            widgets.RectTransform.anchoredPosition = position;
        }
    }

    private Vector2 GetTargetPosition(int index)
    {
        return new Vector2(0f, -index * _rowSpacing);
    }

    private RowWidgets EnsureRowWidgets(GameObject row)
    {
        if (row == null)
            return null;

        if (_rowWidgets.TryGetValue(row, out RowWidgets existing) && existing != null)
            return existing;

        RowWidgets widgets = new RowWidgets
        {
            RectTransform = row.GetComponent<RectTransform>(),
            CanvasGroup = row.GetComponent<CanvasGroup>(),
            Label = FindOrCreateLabel(row.transform)
        };

        ConfigureLabel(widgets.Label);
        EnsureGraphicsAreSafe(row);

        _rowWidgets[row] = widgets;
        return widgets;
    }

    private TextMeshProUGUI FindOrCreateLabel(Transform rowTransform)
    {
        Transform existing = rowTransform.Find("WordLabel");
        TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (label != null)
            return label;

        label = rowTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.gameObject.name = "WordLabel";
            return label;
        }

        GameObject labelObject = new GameObject("WordLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(rowTransform, false);
        return labelObject.GetComponent<TextMeshProUGUI>();
    }

    private void ConfigureLabel(TextMeshProUGUI label)
    {
        if (label == null)
            return;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (label.font == null)
            label.font = TMP_Settings.defaultFontAsset;

        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
    }

    private void EnsureGraphicsAreSafe(GameObject row)
    {
        Graphic[] graphics = row.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            EnsureCanvasRenderer(graphic.gameObject);
            graphic.raycastTarget = false;
        }
    }

    private static void EnsureCanvasRenderer(GameObject target)
    {
        if (target != null && target.GetComponent<CanvasRenderer>() == null)
            target.AddComponent<CanvasRenderer>();
    }
}
