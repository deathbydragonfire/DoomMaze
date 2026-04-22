using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the screen-space weapon sprite Image with frame-by-frame punch animation.
/// Locates the Image at Start via the "WeaponSpriteImage" tag — no cross-GO Inspector
/// wiring required. Layout (size + position) is applied by <see cref="WeaponBase.OnEquip"/>
/// via <see cref="ApplyLayout"/>. Each <see cref="PunchAnimationSet"/> can optionally
/// override the position for its duration, restoring the default on completion.
/// </summary>
public class PunchSpriteSequencer : MonoBehaviour
{
    [SerializeField] private PunchAnimationSet[] _sets;
    [SerializeField] private Sprite              _idleSprite;

    protected Image         _image;
    protected RectTransform _imageRect;
    protected Animator      _animator;
    protected Coroutine     _playbackCoroutine;
    private   int           _lastSetIndex    = -1;
    private   Vector2       _defaultPosition;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        GameObject imageGO = GameObject.FindGameObjectWithTag("WeaponSpriteImage");
        if (imageGO == null)
        {
            Debug.LogError("[PunchSpriteSequencer] No GameObject tagged 'WeaponSpriteImage' found.", this);
            return;
        }

        _image     = imageGO.GetComponent<Image>();
        _imageRect = imageGO.GetComponent<RectTransform>();

        if (_image == null)
        {
            Debug.LogError("[PunchSpriteSequencer] WeaponSpriteImage GO has no UI Image component.", this);
            return;
        }

        if (_sets == null || _sets.Length == 0)
        {
            Debug.LogWarning("[PunchSpriteSequencer] No punch sets assigned.", this);
            return;
        }

        if (_idleSprite == null && _sets[0] != null &&
            _sets[0].Frames != null && _sets[0].Frames.Length > 0)
        {
            _idleSprite = _sets[0].Frames[0];
        }

        ShowIdle();
    }

    /// <summary>
    /// Applies size and bottom-center-anchored position to the weapon sprite image.
    /// Caches the position as the default that per-set overrides restore to.
    /// Called by <see cref="WeaponBase.OnEquip"/> on every weapon switch.
    /// </summary>
    public void ApplyLayout(Vector2 size, Vector2 position)
    {
        if (_imageRect == null) return;
        _imageRect.sizeDelta        = size;
        _imageRect.anchoredPosition = position;
        _defaultPosition            = position;
    }

    /// <summary>
    /// Selects the next punch set (not the same as the last) and plays it.
    /// Interrupts any in-progress playback.
    /// </summary>
    public virtual void PlayNextPunch()
    {
        if (_sets == null || _sets.Length == 0 || _image == null) return;

        int chosen;

        if (_sets.Length == 1)
        {
            chosen = 0;
        }
        else
        {
            do { chosen = Random.Range(0, _sets.Length); }
            while (chosen == _lastSetIndex);
        }

        _lastSetIndex = chosen;

        if (_playbackCoroutine != null)
            StopCoroutine(_playbackCoroutine);

        _playbackCoroutine = StartCoroutine(PlaySet(_sets[chosen]));
    }

    /// <summary>
    /// Called once when the player begins holding the fire button.
    /// Base implementation plays the next punch set — sufficient for semi-auto weapons.
    /// Override in <see cref="LoopingSpriteSequencer"/> for intro-loop-outro behaviour.
    /// </summary>
    public virtual void StartFiring() => PlayNextPunch();

    /// <summary>
    /// Called when the player releases the fire button.
    /// Base implementation is a no-op — the active set plays itself out and returns to idle.
    /// Override in <see cref="LoopingSpriteSequencer"/> to trigger the outro.
    /// </summary>
    public virtual void StopFiring() { }

    protected void ShowIdle()
    {
        if (_image == null) return;
        if (_imageRect != null) _imageRect.anchoredPosition = _defaultPosition;
        _image.sprite  = _idleSprite;
        _image.enabled = _idleSprite != null;
    }

    protected IEnumerator PlaySet(PunchAnimationSet set)
    {
        if (set == null || set.Frames == null || set.Frames.Length == 0) yield break;

        if (_animator != null) _animator.enabled = false;

        if (_imageRect != null && set.OverridePosition)
            _imageRect.anchoredPosition = set.PositionOffset;

        if (set.UseCameraPunch)
            EventBus<CameraPunchEvent>.Raise(new CameraPunchEvent
            {
                EulerAngles = set.CameraPunchEuler,
                Duration    = set.CameraPunchDuration
            });

        float fps   = set.FramesPerSecond > 0f ? set.FramesPerSecond : 12f;
        var   delay = new WaitForSeconds(1f / fps);

        _image.enabled = true;

        for (int i = 0; i < set.Frames.Length; i++)
        {
            _image.sprite = set.Frames[i];
            yield return delay;
        }

        if (_animator != null) _animator.enabled = true;

        ShowIdle();
        _playbackCoroutine = null;
    }
}
