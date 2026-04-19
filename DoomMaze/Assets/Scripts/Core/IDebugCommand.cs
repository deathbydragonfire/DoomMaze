/// <summary>
/// Contract that every registered debug console command must implement.
/// Commands are matched by <see cref="Id"/> against the first token of user input.
/// </summary>
public interface IDebugCommand
{
    /// <summary>Command keyword matched against input, e.g. "god".</summary>
    string Id { get; }

    /// <summary>Short description shown in the help listing.</summary>
    string Description { get; }

    /// <summary>Executes the command with the given arguments.</summary>
    /// <param name="args">Whitespace-split tokens after the command ID.</param>
    /// <param name="console">Console reference for printing feedback.</param>
    void Execute(string[] args, DebugConsole console);
}
