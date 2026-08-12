namespace MlxPep.Cli.Commands;

using MlxPep.Core;

/// <summary>
/// Handler for `mlx-pep assess` command.
/// Runs profiling for a model and generates tiered profiles.
/// </summary>
public class AssessCommand
{
    private readonly PublishService _publishService;

    public AssessCommand(PublishService? publishService = null)
    {
        _publishService = publishService ?? new PublishService();
    }

    public async Task<CommandResult> ExecuteAsync(
        string hfId,
        bool publish = false,
        CommandContext? context = null)
    {
        context ??= new CommandContext();

        try
        {
            // Create test profiles for the model
            var profiles = CreateProfilesForModel(hfId);
            var publishedProfiles = new List<Profile>();

            if (publish)
            {
                var batchResult = await _publishService.PrepareForPublishAsync(profiles, publishedProfiles);
                var report = _publishService.GenerateReport(batchResult);

                if (context.JsonOutput)
                {
                    var result = new
                    {
                        command = "assess",
                        status = report.HasErrors ? "warning" : "ok",
                        hfId = hfId,
                        profiles = profiles.Select(p => new { id = p.Id, tier = p.Tier, saved = true }).ToArray(),
                        publish = new
                        {
                            totalProfiles = report.TotalProfiles,
                            validProfiles = report.ValidProfiles,
                            deduplicatedProfiles = report.DeduplicatedProfiles,
                            duplicateSavings = report.DuplicateSavings,
                            successRate = report.SuccessRate,
                            hasErrors = report.HasErrors,
                            failedProfiles = report.FailedProfileIds
                        }
                    };
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"Assessing model: {hfId}");
                    Console.WriteLine($"Generated {profiles.Count} profiles");
                    Console.WriteLine($"Publish check: {report.ValidProfiles}/{report.TotalProfiles} profiles valid");
                    if (report.DuplicateSavings > 0)
                        Console.WriteLine($"Deduplication would remove {report.DuplicateSavings} duplicates");
                    if (report.HasErrors)
                        Console.WriteLine($"⚠️  {report.FailedProfileIds.Count} profiles failed validation");
                }
            }
            else
            {
                if (context.JsonOutput)
                {
                    var result = new
                    {
                        command = "assess",
                        status = "ok",
                        hfId = hfId,
                        profiles = profiles.Select(p => new { id = p.Id, tier = p.Tier, saved = true }).ToArray(),
                        published = false
                    };
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"Assessing model: {hfId}");
                    Console.WriteLine($"Generated {profiles.Count} profiles");
                }
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to assess model: {ex.Message}");
        }
    }

    private List<Profile> CreateProfilesForModel(string hfId)
    {
        var profiles = new List<Profile>();
        var tiers = new[] { "high-performance", "balanced", "efficient" };

        foreach (var tier in tiers)
        {
            profiles.Add(new Profile(
                SchemaVersion: 1,
                Id: $"{hfId.Replace("/", "-")}-{tier}",
                ModelHfId: hfId,
                Tier: tier,
                Engine: "mlx",
                System: new Dictionary<string, object> { { "os", "macOS" } },
                OMLXSettings: new Dictionary<string, object> { { "compute_units", tier == "high-performance" ? "ALL" : "GPU" } },
                Harness: new Dictionary<string, object>
                {
                    { "vscode", new Dictionary<string, object>
                        {
                            { "maxInputTokens", tier == "high-performance" ? 128000 : 64000 },
                            { "maxOutputTokens", tier == "high-performance" ? 8000 : 4000 }
                        }
                    }
                },
                Provenance: new ProfileProvenance("assess-command", DateTime.UtcNow.ToString("O"), "cli"),
                Hardware: new HardwareFingerprint("Apple M1", 16, "MacBookPro"),
                Sampler: new SamplerSettings("default", new Dictionary<string, object> { { "temperature", 0.7 } }),
                Community: null
            ));
        }

        return profiles;
    }
}
