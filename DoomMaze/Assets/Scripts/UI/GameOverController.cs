using UnityEngine;

/// <summary>
/// Listens to <see cref="GameStateChangedEvent"/> and activates the Death or Victory panel
/// accordingly. Unlocks the cursor when either panel is shown.
/// </summary>
public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameObject _deathPanel;
    [SerializeField] private GameObject _victoryPanel;

    private void Awake()
    {
        if (_deathPanel   == null) Debug.LogError("[GameOverController] _deathPanel is not assigned.");
        if (_victoryPanel == null) Debug.LogError("[GameOverController] _victoryPanel is not assigned.");

        MenuButtonHoverEffect.AttachToButtons(transform);
        SetPanelsActive(false, false);
    }

    private void OnEnable()
    {
        EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
    }

    private void OnDisable()
    {
        EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
    }

    private void OnStateChanged(GameStateChangedEvent e)
    {
        bool isDead    = e.NewState == GameState.Dead;
        bool isVictory = e.NewState == GameState.Victory;

        SetPanelsActive(isDead, isVictory);

        if (isDead || isVictory)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }

    private void SetPanelsActive(bool death, bool victory)
    {
        if (_deathPanel   != null) _deathPanel.SetActive(death);
        if (_victoryPanel != null) _victoryPanel.SetActive(victory);
    }

    /// <summary>Reloads the current scene to restart the game.</summary>
    public void OnRestart()
    {
        SceneFlowManager.Instance?.ReloadCurrentScene();
    }

    /// <summary>Loads the Main Menu scene.</summary>
    public void OnMainMenu()
    {
        SceneFlowManager.Instance?.LoadScene("MainMenu");
    }
}
