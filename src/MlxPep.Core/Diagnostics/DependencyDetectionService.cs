using System.Diagnostics;
using MlxPep.Core.Diagnostics.Probes;

namespace MlxPep.Core.Diagnostics;

/// <summary>
/// Orchestrates dependency detection for all tools.
/// Uses probe-based architecture for testability.
/// </summary>
public class DependencyDetectionService
{
    private readonly Dictionary<string, IDependencyProbe> _probes;

    public DependencyDetectionService()
    {
        // Initialize default probes for each tool
        _probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new DotnetProbe() },
            { "hf-cli", new HuggingFaceCliProbe() },
            { "python3", new Python3Probe() },
            { "copilot-cli", new CopilotCliProbe() },
            { "vscode", new VsCodeProbe(isInsiders: false) },
            { "vscode-insiders", new VsCodeProbe(isInsiders: true) },
            { "omlx", new OmlxProbe() }
        };
    }

    /// <summary>
    /// Inject custom probes for testing.
    /// </summary>
    public DependencyDetectionService(Dictionary<string, IDependencyProbe> probes)
    {
        _probes = probes ?? throw new ArgumentNullException(nameof(probes));
    }

    /// <summary>
    /// Detect all dependencies and return comprehensive report.
    /// </summary>
    public async Task<DependencyReport> DetectAsync()
    {
        var report = new DependencyReport();

        try
        {
            // Detect each tool
            report.Tools["dotnet"] = await DetectDotnetAsync();
            report.Tools["hf-cli"] = await DetectHfCliAsync();
            report.Tools["python3"] = await DetectPython3Async();
            report.Tools["model-assessor"] = await DetectModelAssessorAsync();
            report.Tools["omlx"] = await DetectOmlxAsync();
            report.Tools["vscode"] = await DetectVsCodeAsync();
            report.Tools["vscode-insiders"] = await DetectVsCodeInsidersAsync();
            report.Tools["copilot-cli"] = await DetectCopilotCliAsync();

            // Set overall status based on critical tools
            bool anyInstalled = report.Tools.Values.Any(t => t.Installed);
            report.Status = anyInstalled ? DependencyReportStatus.Success : DependencyReportStatus.PartialSuccess;
        }
        catch (Exception ex)
        {
            report.Status = DependencyReportStatus.Failed;
            report.Warnings.Add($"Unexpected error during detection: {ex.Message}");
        }

        return report;
    }

    private async Task<ToolStatus> DetectDotnetAsync()
    {
        var result = new ToolStatus
        {
            Name = "dotnet",
            DisplayName = ".NET"
        };

        if (!_probes.TryGetValue("dotnet", out var probe))
        {
            result.Message = "Probe not configured";
            return result;
        }

        var probeResult = await probe.ProbeAsync();
        result.RawOutput = probeResult.RawOutput;

        if (!probeResult.Found)
        {
            result.Installed = false;
            result.Message = probeResult.Error ?? "Not found in PATH";
            result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("dotnet");
            return result;
        }

        result.Installed = true;
        result.Version = probe.ParseVersion(probeResult.RawOutput ?? "");
        result.Scope = DetectScope("dotnet");
        result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("dotnet");
        return result;
    }

    private async Task<ToolStatus> DetectHfCliAsync()
    {
        var result = new ToolStatus
        {
            Name = "hf-cli",
            DisplayName = "Hugging Face CLI"
        };

        if (!_probes.TryGetValue("hf-cli", out var probe))
        {
            result.Message = "Probe not configured";
            return result;
        }

        var probeResult = await probe.ProbeAsync();
        result.RawOutput = probeResult.RawOutput;

        if (!probeResult.Found)
        {
            result.Installed = false;
            result.Message = probeResult.Error ?? "Not found in PATH";
            result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("hf-cli");
            return result;
        }

        result.Installed = true;
        result.Version = probe.ParseVersion(probeResult.RawOutput ?? "");
        result.Scope = DetectScope("huggingface-cli");
        result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("hf-cli");
        return result;
    }

    private async Task<ToolStatus> DetectPython3Async()
    {
        var result = new ToolStatus
        {
            Name = "python3",
            DisplayName = "Python 3"
        };

        if (!_probes.TryGetValue("python3", out var probe))
        {
            result.Message = "Probe not configured";
            return result;
        }

        var probeResult = await probe.ProbeAsync();
        result.RawOutput = probeResult.RawOutput;

        if (!probeResult.Found)
        {
            result.Installed = false;
            result.Message = probeResult.Error ?? "Not found in PATH";
            result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("python3");
            return result;
        }

        result.Installed = true;
        result.Version = probe.ParseVersion(probeResult.RawOutput ?? "");
        result.Scope = DetectScope("python3");
        result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("python3");
        return result;
    }

    private async Task<ToolStatus> DetectModelAssessorAsync()
    {
        var result = new ToolStatus
        {
            Name = "model-assessor",
            DisplayName = "model-assessor (Python package)"
        };

        var guidance = DependencyInstallationGuidance.GetGuidance("model-assessor");
        result.InstallGuidance = guidance;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = "-m pip show model-assessor --disable-pip-version-check",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                result.Installed = false;
                result.Message = "python3 -m pip could not be started";
                return result;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(5000))
            {
                result.Installed = false;
                result.Message = "python3 -m pip show timed out";
                return result;
            }

            var output = await stdoutTask;
            var stderr = await stderrTask;
            result.RawOutput = output.Trim();

            if (process.ExitCode != 0)
            {
                result.Installed = false;
                result.Message = "Package not found via python3 -m pip";
                return result;
            }

            // Parse version from "Version: X.Y.Z"
            var versionMatch = System.Text.RegularExpressions.Regex.Match(output, @"Version:\s+(\S+)");
            if (versionMatch.Success)
            {
                result.Installed = true;
                result.Version = versionMatch.Groups[1].Value;
                result.Scope = "python-package";
            }
            else
            {
                result.Installed = false;
                result.Message = "Could not parse version";
            }
        }
        catch (Exception ex)
        {
            result.Installed = false;
            result.Message = ex.Message;
        }

        return result;
    }

    private async Task<ToolStatus> DetectOmlxAsync()
    {
        var result = new ToolStatus
        {
            Name = "omlx",
            DisplayName = "oMLX"
        };

        if (!_probes.TryGetValue("omlx", out var probe))
        {
            result.Message = "Probe not configured";
            return result;
        }

        var probeResult = await probe.ProbeAsync();
        result.RawOutput = probeResult.RawOutput;

        if (!probeResult.Found)
        {
            result.Installed = false;
            result.Message = "App not found and server not running";
            result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("omlx");
            return result;
        }

        result.Installed = true;
        result.ToolPath = probeResult.RawOutput;
        result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("omlx");

        if (probeResult.RawOutput?.Contains("localhost:8000") == true)
        {
            result.Message = "Server running on localhost:8000";
            result.Scope = "running";
        }
        else if (probeResult.RawOutput?.Contains("Applications") == true)
        {
            result.Message = "App bundle installed";
            result.Scope = "app-bundle";
        }

        return result;
    }

    private async Task<ToolStatus> DetectVsCodeAsync()
    {
        var result = new ToolStatus
        {
            Name = "vscode",
            DisplayName = "VS Code"
        };

        if (!_probes.TryGetValue("vscode", out var probe))
        {
            result.Message = "Probe not configured";
            return result;
        }

        var probeResult = await probe.ProbeAsync();
        result.RawOutput = probeResult.RawOutput;
        result.ToolPath = probeResult.RawOutput;

        if (!probeResult.Found)
        {
            result.Installed = false;
            result.Message = "App not found and CLI not in PATH";
            result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("vscode");
            return result;
        }

        result.Installed = true;
        result.Scope = DetectScope("code");
        result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("vscode");

        // Try to detect version via CLI
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "code",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            if (process.Start() && process.WaitForExit(5000) && process.ExitCode == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var versionLine = output.Split('\n')[0].Trim();
                if (!string.IsNullOrEmpty(versionLine))
                {
                    result.Version = versionLine;
                }
            }
        }
        catch { }

        return result;
    }

    private async Task<ToolStatus> DetectVsCodeInsidersAsync()
    {
        var result = new ToolStatus
        {
            Name = "vscode-insiders",
            DisplayName = "VS Code Insiders"
        };

        if (!_probes.TryGetValue("vscode-insiders", out var probe))
        {
            result.Message = "Probe not configured";
            return result;
        }

        var probeResult = await probe.ProbeAsync();
        result.RawOutput = probeResult.RawOutput;
        result.ToolPath = probeResult.RawOutput;

        if (!probeResult.Found)
        {
            result.Installed = false;
            result.Message = "App not found and CLI not in PATH";
            result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("vscode-insiders");
            return result;
        }

        result.Installed = true;
        result.Scope = DetectScope("code-insiders");
        result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("vscode-insiders");

        // Try to detect version via CLI
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "code-insiders",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            if (process.Start() && process.WaitForExit(5000) && process.ExitCode == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var versionLine = output.Split('\n')[0].Trim();
                if (!string.IsNullOrEmpty(versionLine))
                {
                    result.Version = versionLine;
                }
            }
        }
        catch { }

        return result;
    }

    private async Task<ToolStatus> DetectCopilotCliAsync()
    {
        var result = new ToolStatus
        {
            Name = "copilot-cli",
            DisplayName = "GitHub Copilot CLI"
        };

        if (!_probes.TryGetValue("copilot-cli", out var probe))
        {
            result.Message = "Probe not configured";
            return result;
        }

        var probeResult = await probe.ProbeAsync();
        result.RawOutput = probeResult.RawOutput;

        if (!probeResult.Found)
        {
            result.Installed = false;
            result.Message = probeResult.Error ?? "Not found in PATH";
            result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("copilot-cli");
            return result;
        }

        result.Installed = true;
        result.Version = probe.ParseVersion(probeResult.RawOutput ?? "");
        result.Scope = DetectScope("gh");
        result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("copilot-cli");
        return result;
    }

    private static string? DetectScope(string toolName)
    {
        // Try to detect if tool is in user or global scope
        // Heuristic: check common user paths (Homebrew, pip user, etc.)
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var paths = pathEnv.Split(Path.PathSeparator);

            foreach (var path in paths)
            {
                var toolPath = Path.Combine(path, toolName);
                if (File.Exists(toolPath))
                {
                    // Check if in user-local Homebrew
                    if (path.Contains("/opt/homebrew/") || path.Contains("/usr/local/"))
                        return "user";
                    if (path.Contains("/.local/bin"))
                        return "user";
                    if (path.Contains("/System") || path.Contains("/usr/bin"))
                        return "global";
                    return "unknown";
                }
            }
        }
        catch { }

        return "unknown";
    }
}
