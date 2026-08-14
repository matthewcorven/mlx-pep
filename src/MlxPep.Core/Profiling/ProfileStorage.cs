namespace MlxPep.Core.Profiling;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// Manages local profile storage at ~/.mlx-pep/profiles/.
/// Handles JSONL write/read, directory structure, and validation.
/// </summary>
public class ProfileStorage
{
    private static readonly string BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".mlx-pep",
        "profiles");

    public async Task EnsureBaseDirectoryAsync()
    {
        Debug.WriteLine($"[ProfileStorage] Ensuring directory exists: {BaseDirectory}");
        
        try
        {
            Directory.CreateDirectory(BaseDirectory);
            Debug.WriteLine("[ProfileStorage] Base directory ready");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Failed to create directory: {ex.Message}");
            throw;
        }
    }

    public async Task SaveProfileSetAsync(List<Profile> profiles, string modelHfId)
    {
        if (profiles == null || profiles.Count == 0)
            throw new ArgumentException("Profiles list cannot be empty", nameof(profiles));

        if (string.IsNullOrWhiteSpace(modelHfId))
            throw new ArgumentException("Model HF ID cannot be empty", nameof(modelHfId));

        Debug.WriteLine($"[ProfileStorage] Saving {profiles.Count} profiles for {modelHfId}");

        await EnsureBaseDirectoryAsync();

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var profileDirectory = Path.Combine(
            BaseDirectory,
            timestamp,
            modelHfId.Replace('/', '_'));

        try
        {
            Directory.CreateDirectory(profileDirectory);
            Debug.WriteLine($"[ProfileStorage] Created profile directory: {profileDirectory}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Failed to create profile directory: {ex.Message}");
            throw;
        }

        var profileFilePath = Path.Combine(profileDirectory, "profiles.jsonl");
        
        try
        {
            using var writer = new StreamWriter(profileFilePath, append: false);
            
            foreach (var profile in profiles)
            {
                Debug.WriteLine($"[ProfileStorage] Writing profile {profile.Id}");
                var json = JsonSerializer.Serialize(
                    profile,
                    ProfileJsonSerializerContext.Default.Profile);
                await writer.WriteLineAsync(json);
            }

            Debug.WriteLine($"[ProfileStorage] Successfully saved profiles to {profileFilePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Failed to write profiles: {ex.Message}");
            throw;
        }
    }

    public async Task<List<Profile>> LoadProfileSetAsync(string modelHfId)
    {
        if (string.IsNullOrWhiteSpace(modelHfId))
            throw new ArgumentException("Model HF ID cannot be empty", nameof(modelHfId));

        Debug.WriteLine($"[ProfileStorage] Loading profiles for {modelHfId}");

        var profileFolder = await GetMostRecentProfileFolderAsync(modelHfId);
        
        if (profileFolder == null)
        {
            Debug.WriteLine($"[ProfileStorage] No profiles found for {modelHfId}");
            return new List<Profile>();
        }

        var profileFilePath = Path.Combine(profileFolder, "profiles.jsonl");

        if (!File.Exists(profileFilePath))
        {
            Debug.WriteLine($"[ProfileStorage] Profile file not found: {profileFilePath}");
            return new List<Profile>();
        }

        try
        {
            var profiles = new List<Profile>();
            
            using var reader = new StreamReader(profileFilePath);
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                Debug.WriteLine($"[ProfileStorage] Parsing profile from JSONL");
                var profile = JsonSerializer.Deserialize<Profile>(
                    line,
                    ProfileJsonSerializerContext.Default.Profile);

                if (profile != null)
                    profiles.Add(profile);
            }

            Debug.WriteLine($"[ProfileStorage] Loaded {profiles.Count} profiles");
            return profiles;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Failed to load profiles: {ex.Message}");
            throw;
        }
    }

    public async Task<string?> GetMostRecentProfileFolderAsync(string modelHfId)
    {
        if (string.IsNullOrWhiteSpace(modelHfId))
            throw new ArgumentException("Model HF ID cannot be empty", nameof(modelHfId));

        Debug.WriteLine($"[ProfileStorage] Looking for most recent profile folder for {modelHfId}");

        await EnsureBaseDirectoryAsync();

        if (!Directory.Exists(BaseDirectory))
        {
            Debug.WriteLine("[ProfileStorage] Base directory does not exist");
            return null;
        }

        try
        {
            var safeName = modelHfId.Replace('/', '_');
            var timestampDirs = Directory.GetDirectories(BaseDirectory)
                .OrderByDescending(d => d)
                .ToList();

            Debug.WriteLine($"[ProfileStorage] Checking {timestampDirs.Count} timestamp directories");

            foreach (var timestampDir in timestampDirs)
            {
                var modelDir = Path.Combine(timestampDir, safeName);
                
                if (Directory.Exists(modelDir))
                {
                    Debug.WriteLine($"[ProfileStorage] Found profile folder: {modelDir}");
                    return modelDir;
                }
            }

            Debug.WriteLine($"[ProfileStorage] No profile folder found for {modelHfId}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Error searching for profile folder: {ex.Message}");
            throw;
        }
    }

    public static string GetStorageDirectoryPath() => BaseDirectory;
}
