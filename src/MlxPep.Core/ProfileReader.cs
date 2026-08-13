namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Uses ProfileJsonSerializerContext for AOT/trimming compatibility.
/// </summary>
public class ProfileReader
{
    private static readonly ProfileJsonSerializerContext JsonContext = new();

    private readonly ProfileValidator _validator = new();

    /// <summary>
    /// Reads a set of profiles from a JSONL file with validation.
    /// Throws InvalidOperationException if validation fails.
    /// Uses ProfileJsonSerializerContext for source-generated, AOT-compatible deserialization.
    /// </summary>
    public async Task<List<Profile>> ReadProfileSetAsync(string filePath, bool validateAfterRead = true)
    {
        var profiles = new List<Profile>();

        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"[ProfileReader] Profile file not found: {filePath}");
            return profiles;
        }

        Debug.WriteLine($"[ProfileReader] Reading profile set from: {filePath}");

        using var reader = new StreamReader(filePath);
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                Debug.WriteLine($"[ProfileReader] Skipping empty line {lineNumber}");
                continue;
            }

            Debug.WriteLine($"[ProfileReader] Processing JSONL line {lineNumber}: {line.Substring(0, Math.Min(50, line.Length))}...");

            try
            {
                var profile = JsonSerializer.Deserialize<Profile>(line, JsonContext.Profile);
                if (profile != null)
                {
                    Debug.WriteLine($"[ProfileReader] Successfully deserialized profile '{profile.Id}' (tier: {profile.Tier})");
                    profiles.Add(profile);
                }
                else
                {
                    Debug.WriteLine($"[ProfileReader] Deserialization returned null at line {lineNumber}");
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[ProfileReader] Deserialization error at line {lineNumber}: {ex.Message}");
                var msg = $"Failed to deserialize JSONL at line {lineNumber}: {ex.Message}\n\nLine content: {line}\n\nUsing ProfileJsonSerializerContext for source-generated, AOT-compatible deserialization.";
                throw new InvalidOperationException(msg, ex);
            }
        }

        // Validate the entire profile set if requested
        if (validateAfterRead && profiles.Any())
        {
            Debug.WriteLine($"[ProfileReader] Validating profile set: {profiles.Count} profiles from '{filePath}'");
            var result = _validator.ValidateProfileSet(profiles);
            if (!result.IsValid)
            {
                Debug.WriteLine($"[ProfileReader] Profile set validation failed with {result.Errors.Count} errors");
                var errorMessage = $"Failed to validate profile set from '{filePath}':\n" +
                                   string.Join("\n", result.Errors.Select(e => $"  - {e}"));
                throw new InvalidOperationException(errorMessage);
            }

            Debug.WriteLine($"[ProfileReader] Profile set validation passed");

            // Log warnings if any
            if (result.Warnings.Any())
            {
                Debug.WriteLine($"[ProfileReader] Validation produced {result.Warnings.Count} warnings");
                foreach (var warning in result.Warnings)
                {
                    Debug.WriteLine($"[ProfileReader] Warning: {warning}");
                }
            }
        }
        else if (!validateAfterRead)
        {
            Debug.WriteLine($"[ProfileReader] Skipping post-read validation (validateAfterRead=false)");
        }

        Debug.WriteLine($"[ProfileReader] Completed reading profile set: {profiles.Count} profiles");
        return profiles;
    }

    /// <summary>
    /// Writes profiles to a JSONL file (one JSON object per line).
    /// Automatically validates tier uniqueness before writing.
    /// Uses ProfileJsonSerializerContext for source-generated, AOT-compatible serialization.
    /// </summary>
    public async Task WriteProfileSetAsync(string filePath, List<Profile> profiles, bool validateBeforeWrite = true)
    {
        Debug.WriteLine($"[ProfileReader] Writing {profiles.Count} profiles to: {filePath}");

        if (validateBeforeWrite && profiles.Any())
        {
            Debug.WriteLine($"[ProfileReader] Validating {profiles.Count} profiles before write");
            var result = _validator.ValidateProfileSet(profiles);
            if (!result.IsValid)
            {
                Debug.WriteLine($"[ProfileReader] Pre-write validation failed with {result.Errors.Count} errors");
                var errorMessage = $"Failed to validate profiles before writing to '{filePath}':\n" +
                                   string.Join("\n", result.Errors.Select(e => $"  - {e}"));
                throw new InvalidOperationException(errorMessage);
            }

            Debug.WriteLine($"[ProfileReader] Pre-write validation passed");
        }
        else if (!validateBeforeWrite)
        {
            Debug.WriteLine($"[ProfileReader] Skipping pre-write validation (validateBeforeWrite=false)");
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Debug.WriteLine($"[ProfileReader] Creating directory: {directory}");
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(filePath);

        foreach (var profile in profiles)
        {
            var json = JsonSerializer.Serialize(profile, typeof(Profile), JsonContext);
            Debug.WriteLine($"[ProfileReader] Serialized profile '{profile.Id}' to JSONL");
            await writer.WriteLineAsync(json);
        }

        Debug.WriteLine($"[ProfileReader] Completed writing {profiles.Count} profiles");
    }

    /// <summary>
    /// Filters profiles by tier.
    /// </summary>
    public List<Profile> FilterByTier(List<Profile> profiles, string tier)
    {
        Debug.WriteLine($"[ProfileReader] Filtering {profiles.Count} profiles by tier: {tier}");
        var result = profiles.Where(p =>
            p.Tier.Equals(tier, StringComparison.OrdinalIgnoreCase)).ToList();
        Debug.WriteLine($"[ProfileReader] Tier filter result: {result.Count} profiles matched");
        return result;
    }

    /// <summary>
    /// Filters profiles by engine.
    /// </summary>
    public List<Profile> FilterByEngine(List<Profile> profiles, string engine)
    {
        Debug.WriteLine($"[ProfileReader] Filtering {profiles.Count} profiles by engine: {engine}");
        var result = profiles.Where(p =>
            p.Engine.Equals(engine, StringComparison.OrdinalIgnoreCase)).ToList();
        Debug.WriteLine($"[ProfileReader] Engine filter result: {result.Count} profiles matched");
        return result;
    }

    /// <summary>
    /// Filters profiles by multiple engines (OR operation).
    /// </summary>
    public List<Profile> FilterByEngines(List<Profile> profiles, params string[] engines)
    {
        Debug.WriteLine($"[ProfileReader] Filtering {profiles.Count} profiles by engines: {string.Join(", ", engines)}");
        var engineSet = new HashSet<string>(engines, StringComparer.OrdinalIgnoreCase);
        var result = profiles.Where(p => engineSet.Contains(p.Engine)).ToList();
        Debug.WriteLine($"[ProfileReader] Multi-engine filter result: {result.Count} profiles matched");
        return result;
    }

    /// <summary>
    /// Filters profiles by model Hugging Face ID.
    /// </summary>
    public List<Profile> FilterByModel(List<Profile> profiles, string modelHfId)
    {
        Debug.WriteLine($"[ProfileReader] Filtering {profiles.Count} profiles by model: {modelHfId}");
        var result = profiles.Where(p =>
            p.ModelHfId.Equals(modelHfId, StringComparison.OrdinalIgnoreCase)).ToList();
        Debug.WriteLine($"[ProfileReader] Model filter result: {result.Count} profiles matched");
        return result;
    }
}
