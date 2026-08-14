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

            if (listLocal)
            {
                return await ExecuteLocalAsync(context);
            }

            return await ExecuteRemoteAsync(context);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error in profiles list");
            return CommandResult.Failure($"Failed to list profiles: {ex.Message}");
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

            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var serviceClient = new ProfilesServiceClient(httpClient, null);
            var profilesResult = await serviceClient.ListProfilesAsync();

            if (!profilesResult.Success)
            {
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
                Console.WriteLine($"Search Results for: '{query}'");

                if (results.Count == 0)
                {
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
            return CommandResult.Failure($"Failed to search profiles: {ex.Message}");
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
        try
        {
            _logger?.LogDebug("Executing profiles pull command for profile: {profileId}", profileId);

            var localStore = new LocalProfileStore(null);
            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var serviceClient = new ProfilesServiceClient(httpClient, null);

            if (localStore.ProfileExists(profileId))
            {
                var message = $"Profile {profileId} already exists locally.";
                _logger?.LogDebug("{message}", message);

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

            var profileResult = await serviceClient.GetProfileAsync(profileId);
            if (!profileResult.Success)
            {
                var errorMsg = profileResult.Error ?? $"Profile {profileId} not found";
                _logger?.LogDebug("{errorMsg}", errorMsg);
                return CommandResult.Failure(errorMsg);
            }

            var profile = profileResult.Data;
            if (profile == null)
            {
                var errorMsg = $"Profile {profileId} is invalid";
                _logger?.LogDebug("{errorMsg}", errorMsg);
                return CommandResult.Failure(errorMsg);
            }

            _logger?.LogDebug("Validating profile {profileId}", profileId);
            var validator = new ProfileValidator();
            var validationResult = validator.ValidateForLocalUse(profile);
            if (!validationResult.IsValid)
            {
                var errorMsg = $"Profile validation failed: {string.Join("; ", validationResult.Errors)}";
                _logger?.LogDebug("{errorMsg}", errorMsg);
                return CommandResult.Failure(errorMsg);
            }

            _logger?.LogDebug("Saving profile {profileId} to local store", profileId);
            var saveResult = await localStore.SaveProfileAsync(profile);
            if (!saveResult.Success)
            {
                var errorMsg = saveResult.Error ?? $"Failed to save profile {profileId}";
                _logger?.LogDebug("{errorMsg}", errorMsg);
                return CommandResult.Failure(errorMsg);
            }

            var successMsg = $"Profile {profileId} pulled successfully";
            _logger?.LogDebug("{successMsg}", successMsg);

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

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error in profiles pull");
            return CommandResult.Failure($"Failed to pull profile: {ex.Message}");
        }
    }
}
