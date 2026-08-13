using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MlxPep.Core.Storage;

/// <summary>
/// Local file-based storage for profiles.
/// Manages profiles in ~/.mlx-pep/profiles/ directory.
/// </summary>
public class ProfileLocalStore
{
    private readonly string _storageDirectory;
    private readonly ILogger<ProfileLocalStore> _logger;

    public ProfileLocalStore(ILogger<ProfileLocalStore>? logger = null)
    {
        _logger = logger ?? new NullLogger<ProfileLocalStore>();
        _storageDirectory = GetStorageDirectoryPath();

        _logger.LogDebug("ProfileLocalStore initialized with directory: {Directory}", _storageDirectory);
    }

    /// <summary>
    /// Get the base storage directory path for profiles.
    /// Expands ~ to home directory and creates path ~/.mlx-pep/profiles/
    /// </summary>
    public static string GetStorageDirectoryPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".mlx-pep", "profiles");
    }

    /// <summary>
    /// Ensure the storage directory exists.
    /// </summary>
    private void EnsureDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_storageDirectory))
            {
                _logger.LogDebug("Creating profiles directory: {Directory}", _storageDirectory);
                Directory.CreateDirectory(_storageDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create profiles directory: {Directory}", _storageDirectory);
            throw;
        }
    }

    /// <summary>
    /// List all locally stored profiles.
    /// </summary>
    public async Task<IEnumerable<Profile>> ListLocalProfilesAsync()
    {
        try
        {
            _logger.LogDebug("ListLocalProfilesAsync called");

            if (!Directory.Exists(_storageDirectory))
            {
                _logger.LogDebug("Profiles directory does not exist yet: {Directory}", _storageDirectory);
                return Enumerable.Empty<Profile>();
            }

            var profiles = new List<Profile>();
            var jsonFiles = Directory.GetFiles(_storageDirectory, "*.json");

            _logger.LogDebug("Found {Count} JSON files in profiles directory", jsonFiles.Length);

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    var profile = JsonSerializer.Deserialize<Profile>(content);

                    if (profile != null)
                    {
                        profiles.Add(profile);
                        _logger.LogDebug("Loaded profile from {FilePath}: {ProfileId}", filePath, profile.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Error loading profile from {FilePath}: {Exception}", filePath, ex.Message);
                }
            }

            _logger.LogDebug("ListLocalProfilesAsync returned {Count} profiles", profiles.Count);
            return profiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing local profiles");
            throw;
        }
    }

    /// <summary>
    /// Get a specific profile from local storage.
    /// </summary>
    public async Task<Profile?> GetProfileAsync(string profileId)
    {
        try
        {
            _logger.LogDebug("GetProfileAsync called for {ProfileId}", profileId);

            var filePath = GetProfileFilePath(profileId);

            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Profile file not found: {FilePath}", filePath);
                return null;
            }

            var content = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<Profile>(content);

            if (profile != null)
            {
                _logger.LogDebug("Profile {ProfileId} loaded from {FilePath}", profileId, filePath);
            }

            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading profile {ProfileId}", profileId);
            throw;
        }
    }

    /// <summary>
    /// Save a profile to local storage.
    /// </summary>
    public async Task<string> SaveProfileAsync(Profile profile)
    {
        try
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (string.IsNullOrEmpty(profile.Id))
                throw new InvalidOperationException("Profile ID cannot be null or empty");

            _logger.LogDebug("SaveProfileAsync called for profile {ProfileId}", profile.Id);

            EnsureDirectoryExists();

            var filePath = GetProfileFilePath(profile.Id);

            _logger.LogDebug("Saving profile to {FilePath}", filePath);

            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            _logger.LogDebug("Profile {ProfileId} saved successfully to {FilePath}", profile.Id, filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile {ProfileId}", profile?.Id ?? "unknown");
            throw;
        }
    }

    /// <summary>
    /// Check if a profile exists in local storage.
    /// </summary>
    public async Task<bool> ProfileExistsAsync(string profileId)
    {
        try
        {
            _logger.LogDebug("ProfileExistsAsync called for {ProfileId}", profileId);

            var filePath = GetProfileFilePath(profileId);
            var exists = File.Exists(filePath);

            _logger.LogDebug("Profile {ProfileId} exists: {Exists}", profileId, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if profile exists: {ProfileId}", profileId);
            throw;
        }
    }

    /// <summary>
    /// Search local profiles by query.
    /// Matches against profile ID and metadata fields.
    /// </summary>
    public async Task<IEnumerable<Profile>> SearchLocalProfilesAsync(string query)
    {
        try
        {
            _logger.LogDebug("SearchLocalProfilesAsync called with query: {Query}", query);

            var allProfiles = await ListLocalProfilesAsync();
            var queryLower = query.ToLowerInvariant();

            var results = allProfiles.Where(p =>
                (p.Id?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.ModelHfId?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Description?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Tier?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();

            _logger.LogDebug("SearchLocalProfilesAsync returned {Count} results for query: {Query}", results.Count, query);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching local profiles with query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Get the full file path for a profile by ID.
    /// </summary>
    private string GetProfileFilePath(string profileId) =>
        Path.Combine(_storageDirectory, $"{profileId}.json");
}

/// <summary>
/// Null logger for when no logger is provided.
/// </summary>
internal class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
    }
}
