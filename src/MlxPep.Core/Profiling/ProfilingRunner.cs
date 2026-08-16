namespace MlxPep.Core.Profiling;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MlxPep.Core.Detectors;
using MlxPep.Core.Python;

/// <summary>
/// Runs model-assessor subprocess to generate recommendation manifests.
/// Handles subprocess lifecycle, timeout, and JSON parsing.
/// </summary>
public class ProfilingRunner
{
    private const int DefaultTimeoutMinutes = 30;
    private static readonly HashSet<string> RecommendedOmlxSettingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "max_context_window",
        "max_tokens",
        "temperature",
        "top_p",
        "top_k",
        "min_p",
        "repetition_penalty",
        "presence_penalty",
        "force_sampling",
        "mtp_enabled",
        "vlm_mtp_enabled",
        "vlm_mtp_draft_model",
        "mtp_num_draft_tokens",
        "thinking_budget_enabled",
        "thinking_budget_tokens",
        "trust_remote_code",
        "max_tool_result_tokens"
    };
    private static readonly string AssessmentScriptPath = Path.Combine(
        PythonEnvironmentManager.GetModelAssessorScriptsPath(),
        "next_phase",
        "run_assessment.py");
    private static readonly string RecommendationScriptPath = Path.Combine(
        PythonEnvironmentManager.GetModelAssessorScriptsPath(),
        "next_phase",
        "generate_recommendation_report.py");
    private static readonly string ClientConfigScriptPath = Path.Combine(
        PythonEnvironmentManager.GetModelAssessorScriptsPath(),
        "next_phase",
        "generate_client_config_artifacts.py");

    public string? LastClientConfigArtifactDirectory { get; private set; }

    public virtual async Task<bool> IsAvailableAsync()
    {
        Debug.WriteLine("[ProfilingRunner] Checking model-assessor availability");
        
        // Check if model-assessor directory exists with benchmark scripts
        var scriptsPath = PythonEnvironmentManager.GetModelAssessorScriptsPath();
        if (!Directory.Exists(scriptsPath))
        {
            Debug.WriteLine("[ProfilingRunner] Model-assessor scripts directory not found");
            return false;
        }

        if (!File.Exists(AssessmentScriptPath))
        {
            Debug.WriteLine($"[ProfilingRunner] Assessment script not found at {AssessmentScriptPath}");
            return false;
        }

        if (!File.Exists(RecommendationScriptPath))
        {
            Debug.WriteLine($"[ProfilingRunner] Recommendation script not found at {RecommendationScriptPath}");
            return false;
        }

        Debug.WriteLine("[ProfilingRunner] Model-assessor scripts located, checking Python environment");
        
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await RunProcessAsync(
                "python3",
                $"{QuoteArgument(AssessmentScriptPath)} --help",
                cts.Token);

            var available = result.ExitCode == 0;
            Debug.WriteLine($"[ProfilingRunner] Model-assessor scripts available: {available}");
            return available;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[ProfilingRunner] Availability check timeout");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilingRunner] Availability check failed: {ex.Message}");
            return false;
        }
    }

    public virtual async Task<AssessmentRunResult> RunProfilingAsync(
        string modelHfId,
        string? assistantModelId = null,
        string suite = "full",
        string? topologyManifestPath = null)
    {
        Debug.WriteLine($"[ProfilingRunner] Starting profiling for {modelHfId} (suite={suite})");
        
        if (string.IsNullOrWhiteSpace(modelHfId))
            throw new ArgumentException("Model HF ID cannot be empty", nameof(modelHfId));

        try
        {
            LastClientConfigArtifactDirectory = null;

            if (!File.Exists(AssessmentScriptPath))
            {
                Debug.WriteLine($"[ProfilingRunner] Assessment script missing at {AssessmentScriptPath}");
                throw new FileNotFoundException($"run_assessment.py not found at {AssessmentScriptPath}");
            }

            if (!File.Exists(RecommendationScriptPath))
            {
                Debug.WriteLine($"[ProfilingRunner] Recommendation script missing at {RecommendationScriptPath}");
                throw new FileNotFoundException($"generate_recommendation_report.py not found at {RecommendationScriptPath}");
            }

            if (!File.Exists(ClientConfigScriptPath))
            {
                Debug.WriteLine($"[ProfilingRunner] Client config script missing at {ClientConfigScriptPath}");
                throw new FileNotFoundException($"generate_client_config_artifacts.py not found at {ClientConfigScriptPath}");
            }

            var operationId = BuildOperationId();
            var runBaseDir = Path.Combine("results", "mlx-pep-cli", operationId, "runs");
            var normalizedBaseDir = Path.Combine("results", "mlx-pep-cli", operationId, "normalized");
            var recommendationBaseDir = Path.Combine("results", "mlx-pep-cli", operationId, "recommendations");
            var summaryBaseDir = Path.Combine("results", "mlx-pep-cli", operationId, "summaries");
            var clientConfigBaseDir = Path.Combine("results", "mlx-pep-cli", operationId, "client-configs");
            var mtpMode = string.IsNullOrWhiteSpace(assistantModelId) ? "off" : "profile";

            Debug.WriteLine($"[ProfilingRunner] Using operation ID {operationId}");
            Debug.WriteLine($"[ProfilingRunner] Using MTP mode {mtpMode}");

            // Use provided topology manifest or generate a single-instance one
            string resolvedTopologyManifestPath;
            if (!string.IsNullOrWhiteSpace(topologyManifestPath))
            {
                if (!File.Exists(topologyManifestPath))
                {
                    throw new FileNotFoundException($"Topology manifest file not found: {topologyManifestPath}");
                }
                resolvedTopologyManifestPath = Path.GetFullPath(topologyManifestPath);
                Debug.WriteLine($"[ProfilingRunner] Using provided topology manifest at {resolvedTopologyManifestPath}");
            }
            else
            {
                resolvedTopologyManifestPath = CreateSingleInstanceTopologyManifest(
                    operationId,
                    suite,
                    mtpMode,
                    assistantModelId);
                Debug.WriteLine($"[ProfilingRunner] Generated single-instance topology manifest at {resolvedTopologyManifestPath}");
            }

            var assessmentModelId = await ResolveAssessmentModelIdAsync(modelHfId, cts: default);
            if (assessmentModelId.Equals(modelHfId, StringComparison.Ordinal))
            {
                Debug.WriteLine($"[ProfilingRunner] Using requested model ID {assessmentModelId} for assessment");
            }
            else
            {
                Debug.WriteLine($"[ProfilingRunner] Resolved requested model ID {modelHfId} to oMLX model ID {assessmentModelId}");
            }

            var args =
                $"{QuoteArgument(AssessmentScriptPath)} --model-id {QuoteArgument(assessmentModelId)} --suite {QuoteArgument(suite)} --mtp {QuoteArgument(mtpMode)} --results-dir {QuoteArgument(runBaseDir)} --topology-manifest {QuoteArgument(resolvedTopologyManifestPath)}";
            
            if (!string.IsNullOrWhiteSpace(assistantModelId))
            {
                Debug.WriteLine($"[ProfilingRunner] Using assistant model: {assistantModelId}");
                args += $" --assistant-model-id {QuoteArgument(assistantModelId)}";
            }
            else
            {
                Debug.WriteLine("[ProfilingRunner] No assistant model specified");
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

            var modelAssessorRoot = PythonEnvironmentManager.GetModelAssessorRootPath();
            var runManifestPath = FindSingleArtifact(modelAssessorRoot, runBaseDir, "run_manifest.json");
            var runManifestJson = File.ReadAllText(runManifestPath);
            var runResult = ParseRunManifest(runManifestJson);

            if (!runResult.IsSuccess)
            {
                Debug.WriteLine($"[ProfilingRunner] Assessment run {runResult.RunId} completed with non-success status {runResult.Status}");
                throw new InvalidOperationException(
                    $"Model-assessor run ended with non-success status '{runResult.Status}'");
            }

            ValidateBenchmarkResults(modelAssessorRoot, runManifestJson);
            Debug.WriteLine($"[ProfilingRunner] Assessment run {runResult.RunId} completed with status {runResult.Status}");

            var recommendationArgs =
                $"-m scripts.next_phase.generate_recommendation_report --model-id {QuoteArgument(assessmentModelId)} --run-id {QuoteArgument(runResult.RunId)} --runs-dir {QuoteArgument(runBaseDir)} --normalized-dir {QuoteArgument(normalizedBaseDir)} --recommendations-dir {QuoteArgument(recommendationBaseDir)} --summaries-dir {QuoteArgument(summaryBaseDir)}";

            if (!string.IsNullOrWhiteSpace(assistantModelId))
            {
                Debug.WriteLine("[ProfilingRunner] Passing assistant model to recommendation generator");
                recommendationArgs += $" --assistant-model-id {QuoteArgument(assistantModelId)}";
            }
            else
            {
                Debug.WriteLine("[ProfilingRunner] Recommendation generator will run without assistant model filter");
            }

            var recommendationResult = await RunProcessAsync("python3", recommendationArgs, cts.Token);

            if (recommendationResult.ExitCode != 0)
            {
                Debug.WriteLine($"[ProfilingRunner] Recommendation generator failed with exit code {recommendationResult.ExitCode}");
                Debug.WriteLine($"[ProfilingRunner] Recommendation stderr: {recommendationResult.Stderr}");
                throw new InvalidOperationException(
                    $"Recommendation generation failed: {recommendationResult.Stderr}");
            }

            var normalizedManifestPath = FindSingleArtifact(modelAssessorRoot, normalizedBaseDir, "normalized_manifest.json");
            var recommendationManifestPath = FindSingleArtifact(modelAssessorRoot, recommendationBaseDir, "recommendation_manifest.json");

            Debug.WriteLine($"[ProfilingRunner] Normalized manifest: {normalizedManifestPath}");
            Debug.WriteLine($"[ProfilingRunner] Recommendation manifest: {recommendationManifestPath}");

            var clientConfigArgs =
                $"-m scripts.next_phase.generate_client_config_artifacts --recommendation-manifest {QuoteArgument(GetRelativePath(modelAssessorRoot, recommendationManifestPath))} --client-configs-dir {QuoteArgument(clientConfigBaseDir)}";
            var clientConfigResult = await RunProcessAsync("python3", clientConfigArgs, cts.Token);

            if (clientConfigResult.ExitCode != 0)
            {
                Debug.WriteLine($"[ProfilingRunner] Client config generator failed with exit code {clientConfigResult.ExitCode}");
                Debug.WriteLine($"[ProfilingRunner] Client config stderr: {clientConfigResult.Stderr}");
                throw new InvalidOperationException(
                    $"Client config generation failed: {clientConfigResult.Stderr}");
            }

            LastClientConfigArtifactDirectory = ReadRequiredString(clientConfigResult.Stdout, "artifact_dir");
            Debug.WriteLine($"[ProfilingRunner] Client config artifacts generated at {LastClientConfigArtifactDirectory}");

            var manifest = BuildRecommendationManifestFromArtifacts(
                modelHfId,
                assessmentModelId,
                normalizedManifestPath,
                recommendationManifestPath);

            if (manifest == null)
                throw new InvalidOperationException("Failed to parse recommendation manifest");

            Debug.WriteLine($"[ProfilingRunner] Successfully parsed manifest with {manifest.Recommendations.Count} tiers");
            return new AssessmentRunResult(
                OperationId: operationId,
                RunId: runResult.RunId,
                ModelId: runResult.ModelId,
                Status: runResult.Status,
                Suite: runResult.Suite,
                MtpMode: runResult.MtpMode,
                CreatedAt: runResult.CreatedAt,
                RecommendationManifest: manifest);
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

        // Set working directory to model-assessor root so relative paths work
        var modelAssessorRoot = PythonEnvironmentManager.GetModelAssessorRootPath();
        if (Directory.Exists(modelAssessorRoot))
        {
            psi.WorkingDirectory = modelAssessorRoot;
            Debug.WriteLine($"[ProfilingRunner] Set working directory to {modelAssessorRoot}");
        }

        // Load .env file environment variables into subprocess
        LoadDotEnv(psi.Environment);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var outputTask = Task.WhenAll(stdoutTask, stderrTask);

        try
        {
            var completed = await Task.WhenAny(
                outputTask,
                Task.Delay(Timeout.Infinite, ct));

            if (completed == outputTask)
            {
                Debug.WriteLine("[ProfilingRunner] Process output completed before timeout");
                // Output tasks completed normally
                await outputTask;
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

    /// <summary>
    /// Loads .env file into the environment dictionary for subprocess.
    /// Reads from .env at repo root or build output directory.
    /// </summary>
    private static void LoadDotEnv(System.Collections.Generic.IDictionary<string, string?> environment)
    {
        // Try build output directory first (where .env gets copied)
        var dotEnvPaths = new[]
        {
            ".env",
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(PythonEnvironmentManager.GetModelAssessorRootPath(), "..", ".env")
        };

        foreach (var path in dotEnvPaths)
        {
            if (!File.Exists(path))
                continue;

            Debug.WriteLine($"[ProfilingRunner] Loading .env from {path}");
            
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        if (key.StartsWith("export ", StringComparison.Ordinal))
                        {
                            key = key.Substring("export ".Length).Trim();
                            Debug.WriteLine($"[ProfilingRunner] Parsed exported environment key {key}");
                        }

                        var value = parts[1].Trim().Trim('"', '\'');
                        
                        // Expand ~ to home directory
                        if (value.StartsWith("~/", StringComparison.Ordinal))
                        {
                            value = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                value.Substring(2));
                            Debug.WriteLine($"[ProfilingRunner] Expanded home-relative environment value for {key}");
                        }
                        else if (value == "~")
                        {
                            value = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            Debug.WriteLine($"[ProfilingRunner] Expanded bare home-directory environment value for {key}");
                        }
                        else
                        {
                            Debug.WriteLine($"[ProfilingRunner] Using literal environment value for {key}");
                        }
                        
                        environment[key] = value;
                        Debug.WriteLine($"[ProfilingRunner] Set {key}={FormatEnvironmentValueForLog(key, value)}");
                    }
                    else
                    {
                        Debug.WriteLine($"[ProfilingRunner] Skipping malformed .env line: {line}");
                    }
                }
                
                return; // Successfully loaded, stop looking
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilingRunner] Failed to load .env from {path}: {ex.Message}");
            }
        }
    }

    private static string FormatEnvironmentValueForLog(string key, string value)
    {
        return key.Contains("KEY", StringComparison.OrdinalIgnoreCase)
            || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
            || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
            ? "<redacted>"
            : value;
    }

    private static string BuildOperationId()
    {
        return $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
    }

    private static string CreateSingleInstanceTopologyManifest(
        string operationId,
        string suite,
        string mtpMode,
        string? assistantModelId)
    {
        Debug.WriteLine($"[ProfilingRunner] Creating single-instance topology manifest for suite {suite}");

        var modelAssessorRoot = PythonEnvironmentManager.GetModelAssessorRootPath();
        var benchmarkProfilesPath = Path.Combine(modelAssessorRoot, "config", "benchmark_profiles.json");
        var smokeSuitePath = Path.Combine(modelAssessorRoot, "config", "smoke_suite.json");

        using var benchmarkProfiles = JsonDocument.Parse(File.ReadAllText(benchmarkProfilesPath));
        var selectedProfileIds = ReadSelectedProfileIds(suite, benchmarkProfiles.RootElement, smokeSuitePath);
        var profilesById = ReadProfilesById(benchmarkProfiles.RootElement);

        var baseUrl = ResolveOmlxBaseUrl();
        var port = new Uri(baseUrl).Port;
        var workloadMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var instances = new List<string>();

        foreach (var profileId in selectedProfileIds)
        {
            if (!profilesById.TryGetValue(profileId, out var profile))
            {
                Debug.WriteLine($"[ProfilingRunner] Selected profile {profileId} was not found in benchmark_profiles.json");
                throw new InvalidOperationException($"Profile '{profileId}' was not found in benchmark_profiles.json");
            }

            workloadMap[profile.Workload] = "instance-1";
            instances.Add(BuildTopologyInstanceJson(profile, baseUrl, port, mtpMode, assistantModelId));
        }

        var topologyJson = new StringBuilder();
        topologyJson.AppendLine("{");
        topologyJson.AppendLine("  \"instance_topology\": {");
        topologyJson.AppendLine("    \"instance_mode\": \"single\",");
        topologyJson.AppendLine("    \"instance_count\": 1,");
        topologyJson.AppendLine("    \"instances\": [");
        topologyJson.AppendLine(string.Join(",\n", instances.Select(instance => $"      {instance}")));
        topologyJson.AppendLine("    ],");
        topologyJson.AppendLine("    \"workload_to_instance\": {");
        topologyJson.AppendLine(string.Join(",\n", workloadMap.Select(kvp => $"      {JsonSerializer.Serialize(kvp.Key)}: {JsonSerializer.Serialize(kvp.Value)}")));
        topologyJson.AppendLine("    },");
        topologyJson.AppendLine("    \"instance_topology_summary\": \"Single hosted instance is sufficient for CLI assessment runs.\"");
        topologyJson.AppendLine("  }");
        topologyJson.AppendLine("}");

        var relativePath = Path.Combine("results", "mlx-pep-cli", operationId, "topology_manifest.json");
        var absolutePath = Path.Combine(modelAssessorRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllText(absolutePath, topologyJson.ToString());

        return relativePath.Replace('\\', '/');
    }

    private static List<string> ReadSelectedProfileIds(string suite, JsonElement benchmarkProfilesRoot, string smokeSuitePath)
    {
        if (string.Equals(suite, "full", StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine("[ProfilingRunner] Selecting all benchmark profiles for full suite");
            return benchmarkProfilesRoot
                .GetProperty("profiles")
                .EnumerateArray()
                .Select(profile => profile.GetProperty("id").GetString())
                .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
                .Select(profileId => profileId!)
                .ToList();
        }

        if (string.Equals(suite, "smoke", StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine("[ProfilingRunner] Selecting smoke suite profiles");
            using var smokeSuite = JsonDocument.Parse(File.ReadAllText(smokeSuitePath));
            return smokeSuite.RootElement
                .GetProperty("profiles")
                .EnumerateArray()
                .Select(profile => profile.GetString())
                .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
                .Select(profileId => profileId!)
                .ToList();
        }

        Debug.WriteLine($"[ProfilingRunner] Unsupported suite '{suite}' for single-instance topology generation");
        throw new InvalidOperationException($"Unsupported assessment suite '{suite}'");
    }

    private static Dictionary<string, BenchmarkProfile> ReadProfilesById(JsonElement benchmarkProfilesRoot)
    {
        var profiles = new Dictionary<string, BenchmarkProfile>(StringComparer.OrdinalIgnoreCase);

        foreach (var profileElement in benchmarkProfilesRoot.GetProperty("profiles").EnumerateArray())
        {
            var id = profileElement.GetProperty("id").GetString();
            var workload = profileElement.GetProperty("workload").GetString();
            var mtpEnabled = false;

            if (profileElement.TryGetProperty("settings", out var settingsElement) && settingsElement.ValueKind == JsonValueKind.Object)
            {
                mtpEnabled = ReadBooleanSetting(settingsElement, "mtp_enabled") || ReadBooleanSetting(settingsElement, "vlm_mtp_enabled");
            }
            else
            {
                Debug.WriteLine($"[ProfilingRunner] Profile {id} has no settings object; defaulting MTP to false");
            }

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(workload))
            {
                profiles[id!] = new BenchmarkProfile(id!, workload!, mtpEnabled);
                Debug.WriteLine($"[ProfilingRunner] Loaded benchmark profile {id} ({workload})");
            }
            else
            {
                Debug.WriteLine("[ProfilingRunner] Skipping benchmark profile with missing id or workload");
            }
        }

        return profiles;
    }

    private static bool ReadBooleanSetting(JsonElement settingsElement, string propertyName)
    {
        if (settingsElement.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            Debug.WriteLine($"[ProfilingRunner] Read boolean setting {propertyName}={property.GetBoolean()}");
            return property.GetBoolean();
        }

        Debug.WriteLine($"[ProfilingRunner] Boolean setting {propertyName} missing; defaulting to false");
        return false;
    }

    private static string BuildTopologyInstanceJson(
        BenchmarkProfile profile,
        string baseUrl,
        int port,
        string mtpMode,
        string? assistantModelId)
    {
        var mtpEnabled = string.Equals(mtpMode, "off", StringComparison.OrdinalIgnoreCase)
            ? false
            : profile.MtpEnabled;

        var assistantValue = mtpEnabled && !string.IsNullOrWhiteSpace(assistantModelId)
            ? JsonSerializer.Serialize(assistantModelId)
            : "null";

        Debug.WriteLine($"[ProfilingRunner] Topology entry for {profile.ProfileId} will use mtp_enabled={mtpEnabled}");

        return "{" +
            $"\"instance_id\":\"instance-1\"," +
            $"\"port\":{port}," +
            $"\"base_url\":{JsonSerializer.Serialize(baseUrl)}," +
            $"\"workload\":{JsonSerializer.Serialize(profile.Workload)}," +
            $"\"profile_id\":{JsonSerializer.Serialize(profile.ProfileId)}," +
            $"\"mtp_enabled\":{mtpEnabled.ToString().ToLowerInvariant()}," +
            $"\"assistant_model_id\":{assistantValue}," +
            "\"reason\":\"CLI assess runs pin all selected profiles to the current local oMLX instance.\"" +
            "}";
    }

    private static async Task<string> ResolveAssessmentModelIdAsync(string requestedModelId, System.Threading.CancellationToken cts)
    {
        Debug.WriteLine($"[ProfilingRunner] Resolving requested model ID {requestedModelId} against live oMLX inventory");

        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var baseUrl = ResolveOmlxBaseUrl();
            var modelsUrl = $"{baseUrl.TrimEnd('/')}/admin/api/models";
            var responseBody = await httpClient.GetStringAsync(modelsUrl, cts);

            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
            {
                Debug.WriteLine("[ProfilingRunner] oMLX model inventory response does not contain a models array");
                return requestedModelId;
            }

            var modelEntries = modelsElement.EnumerateArray().ToList();

            foreach (var modelEntry in modelEntries)
            {
                var id = GetJsonString(modelEntry, "id");
                if (string.Equals(id, requestedModelId, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[ProfilingRunner] Found exact oMLX model ID match for {requestedModelId}");
                    return id ?? requestedModelId;
                }
            }

            foreach (var modelEntry in modelEntries)
            {
                var id = GetJsonString(modelEntry, "id");
                var sourceRepoId = GetJsonString(modelEntry, "source_repo_id");
                var modelPath = GetJsonString(modelEntry, "model_path");

                if (string.Equals(sourceRepoId, requestedModelId, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[ProfilingRunner] Matched requested model ID {requestedModelId} via source_repo_id");
                    return id ?? requestedModelId;
                }

                if (!string.IsNullOrWhiteSpace(modelPath) &&
                    modelPath.Replace('\\', '/').EndsWith($"/{requestedModelId}", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[ProfilingRunner] Matched requested model ID {requestedModelId} via model_path suffix");
                    return id ?? requestedModelId;
                }
            }

            var requestedLeafName = requestedModelId.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (!string.IsNullOrWhiteSpace(requestedLeafName))
            {
                foreach (var modelEntry in modelEntries)
                {
                    var id = GetJsonString(modelEntry, "id");
                    var displayName = GetJsonString(modelEntry, "display_name");

                    if (string.Equals(id, requestedLeafName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(displayName, requestedLeafName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[ProfilingRunner] Matched requested model ID {requestedModelId} via leaf name {requestedLeafName}");
                        return id ?? requestedLeafName;
                    }
                }
            }
            else
            {
                Debug.WriteLine("[ProfilingRunner] Requested model ID has no leaf segment to try as a fallback");
            }

            Debug.WriteLine($"[ProfilingRunner] No oMLX inventory match found for {requestedModelId}; using requested value");
            return requestedModelId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilingRunner] Failed to resolve oMLX model ID: {ex.Message}");
            return requestedModelId;
        }
    }

    private static string ResolveOmlxBaseUrl()
    {
        var baseUrl = Environment.GetEnvironmentVariable("OMLX_BASE_URL");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            Debug.WriteLine("[ProfilingRunner] Using OMLX_BASE_URL from environment");
            return baseUrl;
        }

        Debug.WriteLine("[ProfilingRunner] OMLX_BASE_URL not set; using default http://127.0.0.1:8000");
        return "http://127.0.0.1:8000";
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            Debug.WriteLine($"[ProfilingRunner] Read string property '{propertyName}' from oMLX inventory payload");
            return property.GetString();
        }

        Debug.WriteLine($"[ProfilingRunner] Property '{propertyName}' missing from oMLX inventory payload");
        return null;
    }

    private static string FindSingleArtifact(string modelAssessorRoot, string relativeBaseDir, string fileName)
    {
        var baseDir = Path.Combine(modelAssessorRoot, relativeBaseDir);
        Debug.WriteLine($"[ProfilingRunner] Searching for {fileName} under {baseDir}");

        if (!Directory.Exists(baseDir))
        {
            Debug.WriteLine($"[ProfilingRunner] Artifact base directory missing: {baseDir}");
            throw new DirectoryNotFoundException($"Expected artifact directory not found: {baseDir}");
        }

        var matches = Directory.GetFiles(baseDir, fileName, SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            Debug.WriteLine($"[ProfilingRunner] No {fileName} artifacts found under {baseDir}");
            throw new FileNotFoundException($"Expected artifact '{fileName}' was not generated under {baseDir}");
        }

        if (matches.Length > 1)
        {
            Debug.WriteLine($"[ProfilingRunner] Multiple {fileName} artifacts found; selecting newest");
            return matches
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .First();
        }

        Debug.WriteLine($"[ProfilingRunner] Found artifact {matches[0]}");
        return matches[0];
    }

    private static RecommendationManifest BuildRecommendationManifestFromArtifacts(
        string modelHfId,
        string assessmentModelId,
        string normalizedManifestPath,
        string recommendationManifestPath)
    {
        Debug.WriteLine("[ProfilingRunner] Building CLI recommendation manifest from normalized artifacts");

        using var normalizedDocument = JsonDocument.Parse(File.ReadAllText(normalizedManifestPath));
        using var recommendationDocument = JsonDocument.Parse(File.ReadAllText(recommendationManifestPath));

        var candidates = ReadCandidates(normalizedDocument.RootElement);
        var selectedCandidates = SelectTierCandidates(recommendationDocument.RootElement, candidates);
        var hardware = DetectHardware();

        var recommendations = new Dictionary<string, TierRecommendation>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = CreateTierRecommendation(modelHfId, assessmentModelId, selectedCandidates["high"], "high"),
            ["balanced"] = CreateTierRecommendation(modelHfId, assessmentModelId, selectedCandidates["balanced"], "balanced"),
            ["efficient"] = CreateTierRecommendation(modelHfId, assessmentModelId, selectedCandidates["efficient"], "efficient")
        };

        return new RecommendationManifest(
            ModelHfId: modelHfId,
            AssessmentVersion: "1.0-workload-winner-collapse",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: recommendations,
            Hardware: hardware);
    }

    private static Dictionary<string, AssessmentCandidate> ReadCandidates(JsonElement normalizedRoot)
    {
        var candidates = new Dictionary<string, AssessmentCandidate>(StringComparer.OrdinalIgnoreCase);

        if (!normalizedRoot.TryGetProperty("candidates", out var candidateArray) || candidateArray.ValueKind != JsonValueKind.Array)
        {
            Debug.WriteLine("[ProfilingRunner] Normalized manifest does not contain a candidates array");
            throw new InvalidOperationException("Normalized manifest does not contain candidates");
        }

        foreach (var candidateElement in candidateArray.EnumerateArray())
        {
            var profileId = candidateElement.TryGetProperty("profile_id", out var profileIdElement)
                ? profileIdElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(profileId))
            {
                Debug.WriteLine("[ProfilingRunner] Skipping candidate without profile_id");
                continue;
            }

            var workload = candidateElement.TryGetProperty("workload", out var workloadElement)
                ? workloadElement.GetString() ?? string.Empty
                : string.Empty;
            var assistantModelId = candidateElement.TryGetProperty("assistant_model_id", out var assistantElement)
                ? assistantElement.GetString()
                : null;
            var settings = candidateElement.TryGetProperty("settings", out var settingsElement) && settingsElement.ValueKind == JsonValueKind.Object
                ? ConvertObject(settingsElement)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var candidate = new AssessmentCandidate(profileId, workload, assistantModelId, settings);
            candidates[profileId] = candidate;
            Debug.WriteLine($"[ProfilingRunner] Loaded normalized candidate {profileId} ({workload})");
        }

        if (candidates.Count == 0)
        {
            Debug.WriteLine("[ProfilingRunner] Normalized manifest contained zero candidates");
            throw new InvalidOperationException("Normalized manifest did not yield any candidates");
        }

        return candidates;
    }

    private static Dictionary<string, AssessmentCandidate> SelectTierCandidates(
        JsonElement recommendationRoot,
        Dictionary<string, AssessmentCandidate> candidates)
    {
        if (!recommendationRoot.TryGetProperty("recommendations", out var recommendationArray) ||
            recommendationArray.ValueKind != JsonValueKind.Array)
        {
            Debug.WriteLine("[ProfilingRunner] Recommendation manifest does not contain a recommendations array");
            throw new InvalidOperationException("Recommendation manifest does not contain recommendations");
        }

        var topRecommendations = new List<AssessmentCandidate>();

        foreach (var recommendationElement in recommendationArray.EnumerateArray())
        {
            var rank = recommendationElement.TryGetProperty("rank", out var rankElement)
                ? rankElement.GetInt32()
                : int.MaxValue;

            if (rank != 1)
            {
                Debug.WriteLine($"[ProfilingRunner] Skipping non-top recommendation with rank {rank}");
                continue;
            }

            var profileId = recommendationElement.TryGetProperty("profile_id", out var profileIdElement)
                ? profileIdElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(profileId))
            {
                Debug.WriteLine("[ProfilingRunner] Skipping top recommendation without profile_id");
                continue;
            }

            if (candidates.TryGetValue(profileId, out var candidate))
            {
                Debug.WriteLine($"[ProfilingRunner] Selected top recommendation candidate {profileId}");
                topRecommendations.Add(candidate);
            }
            else
            {
                Debug.WriteLine($"[ProfilingRunner] Top recommendation candidate {profileId} missing from normalized manifest");
            }
        }

        if (topRecommendations.Count < 3)
        {
            Debug.WriteLine($"[ProfilingRunner] Expected at least 3 top recommendations, found {topRecommendations.Count}");
            throw new InvalidOperationException(
                $"Assessment produced only {topRecommendations.Count} top recommendation(s); cannot collapse into high/balanced/efficient tiers");
        }

        var ordered = topRecommendations
            .OrderBy(candidate => candidate.MaxContextWindow)
            .ThenBy(candidate => candidate.MaxTokens)
            .ThenBy(candidate => candidate.ProfileId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Debug.WriteLine($"[ProfilingRunner] Collapsing {ordered.Count} workload recommendations into three tiers");

        return new Dictionary<string, AssessmentCandidate>(StringComparer.OrdinalIgnoreCase)
        {
            ["efficient"] = ordered.First(),
            ["balanced"] = ordered[ordered.Count / 2],
            ["high"] = ordered.Last()
        };
    }

    private static TierRecommendation CreateTierRecommendation(
        string modelHfId,
        string assessmentModelId,
        AssessmentCandidate candidate,
        string tier)
    {
        Debug.WriteLine($"[ProfilingRunner] Creating tier recommendation '{tier}' from {candidate.ProfileId}");

        var omlx = BuildOmlxSettings(candidate);
        var harness = BuildHarnessSettings(modelHfId, assessmentModelId, candidate, tier);
        var sampler = BuildSamplerSettings(candidate);
        var system = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        system["assessment_workload"] = candidate.Workload;
        system["assessment_profile_id"] = candidate.ProfileId;
        system["assessment_tier_derivation"] = "derived-from-ranked-workload-winners";
        system["assessment_model_id"] = assessmentModelId;

        if (!string.IsNullOrWhiteSpace(candidate.AssistantModelId))
        {
            Debug.WriteLine($"[ProfilingRunner] Tier '{tier}' includes assistant model {candidate.AssistantModelId}");
            system["assistant_model_id"] = candidate.AssistantModelId!;
        }
        else
        {
            Debug.WriteLine($"[ProfilingRunner] Tier '{tier}' does not include an assistant model");
        }

        return new TierRecommendation(
            Tier: tier,
            System: system,
            Omlx: omlx,
            Harness: harness,
            Sampler: sampler.Count > 0 ? sampler : null);
    }

    private static Dictionary<string, object> BuildOmlxSettings(AssessmentCandidate candidate)
    {
        var omlx = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in candidate.Settings)
        {
            if (!RecommendedOmlxSettingKeys.Contains(key))
            {
                Debug.WriteLine($"[ProfilingRunner] Skipping non-recommended oMLX setting {key}");
                continue;
            }

            if (value == null)
            {
                Debug.WriteLine($"[ProfilingRunner] Skipping null oMLX setting {key}");
                continue;
            }

            if (value is string stringValue)
            {
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    Debug.WriteLine($"[ProfilingRunner] Skipping empty-string oMLX setting {key}");
                    continue;
                }

                omlx[key] = stringValue;
                continue;
            }

            omlx[key] = value;
        }

        return omlx;
    }

    private static Dictionary<string, object> BuildHarnessSettings(
        string modelHfId,
        string assessmentModelId,
        AssessmentCandidate candidate,
        string tier)
    {
        var maxInputTokens = candidate.MaxContextWindow > 0
            ? candidate.MaxContextWindow
            : tier switch
            {
                "high" => 131072,
                "balanced" => 65536,
                _ => 16384
            };

        var maxOutputTokens = candidate.MaxTokens > 0
            ? candidate.MaxTokens
            : tier switch
            {
                "high" => 8192,
                "balanced" => 4096,
                _ => 2048
            };

        Debug.WriteLine($"[ProfilingRunner] Derived harness limits for tier '{tier}': input={maxInputTokens}, output={maxOutputTokens}");

        var baseUrl = ResolveOmlxBaseUrl().TrimEnd('/');
        var inferenceBaseUrl = $"{baseUrl}/v1";
        var modelDisplayName = $"{assessmentModelId} ({tier})";
        var modelEntry = BuildChatLanguageModelEntry(
            candidate,
            tier,
            modelDisplayName,
            assessmentModelId,
            inferenceBaseUrl,
            maxInputTokens,
            maxOutputTokens);

        var copilotConfig = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["maxPromptTokens"] = maxInputTokens,
            ["contextWindow"] = maxInputTokens,
            ["maxOutputTokens"] = maxOutputTokens,
            ["modelId"] = assessmentModelId,
            ["displayName"] = modelDisplayName,
            ["baseUrl"] = inferenceBaseUrl,
            ["topK"] = candidate.GetInt("top_k")
        };
        AddOptionalDouble(copilotConfig, "temperature", candidate.GetDouble("temperature"));
        AddOptionalDouble(copilotConfig, "topP", candidate.GetDouble("top_p"));

        var opencodeConfig = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["maxInputTokens"] = maxInputTokens,
            ["maxOutputTokens"] = maxOutputTokens,
            ["modelId"] = assessmentModelId,
            ["displayName"] = modelDisplayName,
            ["providerId"] = "omlx-local",
            ["baseUrl"] = inferenceBaseUrl,
            ["apiKeyEnv"] = "OMLX_API_KEY",
            ["topK"] = candidate.GetInt("top_k")
        };
        AddOptionalDouble(opencodeConfig, "temperature", candidate.GetDouble("temperature"));
        AddOptionalDouble(opencodeConfig, "topP", candidate.GetDouble("top_p"));

        var claudeConfig = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["maxInputTokens"] = maxInputTokens,
            ["maxOutputTokens"] = maxOutputTokens,
            ["modelId"] = assessmentModelId,
            ["displayName"] = modelDisplayName,
            ["baseUrl"] = inferenceBaseUrl,
            ["apiKeyEnv"] = "OMLX_API_KEY",
            ["topK"] = candidate.GetInt("top_k")
        };
        AddOptionalDouble(claudeConfig, "temperature", candidate.GetDouble("temperature"));
        AddOptionalDouble(claudeConfig, "topP", candidate.GetDouble("top_p"));

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["vscode"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["maxInputTokens"] = maxInputTokens,
                ["maxOutputTokens"] = maxOutputTokens,
                ["customSettings"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["inlineChat.defaultModel"] = candidate.ProfileId,
                    ["chat.utilityModel"] = candidate.ProfileId,
                    ["chat.utilitySmallModel"] = candidate.ProfileId
                },
                ["chatLanguageModels"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["models"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        [candidate.ProfileId] = modelEntry
                    }
                }
            },
            ["copilotCli"] = copilotConfig,
            ["opencode"] = opencodeConfig,
            ["claude-code"] = claudeConfig
        };
    }

    private static void AddOptionalDouble(Dictionary<string, object> target, string key, double? value)
    {
        if (value.HasValue)
        {
            target[key] = value.Value;
        }
    }

    private static Dictionary<string, object> BuildChatLanguageModelEntry(
        AssessmentCandidate candidate,
        string tier,
        string displayName,
        string assessmentModelId,
        string inferenceBaseUrl,
        int maxInputTokens,
        int maxOutputTokens)
    {
        var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["available"] = true,
            ["vendor"] = "customendpoint",
            ["name"] = displayName,
            ["modelId"] = assessmentModelId,
            ["url"] = inferenceBaseUrl,
            ["apiType"] = "chat-completions",
            ["toolCalling"] = true,
            ["maxInputTokens"] = maxInputTokens,
            ["maxOutputTokens"] = maxOutputTokens,
            ["tier"] = tier,
            ["workload"] = candidate.Workload
        };

        var temperature = candidate.GetDouble("temperature");
        if (temperature.HasValue)
        {
            entry["temperature"] = temperature.Value;
        }

        var topP = candidate.GetDouble("top_p");
        if (topP.HasValue)
        {
            entry["topP"] = topP.Value;
        }

        var topK = candidate.GetInt("top_k");
        if (topK > 0)
        {
            entry["topK"] = topK;
        }

        return entry;
    }

    private static HardwareAssessment DetectHardware()
    {
        Debug.WriteLine("[ProfilingRunner] Detecting local hardware for generated profiles");

        var hardware = new SystemDetector().Detect();
        return new HardwareAssessment(
            Chip: hardware.Chip,
            MemoryGb: hardware.MemoryGb,
            ModelIdentifier: hardware.ModelIdentifier);
    }

    private static Dictionary<string, object> BuildSamplerSettings(AssessmentCandidate candidate)
    {
        var sampler = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (candidate.Settings.TryGetValue("temperature", out var temperature))
        {
            Debug.WriteLine("[ProfilingRunner] Copying temperature into sampler settings");
            sampler["temperature"] = temperature;
        }
        else
        {
            Debug.WriteLine("[ProfilingRunner] No temperature value present in candidate settings");
        }

        if (candidate.Settings.TryGetValue("top_p", out var topP))
        {
            Debug.WriteLine("[ProfilingRunner] Copying top_p into sampler settings");
            sampler["topP"] = topP;
        }
        else
        {
            Debug.WriteLine("[ProfilingRunner] No top_p value present in candidate settings");
        }

        if (candidate.Settings.TryGetValue("top_k", out var topK))
        {
            Debug.WriteLine("[ProfilingRunner] Copying top_k into sampler settings");
            sampler["topK"] = topK;
        }
        else
        {
            Debug.WriteLine("[ProfilingRunner] No top_k value present in candidate settings");
        }

        if (candidate.Settings.TryGetValue("repetition_penalty", out var repetitionPenalty))
        {
            Debug.WriteLine("[ProfilingRunner] Copying repetition_penalty into sampler settings");
            sampler["repetitionPenalty"] = repetitionPenalty;
        }
        else
        {
            Debug.WriteLine("[ProfilingRunner] No repetition_penalty value present in candidate settings");
        }

        if (candidate.MaxContextWindow > 0)
        {
            Debug.WriteLine($"[ProfilingRunner] Copying max_context_window {candidate.MaxContextWindow} into sampler contextTokens");
            sampler["contextTokens"] = candidate.MaxContextWindow;
        }
        else
        {
            Debug.WriteLine("[ProfilingRunner] No max_context_window value present in candidate settings");
        }

        return sampler;
    }

    private static Dictionary<string, object> ConvertObject(JsonElement element)
    {
        var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = ConvertValue(property.Value);
        }

        return dictionary;
    }

    private static object ConvertValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertValue).ToList(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => string.Empty
        };
    }

    private static string GetRelativePath(string baseDirectory, string path)
    {
        return Path.GetRelativePath(baseDirectory, path).Replace('\\', '/');
    }

    public static AssessmentRunResult ParseRunManifest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var runId = GetRequiredString(root, "run_id");
        var status = GetRequiredString(root, "status");
        var modelId = GetRequiredString(root, "model_id");
        var suite = GetRequiredString(root, "suite");
        var mtpMode = GetRequiredString(root, "mtp_mode");
        var createdAt = GetRequiredString(root, "created_at");

        var recommendationManifest = new RecommendationManifest(
            ModelHfId: modelId,
            AssessmentVersion: "1.0-workload-winner-collapse",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new Dictionary<string, TierRecommendation>(StringComparer.OrdinalIgnoreCase));

        if (status != "success")
        {
            Debug.WriteLine($"[ProfilingRunner] Invalid run status '{status}' in run manifest");
            throw new InvalidOperationException($"Model-assessor run ended with non-success status '{status}'");
        }

        return new AssessmentRunResult(
            OperationId: string.Empty,
            RunId: runId,
            ModelId: modelId,
            Status: status,
            Suite: suite,
            MtpMode: mtpMode,
            CreatedAt: createdAt,
            RecommendationManifest: recommendationManifest);
    }

    public static void ValidateBenchmarkResults(string modelAssessorRootPath, string runManifestJson)
    {
        using var document = JsonDocument.Parse(runManifestJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("artifact_paths", out var artifactPaths) ||
            artifactPaths.ValueKind != JsonValueKind.Object ||
            !artifactPaths.TryGetProperty("benchmark_results", out var benchmarkResults) ||
            benchmarkResults.ValueKind != JsonValueKind.Array)
        {
            Debug.WriteLine("[ProfilingRunner] Run manifest does not contain benchmark_results artifact paths");
            throw new InvalidOperationException("Model-assessor run manifest does not contain benchmark result artifacts");
        }

        var rejectedBenchmarks = new List<string>();
        foreach (var benchmarkResult in benchmarkResults.EnumerateArray())
        {
            if (benchmarkResult.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(benchmarkResult.GetString()))
            {
                Debug.WriteLine("[ProfilingRunner] Encountered blank benchmark result artifact path in run manifest");
                throw new InvalidOperationException("Model-assessor run manifest contains an invalid benchmark result artifact path");
            }

            var benchmarkRelativePath = benchmarkResult.GetString()!;
            var benchmarkPath = Path.Combine(modelAssessorRootPath, benchmarkRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(benchmarkPath))
            {
                Debug.WriteLine($"[ProfilingRunner] Benchmark result artifact missing at {benchmarkPath}");
                throw new InvalidOperationException($"Benchmark result artifact was not found: {benchmarkRelativePath}");
            }

            using var benchmarkDocument = JsonDocument.Parse(File.ReadAllText(benchmarkPath));
            var benchmarkRoot = benchmarkDocument.RootElement;
            var benchmarkStatus = GetRequiredString(benchmarkRoot, "status");
            if (!IsAcceptedBenchmarkStatus(benchmarkStatus))
            {
                Debug.WriteLine($"[ProfilingRunner] Rejecting benchmark result {benchmarkRelativePath} with status {benchmarkStatus}");
                rejectedBenchmarks.Add($"{benchmarkRelativePath} ({benchmarkStatus})");
            }
            else
            {
                Debug.WriteLine($"[ProfilingRunner] Accepted benchmark result {benchmarkRelativePath} with status {benchmarkStatus}");
            }
        }

        if (rejectedBenchmarks.Count > 0)
        {
            Debug.WriteLine($"[ProfilingRunner] Rejecting assessment because benchmark results were incomplete: {string.Join(", ", rejectedBenchmarks)}");
            throw new InvalidOperationException(
                $"Benchmark results were rejected because they did not complete successfully: {string.Join(", ", rejectedBenchmarks)}");
        }

        Debug.WriteLine("[ProfilingRunner] All benchmark result artifacts completed successfully");
    }

    private static bool IsAcceptedBenchmarkStatus(string status)
    {
        return status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("complete", StringComparison.OrdinalIgnoreCase)
            || status.Equals("success", StringComparison.OrdinalIgnoreCase)
            || status.Equals("done", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            return property.GetString()!;
        }

        Debug.WriteLine($"[ProfilingRunner] Required property '{propertyName}' missing from JSON payload");
        throw new InvalidOperationException($"Required property '{propertyName}' missing from model-assessor output");
    }

    private static string ReadRequiredString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return GetRequiredString(root, propertyName);
    }

    private static string? ReadOptionalString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            Debug.WriteLine($"[ProfilingRunner] Optional property '{propertyName}' found in JSON payload");
            return property.GetString();
        }

        Debug.WriteLine($"[ProfilingRunner] Optional property '{propertyName}' not found in JSON payload");
        return null;
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private sealed record AssessmentCandidate(
        string ProfileId,
        string Workload,
        string? AssistantModelId,
        Dictionary<string, object> Settings)
    {
        public int MaxContextWindow => GetInt("max_context_window");

        public int MaxTokens => GetInt("max_tokens");

        public int GetInt(string key)
        {
            if (!Settings.TryGetValue(key, out var value) || value == null)
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} is missing integer setting '{key}'");
                return 0;
            }

            if (value is int intValue)
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has integer setting '{key}'={intValue}");
                return intValue;
            }

            if (value is long longValue && longValue <= int.MaxValue && longValue >= int.MinValue)
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has long setting '{key}'={longValue}");
                return (int)longValue;
            }

            if (value is string stringValue && int.TryParse(stringValue, out var parsedValue))
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has string setting '{key}'={parsedValue}");
                return parsedValue;
            }

            Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has non-integer setting '{key}'");
            return 0;
        }

        public double? GetDouble(string key)
        {
            if (!Settings.TryGetValue(key, out var value) || value == null)
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} is missing double setting '{key}'");
                return null;
            }

            if (value is double doubleValue)
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has double setting '{key}'={doubleValue}");
                return doubleValue;
            }

            if (value is int intValue)
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has int-backed double setting '{key}'={intValue}");
                return intValue;
            }

            if (value is long longValue)
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has long-backed double setting '{key}'={longValue}");
                return longValue;
            }

            if (value is string stringValue && double.TryParse(stringValue, out var parsedValue))
            {
                Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has string-backed double setting '{key}'={parsedValue}");
                return parsedValue;
            }

            Debug.WriteLine($"[ProfilingRunner] Candidate {ProfileId} has non-double setting '{key}'");
            return null;
        }
    }

    private sealed record BenchmarkProfile(string ProfileId, string Workload, bool MtpEnabled);
}
