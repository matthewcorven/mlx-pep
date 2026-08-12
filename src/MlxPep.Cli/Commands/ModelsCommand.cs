namespace MlxPep.Cli.Commands;

/// <summary>
/// Handler for `mlx-pep models` subcommands.
/// Manages model discovery and download from Hugging Face cache.
/// </summary>
public class ModelsListCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            if (context.JsonOutput)
            {
                var result = new
                {
                    command = "models list",
                    status = "ok",
                    models = new object[] { }
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine("Models in Hugging Face cache:");
            }
            
            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to list models: {ex.Message}");
        }
    }
}

public class ModelsGetCommand
{
    public async Task<CommandResult> ExecuteAsync(string hfId, CommandContext context)
    {
        try
        {
            if (context.JsonOutput)
            {
                var result = new
                {
                    command = "models get",
                    status = "ok",
                    hfId = hfId
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"Getting model: {hfId}");
            }
            
            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to get model: {ex.Message}");
        }
    }
}
