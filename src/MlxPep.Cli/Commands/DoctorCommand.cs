using System.Text.Json;
using System.Text.Json.Serialization;
using MlxPep.Core.Diagnostics;

namespace MlxPep.Cli.Commands;

/// <summary>
/// Handler for `mlx-pep doctor` command.
/// Detects system dependencies using the DependencyDetectionService
/// and provides installation guidance.
/// </summary>
public class DoctorCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            var detector = new DependencyDetectionService();
            var report = await detector.DetectAsync();

            if (context.JsonOutput)
            {
                var json = FormatAsJson(report);
                Console.WriteLine(json);
            }
            else
            {
                var table = FormatAsTable(report);
                Console.WriteLine(table);
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Doctor check failed: {ex.Message}");
        }
    }

    private string FormatAsJson(DependencyReport report)
    {
        // Build dependencies object manually to ensure correct JSON structure
        var dependencies = new Dictionary<string, object>();
        foreach (var kvp in report.Tools)
        {
            var toolDep = new Dictionary<string, object?>
            {
                { "installed", kvp.Value.Installed }
            };

            if (kvp.Value.Version != null)
            {
                toolDep["version"] = kvp.Value.Version;
            }

            if (!kvp.Value.Installed && kvp.Value.Message != null)
            {
                toolDep["message"] = kvp.Value.Message;
            }

            dependencies[kvp.Key] = toolDep;
        }

        var result = new Dictionary<string, object>
        {
            { "command", "doctor" },
            { "timestamp", DateTime.UtcNow.ToString("O") },
            { "dependencies", dependencies }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(result, options);
    }

    private string FormatAsTable(DependencyReport report)
    {
        var lines = new List<string>();
        lines.Add("mlx-pep doctor - Dependency Check");
        lines.Add("");

        var installed = 0;
        var missing = 0;

        foreach (var tool in report.Tools.Values.OrderBy(t => t.Name))
        {
            var displayName = tool.DisplayName;
            var statusSymbol = tool.Installed ? "✓" : "✗";

            if (tool.Installed)
            {
                var version = tool.Version ?? "unknown";
                lines.Add($"{statusSymbol} {displayName,-20} v{version}");
                installed++;
            }
            else
            {
                lines.Add($"{statusSymbol} {displayName,-20} not installed");
                var guidance = DependencyInstallationGuidance.GetGuidance(tool.Name);
                if (!string.IsNullOrEmpty(guidance))
                {
                    lines.Add($"  ℹ️  {guidance}");
                }
                missing++;
            }
        }

        lines.Add("");
        lines.Add($"Summary: {installed} installed, {missing} missing");

        if (missing > 0)
        {
            lines.Add("Run `mlx-pep doctor --json` for detailed installation guidance.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
