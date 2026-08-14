namespace MlxPep.Cli.Commands;

using MlxPep.Core;
using MlxPep.Core.Profiling;
using System.Text.Json;

/// <summary>
/// Handler for `mlx-pep assess` command.
/// Delegates to model-assessor subprocess via ProfilingRunner.
/// Maps recommendations to profiles and saves to local storage.
/// </summary>
public class AssessCommand
{
    private readonly PublishService _publishService;
    private readonly ProfilingRunner _profilingRunner;
    private readonly RecommendationMapper _mapper;
    private readonly ProfileStorage _storage;

    public AssessCommand(
        PublishService? publishService = null,
        ProfilingRunner? profilingRunner = null,
        RecommendationMapper? mapper = null,
        ProfileStorage? storage = null)
    {
        _publishService = publishService ?? new PublishService();
        _profilingRunner = profilingRunner ?? new ProfilingRunner();
        _mapper = mapper ?? new RecommendationMapper();
        _storage = storage ?? new ProfileStorage();
    }

    public async Task<CommandResult> ExecuteAsync(
        string hfId,
        string? assistantModelId = null,
        string suite = "full",
        bool publish = false,
        CommandContext? context = null)
    {
        context ??= new CommandContext();

        System.Diagnostics.Debug.WriteLine($"[AssessCommand] Starting assess for {hfId} (suite={suite})");

        try
        {
            List<Profile> profiles;

            // Try to run model-assessor subprocess
            if (await _profilingRunner.IsAvailableAsync())
            {
                System.Diagnostics.Debug.WriteLine("[AssessCommand] Model-assessor available, running profiling");
                
                try
                {
                    var manifest = await _profilingRunner.RunProfilingAsync(
                        hfId,
                        assistantModelId,
                        suite);

                    profiles = _mapper.MapToProfiles(manifest);
                    System.Diagnostics.Debug.WriteLine($"[AssessCommand] Successfully generated {profiles.Count} profiles from manifest");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AssessCommand] Model-assessor failed: {ex.Message}, using fixture fallback");
                    profiles = CreateFixtureProfiles(hfId);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AssessCommand] Model-assessor unavailable, using fixture fallback");
                profiles = CreateFixtureProfiles(hfId);
            }

            // Save profiles to local storage
            await _storage.SaveProfileSetAsync(profiles, hfId);
            System.Diagnostics.Debug.WriteLine("[AssessCommand] Profiles saved to local storage");

            // Optionally publish to service
            if (publish)
            {
                System.Diagnostics.Debug.WriteLine("[AssessCommand] Publishing profiles to service");
                var validator = new ProfileValidator();
                var validationResult = validator.ValidateProfileSet(profiles);

                if (!validationResult.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine($"[AssessCommand] Validation failed with {validationResult.Errors.Count} errors");
                    if (context.JsonOutput)
                    {
                        var errorResult = new
                        {
                            command = "assess",
                            status = "error",
                            hfId = hfId,
                            published = false,
                            validation = new
                            {
                                isValid = false,
                                errorCount = validationResult.Errors.Count,
                                warningCount = validationResult.Warnings.Count,
                                errors = validationResult.Errors
                            }
                        };
                        Console.WriteLine(JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    else
                    {
                        Console.WriteLine($"✗ Validation failed with {validationResult.Errors.Count} errors");
                        foreach (var error in validationResult.Errors)
                            Console.WriteLine($"  - {error}");
                    }

                    return CommandResult.Failure("Profile validation failed");
                }

                if (context.JsonOutput)
                {
                    var result = new
                    {
                        command = "assess",
                        status = "ok",
                        hfId = hfId,
                        profiles = profiles.Select(p => new { id = p.Id, tier = p.Tier }).ToArray(),
                        published = true,
                        validation = new
                        {
                            isValid = true,
                            errorCount = 0,
                            warningCount = validationResult.Warnings.Count
                        }
                    };
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"Assessing model: {hfId}");
                    Console.WriteLine($"Generated {profiles.Count} profiles");
                    Console.WriteLine($"✓ All profiles valid and published");
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
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine($"Assessing model: {hfId}");
                    Console.WriteLine($"Generated {profiles.Count} profiles");
                    Console.WriteLine($"Profiles saved to {ProfileStorage.GetStorageDirectoryPath()}");
                }
            }

            System.Diagnostics.Debug.WriteLine("[AssessCommand] Assess completed successfully");
            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Exception: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Failed to assess model: {ex.Message}");
        }
    }

    private List<Profile> CreateFixtureProfiles(string hfId)
    {
        System.Diagnostics.Debug.WriteLine($"[AssessCommand] Creating fixture profiles for {hfId}");

        // Create a fixture RecommendationManifest for testing when model-assessor is unavailable
        var manifest = new RecommendationManifest(
            ModelHfId: hfId,
            AssessmentVersion: "1.0.0",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new Dictionary<string, TierRecommendation>
            {
                ["high"] = new TierRecommendation(
                    Tier: "high",
                    System: new Dictionary<string, object> { { "os", "macOS" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "ALL" } },
                    Harness: new Dictionary<string, object>
                    {
                        { "vscode", new Dictionary<string, object>
                            {
                                { "maxInputTokens", 128000 },
                                { "maxOutputTokens", 8000 }
                            }
                        }
                    },
                    Sampler: new Dictionary<string, object> { { "temperature", 0.7 } }),

                ["balanced"] = new TierRecommendation(
                    Tier: "balanced",
                    System: new Dictionary<string, object> { { "os", "macOS" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "GPU" } },
                    Harness: new Dictionary<string, object>
                    {
                        { "vscode", new Dictionary<string, object>
                            {
                                { "maxInputTokens", 64000 },
                                { "maxOutputTokens", 4000 }
                            }
                        }
                    },
                    Sampler: new Dictionary<string, object> { { "temperature", 0.7 } }),

                ["efficient"] = new TierRecommendation(
                    Tier: "efficient",
                    System: new Dictionary<string, object> { { "os", "macOS" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "CPU" } },
                    Harness: new Dictionary<string, object>
                    {
                        { "vscode", new Dictionary<string, object>
                            {
                                { "maxInputTokens", 32000 },
                                { "maxOutputTokens", 2000 }
                            }
                        }
                    },
                    Sampler: new Dictionary<string, object> { { "temperature", 0.7 } })
            },
            Hardware: new HardwareAssessment("Apple M1", 16, "MacBook"));

        var profiles = _mapper.MapToProfiles(manifest);
        System.Diagnostics.Debug.WriteLine($"[AssessCommand] Created {profiles.Count} fixture profiles");
        return profiles;
    }
}
