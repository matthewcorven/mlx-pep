namespace MlxPep.Core.Tests;

using System.Text.Json;
using MlxPep.Core;

public sealed class AssessmentRunStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _modelAssessorRoot;
    private readonly string _resultsRoot;

    public AssessmentRunStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"mlx-pep-runstore-{Guid.NewGuid():N}");
        _modelAssessorRoot = Path.Combine(_tempRoot, "model-assessor");
        _resultsRoot = Path.Combine(_modelAssessorRoot, "results", "mlx-pep-cli");
        Directory.CreateDirectory(_resultsRoot);
    }

    [Fact]
    public void ListRuns_FindsVerifiedCompleteRun()
    {
        var operationDir = Path.Combine(_resultsRoot, "op-001");
        var runDir = Path.Combine(operationDir, "runs", "run-001");
        var profileDir = Path.Combine(runDir, "short_coding_mtp_off");
        Directory.CreateDirectory(runDir);
        Directory.CreateDirectory(profileDir);
        Directory.CreateDirectory(Path.Combine(operationDir, "normalized", "n-001"));
        Directory.CreateDirectory(Path.Combine(operationDir, "recommendations", "r-001"));
        Directory.CreateDirectory(Path.Combine(operationDir, "client-configs", "c-001"));
        Directory.CreateDirectory(Path.Combine(operationDir, "summaries"));

        File.WriteAllText(Path.Combine(runDir, "profile_execution_plan.json"), "[{\"profile_id\":\"short_coding_mtp_off\",\"workload\":\"short_coding\",\"mtp_enabled\":false}]");
        File.WriteAllText(Path.Combine(runDir, "short_coding_mtp_off", "01_settings_request.json"), "{\"max_context_window\":16384,\"max_tokens\":1024,\"temperature\":0.1,\"top_p\":0.9,\"top_k\":40}");
        File.WriteAllText(Path.Combine(runDir, "short_coding_mtp_off", "06_bench_results.json"), "{\"status\":\"completed\",\"results\":[{\"test_type\":\"single\",\"pp\":1024,\"tg\":256}]}");
        File.WriteAllText(Path.Combine(runDir, "assistant_probe.json"), "{}");
        File.WriteAllText(Path.Combine(runDir, "03_models.json"), "{}");
        File.WriteAllText(Path.Combine(runDir, "02_profile_fields.json"), "{}");
        File.WriteAllText(Path.Combine(operationDir, "topology_manifest.json"), "{}");
        File.WriteAllText(Path.Combine(operationDir, "normalized", "n-001", "normalized_manifest.json"), "{}");
        File.WriteAllText(Path.Combine(operationDir, "recommendations", "r-001", "recommendation_manifest.json"), "{}");
        File.WriteAllText(
            Path.Combine(operationDir, "client-configs", "c-001", "client_recommendations.json"),
            "{\"client_recommendation_rows\":[{\"profile_id\":\"short_coding_mtp_off\",\"harness_id\":\"github_copilot_cli\",\"harness_display_name\":\"GitHub Copilot CLI\",\"config_surface\":\"shell environment\",\"recommended_values\":[{\"term\":\"COPILOT_MODEL\",\"value\":\"mlx-community/test-model\"},{\"term\":\"COPILOT_PROVIDER_BASE_URL\",\"value\":\"http://127.0.0.1:8000/v1\"}]}]}");
        File.WriteAllText(Path.Combine(operationDir, "client-configs", "c-001", "ai-harness-reference.md"), "# ref");
        File.WriteAllText(Path.Combine(operationDir, "summaries", "summary.md"), "# summary");

        var manifest = new
        {
            artifact_paths = new
            {
                assistant_probe = Relative(Path.Combine(runDir, "assistant_probe.json")),
                benchmark_results = new[] { Relative(Path.Combine(runDir, "short_coding_mtp_off", "06_bench_results.json")) },
                instance_artifacts = new { },
                instance_topology = Relative(Path.Combine(operationDir, "topology_manifest.json")),
                model_inventory = Relative(Path.Combine(runDir, "03_models.json")),
                profile_execution_plan = Relative(Path.Combine(runDir, "profile_execution_plan.json")),
                profile_fields = Relative(Path.Combine(runDir, "02_profile_fields.json")),
                settings_requests = new[] { Relative(Path.Combine(runDir, "short_coding_mtp_off", "01_settings_request.json")) }
            },
            assistant_model_id = (string?)null,
            base_url = "http://127.0.0.1:8000",
            created_at = "2026-08-14T18:26:11.369114+00:00",
            errors = Array.Empty<string>(),
            model_id = "mlx-community/test-model",
            mtp_mode = "off",
            profile_ids = new[] { "short_coding_mtp_off" },
            run_id = "run-001",
            schema_version = "1.0",
            status = "success",
            suite = "smoke",
            warnings = Array.Empty<string>()
        };

        File.WriteAllText(Path.Combine(runDir, "run_manifest.json"), JsonSerializer.Serialize(manifest));

        var store = new AssessmentRunStore(_modelAssessorRoot, _resultsRoot);
        var runs = store.ListRuns();

        var run = Assert.Single(runs);
        Assert.True(run.IsVerifiedComplete);
        Assert.Equal("run-001", run.RunId);
        Assert.Single(run.Profiles);
        Assert.Equal("short_coding", run.Profiles[0].Workload);
        Assert.Single(run.Profiles[0].HarnessGroups);
        Assert.Equal("github_copilot_cli", run.Profiles[0].HarnessGroups[0].HarnessId);
        Assert.Contains("COPILOT_MODEL", run.Profiles[0].HarnessGroups[0].Values.Select(value => value.Key));
        Assert.Contains("| Field | Value |", store.RenderRunSummaryMarkdown(run));
        Assert.Contains("## Generated harness configuration groups", store.RenderRunSummaryMarkdown(run));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string Relative(string absolutePath)
    {
        return Path.GetRelativePath(_modelAssessorRoot, absolutePath).Replace('\\', '/');
    }
}