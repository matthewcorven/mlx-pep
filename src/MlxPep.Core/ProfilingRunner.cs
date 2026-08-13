namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// Abstraction for delegating to the Python model-assessor profiling pipeline.
/// 
/// The model-assessor produces a recommendation manifest with performance data
/// across different optimization levels. ProfilingRunner shells out to it,
/// captures the recommendation output, and maps it to mlx-pep's tiered profiles.
/// 
/// Issue #17: assess command delegates to model-assessor, emits 3 tiers.
/// </summary>
public interface IProfilingRunner
{
    /// <summary>
    /// Runs the profiling pipeline for a model.
    /// </summary>
    /// <param name="hfId">Hugging Face model ID (e.g., "meta-llama/Llama-2-7b")</param>
    /// <param name="assistantModelId">Optional assistant model ID for evaluation</param>
    /// <param name="suite">Test suite: "smoke" (quick) or "full" (comprehensive)</param>
    /// <returns>Recommendation manifest with profiling results</returns>
    Task<RecommendationManifest> RunProfilingAsync(string hfId, string? assistantModelId = null, string suite = "smoke");
}

/// <summary>
/// Result from model-assessor profiling pipeline.
/// Represents recommendations across optimization levels.
/// </summary>
public record RecommendationManifest(
    [property: JsonPropertyName("modelHfId")]
    string ModelHfId,

    [property: JsonPropertyName("suite")]
    string Suite,

    [property: JsonPropertyName("timestamp")]
    string Timestamp,

    [property: JsonPropertyName("recommendations")]
    List<RecommendationTier> Recommendations);

/// <summary>
/// Individual recommendation for a specific optimization tier.
/// </summary>
public record RecommendationTier(
    [property: JsonPropertyName("tier")]
    string Tier, // "high-performance", "balanced", "efficient"

    [property: JsonPropertyName("omlxSettings")]
    Dictionary<string, object> OMLXSettings,

    [property: JsonPropertyName("harnessSettings")]
    Dictionary<string, object> HarnessSettings,

    [property: JsonPropertyName("samplerSettings")]
    Dictionary<string, object>? SamplerSettings = null,

    [property: JsonPropertyName("evidence")]
    ProfilingEvidence? Evidence = null);

/// <summary>
/// Performance evidence from profiling run.
/// </summary>
public record ProfilingEvidence(
    [property: JsonPropertyName("throughput")]
    double? Throughput, // tokens/second

    [property: JsonPropertyName("latency")]
    double? Latency, // ms per token

    [property: JsonPropertyName("memoryPeak")]
    double? MemoryPeak, // GB

    [property: JsonPropertyName("testCount")]
    int TestCount);

/// <summary>
/// Production implementation of IProfilingRunner.
/// Shells out to the Python model-assessor subprocess.
/// </summary>
public class ProfilingRunner : IProfilingRunner
{
    private readonly string _modelAssessorCommand;
    private readonly string _pythonExecutable;

    public ProfilingRunner(string? pythonExecutable = null, string? modelAssessorCommand = null)
    {
        _pythonExecutable = pythonExecutable ?? "python3";
        _modelAssessorCommand = modelAssessorCommand ?? "model-assessor";
    }

    public async Task<RecommendationManifest> RunProfilingAsync(
        string hfId,
        string? assistantModelId = null,
        string suite = "smoke")
    {
        System.Diagnostics.Debug.WriteLine($"[ProfilingRunner] Starting profiling for hfId={hfId}, suite={suite}");

        // Build command: python3 -m model_assessor --hf-id <id> --suite <suite> [--assistant-model-id <id>] --json
        var args = new List<string>
        {
            "-m", "model_assessor",
            "--hf-id", hfId,
            "--suite", suite,
            "--json"
        };

        if (!string.IsNullOrWhiteSpace(assistantModelId))
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilingRunner] Adding assistant model: {assistantModelId}");
            args.Add("--assistant-model-id");
            args.Add(assistantModelId);
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                Arguments = string.Join(" ", args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            System.Diagnostics.Debug.WriteLine($"[ProfilingRunner] Launching: {_pythonExecutable} {psi.Arguments}");

            using var process = Process.Start(psi);
            if (process == null)
            {
                System.Diagnostics.Debug.WriteLine("[ProfilingRunner] Failed to start process");
                throw new InvalidOperationException("Failed to start model-assessor process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilingRunner] Process exited with code {process.ExitCode}: {error}");
                throw new InvalidOperationException($"model-assessor failed: {error}");
            }

            System.Diagnostics.Debug.WriteLine($"[ProfilingRunner] Process completed successfully");

            // Parse JSON output
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var manifest = JsonSerializer.Deserialize<RecommendationManifest>(output, options);

            if (manifest == null)
            {
                System.Diagnostics.Debug.WriteLine("[ProfilingRunner] Failed to deserialize recommendation manifest");
                throw new InvalidOperationException("Failed to parse model-assessor output");
            }

            System.Diagnostics.Debug.WriteLine($"[ProfilingRunner] Parsed manifest with {manifest.Recommendations.Count} tiers");
            return manifest;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilingRunner] Exception during profiling: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Test fixture implementation that returns canned recommendation data.
/// Used for testing without requiring the actual model-assessor subprocess.
/// </summary>
public class FixtureProfilingRunner : IProfilingRunner
{
    private readonly string _hfId;
    private readonly string _suite;

    public FixtureProfilingRunner(string? hfId = null, string? suite = null)
    {
        _hfId = hfId ?? "meta-llama/Llama-2-7b";
        _suite = suite ?? "smoke";
    }

    public Task<RecommendationManifest> RunProfilingAsync(
        string hfId,
        string? assistantModelId = null,
        string suite = "smoke")
    {
        System.Diagnostics.Debug.WriteLine($"[FixtureProfilingRunner] Returning fixture data for {hfId}");

        // Return canned recommendation data for testing
        var manifest = new RecommendationManifest(
            ModelHfId: hfId,
            Suite: suite,
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new List<RecommendationTier>
            {
                new RecommendationTier(
                    Tier: "high-performance",
                    OMLXSettings: new Dictionary<string, object>
                    {
                        { "compute_units", "ALL" },
                        { "dtype", "bfloat16" }
                    },
                    HarnessSettings: new Dictionary<string, object>
                    {
                        { "maxInputTokens", 128000 },
                        { "maxOutputTokens", 8000 }
                    },
                    SamplerSettings: new Dictionary<string, object>
                    {
                        { "temperature", 0.7 },
                        { "topP", 0.9 }
                    },
                    Evidence: new ProfilingEvidence(
                        Throughput: 45.2,
                        Latency: 22.1,
                        MemoryPeak: 14.5,
                        TestCount: 10
                    )
                ),
                new RecommendationTier(
                    Tier: "balanced",
                    OMLXSettings: new Dictionary<string, object>
                    {
                        { "compute_units", "GPU" },
                        { "dtype", "float32" }
                    },
                    HarnessSettings: new Dictionary<string, object>
                    {
                        { "maxInputTokens", 64000 },
                        { "maxOutputTokens", 4000 }
                    },
                    SamplerSettings: new Dictionary<string, object>
                    {
                        { "temperature", 0.7 }
                    },
                    Evidence: new ProfilingEvidence(
                        Throughput: 32.1,
                        Latency: 31.2,
                        MemoryPeak: 10.2,
                        TestCount: 10
                    )
                ),
                new RecommendationTier(
                    Tier: "efficient",
                    OMLXSettings: new Dictionary<string, object>
                    {
                        { "compute_units", "CPU_AND_GPU" },
                        { "dtype", "float16" }
                    },
                    HarnessSettings: new Dictionary<string, object>
                    {
                        { "maxInputTokens", 32000 },
                        { "maxOutputTokens", 2000 }
                    },
                    SamplerSettings: null,
                    Evidence: new ProfilingEvidence(
                        Throughput: 18.5,
                        Latency: 54.0,
                        MemoryPeak: 6.8,
                        TestCount: 10
                    )
                )
            }
        );

        return Task.FromResult(manifest);
    }
}
