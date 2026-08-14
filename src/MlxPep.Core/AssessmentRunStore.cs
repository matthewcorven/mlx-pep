namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MlxPep.Core.Python;

public record AssessmentRunProfileRecord(
    string ProfileId,
    string? Workload,
    bool MtpEnabled,
    string? SettingsPath,
    string? BenchmarkPath,
    IReadOnlyDictionary<string, object?> Settings,
    IReadOnlyList<AssessmentHarnessConfigGroupRecord> HarnessGroups,
    string BenchmarkStatus,
    int BenchmarkRowCount,
    IReadOnlyList<string> TestTypes,
    IReadOnlyList<int> PromptLengths,
    IReadOnlyList<int> GenerationLengths);

public record AssessmentHarnessConfigGroupRecord(
    string HarnessId,
    string HarnessDisplayName,
    string ConfigSurface,
    IReadOnlyList<AssessmentHarnessKeyValueRecord> Values);

public record AssessmentHarnessKeyValueRecord(string Key, string Value);

public record AssessmentRunRecord(
    string OperationId,
    string RunId,
    string ModelId,
    string Suite,
    string Status,
    string MtpMode,
    string CreatedAt,
    string RunManifestPath,
    IReadOnlyList<string> ProfileIds,
    IReadOnlyList<AssessmentRunProfileRecord> Profiles,
    bool IsVerifiedComplete,
    IReadOnlyList<string> MissingArtifacts,
    string? NormalizedManifestPath,
    string? RecommendationManifestPath,
    string? ClientRecommendationsPath,
    string? HarnessReferencePath,
    string? SummaryPath);

public class AssessmentRunStore
{
    private readonly string _modelAssessorRootPath;
    private readonly string _resultsRootPath;

    public AssessmentRunStore(string? modelAssessorRootPath = null, string? resultsRootPath = null)
    {
        _modelAssessorRootPath = modelAssessorRootPath ?? PythonEnvironmentManager.GetModelAssessorRootPath();
        _resultsRootPath = resultsRootPath ?? Path.Combine(_modelAssessorRootPath, "results", "mlx-pep-cli");
    }

    public string GetResultsRootPath() => _resultsRootPath;

    public IReadOnlyList<AssessmentRunRecord> ListRuns(bool requireVerifiedComplete = true, string? modelId = null)
    {
        if (!Directory.Exists(_resultsRootPath))
        {
            return Array.Empty<AssessmentRunRecord>();
        }

        var runs = Directory.GetDirectories(_resultsRootPath)
            .SelectMany(operationDir => Directory.Exists(Path.Combine(operationDir, "runs"))
                ? Directory.GetDirectories(Path.Combine(operationDir, "runs"))
                    .Select(runDir => Path.Combine(runDir, "run_manifest.json"))
                    .Where(File.Exists)
                : Enumerable.Empty<string>())
            .Select(LoadRun)
            .Where(run => run != null)
            .Cast<AssessmentRunRecord>()
            .OrderByDescending(run => run.CreatedAt, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(modelId))
        {
            runs = runs
                .Where(run => MatchesModelId(run.ModelId, modelId))
                .ToList();
        }

        if (requireVerifiedComplete)
        {
            runs = runs.Where(run => run.IsVerifiedComplete).ToList();
        }

        return runs;
    }

    public AssessmentRunRecord? GetRun(string runId)
    {
        return ListRuns(requireVerifiedComplete: false)
            .FirstOrDefault(run => run.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase));
    }

    public AssessmentRunRecord? GetLatestRun(string? modelId = null, bool requireVerifiedComplete = true)
    {
        return ListRuns(requireVerifiedComplete, modelId).FirstOrDefault();
    }

    public string RenderRunListMarkdown(IEnumerable<AssessmentRunRecord> runs)
    {
        var runList = runs.ToList();
        var builder = new StringBuilder();
        builder.AppendLine("| Run ID | Model | Suite | Created | Status | Verified Complete | Profiles |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | ---: |");

        foreach (var run in runList)
        {
            builder.AppendLine($"| `{EscapePipes(run.RunId)}` | `{EscapePipes(run.ModelId)}` | `{EscapePipes(run.Suite)}` | `{EscapePipes(run.CreatedAt)}` | `{EscapePipes(run.Status)}` | `{run.IsVerifiedComplete}` | {run.ProfileIds.Count} |");
        }

        if (runList.Count == 0)
        {
            builder.AppendLine("| _none_ | _none_ | _none_ | _none_ | _none_ | _none_ | 0 |");
        }

        return builder.ToString().TrimEnd();
    }

    public string RenderRunSummaryMarkdown(AssessmentRunRecord run)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Run Overview");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Run ID | `{EscapePipes(run.RunId)}` |");
        builder.AppendLine($"| Model | `{EscapePipes(run.ModelId)}` |");
        builder.AppendLine($"| Suite | `{EscapePipes(run.Suite)}` |");
        builder.AppendLine($"| Created | `{EscapePipes(run.CreatedAt)}` |");
        builder.AppendLine($"| Status | `{EscapePipes(run.Status)}` |");
        builder.AppendLine($"| MTP Mode | `{EscapePipes(run.MtpMode)}` |");
        builder.AppendLine($"| Verified Complete | `{run.IsVerifiedComplete}` |");
        builder.AppendLine();
        builder.AppendLine("## Profiles");
        builder.AppendLine();
        builder.AppendLine("| Profile | Workload | MTP | Context Window | Max Tokens | Temperature | Top P | Top K | Bench Status | Bench Rows | Test Types | Prompt Lengths | Generation Lengths |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- | --- |");

        foreach (var profile in run.Profiles)
        {
            builder.AppendLine(
                $"| `{EscapePipes(profile.ProfileId)}` | `{EscapePipes(profile.Workload ?? "unknown")}` | `{profile.MtpEnabled}` | {ReadInt(profile.Settings, "max_context_window")} | {ReadInt(profile.Settings, "max_tokens")} | {ReadDouble(profile.Settings, "temperature")} | {ReadDouble(profile.Settings, "top_p")} | {ReadInt(profile.Settings, "top_k")} | `{EscapePipes(profile.BenchmarkStatus)}` | {profile.BenchmarkRowCount} | `{string.Join(", ", profile.TestTypes)}` | `{string.Join(", ", profile.PromptLengths)}` | `{string.Join(", ", profile.GenerationLengths)}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Generated harness configuration groups");

        foreach (var profile in run.Profiles)
        {
            builder.AppendLine();
            builder.AppendLine($"### `{EscapePipes(profile.ProfileId)}`");
            builder.AppendLine();
            if (profile.HarnessGroups.Count == 0)
            {
                builder.AppendLine("No harness configuration groups recorded.");
                continue;
            }

            builder.AppendLine("| Group | Config Surface | Key | Value |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var group in profile.HarnessGroups)
            {
                foreach (var value in group.Values)
                {
                    builder.AppendLine($"| `{EscapePipes(group.HarnessDisplayName)}` | `{EscapePipes(group.ConfigSurface)}` | `{EscapePipes(value.Key)}` | `{EscapePipes(value.Value)}` |");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Artifacts");
        builder.AppendLine();
        builder.AppendLine("| Artifact | Path |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Run Manifest | `{EscapePipes(GetRelativeToModelAssessor(run.RunManifestPath))}` |");
        builder.AppendLine($"| Normalized Manifest | `{EscapePipes(GetRelativeToModelAssessor(run.NormalizedManifestPath))}` |");
        builder.AppendLine($"| Recommendation Manifest | `{EscapePipes(GetRelativeToModelAssessor(run.RecommendationManifestPath))}` |");
        builder.AppendLine($"| Client Recommendations | `{EscapePipes(GetRelativeToModelAssessor(run.ClientRecommendationsPath))}` |");
        builder.AppendLine($"| Harness Reference | `{EscapePipes(GetRelativeToModelAssessor(run.HarnessReferencePath))}` |");
        builder.AppendLine($"| Summary | `{EscapePipes(GetRelativeToModelAssessor(run.SummaryPath))}` |");

        if (run.MissingArtifacts.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Missing Artifacts");
            builder.AppendLine();
            builder.AppendLine("| Missing |");
            builder.AppendLine("| --- |");
            foreach (var missing in run.MissingArtifacts)
            {
                builder.AppendLine($"| `{EscapePipes(missing)}` |");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private AssessmentRunRecord? LoadRun(string runManifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(runManifestPath));
        var root = document.RootElement;

        var runId = GetRequiredString(root, "run_id");
        var modelId = GetRequiredString(root, "model_id");
        var suite = GetRequiredString(root, "suite");
        var status = GetRequiredString(root, "status");
        var mtpMode = GetRequiredString(root, "mtp_mode");
        var createdAt = GetRequiredString(root, "created_at");
        var profileIds = root.TryGetProperty("profile_ids", out var profileIdsElement)
            ? profileIdsElement.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList()
            : new List<string>();

        var runDirectory = Path.GetDirectoryName(runManifestPath)!;
        var operationDirectory = Directory.GetParent(Directory.GetParent(runDirectory)!.FullName)!.FullName;
        var operationId = Path.GetFileName(operationDirectory);

        var artifactPathsElement = root.GetProperty("artifact_paths");
        var missingArtifacts = new List<string>();
        foreach (var relativePath in FlattenArtifactPaths(artifactPathsElement))
        {
            var absolutePath = Path.Combine(_modelAssessorRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                missingArtifacts.Add(relativePath);
            }
        }

        var normalizedManifestPath = FindFirst(operationDirectory, "normalized", "normalized_manifest.json");
        var recommendationManifestPath = FindFirst(operationDirectory, "recommendations", "recommendation_manifest.json");
        var clientRecommendationsPath = FindFirst(operationDirectory, "client-configs", "client_recommendations.json");
        var harnessReferencePath = FindFirst(operationDirectory, "client-configs", "ai-harness-reference.md");
        var summaryPath = FindFirst(operationDirectory, "summaries", "*.md");

        AddIfMissing(missingArtifacts, normalizedManifestPath, "normalized_manifest.json");
        AddIfMissing(missingArtifacts, recommendationManifestPath, "recommendation_manifest.json");
        AddIfMissing(missingArtifacts, clientRecommendationsPath, "client_recommendations.json");
        AddIfMissing(missingArtifacts, harnessReferencePath, "ai-harness-reference.md");
        AddIfMissing(missingArtifacts, summaryPath, "summary markdown");

        var executionPlanPath = root.GetProperty("artifact_paths").GetProperty("profile_execution_plan").GetString();
        var workloadsByProfileId = string.IsNullOrWhiteSpace(executionPlanPath)
            ? new Dictionary<string, (string? Workload, bool MtpEnabled)>(StringComparer.OrdinalIgnoreCase)
            : LoadExecutionPlan(Path.Combine(_modelAssessorRootPath, executionPlanPath!.Replace('/', Path.DirectorySeparatorChar)));

        var settingsPaths = root.GetProperty("artifact_paths").GetProperty("settings_requests").EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
        var benchmarkPaths = root.GetProperty("artifact_paths").GetProperty("benchmark_results").EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();

        var harnessGroupsByProfileId = string.IsNullOrWhiteSpace(clientRecommendationsPath)
            ? new Dictionary<string, List<AssessmentHarnessConfigGroupRecord>>(StringComparer.OrdinalIgnoreCase)
            : LoadHarnessGroups(clientRecommendationsPath!);

        var profiles = profileIds.Select(profileId => BuildProfileRecord(profileId, settingsPaths, benchmarkPaths, workloadsByProfileId, harnessGroupsByProfileId)).ToList();
        var isVerifiedComplete = status.Equals("success", StringComparison.OrdinalIgnoreCase) && missingArtifacts.Count == 0;

        return new AssessmentRunRecord(
            OperationId: operationId,
            RunId: runId,
            ModelId: modelId,
            Suite: suite,
            Status: status,
            MtpMode: mtpMode,
            CreatedAt: createdAt,
            RunManifestPath: runManifestPath,
            ProfileIds: profileIds,
            Profiles: profiles,
            IsVerifiedComplete: isVerifiedComplete,
            MissingArtifacts: missingArtifacts,
            NormalizedManifestPath: normalizedManifestPath,
            RecommendationManifestPath: recommendationManifestPath,
            ClientRecommendationsPath: clientRecommendationsPath,
            HarnessReferencePath: harnessReferencePath,
            SummaryPath: summaryPath);
    }

    private AssessmentRunProfileRecord BuildProfileRecord(
        string profileId,
        List<string> settingsPaths,
        List<string> benchmarkPaths,
        Dictionary<string, (string? Workload, bool MtpEnabled)> workloadsByProfileId,
        Dictionary<string, List<AssessmentHarnessConfigGroupRecord>> harnessGroupsByProfileId)
    {
        var settingsRelativePath = settingsPaths.FirstOrDefault(path => path.Contains($"/{profileId}/", StringComparison.OrdinalIgnoreCase));
        var benchmarkRelativePath = benchmarkPaths.FirstOrDefault(path => path.Contains($"/{profileId}/", StringComparison.OrdinalIgnoreCase));

        var settings = settingsRelativePath == null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : LoadSettings(Path.Combine(_modelAssessorRootPath, settingsRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var benchmark = benchmarkRelativePath == null
            ? (Status: "missing", RowCount: 0, TestTypes: (IReadOnlyList<string>)Array.Empty<string>(), PromptLengths: (IReadOnlyList<int>)Array.Empty<int>(), GenerationLengths: (IReadOnlyList<int>)Array.Empty<int>())
            : LoadBenchmark(Path.Combine(_modelAssessorRootPath, benchmarkRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        workloadsByProfileId.TryGetValue(profileId, out var workloadInfo);
        harnessGroupsByProfileId.TryGetValue(profileId, out var harnessGroups);

        return new AssessmentRunProfileRecord(
            ProfileId: profileId,
            Workload: workloadInfo.Workload,
            MtpEnabled: workloadInfo.MtpEnabled,
            SettingsPath: settingsRelativePath,
            BenchmarkPath: benchmarkRelativePath,
            Settings: settings,
            HarnessGroups: harnessGroups ?? new List<AssessmentHarnessConfigGroupRecord>(),
            BenchmarkStatus: benchmark.Status,
            BenchmarkRowCount: benchmark.RowCount,
            TestTypes: benchmark.TestTypes,
            PromptLengths: benchmark.PromptLengths,
            GenerationLengths: benchmark.GenerationLengths);
    }

    private Dictionary<string, List<AssessmentHarnessConfigGroupRecord>> LoadHarnessGroups(string clientRecommendationsPath)
    {
        var absolutePath = Path.IsPathRooted(clientRecommendationsPath)
            ? clientRecommendationsPath
            : Path.Combine(_modelAssessorRootPath, clientRecommendationsPath.Replace('/', Path.DirectorySeparatorChar));

        using var document = JsonDocument.Parse(File.ReadAllText(absolutePath));
        if (!TryGetHarnessRows(document.RootElement, out var rowsElement) || rowsElement.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, List<AssessmentHarnessConfigGroupRecord>>(StringComparer.OrdinalIgnoreCase);
        }

        var grouped = new Dictionary<string, List<AssessmentHarnessConfigGroupRecord>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rowsElement.EnumerateArray())
        {
            var profileId = GetRequiredString(row, "profile_id");
            if (string.IsNullOrWhiteSpace(profileId))
                continue;

            var values = row.TryGetProperty("recommended_values", out var valuesElement) && valuesElement.ValueKind == JsonValueKind.Array
                ? valuesElement.EnumerateArray()
                    .Select(value => new AssessmentHarnessKeyValueRecord(
                        GetRequiredString(value, "term"),
                        GetRequiredString(value, "value")))
                    .Where(value => !string.IsNullOrWhiteSpace(value.Key))
                    .ToList()
                : new List<AssessmentHarnessKeyValueRecord>();

            var group = new AssessmentHarnessConfigGroupRecord(
                HarnessId: GetRequiredString(row, "harness_id"),
                HarnessDisplayName: GetRequiredString(row, "harness_display_name"),
                ConfigSurface: GetRequiredString(row, "config_surface"),
                Values: values);

            if (!grouped.TryGetValue(profileId, out var list))
            {
                list = new List<AssessmentHarnessConfigGroupRecord>();
                grouped[profileId] = list;
            }

            list.Add(group);
        }

        return grouped;
    }

    private static bool TryGetHarnessRows(JsonElement root, out JsonElement rowsElement)
    {
        if (root.TryGetProperty("client_recommendation_rows", out rowsElement))
            return true;

        if (root.TryGetProperty("ai_harness_reference_rows", out rowsElement))
            return true;

        rowsElement = default;
        return false;
    }

    private static Dictionary<string, (string? Workload, bool MtpEnabled)> LoadExecutionPlan(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateArray()
            .Where(item => item.TryGetProperty("profile_id", out _))
            .ToDictionary(
                item => item.GetProperty("profile_id").GetString()!,
                item => (
                    item.TryGetProperty("workload", out var workload) ? workload.GetString() : null,
                    item.TryGetProperty("mtp_enabled", out var mtpEnabled) && mtpEnabled.ValueKind is JsonValueKind.True or JsonValueKind.False && mtpEnabled.GetBoolean()),
                StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> LoadSettings(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => ConvertJsonValue(property.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static (string Status, int RowCount, IReadOnlyList<string> TestTypes, IReadOnlyList<int> PromptLengths, IReadOnlyList<int> GenerationLengths) LoadBenchmark(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() ?? "unknown" : "unknown";
        if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array)
        {
            return (status, 0, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<int>());
        }

        var results = resultsElement.EnumerateArray().ToList();
        return (
            status,
            results.Count,
            results.Select(result => result.TryGetProperty("test_type", out var testType) ? testType.GetString() : null).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            results.Where(result => result.TryGetProperty("pp", out var promptLength) && promptLength.TryGetInt32(out _)).Select(result => result.GetProperty("pp").GetInt32()).Distinct().OrderBy(value => value).ToList(),
            results.Where(result => result.TryGetProperty("tg", out var generationLength) && generationLength.TryGetInt32(out _)).Select(result => result.GetProperty("tg").GetInt32()).Distinct().OrderBy(value => value).ToList());
    }

    private IEnumerable<string> FlattenArtifactPaths(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    yield return value!;
                yield break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var path in FlattenArtifactPaths(item))
                        yield return path;
                }
                yield break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var path in FlattenArtifactPaths(property.Value))
                        yield return path;
                }
                yield break;
        }
    }

    private static string? FindFirst(string operationDirectory, string childDirectory, string searchPattern)
    {
        var directory = Path.Combine(operationDirectory, childDirectory);
        if (!Directory.Exists(directory))
            return null;

        return Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static void AddIfMissing(List<string> missingArtifacts, string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            missingArtifacts.Add(label);
        }
    }

    private string GetRelativeToModelAssessor(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return "missing";

        return Path.GetRelativePath(_modelAssessorRootPath, absolutePath).Replace('\\', '/');
    }

    private static string EscapePipes(string? value)
    {
        return (value ?? string.Empty).Replace("|", "\\|");
    }

    private static bool MatchesModelId(string runModelId, string requestedModelId)
    {
        if (runModelId.Equals(requestedModelId, StringComparison.OrdinalIgnoreCase))
            return true;

        var requestedLeaf = requestedModelId.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (!string.IsNullOrWhiteSpace(requestedLeaf) &&
            runModelId.Equals(requestedLeaf, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> settings, string key)
    {
        if (!settings.TryGetValue(key, out var value) || value == null)
            return 0;

        return value switch
        {
            int intValue => intValue,
            long longValue => Convert.ToInt32(longValue),
            double doubleValue => Convert.ToInt32(doubleValue),
            _ => 0
        };
    }

    private static string ReadDouble(IReadOnlyDictionary<string, object?> settings, string key)
    {
        if (!settings.TryGetValue(key, out var value) || value == null)
            return "0";

        return value switch
        {
            double doubleValue => doubleValue.ToString("0.###"),
            int intValue => intValue.ToString(),
            long longValue => longValue.ToString(),
            _ => value.ToString() ?? "0"
        };
    }

    private static object? ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value), StringComparer.OrdinalIgnoreCase),
            _ => null
        };
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}