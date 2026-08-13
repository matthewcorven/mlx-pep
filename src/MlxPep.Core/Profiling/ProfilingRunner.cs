namespace MlxPep.Core.Profiling;

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Abstraction for running the Python model-assessor subprocess.
/// Shells out to model-assessor runner, captures output, and parses recommendation manifest.
/// </summary>
public class ProfilingRunner
{
    private readonly string? _modelAssessorPath;
    private readonly TimeSpan _timeout;

    public ProfilingRunner(string? modelAssessorPath = null, TimeSpan? timeout = null)
    {
        _modelAssessorPath = modelAssessorPath;
        _timeout = timeout ?? TimeSpan.FromMinutes(30); // Default 30-minute timeout for profiling
        Debug.WriteLine($"[ProfilingRunner] Initialized with timeout: {_timeout.TotalSeconds} seconds");
    }

    /// <summary>
    /// Runs the model-assessor for a given Hugging Face model ID.
    /// </summary>
    public async Task<RecommendationManifest> RunProfilingAsync(
        string hfId,
        string? assistantModelId = null,
        string suite = "full",
        CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine($"[ProfilingRunner] Starting profiling for {hfId} (suite: {suite})");

            // Determine model-assessor path
            var assessorPath = _modelAssessorPath ?? "python3 -m model_assessor.cli";
            Debug.WriteLine($"[ProfilingRunner] Using assessor path: {assessorPath}");

            // Build command arguments
            var args = new StringBuilder();
            args.Append($"assess --model-id {EscapeArg(hfId)}");
            args.Append($" --suite {suite}");

            if (!string.IsNullOrEmpty(assistantModelId))
            {
                args.Append($" --assistant-model {EscapeArg(assistantModelId)}");
                Debug.WriteLine($"[ProfilingRunner] Using assistant model: {assistantModelId}");
            }

            args.Append(" --format json");
            Debug.WriteLine($"[ProfilingRunner] Full command: {assessorPath} {args}");

            // Start the subprocess
            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"-m model_assessor.cli {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                Debug.WriteLine($"[ProfilingRunner] Starting subprocess");
                process.Start();

                // Capture stdout and stderr
                var stdoutBuilder = new StringBuilder();
                var stderrBuilder = new StringBuilder();

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                // Wait with timeout
                var completedTask = await Task.WhenAny(
                    Task.WhenAll(stdoutTask, stderrTask),
                    Task.Delay(_timeout, cancellationToken)
                );

                if (completedTask != stdoutTask.WhenAll(stderrTask))
                {
                    Debug.WriteLine($"[ProfilingRunner] Timeout after {_timeout.TotalSeconds} seconds");
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    throw new TimeoutException($"Model-assessor did not complete within {_timeout.TotalSeconds} seconds");
                }

                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                if (!process.WaitForExit((int)_timeout.TotalMilliseconds))
                {
                    Debug.WriteLine($"[ProfilingRunner] Process did not exit within timeout");
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    throw new TimeoutException("Model-assessor subprocess did not terminate");
                }

                Debug.WriteLine($"[ProfilingRunner] Process exited with code: {process.ExitCode}");

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine($"[ProfilingRunner] Subprocess failed with stderr: {stderr}");
                    throw new InvalidOperationException(
                        $"Model-assessor failed with exit code {process.ExitCode}: {stderr}");
                }

                if (string.IsNullOrWhiteSpace(stdout))
                {
                    Debug.WriteLine($"[ProfilingRunner] No output from model-assessor");
                    throw new InvalidOperationException("Model-assessor produced no output");
                }

                Debug.WriteLine($"[ProfilingRunner] Parsing recommendation manifest");
                var manifest = JsonSerializer.Deserialize<RecommendationManifest>(
                    stdout,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (manifest == null)
                {
                    Debug.WriteLine($"[ProfilingRunner] Failed to deserialize manifest");
                    throw new InvalidOperationException("Failed to parse recommendation manifest");
                }

                Debug.WriteLine($"[ProfilingRunner] Successfully parsed manifest for {manifest.ModelHfId}");
                return manifest;
            }
        }
        catch (OperationCanceledException ex)
        {
            Debug.WriteLine($"[ProfilingRunner] Operation cancelled: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilingRunner] Error during profiling: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Checks if model-assessor is available on the system.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            Debug.WriteLine($"[ProfilingRunner] Checking if model-assessor is available");

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "-m model_assessor.cli --version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                if (!process.WaitForExit(5000)) // 5 second timeout for version check
                {
                    Debug.WriteLine($"[ProfilingRunner] Version check timed out");
                    try { process.Kill(); } catch { }
                    return false;
                }

                var available = process.ExitCode == 0;
                Debug.WriteLine($"[ProfilingRunner] model-assessor available: {available}");
                return await Task.FromResult(available);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilingRunner] Error checking availability: {ex.Message}");
            return await Task.FromResult(false);
        }
    }

    private static string EscapeArg(string arg)
    {
        if (arg.Contains(" "))
        {
            return $"\"{arg}\"";
        }
        return arg;
    }
}
