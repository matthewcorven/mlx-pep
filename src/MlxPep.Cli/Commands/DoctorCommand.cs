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

    public DoctorCommand()
    {
        _detector = new DependencyDetectionService();
    }

    // Constructor for testing with injected dependencies
    public DoctorCommand(DependencyDetectionService detector)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            var report = await _detector.DetectAsync();

            if (context.JsonOutput)
            {
                return OutputJson(report);
            }

            OutputTable(report);
            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Doctor command failed: {ex.Message}");
            return CommandResult.Failure($"Doctor check failed: {ex.Message}");
        }
    }

    private CommandResult OutputJson(DependencyReport report)
    {
        var jsonTools = new Dictionary<string, object>();

        foreach (var (name, tool) in report.Tools)
        {
            var toolObj = new
            {
                installed = tool.Installed,
                version = tool.Version,
                scope = tool.Scope,
                message = tool.Message,
                toolPath = tool.ToolPath,
                installGuidance = tool.InstallGuidance
            };
            jsonTools[name] = toolObj;
        }

        var result = new
        {
            command = "doctor",
            status = report.Status.ToString().ToLowerInvariant(),
            generatedAt = report.GeneratedAt,
            summary = GetSummary(report),
            tools = jsonTools,
            warnings = report.Warnings
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(result, options));
        return CommandResult.Success();
    }

    private void OutputTable(DependencyReport report)
    {
        Console.WriteLine("mlx-pep doctor — Dependency Check\n");

        var rows = new List<string[]>();
        bool allInstalled = true;

        foreach (var (name, tool) in report.Tools.OrderBy(x => x.Value.DisplayName))
        {
            var icon = tool.Installed ? "✓" : "✗";
            var statusStr = GetStatusString(tool);
            rows.Add(new[] { icon, PadName(tool.DisplayName), statusStr });

            if (!tool.Installed)
                allInstalled = false;
        }

        // Print table with aligned columns
        int maxNameLen = rows.Max(r => r[1].Length);
        foreach (var row in rows)
        {
            Console.WriteLine($"  {row[0]}  {row[1].PadRight(maxNameLen)}  {row[2]}");
        }

        Console.WriteLine();
        Console.WriteLine(GetSummaryLine(report));

        if (!allInstalled && report.Warnings.Count > 0)
        {
            Console.WriteLine("\nWarnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  ⚠ {warning}");
            }
        }
    }

    private string GetSummary(DependencyReport report)
    {
        var installed = report.Tools.Count(d => d.Value.Installed);
        var total = report.Tools.Count;
        return $"{installed}/{total} dependencies installed";
    }

    private string GetSummaryLine(DependencyReport report)
    {
        var installed = report.Tools.Count(d => d.Value.Installed);
        var total = report.Tools.Count;

        if (installed == total)
        {
            return "✓ All dependencies installed.";
        }

        var missing = total - installed;
        return $"⚠ {missing} dependency(ies) missing. Run 'mlx-pep doctor --json' for installation guidance.";
    }

    private string GetStatusString(ToolStatus tool)
    {
        if (tool.Installed)
        {
            if (!string.IsNullOrEmpty(tool.Version))
                return $"{tool.Version}";
            return "Installed";
        }

        return tool.Message ?? "Not found";
    }

    private string PadName(string name)
    {
        return name;
    }
}
