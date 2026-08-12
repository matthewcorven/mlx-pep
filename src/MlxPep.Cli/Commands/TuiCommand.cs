namespace MlxPep.Cli.Commands;

/// <summary>
/// Handler for `mlx-pep tui` command.
/// Launches the Terminal.Gui interactive interface.
/// </summary>
public class TuiCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            if (context.JsonOutput)
            {
                var result = new
                {
                    command = "tui",
                    status = "error",
                    message = "TUI cannot be used with --json output"
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return CommandResult.Failure("TUI cannot be used with --json output", 1);
            }
            
            Console.WriteLine("Launching Terminal.Gui interface...");
            // TODO: Launch Terminal.Gui interface
            
            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to launch TUI: {ex.Message}");
        }
    }
}
