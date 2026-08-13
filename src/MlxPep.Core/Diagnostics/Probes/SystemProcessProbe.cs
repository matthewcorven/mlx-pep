using System.Diagnostics;

namespace MlxPep.Core.Diagnostics.Probes;

/// <summary>
/// Probes for tools via system process execution.
/// Runs CLI commands and captures stdout.
/// </summary>
public class SystemProcessProbe : IDependencyProbe
{
    private readonly string _command;
    private readonly string[]? _args;
    private readonly TimeSpan _timeout;

    public SystemProcessProbe(string command, string[]? args = null, TimeSpan? timeout = null)
    {
        _command = command;
        _args = args;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<ProbeResult> ProbeAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _command,
                    Arguments = _args != null ? string.Join(" ", _args) : "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return new ProbeResult { Found = false, Error = "Failed to start process" };
            }

            if (!await Task.Run(() => process.WaitForExit((int)_timeout.TotalMilliseconds)))
            {
                try { process.Kill(); } catch { }
                return new ProbeResult { Found = false, Error = "Process timeout" };
            }

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                return new ProbeResult { Found = false, Error = error.Trim() };
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            return new ProbeResult { Found = true, RawOutput = output.Trim() };
        }
        catch (Exception ex)
        {
            return new ProbeResult { Found = false, Error = ex.Message };
        }
    }

    public virtual string? ParseVersion(string rawOutput)
    {
        // Default: return first line, trimmed.
        // Subclasses override for specific parsing logic.
        return string.IsNullOrWhiteSpace(rawOutput) ? null : rawOutput.Split('\n')[0].Trim();
    }
}

/// <summary>
/// Probe for dotnet CLI.
/// Runs `dotnet --version` and parses output.
/// </summary>
public class DotnetProbe : SystemProcessProbe
{
    public DotnetProbe() : base("dotnet", new[] { "--version" }) { }

    public override string? ParseVersion(string rawOutput)
    {
        // dotnet --version outputs something like "10.0.0\n"
        return base.ParseVersion(rawOutput);
    }
}

/// <summary>
/// Probe for Hugging Face CLI.
/// Runs `huggingface-cli --version` and parses output.
/// </summary>
public class HuggingFaceCliProbe : SystemProcessProbe
{
    public HuggingFaceCliProbe() : base("huggingface-cli", new[] { "--version" }) { }

    public override string? ParseVersion(string rawOutput)
    {
        // huggingface-cli --version outputs something like "huggingface_hub version: 0.19.0"
        var match = System.Text.RegularExpressions.Regex.Match(rawOutput, @"(\d+\.\d+\.\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}

/// <summary>
/// Probe for python3 CLI.
/// Runs `python3 --version` and parses output.
/// </summary>
public class Python3Probe : SystemProcessProbe
{
    public Python3Probe() : base("python3", new[] { "--version" }) { }

    public override string? ParseVersion(string rawOutput)
    {
        // python3 --version outputs "Python 3.11.0" or similar
        var match = System.Text.RegularExpressions.Regex.Match(rawOutput, @"(\d+\.\d+\.\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}

/// <summary>
/// Probe for GitHub Copilot CLI.
/// Runs `gh copilot --version` and parses output.
/// </summary>
public class CopilotCliProbe : SystemProcessProbe
{
    public CopilotCliProbe() : base("gh", new[] { "copilot", "--version" }) { }

    public override string? ParseVersion(string rawOutput)
    {
        // gh copilot --version outputs something like "gh version 2.45.0 (2024-01-01)"
        var match = System.Text.RegularExpressions.Regex.Match(rawOutput, @"(\d+\.\d+\.\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
