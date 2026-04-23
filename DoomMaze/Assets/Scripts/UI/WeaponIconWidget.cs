using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the active weapon's name and its first viewmodel sprite as a HUD icon.
/// </summary>
public class WeaponIconWidget : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _weaponNameLabel;
    [SerializeField] private Image           _weaponIcon;
    [Header("Title Animation")]
    [SerializeField] private float           _titleExitDuration = 0.08f;
    [SerializeField] private float           _titleEnterDuration = 0.14f;
    [SerializeField] private float           _titleSlideDistance = 14f;
    [SerializeField] private float           _titleShakeDistance = 5f;
    [SerializeField] private float           _titleShakeCycles = 2.25f;
    [SerializeField] private float           _titlePunchScale = 1.04f;

    private RectTransform _weaponNameRect;
    private Vector2       _baseLabelAnchoredPosition;
    private Vector3       _baseLabelScale = Vector3.one;
    private Color         _baseLabelColor = Color.white;
    private Coroutine     _titleAnimationRoutine;

    private void Awake()
    {
        if (_weaponNameLabel == null) Debug.LogError("[WeaponIconWidget] _weaponNameLabel is not assigned.");

        if (_weaponNameLabel != null)
        {
            _weaponNameRect = _weaponNameLabel.rectTransform;
            _baseLabelAnchoredPosition = _weaponNameRect.anchoredPosition;
            _baseLabelScale = _weaponNameRect.localScale;
            _baseLabelColor = _weaponNameLabel.color;
        }
    }

    private void OnDisable()
    {
        if (_titleAnimationRoutine != null)
        {
            StopCoroutine(_titleAnimationRoutine);
            _titleAnimationRoutine = null;
        }

        ResetTitleVisualState();
    }

    /// <summary>Updates the weapon name label and icon sprite from the given <see cref="WeaponData"/>.</summary>
    public void SetWeapon(WeaponData data)
    {
        if (data == null) return;

        UpdateWeaponIcon(data);

        if (_weaponNameLabel == null)
            return;

        if (_titleAnimationRoutine != null)
        {
            StopCoroutine(_titleAnimationRoutine);
            _titleAnimationRoutine = null;
            ResetTitleVisualState();
        }

        string nextTitle = data.DisplayName;
        bool hasExistingTitle = !string.IsNullOrEmpty(_weaponNameLabel.text);
        bool shouldAnimate = hasExistingTitle && !string.Equals(_weaponNameLabel.text, nextTitle);

        if (!shouldAnimate)
        {
            _weaponNameLabel.text = nextTitle;
            ResetTitleVisualState();
            return;
        }

        _titleAnimationRoutine = StartCoroutine(AnimateTitleChange(nextTitle));
    }

    private void UpdateWeaponIcon(WeaponData data)
    {
        if (_weaponIcon == null)
            return;

        bool hasSprite = data.ViewmodelSprites != null && data.ViewmodelSprites.Length > 0;
        _weaponIcon.sprite = hasSprite ? data.ViewmodelSprites[0] : null;
        _weaponIcon.enabled = hasSprite;
    }

    private IEnumerator AnimateTitleChange(string nextTitle)
    {
        yield return AnimateTitlePhase(
            _weaponNameLabel.text,
            _baseLabelAnchoredPosition,
            _baseLabelAnchoredPosition + Vector2.left * _titleSlideDistance,
            _baseLabelScale,
            _baseLabelScale * _titlePunchScale,
            1f,
            0f,
            Mathf.Max(0.01f, _titleExitDuration));

        _weaponNameLabel.text = nextTitle;

        yield return AnimateTitlePhase(
            nextTitle,
            _baseLabelAnchoredPosition + Vector2.right * _titleSlideDistance,
            _baseLabelAnchoredPosition,
            _baseLabelScale * 0.98f,
            _baseLabelScale,
            0f,
            1f,
            Mathf.Max(0.01f, _titleEnterDuration));

        ResetTitleVisualState();
        _titleAnimationRoutine = null;
    }

    private IEnumerator AnimateTitlePhase(
        string title,
        Vector2 startPosition,
        Vector2 endPosition,
        Vector3 startScale,
        Vector3 endScale,
        float startAlpha,
        float endAlpha,
        float duration)
    {
        _weaponNameLabel.text = title;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float damping = 1f - t;
            float shake = Mathf.Sin(t * Mathf.PI * 2f * _titleShakeCycles) * _titleShakeDistance * damping;

            if (_weaponNameRect != null)
            {
                _weaponNameRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, t) + new Vector2(shake, 0f);
                _weaponNameRect.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            }

            Color color = _baseLabelColor;
            color.a = Mathf.Lerp(startAlpha, endAlpha, t);
            _weaponNameLabel.color = color;

            yield return null;
        }
    }

    private void ResetTitleVisualState()
    {
        if (_weaponNameRect != null)
        {
            _weaponNameRect.anchoredPosition = _baseLabelAnchoredPosition;
            _weaponNameRect.localScale = _baseLabelScale;
        }

        if (_weaponNameLabel != null)
            _weaponNameLabel.color = _baseLabelColor;
    }
}
