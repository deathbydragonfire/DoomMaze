using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton that owns the <see cref="DoomMazeInputActions"/> wrapper
/// instance. All systems access input through <see cref="Controls"/> — never directly.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    /// <summary>The shared input actions wrapper. All systems read input through this.</summary>
    public DoomMazeInputActions Controls { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Controls = new DoomMazeInputActions();
        FixUIPointBinding();
        Controls.Enable();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureUIInputModule();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Controls?.Disable();
        Controls?.Dispose();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureUIInputModule();
    }

    /// <summary>
    /// Corrects the Point action binding from delta to position so the
    /// <see cref="InputSystemUIInputModule"/> receives actual screen coordinates.
    /// </summary>
    private void FixUIPointBinding()
    {
        InputAction pointAction = Controls.UI.Point;
        pointAction.Disable();

        for (int i = 0; i < pointAction.bindings.Count; i++)
        {
            if (pointAction.bindings[i].path.Contains("delta"))
            {
                pointAction.ApplyBindingOverride(i, "<Mouse>/position");
                break;
            }
        }

        pointAction.Enable();
    }

    /// <summary>
    /// Finds the <see cref="InputSystemUIInputModule"/> in the active scene and wires
    /// it to the UI action map from <see cref="Controls"/>.
    /// </summary>
    public void ConfigureUIInputModule()
    {
        InputSystemUIInputModule module = FindFirstObjectByType<InputSystemUIInputModule>();
        if (module == null)
            return;

        module.actionsAsset   = Controls.asset;
        module.point          = InputActionReference.Create(Controls.UI.Point);
        module.leftClick      = InputActionReference.Create(Controls.UI.Click);
        module.scrollWheel    = InputActionReference.Create(Controls.UI.ScrollWheel);
        module.move           = InputActionReference.Create(Controls.UI.Navigate);
        module.submit         = InputActionReference.Create(Controls.UI.Submit);
        module.cancel         = InputActionReference.Create(Controls.UI.Cancel);
    }

    /// <summary>Enables the Player action map and disables the UI map.</summary>
    public void EnablePlayerControls()
    {
        Controls.Player.Enable();
        Controls.UI.Disable();
    }

    /// <summary>
    /// Enables the UI action map and disables the Player map,
    /// keeping the Pause action active so the player can unpause.
    /// </summary>
    public void EnableUIControls()
    {
        Controls.UI.Enable();
        Controls.Player.Disable();
        Controls.Player.Pause.Enable();
    }
}
