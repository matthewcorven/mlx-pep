namespace MlxPep.Cli.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MlxPep.Cli.Services;
using MlxPep.Core;

/// <summary>
/// Handler for `mlx-pep profiles` subcommands.
/// Manages community profiles: list, search, pull.
/// </summary>
public class ProfilesListCommand
{
    private readonly ILogger<ProfilesListCommand>? _logger;

    public ProfilesListCommand(ILogger<ProfilesListCommand>? logger = null)
    {
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context, bool listLocal = false)
    {
        try
        {
            _logger?.LogDebug("Executing profiles list command");
            context.Verbose("ProfilesListCommand", $"Profiles list invoked with listLocal={listLocal}.");

            if (listLocal)
            {
                context.Verbose("ProfilesListCommand", "Local list branch selected.");
                return await ExecuteLocalAsync(context);
            }

            context.Verbose("ProfilesListCommand", "Remote list branch selected.");
            return await ExecuteRemoteAsync(context);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error in profiles list");
            context.Verbose("ProfilesListCommand", $"Profiles list failed with {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Failed to list profiles: {ex.Message}");
        }
        finally
        {
            context.Verbose("ProfilesListCommand", "Profiles list command finished execution path.");
        }
    }

    private async Task<CommandResult> ExecuteRemoteAsync(CommandContext context)
    {
        using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var serviceClient = new ProfilesServiceClient(httpClient, null);
        var result = await serviceClient.ListProfilesAsync();

        if (!result.Success)
        {
            return CommandResult.Failure(result.Error ?? "Failed to fetch remote profiles");
        }

        var profiles = result.Data ?? new List<Profile>();

        if (context.JsonOutput)
        {
            var jsonResult = new
            {
                command = "profiles list",
                status = "ok",
                source = "remote",
                profile_count = profiles.Count,
                profiles = profiles.Select(p => new
                {
                    id = p.Id,
                    modelHfId = p.ModelHfId,
                    tier = p.Tier,
                    engine = p.Engine
                }).ToList()
            };
            Console.WriteLine(JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            if (profiles.Count == 0)
            {
                Console.WriteLine("No remote profiles available.");
                return CommandResult.Success();
            }

            Console.WriteLine("Community Profiles (Remote):");
            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"{"ID",-35} {"Model",-20} {"Tier",-12}");
            Console.WriteLine(new string('-', 80));

            foreach (var profile in profiles)
            {
                var modelDisplay = profile.ModelHfId.Length > 19 ? profile.ModelHfId[..19] : profile.ModelHfId;
                Console.WriteLine($"{profile.Id,-35} {modelDisplay,-20} {profile.Tier,-12}");
            }

            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"Total: {profiles.Count} profiles");
        }

        return CommandResult.Success();
    }

    private async Task<CommandResult> ExecuteLocalAsync(CommandContext context)
    {
        var localStore = new LocalProfileStore(null);
        var result = await localStore.ListLocalAsync();

        if (!result.Success)
        {
            return CommandResult.Failure(result.Error ?? "Failed to list local profiles");
        }

        var profiles = result.Data ?? new List<Profile>();

        if (context.JsonOutput)
        {
            var jsonResult = new
            {
                command = "profiles list",
                status = "ok",
                source = "local",
                profile_count = profiles.Count,
                storage_path = localStore.GetStoragePath(),
                profiles = profiles.Select(p => new
                {
                    id = p.Id,
                    modelHfId = p.ModelHfId,
                    tier = p.Tier,
                    engine = p.Engine
                }).ToList()
            };
            Console.WriteLine(JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            if (profiles.Count == 0)
            {
                Console.WriteLine($"No local profiles found in {localStore.GetStoragePath()}");
                return CommandResult.Success();
            }

            Console.WriteLine($"Local Profiles: {localStore.GetStoragePath()}");
            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"{"ID",-35} {"Model",-20} {"Tier",-12}");
            Console.WriteLine(new string('-', 80));

            foreach (var profile in profiles)
            {
                var modelDisplay = profile.ModelHfId.Length > 19 ? profile.ModelHfId[..19] : profile.ModelHfId;
                Console.WriteLine($"{profile.Id,-35} {modelDisplay,-20} {profile.Tier,-12}");
            }

            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"Total: {profiles.Count} profiles");
        }

        return CommandResult.Success();
    }
}

public class ProfilesSearchCommand
{
    private readonly ILogger<ProfilesSearchCommand>? _logger;

    public ProfilesSearchCommand(ILogger<ProfilesSearchCommand>? logger = null)
    {
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteAsync(string query, CommandContext context)
    {
        try
        {
            _logger?.LogDebug("Executing profiles search command with query: {query}", query);
            context.Verbose("ProfilesSearchCommand", $"Profiles search invoked with query '{query}'.");

            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var serviceClient = new ProfilesServiceClient(httpClient, null);
            var profilesResult = await serviceClient.ListProfilesAsync();

            if (!profilesResult.Success)
            {
                context.Verbose("ProfilesSearchCommand", "Remote profile list lookup failed during search.");
                return CommandResult.Failure(profilesResult.Error ?? "Failed to fetch profiles");
            }

            var allProfiles = profilesResult.Data ?? new List<Profile>();
            var queryLower = query.ToLowerInvariant();
            var results = allProfiles.Where(p =>
                p.Id.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                p.ModelHfId.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                p.Tier.Contains(queryLower, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (context.JsonOutput)
            {
                context.Verbose("ProfilesSearchCommand", "JSON output branch selected for profiles search.");
                var result = new
                {
                    command = "profiles search",
                    status = "ok",
                    query = query,
                    result_count = results.Count,
                    results = results.Select(p => new
                    {
                        id = p.Id,
                        modelHfId = p.ModelHfId,
                        tier = p.Tier,
                        engine = p.Engine
                    }).ToList()
                };
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                context.Verbose("ProfilesSearchCommand", $"Text output branch selected for profiles search with {results.Count} results.");
                Console.WriteLine($"Search Results for: '{query}'");

                if (results.Count == 0)
                {
                    context.Verbose("ProfilesSearchCommand", "Search returned zero matching profiles.");
                    Console.WriteLine("No profiles found matching the search query.");
                    return CommandResult.Success();
                }

                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"{"ID",-35} {"Model",-20} {"Tier",-12}");
                Console.WriteLine(new string('-', 80));

                foreach (var profile in results)
                {
                    var modelDisplay = profile.ModelHfId.Length > 19 ? profile.ModelHfId[..19] : profile.ModelHfId;
                    Console.WriteLine($"{profile.Id,-35} {modelDisplay,-20} {profile.Tier,-12}");
                }

                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"Found: {results.Count} profiles");
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error in profiles search");
            context.Verbose("ProfilesSearchCommand", $"Profiles search failed with {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Failed to search profiles: {ex.Message}");
        }
        finally
        {
            context.Verbose("ProfilesSearchCommand", "Profiles search command finished execution path.");
        }
    }
}

public class ProfilesPullCommand
{
    private readonly ILogger<ProfilesPullCommand>? _logger;

    public ProfilesPullCommand(ILogger<ProfilesPullCommand>? logger = null)
    {
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteAsync(string profileId, CommandContext context)
    {
        using var progress = context.CreateProgressScope("profiles pull", 4);
        try
        {
            _logger?.LogDebug("Executing profiles pull command for profile: {profileId}", profileId);
            context.Verbose("ProfilesPullCommand", $"Profiles pull invoked for '{profileId}'.");

            progress.StartStep("check local profile cache");
            var localStore = new LocalProfileStore(null);
            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var serviceClient = new ProfilesServiceClient(httpClient, null);

            if (localStore.ProfileExists(profileId))
            {
                var message = $"Profile {profileId} already exists locally.";
                _logger?.LogDebug("{message}", message);
                context.Verbose("ProfilesPullCommand", "Profile already existed locally; skipping remote fetch.");
                progress.CompleteStep("profile already existed locally");

                if (!context.JsonOutput)
                {
                    Console.WriteLine($"Warning: {message}");
                    Console.WriteLine($"Location: {localStore.GetProfilePath(profileId)}");
                }
                else
                {
                    var result = new
                    {
                        command = "profiles pull",
                        status = "skipped",
                        message = message,
                        profileId = profileId,
                        path = localStore.GetProfilePath(profileId)
                    };
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                }

                return CommandResult.Success();
            }
            progress.CompleteStep("profile not present locally");

            progress.StartStep("fetch remote profile");
            var profileResult = await serviceClient.GetProfileAsync(profileId);
            if (!profileResult.Success)
            {
                var errorMsg = profileResult.Error ?? $"Profile {profileId} not found";
                _logger?.LogDebug("{errorMsg}", errorMsg);
                context.Verbose("ProfilesPullCommand", "Remote profile fetch failed.");
                progress.CompleteStep("remote profile fetch failed");
                return CommandResult.Failure(errorMsg);
            }
            progress.CompleteStep("remote profile fetched");

            var profile = profileResult.Data;
            if (profile == null)
            {
                var errorMsg = $"Profile {profileId} is invalid";
                _logger?.LogDebug("{errorMsg}", errorMsg);
                context.Verbose("ProfilesPullCommand", "Remote profile payload was null.");
                return CommandResult.Failure(errorMsg);
            }

            _logger?.LogDebug("Validating profile {profileId}", profileId);
            progress.StartStep("validate and save profile");
            var validator = new ProfileValidator();
            var validationResult = validator.ValidateForLocalUse(profile);
            if (!validationResult.IsValid)
            {
                var errorMsg = $"Profile validation failed: {string.Join("; ", validationResult.Errors)}";
                _logger?.LogDebug("{errorMsg}", errorMsg);
                context.Verbose("ProfilesPullCommand", $"Profile validation failed with {validationResult.Errors.Count} errors.");
                progress.CompleteStep("profile validation failed");
                return CommandResult.Failure(errorMsg);
            }

            _logger?.LogDebug("Saving profile {profileId} to local store", profileId);
            var saveResult = await localStore.SaveProfileAsync(profile);
            if (!saveResult.Success)
            {
                var errorMsg = saveResult.Error ?? $"Failed to save profile {profileId}";
                _logger?.LogDebug("{errorMsg}", errorMsg);
                context.Verbose("ProfilesPullCommand", "Saving the pulled profile to the local store failed.");
                progress.CompleteStep("profile save failed");
                return CommandResult.Failure(errorMsg);
            }
            progress.CompleteStep("profile validated and saved");

            var successMsg = $"Profile {profileId} pulled successfully";
            _logger?.LogDebug("{successMsg}", successMsg);
            context.Verbose("ProfilesPullCommand", "Pulled profile successfully; rendering output.");

            progress.StartStep("render pull result");
            if (context.JsonOutput)
            {
                var result = new
                {
                    command = "profiles pull",
                    status = "ok",
                    message = successMsg,
                    profileId = profileId,
                    path = localStore.GetProfilePath(profileId),
                    modelHfId = profile.ModelHfId,
                    tier = profile.Tier
                };
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine(successMsg);
                Console.WriteLine($"Location: {localStore.GetProfilePath(profileId)}");
            }
            progress.CompleteStep("rendered pull result");

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error in profiles pull");
            context.Verbose("ProfilesPullCommand", $"Profiles pull failed with {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Failed to pull profile: {ex.Message}");
        }
        finally
        {
            context.Verbose("ProfilesPullCommand", "Profiles pull command finished execution path.");
        }
    }
}
