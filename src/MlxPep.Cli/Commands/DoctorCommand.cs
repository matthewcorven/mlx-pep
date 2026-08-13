using System.Text.Json;
using MlxPep.Core.Diagnostics;

namespace MlxPep.Cli.Commands;

/// <summary>
/// Handler for `mlx-pep doctor` command.
/// Detects system dependencies and provides installation guidance.
/// </summary>
public class DoctorCommand
{
    private readonly DependencyDetectionService _detector;

    public DoctorCommand(DependencyDetectionService? detector = null)
    {
        _detector = detector ?? new DependencyDetectionService();
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            var report = await _detector.DetectAsync();

            if (context.JsonOutput)
            {
                OutputJson(report);
            }
            else
            {
                OutputTable(report);
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Doctor check failed: {ex.Message}");
        }
    }

    private void OutputJson(DependencyReport report)
    {
        var output = new
        {
            status = report.Status.ToString(),
            generatedAt = report.GeneratedAt,
            tools = report.Tools.Select(t => new
            {
                name = t.ToolName,
                installed = t.Installed,
                version = t.Version,
                scope = t.Scope,
                detectionMethod = t.DetectionMethod,
                installGuidance = t.InstallGuidance,
                message = t.Message,
                detectedAt = t.DetectedAt
            })
        };

        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    private void OutputTable(DependencyReport report)
    {
        Console.WriteLine("mlx-pep doctor - Dependency Check");
        Console.WriteLine("==================================");
        Console.WriteLine();

        foreach (var tool in report.Tools)
        {
            var statusIcon = tool.Installed ? "✓" : "✗";
            var statusText = tool.Installed 
                ? $"Installed (v{tool.Version})" 
                : "Not installed";
            
            Console.WriteLine($"{statusIcon} {tool.ToolName,-20} {statusText}");
            
            if (!string.IsNullOrEmpty(tool.InstallGuidance) && !tool.Installed)
            {
                Console.WriteLine($"  → {tool.InstallGuidance}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Overall status: {report.Status}");
    }
}

/// <summary>
/// Status of a single dependency (deprecated - use DependencyReport instead).
/// </summary>
public class DependencyStatus
{
    public bool Installed { get; set; }
    public string? Version { get; set; }
    public string? Message { get; set; }
}
