#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>
/// Debug command that toggles noclip movement by raising <see cref="NoclipChangedEvent"/>
/// on the <see cref="EventBus{T}"/>. <see cref="PlayerMovement"/> owns the response.
/// Usage: <c>noclip</c>
/// </summary>
public class NoclipCommand : IDebugCommand
{
    public string Id          => "noclip";
    public string Description => "Toggles noclip movement mode.";

    private bool _noclipActive;

    public void Execute(string[] args, DebugConsole console)
    {
        _noclipActive = !_noclipActive;
        EventBus<NoclipChangedEvent>.Raise(new NoclipChangedEvent { IsActive = _noclipActive });
        console.Print($"Noclip {(_noclipActive ? "ON" : "OFF")}");
    }
}
#endif
