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

    private readonly List<GameObject> _activeEntries = new List<GameObject>();

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
        ShowMessage(e.Message);
    }

    /// <summary>Displays a pickup notification message in the feed.</summary>
    public void ShowMessage(string text)
    {
        if (_entryPrefab == null) return;

        if (_activeEntries.Count >= _maxEntries)
        {
            GameObject oldest = _activeEntries[0];
            _activeEntries.RemoveAt(0);
            Destroy(oldest);
        }

        GameObject entry = Instantiate(_entryPrefab, transform);
        _activeEntries.Add(entry);

        TextMeshProUGUI label = entry.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = text;

        StartCoroutine(EntryLifetimeRoutine(entry));
    }

    private IEnumerator EntryLifetimeRoutine(GameObject entry)
    {
        yield return new WaitForSecondsRealtime(_displayDuration);

        CanvasGroup group = entry != null ? entry.GetComponent<CanvasGroup>() : null;

        if (group != null)
        {
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                if (entry == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
        }

        if (entry != null)
        {
            _activeEntries.Remove(entry);
            Destroy(entry);
        }
    }
}
