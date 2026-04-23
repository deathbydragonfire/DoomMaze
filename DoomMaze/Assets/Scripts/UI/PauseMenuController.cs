using UnityEngine;

/// <summary>
/// Pause overlay toggled by <see cref="PauseChangedEvent"/>. Uses a <see cref="CanvasGroup"/>
/// to show and hide without destroying the GameObject.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject  _settingsPanel;

    private void Awake()
    {
        if (_canvasGroup   == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_settingsPanel == null) Debug.LogError("[PauseMenuController] _settingsPanel is not assigned.");

        MenuButtonHoverEffect.AttachToButtons(transform);
        SetVisible(false);
    }

    private void OnEnable()
    {
        EventBus<PauseChangedEvent>.Subscribe(OnPauseChanged);
    }

    private void OnDisable()
    {
        EventBus<PauseChangedEvent>.Unsubscribe(OnPauseChanged);
    }

    private void OnPauseChanged(PauseChangedEvent e)
    {
        SetVisible(e.IsPaused);
    }

    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha          = visible ? 1f : 0f;
        _canvasGroup.interactable   = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    /// <summary>Unpauses the game.</summary>
    public void OnResume()
    {
        PauseManager.Instance?.SetPaused(false);
    }

    /// <summary>Unpauses and loads the Main Menu scene.</summary>
    public void OnMainMenu()
    {
        PauseManager.Instance?.SetPaused(false);
        SceneFlowManager.Instance?.LoadScene("MainMenu");
    }

    /// <summary>Quits the application.</summary>
    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
