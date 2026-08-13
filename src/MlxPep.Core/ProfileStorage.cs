namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Handles persistent storage of profiles in ~/.mlx-pep/profiles/ directory.
/// Manages directory creation, JSONL file I/O, and profile validation.
/// </summary>
public class ProfileStorage
{
    private readonly string _baseDirectory;
    private readonly ProfileValidator _validator;

    public ProfileStorage(string? baseDirectory = null)
    {
        if (baseDirectory != null)
        {
            Debug.WriteLine($"[ProfileStorage] Using custom base directory: {baseDirectory}");
            _baseDirectory = baseDirectory;
        }
        else
        {
            _baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mlx-pep", "profiles");
            Debug.WriteLine($"[ProfileStorage] Using default base directory: {_baseDirectory}");
        }
        _validator = new ProfileValidator();
    }

    /// <summary>
    /// Ensures the base profiles directory exists, creating it if necessary.
    /// </summary>
    public async Task<bool> EnsureBaseDirectoryAsync()
    {
        try
        {
            if (!Directory.Exists(_baseDirectory))
            {
                Debug.WriteLine($"[ProfileStorage] Creating base directory: {_baseDirectory}");
                Directory.CreateDirectory(_baseDirectory);
            }
            else
            {
                Debug.WriteLine($"[ProfileStorage] Base directory already exists: {_baseDirectory}");
            }
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Error creating base directory: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Saves a set of profiles to a JSONL file with validation.
    /// Directory structure: ~/.mlx-pep/profiles/{date}/{hf_id}/
    /// </summary>
    public async Task<string> SaveProfileSetAsync(
        string hfId,
        List<Profile> profiles,
        string? dateFolder = null)
    {
        if (profiles == null || profiles.Count == 0)
        {
            Debug.WriteLine($"[ProfileStorage] No profiles to save for {hfId}");
            throw new ArgumentException("Profile set cannot be empty", nameof(profiles));
        }

        try
        {
            await EnsureBaseDirectoryAsync();

            // Use provided date or current date
            dateFolder ??= DateTime.UtcNow.ToString("yyyy-MM-dd");
            Debug.WriteLine($"[ProfileStorage] Using date folder: {dateFolder}");

            var modelFolder = Path.Combine(_baseDirectory, dateFolder, hfId.Replace("/", "-"));
            Debug.WriteLine($"[ProfileStorage] Creating model folder: {modelFolder}");
            Directory.CreateDirectory(modelFolder);

            // Save one file per tier for easy lookup
            var filePath = Path.Combine(modelFolder, "profiles.jsonl");
            Debug.WriteLine($"[ProfileStorage] Saving profiles to: {filePath}");

            using (var writer = new StreamWriter(filePath, append: false))
            {
                foreach (var profile in profiles)
                {
                    // Validate profile before writing
                    var validationResult = _validator.ValidateProfileSet(new List<Profile> { profile });
                    if (!validationResult.IsValid)
                    {
                        Debug.WriteLine($"[ProfileStorage] Validation failed for profile {profile.Id}: {string.Join(", ", validationResult.Errors)}");
                        throw new InvalidOperationException($"Profile validation failed: {string.Join(", ", validationResult.Errors)}");
                    }

                    var json = JsonSerializer.Serialize(profile, ProfileJsonSerializerContext.Default.Profile);
                    Debug.WriteLine($"[ProfileStorage] Writing profile: {profile.Id} (tier: {profile.Tier})");
                    await writer.WriteLineAsync(json);
                }
            }

            Debug.WriteLine($"[ProfileStorage] Successfully saved {profiles.Count} profiles to {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Error saving profile set: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Loads profiles from a JSONL file.
    /// </summary>
    public async Task<List<Profile>> LoadProfileSetAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"[ProfileStorage] File not found: {filePath}");
                return new List<Profile>();
            }

            Debug.WriteLine($"[ProfileStorage] Loading profiles from: {filePath}");
            var profiles = new List<Profile>();

            using (var reader = new StreamReader(filePath))
            {
                string? line;
                int lineNumber = 0;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        Debug.WriteLine($"[ProfileStorage] Skipping empty line {lineNumber}");
                        continue;
                    }

                    try
                    {
                        var profile = JsonSerializer.Deserialize<Profile>(line, ProfileJsonSerializerContext.Default.Profile);
                        if (profile != null)
                        {
                            Debug.WriteLine($"[ProfileStorage] Loaded profile {lineNumber}: {profile.Id}");
                            profiles.Add(profile);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"[ProfileStorage] Error parsing line {lineNumber}: {ex.Message}");
                        throw;
                    }
                }
            }

            Debug.WriteLine($"[ProfileStorage] Successfully loaded {profiles.Count} profiles from {filePath}");
            return profiles;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Error loading profile set: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the most recent profile folder for a given model.
    /// </summary>
    public async Task<string?> GetMostRecentProfileFolderAsync(string hfId)
    {
        try
        {
            await EnsureBaseDirectoryAsync();

            var modelId = hfId.Replace("/", "-");
            Debug.WriteLine($"[ProfileStorage] Searching for recent profiles for {modelId}");

            var dateFolders = Directory.GetDirectories(_baseDirectory);
            if (dateFolders.Length == 0)
            {
                Debug.WriteLine($"[ProfileStorage] No date folders found");
                return null;
            }

            // Sort descending to get the most recent date
            Array.Sort(dateFolders, (a, b) => string.Compare(Path.GetFileName(b), Path.GetFileName(a)));

            foreach (var dateFolder in dateFolders)
            {
                var modelFolder = Path.Combine(dateFolder, modelId);
                if (Directory.Exists(modelFolder))
                {
                    Debug.WriteLine($"[ProfileStorage] Found profiles in: {modelFolder}");
                    return modelFolder;
                }
            }

            Debug.WriteLine($"[ProfileStorage] No profiles found for {modelId}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileStorage] Error searching for profiles: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the full path to the base profiles directory.
    /// </summary>
    public string GetBaseDirectory() => _baseDirectory;
}
