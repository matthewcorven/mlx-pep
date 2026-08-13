namespace MlxPep.Cli.Commands;

using System.Diagnostics;
using System.Text.Json.Serialization;

/// <summary>
/// Handler for `mlx-pep doctor` command.
/// Detects system dependencies and provides installation guidance.
/// </summary>
public class DoctorCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            var dependencies = new Dictionary<string, DependencyStatus>
            {
                { "dotnet", await DetectDotnetAsync() },
                { "python3", await DetectPython3Async() },
                { "hf-cli", await DetectHfCliAsync() },
                { "omlx", await DetectOmlxAsync() },
                { "vscode", await DetectVsCodeAsync() },
                { "vscode-insiders", await DetectVsCodeInsidersAsync() },
                { "copilot-cli", await DetectCopilotCliAsync() }
            };

            if (context.JsonOutput)
            {
                OutputJson(dependencies);
            }
            else
            {
                OutputTable(dependencies);
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Doctor check failed: {ex.Message}");
        }
    }

    private void OutputJson(Dictionary<string, DependencyStatus> dependencies)
    {
        var result = new
        {
            command = "doctor",
            timestamp = DateTime.UtcNow.ToString("O"),
            dependencies
        };
        
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, options));
    }

    private void OutputTable(Dictionary<string, DependencyStatus> dependencies)
    {
        Console.WriteLine("mlx-pep doctor - Dependency Check");
        Console.WriteLine();
        
        var installed = 0;
        var missing = 0;
        
        foreach (var (name, status) in dependencies)
        {
            var displayName = name.ToDisplayName();
            var statusSymbol = status.Installed ? "✓" : "✗";
            
            if (status.Installed)
            {
                var version = status.Version ?? "unknown";
                Console.WriteLine($"{statusSymbol} {displayName,-20} v{version}");
                installed++;
            }
            else
            {
                Console.WriteLine($"{statusSymbol} {displayName,-20} not installed");
                missing++;
            }
        }
        
        Console.WriteLine();
        Console.WriteLine($"Summary: {installed} installed, {missing} missing");
        
        if (missing > 0)
        {
            Console.WriteLine("Run `mlx-pep doctor --json` for installation guidance.");
        }
    }

    private async Task<DependencyStatus> DetectDotnetAsync()
    {
        return await TryRunCommandAsync("dotnet", "--version");
    }

    private async Task<DependencyStatus> DetectPython3Async()
    {
        return await TryRunCommandAsync("python3", "--version");
    }

    private async Task<DependencyStatus> DetectHfCliAsync()
    {
        return await TryRunCommandAsync("huggingface-cli", "--version");
    }

    private async Task<DependencyStatus> DetectOmlxAsync()
    {
        // Try pip show mlx-lm
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pip",
                Arguments = "show mlx-lm",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    return new DependencyStatus { Installed = false, Message = "Could not start pip process" };

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && output.Contains("Name: mlx-lm"))
                {
                    var versionLine = output.Split('\n').FirstOrDefault(l => l.StartsWith("Version:"));
                    var version = versionLine?.Split(':').LastOrDefault()?.Trim() ?? "installed";
                    return new DependencyStatus { Installed = true, Version = version };
                }
            }
        }
        catch { }

        return new DependencyStatus { Installed = false, Message = "pip command failed or mlx-lm not found" };
    }

    private async Task<DependencyStatus> DetectVsCodeAsync()
    {
        return await DetectVsCodeEditorAsync("code", "VS Code");
    }

    private async Task<DependencyStatus> DetectVsCodeInsidersAsync()
    {
        return await DetectVsCodeEditorAsync("code-insiders", "VS Code Insiders");
    }

    private async Task<DependencyStatus> DetectVsCodeEditorAsync(string command, string name)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    return new DependencyStatus { Installed = false, Message = "Command not found" };

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var version = output.Split('\n')[0].Trim();
                    return new DependencyStatus { Installed = true, Version = version };
                }
            }
        }
        catch { }

        return new DependencyStatus { Installed = false, Message = $"{name} not found" };
    }

    private async Task<DependencyStatus> DetectCopilotCliAsync()
    {
        return await TryRunCommandAsync("copilot", "--version");
    }

    private async Task<DependencyStatus> TryRunCommandAsync(string command, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    return new DependencyStatus { Installed = false, Message = "Command not found" };

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var version = ExtractVersion(output);
                    return new DependencyStatus { Installed = true, Version = version };
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to detect {command}: {ex.Message}");
        }

        return new DependencyStatus { Installed = false, Message = $"{command} not found in PATH" };
    }

    private string ExtractVersion(string output)
    {
        var lines = output.Split('\n');
        var firstLine = lines[0].Trim();
        
        // Try to extract version number
        var parts = firstLine.Split(new[] { ' ', 'v', 'V' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part[0] >= '0' && part[0] <= '9')
                return part.Split(new[] { '\r' }, StringSplitOptions.None)[0];
        }
        
        return firstLine;
    }
}

/// <summary>
/// Status of a single dependency.
/// </summary>
public class DependencyStatus
{
    [JsonPropertyName("installed")]
    public bool Installed { get; set; }
    
    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }
    
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }
}

/// <summary>
/// Extension methods for DoctorCommand.
/// </summary>
internal static class StringExtensions
{
    public static string ToDisplayName(this string name) => name switch
    {
        "dotnet" => ".NET",
        "python3" => "Python 3",
        "hf-cli" => "Hugging Face CLI",
        "omlx" => "oMLX",
        "vscode" => "VS Code",
        "vscode-insiders" => "VS Code Insiders",
        "copilot-cli" => "Copilot CLI",
        _ => name
    };
}
