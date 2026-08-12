namespace MlxPep.Cli.Commands;

/// <summary>
/// Result of command execution, carrying exit code and optional output.
/// </summary>
public class CommandResult
{
    public int ExitCode { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
    
    public CommandResult(int exitCode = 0, string? message = null, object? data = null)
    {
        ExitCode = exitCode;
        Message = message;
        Data = data;
    }
    
    public static CommandResult Success(string? message = null, object? data = null) 
        => new(0, message, data);
    
    public static CommandResult Failure(string message, int exitCode = 1) 
        => new(exitCode, message);
}
