namespace MlxPep.Cli.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MlxPep.Cli.Services;
using MlxPep.Core;

/// <summary>
/// Handler for `mlx-pep profiles` subcommands.
/// Manages community profiles: list, search, pull.
/// </summary>
public class ProfilesListCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context, bool listLocal = false)
    {
        try
        {
            Console.WriteLine("[DEBUG] Executing profiles list command");

            if (listLocal)
            {
                return await ExecuteLocalAsync(context);
            }

            return await ExecuteRemoteAsync(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error in profiles list: {ex.Message}");
            return CommandResult.Failure($"Failed to list profiles: {ex.Message}");
        }
    }

    private async Task<CommandResult> ExecuteRemoteAsync(CommandContext context)
    {
        var serviceClient = new ProfilesServiceClient();
        var profiles = await serviceClient.ListProfilesAsync();

        if (context.JsonOutput)
        {
            var result = new
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
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
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
        var localStore = new LocalProfileStore();
        var profiles = await localStore.ListLocalAsync();

        if (context.JsonOutput)
        {
            var result = new
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
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
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
    public async Task<CommandResult> ExecuteAsync(string query, CommandContext context)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Executing profiles search command with query: {query}");

            var serviceClient = new ProfilesServiceClient();
            var allProfiles = await serviceClient.ListProfilesAsync();

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
            Console.WriteLine($"[DEBUG] Error in profiles search: {ex.Message}");
            return CommandResult.Failure($"Failed to search profiles: {ex.Message}");
        }
    }
}

public class ProfilesPullCommand
{
    public async Task<CommandResult> ExecuteAsync(string profileId, CommandContext context)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Executing profiles pull command for profile: {profileId}");

            var localStore = new LocalProfileStore();
            var serviceClient = new ProfilesServiceClient();

            if (localStore.ProfileExists(profileId))
            {
                var message = $"Profile {profileId} already exists locally.";
                Console.WriteLine($"[DEBUG] {message}");

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

            var profile = await serviceClient.GetProfileAsync(profileId);
            if (profile == null)
            {
                var errorMsg = $"Profile {profileId} not found on service";
                Console.WriteLine($"[DEBUG] {errorMsg}");
                return CommandResult.Failure(errorMsg);
            }

            Console.WriteLine($"[DEBUG] Validating profile {profileId}");
            var validator = new ProfileValidator();
            var validationResult = validator.ValidateForLocalUse(profile);
            if (!validationResult.IsValid)
            {
                var errorMsg = $"Profile validation failed: {string.Join("; ", validationResult.Errors)}";
                Console.WriteLine($"[DEBUG] {errorMsg}");
                return CommandResult.Failure(errorMsg);
            }

            Console.WriteLine($"[DEBUG] Saving profile {profileId} to local store");
            var saved = await localStore.SaveProfileAsync(profile);
            if (!saved)
            {
                var errorMsg = $"Failed to save profile {profileId} to local store";
                Console.WriteLine($"[DEBUG] {errorMsg}");
                return CommandResult.Failure(errorMsg);
            }

            var successMsg = $"Profile {profileId} pulled successfully";
            Console.WriteLine($"[DEBUG] {successMsg}");

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
            Console.WriteLine($"[DEBUG] Error in profiles pull: {ex.Message}");
            return CommandResult.Failure($"Failed to pull profile: {ex.Message}");
        }
    }
}
