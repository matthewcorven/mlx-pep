using MlxPep.Cli.Commands;

namespace MlxPep.Tui;

/// <summary>
/// Main application entry point for the mlx-pep terminal results browser.
/// The TUI wraps CLI command handlers without adding business logic.
/// </summary>
internal class Program
{
    static void Main()
    {
        InteractiveResultsBrowser.Run();
    }
}
