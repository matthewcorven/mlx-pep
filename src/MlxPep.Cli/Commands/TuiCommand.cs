namespace MlxPep.Cli.Commands;

using MlxPep.Core;

/// <summary>
/// Handler for `mlx-pep tui` command.
/// Launches a terminal-based results and assessment browser.
/// </summary>
public class TuiCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            context.Verbose("TuiCommand", "Starting TUI command.");
            if (context.JsonOutput)
            {
                context.Verbose("TuiCommand", "JSON output was requested; TUI cannot run in JSON mode.");
                var result = new
                {
                    command = "tui",
                    status = "error",
                    message = "TUI cannot be used with --json output"
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return CommandResult.Failure("TUI cannot be used with --json output", 1);
            }

            context.Verbose("TuiCommand", "Launching interactive results browser.");
            InteractiveResultsBrowser.Run();

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            context.Verbose("TuiCommand", $"TUI command failed with {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Failed to launch TUI: {ex.Message}");
        }
        finally
        {
            context.Verbose("TuiCommand", "TUI command finished execution path.");
        }
    }
}
