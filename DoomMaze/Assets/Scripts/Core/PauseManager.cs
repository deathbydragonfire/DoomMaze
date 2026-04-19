using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Persistent singleton that handles pause/unpause by manipulating
/// <see cref="Time.timeScale"/>, cursor lock state, and <see cref="GameManager"/> state.
/// Listens to the Pause input action from <see cref="InputManager"/>.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private bool _inputBound;

    private void Start()
    {
        TryBindInput();
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null && _inputBound)
            InputManager.Instance.Controls.Player.Pause.performed -= OnPausePerformed;
    }

    /// <summary>Called by RuntimeBootstrapper after Bootstrap managers are ready.</summary>
    public void TryBindInput()
    {
        if (_inputBound || InputManager.Instance == null) return;
        InputManager.Instance.Controls.Player.Pause.performed += OnPausePerformed;
        _inputBound = true;
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    /// <summary>Toggles between paused and playing states.</summary>
    public void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    /// <summary>Explicitly sets the pause state.</summary>
    public void SetPaused(bool paused)
    {
        IsPaused = paused;

        if (paused)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            InputManager.Instance?.EnableUIControls();
            GameManager.Instance?.SetState(GameState.Paused);
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            InputManager.Instance?.EnablePlayerControls();
            GameManager.Instance?.SetState(GameState.Playing);
        }

        EventBus<PauseChangedEvent>.Raise(new PauseChangedEvent { IsPaused = paused });
    }
}
