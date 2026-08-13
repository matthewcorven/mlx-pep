namespace MlxPep.Cli.Commands;

using MlxPep.Core;
using System.Text.Json;

/// <summary>
/// Handler for `mlx-pep assess <hf-id> [--assistant-model-id X] [--suite smoke|full]` command.
/// Runs profiling via model-assessor and generates tiered profiles.
/// 
/// Issue #17: assess command delegates to model-assessor, emits 3 tiers.
/// </summary>
public class AssessCommand
{
    private readonly IProfilingRunner _profilingRunner;
    private readonly PublishService _publishService;
    private readonly string _profilesDirectory;

    public AssessCommand(
        IProfilingRunner? profilingRunner = null,
        PublishService? publishService = null,
        string? profilesDirectory = null)
    {
        _profilingRunner = profilingRunner ?? new ProfilingRunner();
        _publishService = publishService ?? new PublishService();
        _profilesDirectory = profilesDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mlx-pep",
            "profiles");
    }

    public async Task<CommandResult> ExecuteAsync(
        string hfId,
        string? assistantModelId = null,
        string suite = "smoke",
        bool publish = false,
        CommandContext? context = null)
    {
        context ??= new CommandContext();

        try
        {
            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Starting assessment: hfId={hfId}, assistantModelId={assistantModelId}, suite={suite}, publish={publish}");

            // Run profiling pipeline via model-assessor
            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Calling ProfilingRunner");
            var manifest = await _profilingRunner.RunProfilingAsync(hfId, assistantModelId, suite);

            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Received manifest with {manifest.Recommendations.Count} recommendations");

            // Map recommendations to profiles
            var profiles = MapRecommendationsToProfiles(hfId, manifest);

            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Mapped {profiles.Count} profiles");

            // Save profiles locally
            await SaveProfilesToDiskAsync(profiles);

            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Saved profiles to disk");

            // Validate profiles
            var validator = new ProfileValidator();
            var validationResult = validator.ValidateProfileSet(profiles);

            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Validation result: isValid={validationResult.IsValid}");

            if (publish)
            {
                System.Diagnostics.Debug.WriteLine($"[AssessCommand] Publishing profiles");
                var publishResult = await _publishService.PrepareForPublishAsync(profiles);

                if (context.JsonOutput)
                {
                    var result = new
                    {
                        command = "assess",
                        status = publishResult.ValidCount == profiles.Count ? "ok" : "partial",
                        hfId = hfId,
                        suite = suite,
                        profiles = profiles.Select(p => new { id = p.Id, tier = p.Tier }).ToArray(),
                        published = publishResult.ValidCount,
                        validation = new
                        {
                            isValid = validationResult.IsValid,
                            errorCount = validationResult.Errors.Count,
                            warningCount = validationResult.Warnings.Count,
                            errors = validationResult.Errors,
                            warnings = validationResult.Warnings
                        }
                    };
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"✓ Assessed {hfId}");
                    Console.WriteLine($"  Generated {profiles.Count} profiles");
                    Console.WriteLine($"  Valid for publishing: {publishResult.ValidCount}/{profiles.Count}");
                    if (!validationResult.IsValid)
                        Console.WriteLine($"  ⚠️  {validationResult.Errors.Count} validation errors");
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
                        suite = suite,
                        profiles = profiles.Select(p => new { id = p.Id, tier = p.Tier }).ToArray(),
                        saved = true,
                        published = false
                    };
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"✓ Assessed {hfId}");
                    Console.WriteLine($"  Generated {profiles.Count} profiles");
                    Console.WriteLine($"  Saved to {_profilesDirectory}");
                }
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Exception: {ex.Message}");
            return CommandResult.Failure($"Failed to assess model: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps model-assessor recommendation tiers to mlx-pep Profile records.
    /// </summary>
    private List<Profile> MapRecommendationsToProfiles(
        string hfId,
        RecommendationManifest manifest)
    {
        System.Diagnostics.Debug.WriteLine($"[AssessCommand.MapRecommendationsToProfiles] Mapping {manifest.Recommendations.Count} recommendations");

        var profiles = new List<Profile>();

        foreach (var rec in manifest.Recommendations)
        {
            System.Diagnostics.Debug.WriteLine($"[AssessCommand.MapRecommendationsToProfiles] Processing tier: {rec.Tier}");

            var profileId = $"{hfId.Replace("/", "-")}-{rec.Tier}";

            // Build harness config from recommendation
            var harness = new Dictionary<string, object>();
            if (rec.HarnessSettings.Any())
            {
                harness["vscode"] = rec.HarnessSettings;
            }

            // Convert sampler settings
            SamplerSettings? sampler = null;
            if (rec.SamplerSettings?.Any() == true)
            {
                sampler = ConvertSamplerSettings(rec.SamplerSettings);
            }

            var profile = new Profile(
                SchemaVersion: 1,
                Id: profileId,
                ModelHfId: hfId,
                Tier: rec.Tier,
                Engine: "mlx",
                System: new Dictionary<string, object> { { "os", "macOS" } },
                OMLXSettings: rec.OMLXSettings,
                Harness: harness,
                Provenance: new ProfileProvenance("assess-command", DateTime.UtcNow.ToString("O"), "cli"),
                Hardware: new HardwareFingerprint("Apple M1", 16, "MacBookPro"),
                Sampler: sampler
            );

            profiles.Add(profile);
        }

        return profiles;
    }

    /// <summary>
    /// Converts sampler settings dict to SamplerSettings record.
    /// </summary>
    private SamplerSettings ConvertSamplerSettings(Dictionary<string, object> settings)
    {
        System.Diagnostics.Debug.WriteLine($"[AssessCommand.ConvertSamplerSettings] Converting sampler settings");

        double? temperature = TryGetDoubleValue(settings, "temperature");
        double? topP = TryGetDoubleValue(settings, "topP");
        int? topK = TryGetIntValue(settings, "topK");
        double? repPenalty = TryGetDoubleValue(settings, "repetitionPenalty");
        int? contextTokens = TryGetIntValue(settings, "contextTokens");

        return new SamplerSettings(
            Temperature: temperature,
            TopP: topP,
            TopK: topK,
            RepetitionPenalty: repPenalty,
            ContextTokens: contextTokens);
    }

    private double? TryGetDoubleValue(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val))
            return null;

        if (val is double d)
            return d;

        if (double.TryParse(val?.ToString(), out var parsed))
            return parsed;

        return null;
    }

    private int? TryGetIntValue(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val))
            return null;

        if (val is int i)
            return i;

        if (int.TryParse(val?.ToString(), out var parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Saves profiles to ~/.mlx-pep/profiles/ directory in JSONL format.
    /// </summary>
    private async Task SaveProfilesToDiskAsync(List<Profile> profiles)
    {
        System.Diagnostics.Debug.WriteLine($"[AssessCommand.SaveProfilesToDiskAsync] Saving {profiles.Count} profiles to {_profilesDirectory}");

        // Ensure directory exists
        Directory.CreateDirectory(_profilesDirectory);

        // Save each profile as a separate JSONL file
        foreach (var profile in profiles)
        {
            var fileName = $"{profile.Id}.jsonl";
            var filePath = Path.Combine(_profilesDirectory, fileName);

            System.Diagnostics.Debug.WriteLine($"[AssessCommand.SaveProfilesToDiskAsync] Writing profile to {filePath}");

            var json = JsonSerializer.Serialize(profile, ProfileJsonSerializerContext.Default.Profile);
            await File.WriteAllTextAsync(filePath, json + Environment.NewLine);
        }

        System.Diagnostics.Debug.WriteLine($"[AssessCommand.SaveProfilesToDiskAsync] All profiles saved");
    }
}
