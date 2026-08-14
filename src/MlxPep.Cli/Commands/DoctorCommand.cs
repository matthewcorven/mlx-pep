using System.Text.Json;
using MlxPep.Core.Diagnostics;

namespace MlxPep.Cli.Commands;

/// <summary>
/// Handler for `mlx-pep doctor` command.
/// Detects system dependencies using the DependencyDetectionService
/// and provides installation guidance.
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
            tools = report.Tools.Select(kvp => new
            {
                name = kvp.Key,
                displayName = kvp.Value.DisplayName,
                installed = kvp.Value.Installed,
                version = kvp.Value.Version,
                scope = kvp.Value.Scope,
                installGuidance = kvp.Value.InstallGuidance,
                message = kvp.Value.Message
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

        var installed = 0;
        var missing = 0;

        foreach (var kvp in report.Tools.OrderBy(t => t.Value.DisplayName))
        {
            var tool = kvp.Value;
            var statusIcon = tool.Installed ? "✓" : "✗";
            var statusText = tool.Installed 
                ? $"Installed (v{tool.Version})" 
                : "Not installed";
            
            Console.WriteLine($"{statusIcon} {tool.DisplayName,-20} {statusText}");
            
            if (tool.Installed)
                installed++;
            else
                missing++;
            
            if (!string.IsNullOrEmpty(tool.InstallGuidance) && !tool.Installed)
            {
                Console.WriteLine($"  → {tool.InstallGuidance}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Summary: {installed} installed, {missing} missing");
    }
}
