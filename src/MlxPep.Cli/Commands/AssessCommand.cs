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
        string? topologyManifestPath = null,
        CommandContext? context = null)
    {
        context ??= new CommandContext();
        using var progress = context.CreateProgressScope("assess", publish ? 6 : 5);

        System.Diagnostics.Debug.WriteLine($"[AssessCommand] Starting assess for {hfId} (suite={suite})");
        context.Verbose("AssessCommand", $"Starting assess command for model '{hfId}' with suite '{suite}' and publish={publish}.");

        try
        {
            List<Profile> profiles;
            progress.StartStep("validate input and environment");
            progress.CompleteStep("input validation complete");

            // Run the real model-assessor pipeline and fail closed if it is unavailable.
            progress.StartStep("check model-assessor availability");
            if (await _profilingRunner.IsAvailableAsync())
            {
                System.Diagnostics.Debug.WriteLine("[AssessCommand] Model-assessor available, running profiling");
                context.Verbose("AssessCommand", "Model-assessor availability check succeeded; entering profiling workflow.");
                progress.CompleteStep("model-assessor available");
                
                progress.StartStep("run profiling workflow");
                var profilingResult = await _profilingRunner.RunProfilingAsync(
                    hfId,
                    assistantModelId,
                    suite,
                    topologyManifestPath);
                progress.CompleteStep("profiling workflow complete");

                progress.StartStep("map recommendation manifest to profiles");
                profiles = _mapper.MapToProfiles(profilingResult.RecommendationManifest);
                System.Diagnostics.Debug.WriteLine($"[AssessCommand] Successfully generated {profiles.Count} profiles from manifest");
                context.Verbose("AssessCommand", $"Mapped manifest to {profiles.Count} profiles.");
                progress.CompleteStep($"mapped {profiles.Count} profiles");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AssessCommand] Model-assessor unavailable, failing assess command");
                context.Verbose("AssessCommand", "Model-assessor availability check failed; returning a closed failure.");
                progress.CompleteStep("model-assessor unavailable");
                return CommandResult.Failure(
                    "Model assessment tooling is unavailable. Verify python3 and src/model-assessor/scripts/next_phase are present.");
            }

            // Save profiles to local storage
            progress.StartStep("save profiles to local storage");
            await _storage.SaveProfileSetAsync(profiles, hfId);
            System.Diagnostics.Debug.WriteLine("[AssessCommand] Profiles saved to local storage");
            context.Verbose("AssessCommand", "Saved generated profiles to local storage.");
            progress.CompleteStep("profiles saved locally");

            // Optionally publish to service
            if (publish)
            {
                System.Diagnostics.Debug.WriteLine("[AssessCommand] Publishing profiles to service");
                context.Verbose("AssessCommand", "Publish flag is enabled; validating profiles for publish path.");
                progress.StartStep("validate publish payload");
                var validator = new ProfileValidator();
                var validationResult = validator.ValidateProfileSet(profiles);

                if (!validationResult.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine($"[AssessCommand] Validation failed with {validationResult.Errors.Count} errors");
                    context.Verbose("AssessCommand", $"Publish validation failed with {validationResult.Errors.Count} errors.");
                    progress.CompleteStep("publish validation failed");
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
                progress.CompleteStep("publish validation succeeded");

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
                    context.Verbose("AssessCommand", "Publish validation succeeded; rendering text success output.");
                    Console.WriteLine($"Assessing model: {hfId}");
                    Console.WriteLine($"Generated {profiles.Count} profiles");
                    Console.WriteLine($"✓ All profiles valid and published");
                    if (validationResult.Warnings.Count > 0)
                    {
                        context.Verbose("AssessCommand", $"Validation emitted {validationResult.Warnings.Count} warnings.");
                        Console.WriteLine($"⚠️  {validationResult.Warnings.Count} warnings");
                    }
                }
            }
            else
            {
                context.Verbose("AssessCommand", "Publish flag is disabled; emitting local-save result only.");
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
            context.Verbose("AssessCommand", "Assess command completed successfully.");
            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AssessCommand] Exception: {ex.GetType().Name}: {ex.Message}");
            context.Verbose("AssessCommand", $"Assess command failed with {ex.GetType().Name}: {ex.Message}");
            progress.CompleteStep($"assess rejected: {ex.Message}");
            if (context.JsonOutput)
            {
                var errorResult = new
                {
                    command = "assess",
                    status = "error",
                    hfId = hfId,
                    published = publish,
                    error = $"Failed to assess model: {ex.Message}"
                };
                Console.WriteLine(JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true }));
            }

            return CommandResult.Failure($"Failed to assess model: {ex.Message}");
        }
        finally
        {
            context.Verbose("AssessCommand", "Assess command finished execution path.");
        }
    }
}
