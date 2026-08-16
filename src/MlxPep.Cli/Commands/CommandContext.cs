namespace MlxPep.Cli.Commands;

/// <summary>
/// Execution context for a CLI command, carrying common options like --json.
/// </summary>
public sealed record ProgressUpdate(
    string Operation,
    int StepNumber,
    int TotalSteps,
    double WorkPercent,
    double OverallPercent,
    string Detail);

public class CommandContext
{
    public bool JsonOutput { get; set; }
    public bool VerboseOutput { get; set; }
    public bool ProgressOutput { get; set; }
    public Action<ProgressUpdate>? ProgressCallback { get; set; }

    public CommandContext(bool jsonOutput = false, bool verboseOutput = false, bool progressOutput = false)
    {
        JsonOutput = jsonOutput;
        VerboseOutput = verboseOutput;
        ProgressOutput = progressOutput;

        if (VerboseOutput)
        {
            CliRuntime.EnsureVerboseTraceListener();
        }
    }

    public void Verbose(string source, string message)
    {
        if (VerboseOutput)
        {
            CliRuntime.WriteVerbose(source, message);
        }
    }

    public CliProgressScope CreateProgressScope(string operation, int totalSteps)
    {
        return new CliProgressScope(ProgressOutput, operation, totalSteps, ProgressCallback);
    }
}
