namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Handles profile I/O, serialization, and deduplication.
/// Issue #27: profiling: publish-flow polish + community metadata
/// </summary>
public class ProfileReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions JsonLOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Reads a set of profiles from a JSONL file.
    /// </summary>
    public async Task<List<Profile>> ReadProfileSetAsync(string filePath)
    {
        var profiles = new List<Profile>();

        if (!File.Exists(filePath))
            return profiles;

        using var reader = new StreamReader(filePath);
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var profile = JsonSerializer.Deserialize<Profile>(line, JsonLOptions);
                if (profile != null)
                    profiles.Add(profile);
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }

        return profiles;
    }

    /// <summary>
    /// Writes profiles to a JSONL file (one JSON object per line).
    /// </summary>
    public async Task WriteProfileSetAsync(string filePath, List<Profile> profiles)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using var writer = new StreamWriter(filePath);

        foreach (var profile in profiles)
        {
            var json = JsonSerializer.Serialize(profile, JsonLOptions);
            await writer.WriteLineAsync(json);
        }
    }

    /// <summary>
    /// Deduplicates profiles by dedupKey, keeping the newest (by CreatedAt).
    /// Only applies to profiles with community metadata containing a dedupKey.
    /// </summary>
    public List<Profile> DeduplicateByDedupKey(List<Profile> profiles)
    {
        var dedupGroups = new Dictionary<string, List<Profile>>();

        foreach (var profile in profiles)
        {
            var key = profile.Community?.DedupKey;

            if (string.IsNullOrEmpty(key))
                continue;

            if (!dedupGroups.ContainsKey(key))
                dedupGroups[key] = new List<Profile>();

            dedupGroups[key].Add(profile);
        }

        var result = new List<Profile>(profiles);

        foreach (var (dedupKey, group) in dedupGroups)
        {
            if (group.Count <= 1)
                continue;

            // Sort by CreatedAt descending (newest first)
            var sorted = group.OrderByDescending(p => p.Provenance.CreatedAt).ToList();
            var newest = sorted[0];

            // Remove all but the newest
            foreach (var old in sorted.Skip(1))
                result.Remove(old);
        }

        return result;
    }

    /// <summary>
    /// Finds profiles with duplicate dedupKeys.
    /// </summary>
    public Dictionary<string, List<Profile>> FindDuplicatesByDedupKey(List<Profile> profiles)
    {
        var groups = new Dictionary<string, List<Profile>>();

        foreach (var profile in profiles)
        {
            var key = profile.Community?.DedupKey;
            if (string.IsNullOrEmpty(key))
                continue;

            if (!groups.ContainsKey(key))
                groups[key] = new List<Profile>();

            groups[key].Add(profile);
        }

        return groups.Where(g => g.Value.Count > 1)
            .ToDictionary(g => g.Key, g => g.Value);
    }

    /// <summary>
    /// Searches profiles by description, tags, or keywords.
    /// </summary>
    public List<Profile> SearchProfiles(List<Profile> profiles, string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        return profiles.Where(p =>
        {
            var community = p.Community;
            if (community == null)
                return false;

            if (!string.IsNullOrEmpty(community.Description) &&
                community.Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
                return true;

            if (community.Tags?.Any(t => t.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)) == true)
                return true;

            if (community.Keywords?.Any(k => k.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)) == true)
                return true;

            return false;
        }).ToList();
    }

    /// <summary>
    /// Filters profiles by hardware requirements.
    /// </summary>
    public List<Profile> FilterByHardware(List<Profile> profiles, int memoryGb, string? hardwareFamily = null)
    {
        return profiles.Where(p =>
        {
            var community = p.Community;
            if (community == null)
                return true;

            // Check memory range
            if (community.MinMemoryGb.HasValue && memoryGb < community.MinMemoryGb)
                return false;

            if (community.MaxMemoryGb.HasValue && memoryGb > community.MaxMemoryGb)
                return false;

            // Check hardware family if specified
            if (!string.IsNullOrEmpty(hardwareFamily) &&
                !string.IsNullOrEmpty(community.HardwareFamily) &&
                !community.HardwareFamily.Equals(hardwareFamily, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }).ToList();
    }

    /// <summary>
    /// Filters profiles to only those with community metadata.
    /// </summary>
    public List<Profile> FilterPublishable(List<Profile> profiles)
    {
        return profiles.Where(p => p.Community != null).ToList();
    }

    /// <summary>
    /// Filters profiles by engine.
    /// </summary>
    public List<Profile> FilterByEngine(List<Profile> profiles, string engine)
    {
        return profiles.Where(p =>
            p.Engine.Equals(engine, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Filters profiles by multiple engines (OR operation).
    /// </summary>
    public List<Profile> FilterByEngines(List<Profile> profiles, params string[] engines)
    {
        var engineSet = new HashSet<string>(engines, StringComparer.OrdinalIgnoreCase);
        return profiles.Where(p => engineSet.Contains(p.Engine)).ToList();
    }
}
