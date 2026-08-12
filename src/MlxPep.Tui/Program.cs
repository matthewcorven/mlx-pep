using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MlxPep.Tui;

/// <summary>
/// Main application entry point for the mlx-pep Terminal.Gui presentation layer.
/// The TUI wraps CLI command handlers without adding business logic.
/// All operations invoke the CLI via process calls.
/// </summary>
internal class Program
{
    static void Main()
    {
        using IApplication app = Application.Create();
        app.Init();

        using Window window = new() 
        { 
            Title = "mlx-pep — Local SLM/LLM Profiler",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        Label label = new()
        {
            Text = "Welcome to mlx-pep TUI",
            X = Pos.Center(),
            Y = Pos.Center()
        };
        
        window.Add(label);

        app.Run(window);
    }
}
