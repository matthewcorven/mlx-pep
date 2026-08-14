namespace MlxPep.Cli.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MlxPep.Core;

/// <summary>
/// Manages local profile storage in ~/.mlx-pep/profiles/
/// Handles saving, loading, and listing profiles from the local filesystem.
/// Thread-safe via SemaphoreSlim for directory/file access synchronization.
/// </summary>
public class LocalProfileStore
{
    private readonly string _storagePath;
    private readonly ILogger<LocalProfileStore> _logger;
    private readonly SemaphoreSlim _storageLock;

    public LocalProfileStore(ILogger<LocalProfileStore>? logger = null, string? basePath = null)
    {
        var home = basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _storagePath = Path.Combine(home, ".mlx-pep", "profiles");
        _logger = logger ?? new NullLogger<LocalProfileStore>();
        _storageLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Saves a profile to the local store.
    /// Creates the directory structure if it doesn't exist.
    /// Thread-safe via SemaphoreSlim.
    /// </summary>
    public async Task<Result<bool>> SaveProfileAsync(Profile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        await _storageLock.WaitAsync();
        try
        {
            _logger.LogDebug("Saving profile {profileId} to local store", profile.Id);

            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
                _logger.LogDebug("Created directory {storagePath}", _storagePath);
            }

            var filePath = Path.Combine(_storagePath, $"{profile.Id}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Profile {profileId} saved to {filePath}", profile.Id, filePath);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to save profile {profileId}", profile.Id);
            return Result<bool>.Fail(ex);
        }
        finally
        {
            _storageLock.Release();
        }
    }

    /// <summary>
    /// Loads a profile from the local store by ID.
    /// Thread-safe via SemaphoreSlim.
    /// </summary>
    public async Task<Result<Profile>> LoadProfileAsync(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) throw new ArgumentNullException(nameof(profileId));

        await _storageLock.WaitAsync();
        try
        {
            var filePath = Path.Combine(_storagePath, $"{profileId}.json");

            _logger.LogDebug("Loading profile {profileId} from {filePath}", profileId, filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Profile {profileId} not found in local store", profileId);
                return Result<Profile>.Fail($"Profile {profileId} not found");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<Profile>(json);

            if (profile == null)
            {
                _logger.LogDebug("Profile {profileId} deserialized to null", profileId);
                return Result<Profile>.Fail($"Profile {profileId} is invalid");
            }

            _logger.LogDebug("Loaded profile {profileId} from local store", profileId);
            return Result<Profile>.Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load profile {profileId}", profileId);
            return Result<Profile>.Fail(ex);
        }
        finally
        {
            _storageLock.Release();
        }
    }

    /// <summary>
    /// Lists all profiles in the local store.
    /// Thread-safe via SemaphoreSlim.
    /// </summary>
    public async Task<Result<List<Profile>>> ListLocalAsync()
    {
        await _storageLock.WaitAsync();
        try
        {
            var profiles = new List<Profile>();

            _logger.LogDebug("Listing profiles from {storagePath}", _storagePath);

            if (!Directory.Exists(_storagePath))
            {
                _logger.LogDebug("Local profile directory does not exist: {storagePath}", _storagePath);
                return Result<List<Profile>>.Ok(profiles);
            }

            var jsonFiles = Directory.GetFiles(_storagePath, "*.json");
            _logger.LogDebug("Found {count} profile files", jsonFiles.Length);

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
                    _logger.LogDebug(ex, "Failed to deserialize profile from {file}", file);
                }
            }

            _logger.LogDebug("Loaded {count} profiles from local store", profiles.Count);
            return Result<List<Profile>>.Ok(profiles);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error listing local profiles");
            return Result<List<Profile>>.Fail(ex);
        }
        finally
        {
            _storageLock.Release();
        }
    }

    /// <summary>
    /// Checks if a profile exists in the local store.
    /// </summary>
    public bool ProfileExists(string profileId)
    {
        var filePath = Path.Combine(_storagePath, $"{profileId}.json");
        var exists = File.Exists(filePath);
        _logger.LogDebug("Profile {profileId} exists: {exists}", profileId, exists);
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

/// <summary>
/// No-op logger implementation for use when no real logger is available.
/// </summary>
internal class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}
