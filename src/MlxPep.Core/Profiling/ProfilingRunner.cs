namespace MlxPep.Core.Profiling;

using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Runs model-assessor subprocess to generate recommendation manifests.
/// Handles subprocess lifecycle, timeout, and JSON parsing.
/// </summary>
public class ProfilingRunner
{
    private const int DefaultTimeoutMinutes = 30;

    public async Task<bool> IsAvailableAsync()
    {
        Debug.WriteLine("[ProfilingRunner] Checking model-assessor availability");
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await RunProcessAsync(
                "python3",
                "-m model_assessor.cli --version",
                cts.Token);

            var available = result.ExitCode == 0;
            Debug.WriteLine($"[ProfilingRunner] Model-assessor available: {available}");
            return available;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[ProfilingRunner] Version check timeout");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilingRunner] Availability check failed: {ex.Message}");
            return false;
        }
    }

    public async Task<RecommendationManifest> RunProfilingAsync(
        string modelHfId,
        string? assistantModelId = null,
        string suite = "full")
    {
        Debug.WriteLine($"[ProfilingRunner] Starting profiling for {modelHfId} (suite={suite})");
        
        if (string.IsNullOrWhiteSpace(modelHfId))
            throw new ArgumentException("Model HF ID cannot be empty", nameof(modelHfId));

        try
        {
            var args = $"-m model_assessor.cli assess {modelHfId} --suite {suite} --output json";
            
            if (!string.IsNullOrWhiteSpace(assistantModelId))
            {
                Debug.WriteLine($"[ProfilingRunner] Using assistant model: {assistantModelId}");
                args += $" --assistant-model-id {assistantModelId}";
            }

            using var cts = new System.Threading.CancellationTokenSource(
                TimeSpan.FromMinutes(DefaultTimeoutMinutes));

            var result = await RunProcessAsync("python3", args, cts.Token);

            if (result.ExitCode != 0)
            {
                Debug.WriteLine($"[ProfilingRunner] Process failed with exit code {result.ExitCode}");
                Debug.WriteLine($"[ProfilingRunner] stderr: {result.Stderr}");
                throw new InvalidOperationException(
                    $"Model-assessor failed: {result.Stderr}");
            }

            Debug.WriteLine("[ProfilingRunner] Parsing JSON manifest");
            var manifest = JsonSerializer.Deserialize<RecommendationManifest>(
                result.Stdout,
                ProfileJsonSerializerContext.Default.RecommendationManifest);

            if (manifest == null)
                throw new InvalidOperationException("Failed to parse recommendation manifest");

            Debug.WriteLine($"[ProfilingRunner] Successfully parsed manifest with {manifest.Recommendations.Count} tiers");
            return manifest;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[ProfilingRunner] Profiling timeout after {DefaultTimeoutMinutes} minutes");
            throw new InvalidOperationException(
                $"Profiling timeout after {DefaultTimeoutMinutes} minutes");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilingRunner] Exception: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string fileName,
        string arguments,
        System.Threading.CancellationToken ct)
    {
        Debug.WriteLine($"[ProfilingRunner] Starting process: {fileName} {arguments}");
        
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            var completed = await Task.WhenAny(
                Task.WhenAll(stdoutTask, stderrTask),
                Task.Delay(Timeout.Infinite, ct));

            if (completed == stdoutTask.ContinueWith(_ => (object?)null))
            {
                // Output tasks completed normally
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                process.WaitForExit();
                Debug.WriteLine($"[ProfilingRunner] Process exited with code {process.ExitCode}");
                return (process.ExitCode, stdout, stderr);
            }
            else
            {
                // Timeout via cancellation token
                Debug.WriteLine("[ProfilingRunner] Process timeout, killing");
                process.Kill(true);
                throw new OperationCanceledException("Process timeout");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[ProfilingRunner] Cancellation during process wait");
            try { process.Kill(true); } catch { }
            throw;
        }
    }
}
