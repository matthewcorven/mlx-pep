namespace MlxPep.Core.Diagnostics.Probes;

/// <summary>
/// Probes for detecting tools on the file system.
/// Checks for app bundles, executables, or config files.
/// </summary>
public class FileSystemProbe : IDependencyProbe
{
    private readonly string _path;
    private readonly bool _isDirectory;

    public FileSystemProbe(string path, bool isDirectory = false)
    {
        _path = path;
        _isDirectory = isDirectory;
    }

    public Task<ProbeResult> ProbeAsync()
    {
        try
        {
            bool exists = _isDirectory ? Directory.Exists(_path) : File.Exists(_path);
            return Task.FromResult(new ProbeResult
            {
                Found = exists,
                RawOutput = exists ? _path : null
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ProbeResult { Found = false, Error = ex.Message });
        }
    }

    public string? ParseVersion(string rawOutput)
    {
        // File system probe doesn't parse version; version detection is separate.
        return null;
    }
}

/// <summary>
/// Probes for the presence of a command on PATH.
/// Uses `which` (Unix) or `where` (Windows) to locate executable.
/// </summary>
public class PathProbe : IDependencyProbe
{
    private readonly string _command;

    public PathProbe(string command)
    {
        _command = command;
    }

    public async Task<ProbeResult> ProbeAsync()
    {
        try
        {
            string whichCmd = OperatingSystem.IsWindows() ? "where" : "which";
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = whichCmd,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            // Use ArgumentList to safely pass the command parameter.
            // ArgumentList automatically handles escaping and prevents command injection.
            process.StartInfo.ArgumentList.Add(_command);

            if (!process.Start())
            {
                return new ProbeResult { Found = false };
            }

            if (!await Task.Run(() => process.WaitForExit(5000)))
            {
                try { process.Kill(); } catch { }
                return new ProbeResult { Found = false };
            }

            if (process.ExitCode != 0)
            {
                return new ProbeResult { Found = false };
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var path = output.Trim();
            return new ProbeResult { Found = !string.IsNullOrEmpty(path), RawOutput = path };
        }
        catch
        {
            return new ProbeResult { Found = false };
        }
    }

    public string? ParseVersion(string rawOutput)
    {
        // PATH probe doesn't parse version; version detection is separate.
        return null;
    }
}

/// <summary>
/// Probes for environment variables.
/// </summary>
public class EnvironmentVariableProbe : IDependencyProbe
{
    private readonly string _variableName;

    public EnvironmentVariableProbe(string variableName)
    {
        _variableName = variableName;
    }

    public Task<ProbeResult> ProbeAsync()
    {
        try
        {
            var value = Environment.GetEnvironmentVariable(_variableName);
            return Task.FromResult(new ProbeResult
            {
                Found = !string.IsNullOrEmpty(value),
                RawOutput = value
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ProbeResult { Found = false, Error = ex.Message });
        }
    }

    public string? ParseVersion(string rawOutput)
    {
        // Environment variable probe doesn't parse version.
        return null;
    }
}

/// <summary>
/// Probes for VS Code app bundle and CLI.
/// Handles macOS app bundle detection.
/// </summary>
public class VsCodeProbe : IDependencyProbe
{
    private readonly bool _isInsiders;

    public VsCodeProbe(bool isInsiders = false)
    {
        _isInsiders = isInsiders;
    }

    public async Task<ProbeResult> ProbeAsync()
    {
        // Check for app bundle (macOS)
        string appPath = _isInsiders
            ? "/Applications/Visual Studio Code - Insiders.app"
            : "/Applications/Visual Studio Code.app";

        if (Directory.Exists(appPath))
        {
            return new ProbeResult { Found = true, RawOutput = appPath };
        }

        // Check for CLI command on PATH
        string command = _isInsiders ? "code-insiders" : "code";
        var pathProbe = new PathProbe(command);
        var result = await pathProbe.ProbeAsync();
        if (result.Found)
        {
            return result;
        }

        return new ProbeResult { Found = false };
    }

    public string? ParseVersion(string rawOutput)
    {
        // Version detection for VS Code requires running the CLI.
        // This is handled separately in DependencyDetectionService.
        return null;
    }
}

/// <summary>
/// Probes for oMLX app bundle and server on :8000.
/// </summary>
public class OmlxProbe : IDependencyProbe
{
    public async Task<ProbeResult> ProbeAsync()
    {
        // Check for app bundle (macOS)
        string appPath = "/Applications/oMLX.app";
        if (Directory.Exists(appPath))
        {
            return new ProbeResult { Found = true, RawOutput = appPath };
        }

        // Check for running server on :8000
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await client.GetAsync("http://localhost:8000/health");
            if (response.IsSuccessStatusCode)
            {
                return new ProbeResult { Found = true, RawOutput = "localhost:8000" };
            }
        }
        catch { }

        return new ProbeResult { Found = false };
    }

    public string? ParseVersion(string rawOutput)
    {
        // oMLX version detection would require API call.
        // This is handled separately in DependencyDetectionService.
        return null;
    }
}
