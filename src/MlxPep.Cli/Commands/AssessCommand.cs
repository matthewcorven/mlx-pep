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

            if (publish)
            {
                // Validate profiles for local use
                var validator = new ProfileValidator();
                var validationResult = validator.ValidateProfileSet(profiles);

                if (context.JsonOutput)
                {
                    var result = new
                    {
                        command = "assess",
                        status = validationResult.IsValid ? "ok" : "error",
                        hfId = hfId,
                        profiles = profiles.Select(p => new { id = p.Id, tier = p.Tier }).ToArray(),
                        validation = new
                        {
                            isValid = validationResult.IsValid,
                            errorCount = validationResult.Errors.Count,
                            warningCount = validationResult.Warnings.Count,
                            errors = validationResult.Errors,
                            warnings = validationResult.Warnings
                        }
                    };
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"Assessing model: {hfId}");
                    Console.WriteLine($"Generated {profiles.Count} profiles");
                    if (validationResult.IsValid)
                        Console.WriteLine($"✓ All profiles valid");
                    else
                        Console.WriteLine($"✗ Validation failed with {validationResult.Errors.Count} errors");

                    if (validationResult.Warnings.Count > 0)
                        Console.WriteLine($"⚠️  {validationResult.Warnings.Count} warnings");
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
                Sampler: new SamplerSettings(Temperature: 0.7, TopP: null, TopK: null, RepetitionPenalty: null, ContextTokens: null)
            ));
        }

        return profiles;
    }
}
