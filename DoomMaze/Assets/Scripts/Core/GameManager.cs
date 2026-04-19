using UnityEngine;

/// <summary>
/// Persistent singleton that owns the authoritative game state and drives
/// transitions via EventBus. All game systems respond to state events;
/// no logic lives here beyond routing.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }

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

    private void Start()
    {
        SetState(GameState.Boot);
    }

    /// <summary>
    /// Transitions to <paramref name="newState"/> and raises
    /// <see cref="GameStateChangedEvent"/> on the EventBus.
    /// Has no effect if <paramref name="newState"/> is already active.
    /// </summary>
    public void SetState(GameState newState)
    {
        if (CurrentState == newState)
        {
            Debug.LogWarning($"[GameManager] SetState called with already-active state: {newState}");
            return;
        }

        GameState previousState = CurrentState;
        CurrentState = newState;

        EventBus<GameStateChangedEvent>.Raise(new GameStateChangedEvent
        {
            NewState = newState,
            PreviousState = previousState
        });
    }
}
