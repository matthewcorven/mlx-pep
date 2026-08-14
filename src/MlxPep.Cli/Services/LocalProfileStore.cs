namespace MlxPep.Cli.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MlxPep.Core;

/// <summary>
/// Manages local profile storage in ~/.mlx-pep/profiles/
/// Handles saving, loading, and listing profiles from the local filesystem.
/// </summary>
public class LocalProfileStore
{
    private readonly string _storagePath;

    public LocalProfileStore(string? basePath = null)
    {
        var home = basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _storagePath = Path.Combine(home, ".mlx-pep", "profiles");
    }

    /// <summary>
    /// Saves a profile to the local store.
    /// Creates the directory structure if it doesn't exist.
    /// </summary>
    public async Task<bool> SaveProfileAsync(Profile profile)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Saving profile {profile.Id} to local store");

            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
                Console.WriteLine($"[DEBUG] Created directory {_storagePath}");
            }

            var filePath = Path.Combine(_storagePath, $"{profile.Id}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(filePath, json);
            Console.WriteLine($"[DEBUG] Profile {profile.Id} saved to {filePath}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Failed to save profile {profile.Id}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads a profile from the local store by ID.
    /// Returns null if the profile doesn't exist or cannot be deserialized.
    /// </summary>
    public async Task<Profile?> LoadProfileAsync(string profileId)
    {
        try
        {
            var filePath = Path.Combine(_storagePath, $"{profileId}.json");

            Console.WriteLine($"[DEBUG] Loading profile {profileId} from {filePath}");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[DEBUG] Profile {profileId} not found in local store");
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<Profile>(json);

            Console.WriteLine($"[DEBUG] Loaded profile {profileId} from local store");
            return profile;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Failed to load profile {profileId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Lists all profiles in the local store.
    /// </summary>
    public async Task<List<Profile>> ListLocalAsync()
    {
        return await Task.Run(() =>
        {
            var profiles = new List<Profile>();

            try
            {
                Console.WriteLine($"[DEBUG] Listing profiles from {_storagePath}");

                if (!Directory.Exists(_storagePath))
                {
                    Console.WriteLine($"[DEBUG] Local profile directory does not exist: {_storagePath}");
                    return profiles;
                }

                var jsonFiles = Directory.GetFiles(_storagePath, "*.json");
                Console.WriteLine($"[DEBUG] Found {jsonFiles.Length} profile files");

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var profile = JsonSerializer.Deserialize<Profile>(json);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Failed to deserialize profile from {file}: {ex.Message}");
                    }
                }

                Console.WriteLine($"[DEBUG] Loaded {profiles.Count} profiles from local store");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error listing local profiles: {ex.Message}");
            }

            return profiles;
        });
    }

    /// <summary>
    /// Checks if a profile exists in the local store.
    /// </summary>
    public bool ProfileExists(string profileId)
    {
        var filePath = Path.Combine(_storagePath, $"{profileId}.json");
        var exists = File.Exists(filePath);
        Console.WriteLine($"[DEBUG] Profile {profileId} exists: {exists}");
        return exists;
    }

    /// <summary>
    /// Gets the full filesystem path to a profile file.
    /// </summary>
    public string GetProfilePath(string profileId)
    {
        return Path.Combine(_storagePath, $"{profileId}.json");
    }

    /// <summary>
    /// Gets the storage directory path.
    /// </summary>
    public string GetStoragePath()
    {
        return _storagePath;
    }
}
