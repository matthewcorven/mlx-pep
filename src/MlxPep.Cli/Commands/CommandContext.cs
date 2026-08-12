namespace MlxPep.Cli.Commands;

/// <summary>
/// Execution context for a CLI command, carrying common options like --json.
/// </summary>
public class CommandContext
{
    public bool JsonOutput { get; set; }
    
    public CommandContext(bool jsonOutput = false)
    {
        JsonOutput = jsonOutput;
    }
}
