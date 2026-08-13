namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Handles profile JSONL I/O with validation.
/// Issue #8: core: profile schema records + STJ source-gen + JSONL validation
///
/// JSONL format: one JSON object per line, one line per tier (high|balanced|efficient).
/// Round-trip validation ensures serialization fidelity and tier uniqueness.
/// </summary>
public class ProfileReader
{
    private static readonly JsonSerializerOptions JsonLOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ProfileValidator _validator = new();

    /// <summary>
    /// Reads a set of profiles from a JSONL file with validation.
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    public async Task<List<Profile>> ReadProfileSetAsync(string filePath, bool validateAfterRead = true)
    {
        var profiles = new List<Profile>();

        if (!File.Exists(filePath))
            return profiles;

        using var reader = new StreamReader(filePath);
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var profile = JsonSerializer.Deserialize<Profile>(line, JsonLOptions);
                if (profile != null)
                    profiles.Add(profile);
            }
            catch (JsonException ex)
            {
                var msg = $"Failed to deserialize JSONL at line {lineNumber}: {ex.Message}\n\nLine content: {line}\n\nNote: Use source-generated JsonSerializerContext from ProfileJsonSerializerContext, not reflection-based deserialization.";
                throw new InvalidOperationException(msg, ex);
            }
        }

        // Validate the entire profile set if requested
        if (validateAfterRead && profiles.Any())
        {
            var result = _validator.ValidateProfileSet(profiles);
            if (!result.IsValid)
            {
                var errorMessage = $"Failed to validate profile set from '{filePath}':\n" +
                                   string.Join("\n", result.Errors.Select(e => $"  - {e}"));
                throw new InvalidOperationException(errorMessage);
            }

            // Log warnings if any
            if (result.Warnings.Any())
            {
                foreach (var warning in result.Warnings)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileReader] Warning: {warning}");
                }
            }
        }

        return profiles;
    }

    /// <summary>
    /// Writes profiles to a JSONL file (one JSON object per line).
    /// Automatically validates tier uniqueness before writing.
    /// </summary>
    public async Task WriteProfileSetAsync(string filePath, List<Profile> profiles, bool validateBeforeWrite = true)
    {
        if (validateBeforeWrite && profiles.Any())
        {
            var result = _validator.ValidateProfileSet(profiles);
            if (!result.IsValid)
            {
                var errorMessage = $"Failed to validate profiles before writing to '{filePath}':\n" +
                                   string.Join("\n", result.Errors.Select(e => $"  - {e}"));
                throw new InvalidOperationException(errorMessage);
            }
        }

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
    /// Filters profiles by tier.
    /// </summary>
    public List<Profile> FilterByTier(List<Profile> profiles, string tier)
    {
        return profiles.Where(p =>
            p.Tier.Equals(tier, StringComparison.OrdinalIgnoreCase)).ToList();
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

    /// <summary>
    /// Filters profiles by model Hugging Face ID.
    /// </summary>
    public List<Profile> FilterByModel(List<Profile> profiles, string modelHfId)
    {
        return profiles.Where(p =>
            p.ModelHfId.Equals(modelHfId, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
