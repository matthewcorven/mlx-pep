namespace MlxPep.Cli.Commands;

/// <summary>
/// Handler for `mlx-pep apply` command.
/// Applies a profile to the local system or harness.
/// </summary>
public class ApplyCommand
{
    public async Task<CommandResult> ExecuteAsync(
        string profile,
        string? harness = null,
        string? output = null,
        bool dryRun = false,
        bool backup = true,
        CommandContext? context = null)
    {
        context ??= new CommandContext();
        
        try
        {
            if (context.JsonOutput)
            {
                var result = new
                {
                    command = "apply",
                    status = "ok",
                    profile = profile,
                    harness = harness ?? "opencode",
                    dryRun = dryRun
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"Applying profile: {profile}");
                Console.WriteLine($"Target harness: {harness ?? "opencode"}");
            }
            
            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to apply profile: {ex.Message}");
        }
    }
}
