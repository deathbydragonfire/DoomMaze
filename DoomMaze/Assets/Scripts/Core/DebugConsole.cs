#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Persistent singleton that owns the in-game debug console overlay.
/// Toggled with the backtick / tilde key. Pauses the game while open.
/// Commands are registered via <see cref="RegisterCommand"/> and executed on submit.
/// </summary>
public class DebugConsole : MonoBehaviour
{
    public static DebugConsole Instance { get; private set; }

    [SerializeField] private GameObject      _consoleRoot;
    [SerializeField] private TMP_InputField  _inputField;
    [SerializeField] private TextMeshProUGUI _outputText;
    [SerializeField] private ScrollRect      _scrollRect;
    [SerializeField] private WeaponDatabase  _weaponDatabase;
    [SerializeField] private EnemyData[]     _spawnableEnemies;
    [SerializeField] private AmmoTypeData[]  _ammoTypes;

    private readonly Dictionary<string, IDebugCommand> _commands = new Dictionary<string, IDebugCommand>(16);
    private bool _isVisible;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!ValidateSerializedFields())
        {
            enabled = false;
            return;
        }

        _consoleRoot.SetActive(false);
    }

    private void Start()
    {
        RegisterCommand(new GodCommand());
        RegisterCommand(new GiveWeaponsCommand(_weaponDatabase, _ammoTypes));
        RegisterCommand(new SpawnEnemyCommand(_spawnableEnemies));
        RegisterCommand(new NoclipCommand());

        _inputField.onSubmit.AddListener(OnInputSubmit);
    }

    private void OnDestroy()
    {
        _inputField?.onSubmit.RemoveListener(OnInputSubmit);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (!Keyboard.current.backquoteKey.wasPressedThisFrame) return;

        if (_isVisible)
            CloseConsole();
        else
            OpenConsole();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Adds a command to the registry. No-op if the ID is already registered.</summary>
    public void RegisterCommand(IDebugCommand command)
    {
        if (_commands.ContainsKey(command.Id))
        {
            Debug.LogWarning($"[DebugConsole] Command '{command.Id}' is already registered. Skipping duplicate.");
            return;
        }
        _commands[command.Id] = command;
    }

    /// <summary>Prints a line of output text to the console scroll view.</summary>
    public void Print(string message)
    {
        _outputText.text += $"\n{message}";
        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>Clears all output lines from the scroll view.</summary>
    public void Clear()
    {
        _outputText.text = string.Empty;
    }

    /// <summary>Opens or closes the console overlay.</summary>
    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        _consoleRoot.SetActive(visible);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OpenConsole()
    {
        SetVisible(true);
        PauseManager.Instance?.SetPaused(true);
        InputManager.Instance?.EnableUIControls();
        _inputField.ActivateInputField();
    }

    private void CloseConsole()
    {
        SetVisible(false);
        PauseManager.Instance?.SetPaused(false);
        InputManager.Instance?.EnablePlayerControls();
    }

    private void OnInputSubmit(string rawInput)
    {
        string trimmed = rawInput.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            _inputField.text = string.Empty;
            _inputField.ActivateInputField();
            return;
        }

        string[] tokens = trimmed.Split(' ');
        string commandId = tokens[0].ToLowerInvariant();

        if (_commands.TryGetValue(commandId, out IDebugCommand command))
        {
            string[] args = new string[tokens.Length - 1];
            for (int i = 1; i < tokens.Length; i++)
                args[i - 1] = tokens[i];

            command.Execute(args, this);
        }
        else
        {
            Print($"Unknown command: {tokens[0]}");
        }

        _inputField.text = string.Empty;
        _inputField.ActivateInputField();
    }

    private bool ValidateSerializedFields()
    {
        bool valid = true;

        if (_consoleRoot == null)
        {
            Debug.LogError("[DebugConsole] _consoleRoot is not assigned.");
            valid = false;
        }
        if (_inputField == null)
        {
            Debug.LogError("[DebugConsole] _inputField is not assigned.");
            valid = false;
        }
        if (_outputText == null)
        {
            Debug.LogError("[DebugConsole] _outputText is not assigned.");
            valid = false;
        }
        if (_scrollRect == null)
        {
            Debug.LogError("[DebugConsole] _scrollRect is not assigned.");
            valid = false;
        }

        return valid;
    }
}
#endif
